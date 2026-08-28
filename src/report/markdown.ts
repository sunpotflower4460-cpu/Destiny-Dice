import { CONDITION_LABELS } from '../dashboard/model';
import type { BinomialSummary, ConfirmatoryLabel } from '../stats';
import type { FinalReportModel } from './types';

function pct(value: number | null): string {
  return value === null ? '—' : `${(value * 100).toFixed(2)}%`;
}

function num(value: number | null, digits = 3): string {
  return value === null ? '—' : value.toFixed(digits);
}

function sci(value: number | null): string {
  if (value === null) return '—';
  if (value === 0) return '0';
  if (Math.abs(value) < 0.001 || Math.abs(value) >= 1000) return value.toExponential(3);
  return value.toFixed(4);
}

function labelJa(label: ConfirmatoryLabel): string {
  if (label === 'positive_pre_registered_result') return '事前登録した陽性基準を満たした';
  if (label === 'negative_evidence') return '事前登録した陰性BF基準を満たした';
  return '保留';
}

function summaryText(summary: BinomialSummary): string {
  return `nBits=${summary.nBits}, hit率=${pct(summary.hitRate)}（偶然50.00%）, z=${num(summary.z)}, BF10=${sci(summary.bf10)}`;
}

export function renderFinalReportMarkdown(report: FinalReportModel): string {
  const lines: string[] = [];
  lines.push('# Intention Dice 最終レポート');
  lines.push('');
  lines.push(`- Experiment ID: \`${report.experimentId}\``);
  lines.push(`- 実験期間: ${report.experimentStartDate} 〜 ${report.experimentEndDate}`);
  lines.push(`- Protocol: ${report.protocol.protocolVersion} / ${report.protocol.analysisPlanVersion} / ${report.protocol.statsVersion}`);
  lines.push(`- Genesis hash: \`${report.genesisHash}\``);
  lines.push(`- Final chain head: \`${report.headHash}\``);
  lines.push(`- Ledger entries: ${report.ledgerEntries}`);
  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('## 1. 確証パート — 事前登録した問いだけ');
  lines.push('');
  lines.push('この節は登録時に固定した判定ルールだけで計算しています。探索で見つかったパターンは判定へ混ぜていません。');
  lines.push('');
  lines.push('### Layer A — 物理乱数');
  lines.push('');
  lines.push('| 条件 | ANU+valid session | hit率 | 偶然 | Holm補正p | BF10 | 判定 |');
  lines.push('|---|---:|---:|---:|---:|---:|---|');
  for (const row of report.confirmatory.layerA.conditions) {
    lines.push(`| ${CONDITION_LABELS[row.condition]} | ${row.sessions} | ${pct(row.hitRate)} | 50.00% | ${sci(row.holmAdjustedP)} | ${sci(row.bf10)} | ${labelJa(row.label)} |`);
  }
  lines.push('');
  lines.push(`主要sampleは \`${report.confirmatory.layerA.primarySample}\`。fallback session=${report.confirmatory.layerA.exclusions.fallbackSessions}、ritual invalid=${report.confirmatory.layerA.exclusions.ritualInvalidSessions}。fallbackは記録から消していません。`);
  lines.push('');
  lines.push('### Layer C — 願いのランダム化比較');
  lines.push('');
  const c = report.confirmatory.layerC;
  if (!c.enabled || !c.result) {
    lines.push('Layer Cは事前登録で無効です。');
  } else {
    lines.push('| 群 | 判定済みn | 実現 | 未実現 | 実現率 | 95%CI |');
    lines.push('|---|---:|---:|---:|---:|---|');
    for (const arm of [c.result.comparison.practice, c.result.comparison.sealed]) {
      lines.push(`| ${arm.arm === 'practice' ? '実践群' : '封印群（ベースライン）'} | ${arm.n} | ${arm.realized} | ${arm.notRealized} | ${pct(arm.realizationRate)} | ${arm.ci95 ? `${pct(arm.ci95.lower)}–${pct(arm.ci95.upper)}` : '—'} |`);
    }
    lines.push('');
    lines.push(`- 実践−封印の実現率差: ${pct(c.result.comparison.riskDifference)}`);
    lines.push(`- Fisher両側p: ${sci(c.result.fisherTwoSidedP)}`);
    lines.push(`- BF10: ${sci(c.result.comparison.bf10)}`);
    lines.push(`- 判定: **${labelJa(c.result.label)}**`);
    lines.push(`- 実験終了までに締切到来・判定済み: ${c.eligibleJudgedWishes}件`);
    lines.push(`- 締切到来済みだが未判定（欠測のまま）: ${c.eligibleUnjudgedWishes}件`);
    lines.push(`- 実験終了後に締切が来るため主要分母外: ${c.postExperimentDeadlineWishes}件`);
    lines.push(`- 未割付: ${c.unassignedWishes}件`);
  }
  lines.push('');
  lines.push('### 証拠等級');
  lines.push('');
  for (const grade of report.confirmatory.evidenceGrades) lines.push(`- Layer ${grade.layer} ${grade.grade}: ${grade.note}`);
  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('## 2. 探索パート — 次の事前登録実験の仮説を探す');
  lines.push('');
  lines.push(`> ${report.exploratory.warning}`);
  lines.push('');
  lines.push('### Layer B — ritual前後の気分・エネルギー');
  lines.push('');
  for (const row of report.exploratory.layerB.conditions) {
    lines.push(`- ${CONDITION_LABELS[row.condition]}: 気分Δ=${num(row.valenceChange.mean, 2)} / エネルギーΔ=${num(row.energyChange.mean, 2)}（非盲検・プラセボ込み）`);
  }
  lines.push('');
  lines.push('### 1. 強度勾配（dose-response）');
  lines.push('');
  lines.push(`dose×z 相関: ${num(report.exploratory.doseResponse.doseZCorrelation)}`);
  for (const group of report.exploratory.doseResponse.groups) lines.push(`- ${group.label}: ${summaryText(group)}`);
  lines.push('');
  lines.push('### 2. 習慣化・時間発展');
  lines.push('');
  for (const quarter of report.exploratory.quarterlyTrend.quarters) lines.push(`- Q${quarter.quarter}: ${summaryText(quarter)}`);
  for (const row of report.exploratory.quarterlyTrend.byCondition) lines.push(`- ${CONDITION_LABELS[row.condition]} session順序×z相関=${num(row.ordinalZCorrelation)}, slope=${num(row.zSlopePerSession)}`);
  lines.push('');
  lines.push('### 3. 状態依存');
  lines.push('');
  const state = report.exploratory.stateDependence.correlations;
  lines.push(`- 気分pre×z: ${num(state.moodPreVWithZ)}`);
  lines.push(`- エネルギーpre×z: ${num(state.moodPreEWithZ)}`);
  lines.push(`- 時刻×z: ${num(state.hourWithZ)}`);
  lines.push(`- 月相×z: ${num(state.lunarPhaseWithZ)}`);
  lines.push('');
  lines.push('### 4. 予言の答え合わせ');
  lines.push('');
  lines.push(`セッション手応え×z相関: ${num(report.exploratory.predictionCalibration.confidenceZCorrelation)}`);
  for (const row of report.exploratory.predictionAnswerCheck) {
    lines.push(`- ${CONDITION_LABELS[row.condition]} 立て札: 「${row.registeredPrediction}」 / 実測判定: ${labelJa(row.finalLabel)} / 自動○×: 保留（v1の自由文には機械解釈ルールを事前固定していないため）`);
  }
  lines.push('');
  lines.push('### 5. ミラクル日のプロファイル');
  lines.push('');
  lines.push(`|z|≥2: ${report.exploratory.miracleProfile.resonanceSessions} session / 的方向z≥3: ${report.exploratory.miracleProfile.targetMiracleSessions} session。`);
  lines.push(`平均気分pre=${num(report.exploratory.miracleProfile.averageMoodPreV, 2)}、平均エネルギーpre=${num(report.exploratory.miracleProfile.averageMoodPreE, 2)}、平均時刻=${num(report.exploratory.miracleProfile.averageHour, 2)}。`);
  lines.push('');
  lines.push('### 6. Layer C 経路分解');
  lines.push('');
  if (report.exploratory.layerC) {
    for (const row of report.exploratory.layerC.pathways) lines.push(`- ${row.pathway}: 実践=${row.practice}, 封印=${row.sealed}`);
  } else lines.push('Layer C無効。');
  lines.push('');
  lines.push('### 7. Layer C 層別の効きめ');
  lines.push('');
  if (report.exploratory.layerC) {
    for (const row of report.exploratory.layerC.strata.likelihood) lines.push(`- ${row.label}: risk difference=${pct(row.comparison.riskDifference)}, BF10=${sci(row.comparison.bf10)}`);
    for (const row of report.exploratory.layerC.strata.influence) lines.push(`- ${row.label}: risk difference=${pct(row.comparison.riskDifference)}, BF10=${sci(row.comparison.bf10)}`);
  }
  lines.push('');
  lines.push('### 8. 実現までの日数');
  lines.push('');
  lines.push('v1のwish/judgment schemaには実現した正確な日付 `realizedAt` がありません。締切日や判定日を実現日として代用すると後付けの測定になるため、この分析は **測定不能** と明記します。');
  lines.push('');
  lines.push('### 9. 願い文の特徴');
  lines.push('');
  const text = report.exploratory.wishTextFeatures;
  if (!text) lines.push('Layer C無効。');
  else {
    lines.push(`判定済み${text.judgedWishes}件。平均文字数=${num(text.meanCharactersOverall, 1)}、実現=${num(text.meanCharactersRealized, 1)}、未実現=${num(text.meanCharactersNotRealized, 1)}。`);
    lines.push('「具体性」の客観的な採点規則はv1で事前固定されていないため、文字数以外の意味解釈は自動判定しません。');
  }
  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('## 3. 既知の限界');
  lines.push('');
  for (const limitation of report.limitations) lines.push(`- ${limitation}`);
  lines.push('');
  lines.push('## 4. 再現性');
  lines.push('');
  lines.push('このレポートはexportされたappend-only ledgerとgenesis内の凍結protocol metadataから再生成できます。ローカルhash chainはtamper-evidentであり、外部anchorなしに完全改竄不能とは表現しません。');
  lines.push('');
  lines.push('```sh');
  lines.push('pnpm verify-export <experiment.json>');
  lines.push('pnpm generate-report <experiment.json>');
  lines.push('```');
  lines.push('');
  return lines.join('\n');
}
