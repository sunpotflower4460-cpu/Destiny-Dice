import type { Condition } from '../registration/types';
import type { RngSource } from '../rng/types';
import type {
  ExploratoryLayerCResult,
  FinalLayerAResult,
  FinalLayerCResult,
  LayerBExploratoryResult,
  MiracleProfileResult,
  PredictionCalibrationResult,
  QuarterlyTrendResult,
  StateDependenceResult,
} from '../stats';
import type { EXPLORATORY_WARNING } from '../stats/exploratory';

export type EvidenceGrade = {
  layer: 'A' | 'B' | 'C';
  grade: '★★★' | '★★' | '★';
  note: string;
};

export type PredictionAnswerCheck = {
  condition: Condition;
  registeredPrediction: string;
  finalLabel: FinalLayerAResult['conditions'][number]['label'];
  machineVerdict: '保留';
  reason: 'free_text_prediction_has_no_frozen_machine_semantics';
};

export type WishTextFeatureSummary = {
  status: 'partial_v1';
  specificityOperationalized: false;
  judgedWishes: number;
  meanCharactersOverall: number | null;
  meanCharactersRealized: number | null;
  meanCharactersNotRealized: number | null;
  byArm: {
    practice: { n: number; meanCharacters: number | null };
    sealed: { n: number; meanCharacters: number | null };
  };
};

export type FinalLayerCReport = {
  enabled: boolean;
  result: FinalLayerCResult | null;
  eligibleJudgedWishes: number;
  eligibleUnjudgedWishes: number;
  postExperimentDeadlineWishes: number;
  unassignedWishes: number;
  assignmentSourceCounts: Record<RngSource, number>;
};

export type FinalReportModel = {
  reportVersion: 'final-report-v1';
  experimentId: string;
  experimentStartDate: string;
  experimentEndDate: string;
  genesisHash: string;
  headHash: string;
  ledgerEntries: number;
  protocol: {
    protocolVersion: string;
    analysisPlanVersion: string;
    statsVersion: string;
    rngPolicyVersion: string;
    canonicalizationVersion: string;
  };
  confirmatory: {
    layerA: FinalLayerAResult;
    layerC: FinalLayerCReport;
    evidenceGrades: EvidenceGrade[];
  };
  exploratory: {
    warning: typeof EXPLORATORY_WARNING;
    layerB: LayerBExploratoryResult;
    doseResponse: ReturnType<typeof import('../stats/exploratory').doseResponse>;
    quarterlyTrend: QuarterlyTrendResult;
    stateDependence: StateDependenceResult;
    predictionCalibration: PredictionCalibrationResult;
    predictionAnswerCheck: PredictionAnswerCheck[];
    miracleProfile: MiracleProfileResult;
    layerC: ExploratoryLayerCResult | null;
    timeToRealization: {
      status: 'not_measurable_v1';
      reason: 'wish_schema_does_not_record_realized_at';
    };
    wishTextFeatures: WishTextFeatureSummary | null;
  };
  limitations: readonly string[];
};
