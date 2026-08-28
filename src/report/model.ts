import type { StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import {
  EXPLORATORY_WARNING,
  analyzeExploratoryLayerC,
  analyzeFinalLayerA,
  analyzeFinalLayerC,
  analyzeLayerBMood,
  doseResponse,
  miracleProfile,
  predictionCalibration,
  quarterlyTrend,
  stateDependence,
  type LayerASessionObservation,
} from '../stats';
import { finalExperimentDate, projectFinalLayerC, projectReportSessions } from './ledgerProjection';
import type { FinalReportModel, WishTextFeatureSummary } from './types';

export const FINAL_REPORT_LIMITATIONS = [
  'n=1の個人実験であり、一般集団への普遍的な因果効果を示すものではありません。',
  'Layer BとLayer Cは盲検不能です。Layer Cの実現判定は自己判定です。',
  '条件の日替わり設計はキャリーオーバーを無視できるという仮定を含みます。',
  '封印群への注意を完全には遮断できず、登録した記憶は残ります。',
  '類似願いの重複はノイズ源になり得ます。',
  'マイクロ願いの結果は人生の大きな願いへそのまま一般化できません。',
  '外部RNG APIへの依存があり、fallback sessionはLayer A主要確証sampleへ代入しません。',
  'Layer Aが検証するのは、物理乱数へ直接現れる形の効果に限られます。',
  'ミラクル演出そのものが期待や気分へ影響する可能性があります。',
] as const;

function mean(values: readonly number[]): number | null {
  return values.length === 0 ? null : values.reduce((sum, value) => sum + value, 0) / values.length;
}

function wishTextFeatures(rows: readonly { arm: 'practice' | 'sealed'; outcome: string; text: string }[]): WishTextFeatureSummary {
  const chars = (items: readonly typeof rows[number][]) => mean(items.map((item) => [...item.text].length));
  const practice = rows.filter((item) => item.arm === 'practice');
  const sealed = rows.filter((item) => item.arm === 'sealed');
  return {
    status: 'partial_v1',
    specificityOperationalized: false,
    judgedWishes: rows.length,
    meanCharactersOverall: chars(rows),
    meanCharactersRealized: chars(rows.filter((item) => item.outcome === 'realized')),
    meanCharactersNotRealized: chars(rows.filter((item) => item.outcome !== 'realized')),
    byArm: {
      practice: { n: practice.length, meanCharacters: chars(practice) },
      sealed: { n: sealed.length, meanCharacters: chars(sealed) },
    },
  };
}

export function buildFinalReportModel(entries: readonly StoredLedgerEntry[], registration: RegistrationPayload): FinalReportModel {
  if (entries.length === 0 || entries[0]?.type !== 'registration') throw new Error('final report requires registration genesis');
  const sessions = projectReportSessions(entries, registration);
  const layerAObservations: LayerASessionObservation[] = sessions.map((session) => ({
    condition: session.condition,
    rngSource: session.rngSource,
    ritualValid: session.ritualValid,
    nBits: session.nBits,
    hits: session.hits,
    confidence: session.confidence,
    ritualSeconds: session.ritualSeconds,
    date: session.date,
  }));
  const finalA = analyzeFinalLayerA(layerAObservations, registration.decisionRuleA);
  const layerCProjection = projectFinalLayerC(entries, registration);
  const finalC = registration.layerC.enabled
    ? analyzeFinalLayerC(layerCProjection.observations, registration.layerC.decisionRuleC)
    : null;

  return {
    reportVersion: 'final-report-v1',
    experimentId: registration.experimentId,
    experimentStartDate: registration.startDate,
    experimentEndDate: finalExperimentDate(registration),
    genesisHash: entries[0]!.entryHash,
    headHash: entries.at(-1)!.entryHash,
    ledgerEntries: entries.length,
    protocol: {
      protocolVersion: registration.protocolVersion,
      analysisPlanVersion: registration.analysisPlanVersion,
      statsVersion: registration.statsVersion,
      rngPolicyVersion: registration.rngPolicyVersion,
      canonicalizationVersion: registration.canonicalizationVersion,
    },
    confirmatory: {
      layerA: finalA,
      layerC: {
        enabled: registration.layerC.enabled,
        result: finalC,
        eligibleJudgedWishes: layerCProjection.observations.length,
        eligibleUnjudgedWishes: layerCProjection.eligibleUnjudgedWishes,
        postExperimentDeadlineWishes: layerCProjection.postExperimentDeadlineWishes,
        unassignedWishes: layerCProjection.unassignedWishes,
        assignmentSourceCounts: layerCProjection.assignmentSourceCounts,
      },
      evidenceGrades: [
        { layer: 'A', grade: '★★★', note: '物理計測。主要確証解析はANUかつritual-valid sessionのみ。' },
        { layer: 'C', grade: '★★', note: 'ランダム化比較。ただし実現判定は非盲検の自己判定。' },
        { layer: 'B', grade: '★', note: '非盲検・プラセボ込みの短期pre/post測定。' },
      ],
    },
    exploratory: {
      warning: EXPLORATORY_WARNING,
      layerB: analyzeLayerBMood(sessions.map((session) => ({
        condition: session.condition,
        ritualValid: session.ritualValid,
        moodPreV: session.moodPreV,
        moodPreE: session.moodPreE,
        moodPostV: session.moodPostV,
        moodPostE: session.moodPostE,
      }))),
      doseResponse: doseResponse(layerAObservations),
      quarterlyTrend: quarterlyTrend(layerAObservations, registration.startDate, registration.days),
      stateDependence: stateDependence(sessions),
      predictionCalibration: predictionCalibration(layerAObservations),
      predictionAnswerCheck: ([0, 1, 2, 3, 4] as const).map((condition) => ({
        condition,
        registeredPrediction: registration.predictionByCondition[condition] ?? '',
        finalLabel: finalA.conditions[condition]!.label,
        machineVerdict: '保留' as const,
        reason: 'free_text_prediction_has_no_frozen_machine_semantics' as const,
      })),
      miracleProfile: miracleProfile(sessions),
      layerC: registration.layerC.enabled ? analyzeExploratoryLayerC(layerCProjection.observations) : null,
      timeToRealization: { status: 'not_measurable_v1', reason: 'wish_schema_does_not_record_realized_at' },
      wishTextFeatures: registration.layerC.enabled ? wishTextFeatures(layerCProjection.textRows) : null,
    },
    limitations: FINAL_REPORT_LIMITATIONS,
  };
}
