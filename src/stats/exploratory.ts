import type { Condition } from '../registration/types';
import { zScore } from './core';
import { pearsonCorrelation, ordinaryLeastSquaresSlope } from './math';
import {
  CONDITIONS,
  summarizeObservations,
  type BinomialSummary,
  type LayerASessionObservation,
} from './layerA';

export const EXPLORATORY_WARNING =
  '探索的分析です。多重比較を含むため、ここで見えたパターンは実証ではなく仮説です。確かめるには新しい事前登録実験が必要です。' as const;

export type CalibrationBin = BinomialSummary & {
  minConfidence: number;
  maxConfidence: number;
};

export type PredictionCalibrationResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  sessions: number;
  confidenceZCorrelation: number | null;
  bins: CalibrationBin[];
};

const CONFIDENCE_BINS = [
  { min: 0, max: 20 },
  { min: 21, max: 40 },
  { min: 41, max: 60 },
  { min: 61, max: 80 },
  { min: 81, max: 100 },
] as const;

function sessionZ(item: Pick<LayerASessionObservation, 'hits' | 'nBits'>): number {
  return zScore(item.hits, item.nBits);
}

function usableExploratorySessions(
  observations: readonly LayerASessionObservation[],
): LayerASessionObservation[] {
  return observations.filter((item) => item.ritualValid);
}

export function predictionCalibration(
  observations: readonly LayerASessionObservation[],
): PredictionCalibrationResult {
  const usable = usableExploratorySessions(observations).filter(
    (item): item is LayerASessionObservation & { confidence: number } => item.confidence !== undefined,
  );
  const confidences = usable.map((item) => item.confidence);
  const zs = usable.map(sessionZ);

  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    sessions: usable.length,
    confidenceZCorrelation: pearsonCorrelation(confidences, zs),
    bins: CONFIDENCE_BINS.map(({ min, max }) => ({
      minConfidence: min,
      maxConfidence: max,
      ...summarizeObservations(usable.filter((item) => item.confidence >= min && item.confidence <= max)),
    })),
  };
}

const CONDITION_DOSE: Record<Condition, 0 | 1 | 2> = {
  0: 0,
  1: 1,
  2: 1,
  3: 1,
  4: 2,
};

export type DoseGroup = BinomialSummary & {
  dose: 0 | 1 | 2;
  label: 'none' | 'single_practice' | 'full_combo';
};

export type ConditionDurationTrend = {
  condition: Condition;
  sessions: number;
  ritualSecondsZCorrelation: number | null;
};

export type DoseResponseResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  doseZCorrelation: number | null;
  groups: DoseGroup[];
  durationByCondition: ConditionDurationTrend[];
};

export function doseResponse(observations: readonly LayerASessionObservation[]): DoseResponseResult {
  const usable = usableExploratorySessions(observations);
  const doses = usable.map((item) => CONDITION_DOSE[item.condition]);
  const zs = usable.map(sessionZ);
  const groupDefinitions = [
    { dose: 0 as const, label: 'none' as const },
    { dose: 1 as const, label: 'single_practice' as const },
    { dose: 2 as const, label: 'full_combo' as const },
  ];

  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    doseZCorrelation: pearsonCorrelation(doses, zs),
    groups: groupDefinitions.map(({ dose, label }) => ({
      dose,
      label,
      ...summarizeObservations(usable.filter((item) => CONDITION_DOSE[item.condition] === dose)),
    })),
    durationByCondition: CONDITIONS.map((condition) => {
      const withDuration = usable.filter(
        (item): item is LayerASessionObservation & { ritualSeconds: number } =>
          item.condition === condition && item.ritualSeconds !== undefined,
      );
      return {
        condition,
        sessions: withDuration.length,
        ritualSecondsZCorrelation: pearsonCorrelation(
          withDuration.map((item) => item.ritualSeconds),
          withDuration.map(sessionZ),
        ),
      };
    }),
  };
}

function parseIsoDate(date: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new TypeError('date must be YYYY-MM-DD');
  const timestamp = Date.parse(`${date}T00:00:00.000Z`);
  if (!Number.isFinite(timestamp)) throw new RangeError('date must be a valid calendar date');
  const roundTrip = new Date(timestamp).toISOString().slice(0, 10);
  if (roundTrip !== date) throw new RangeError('date must be a valid calendar date');
  return timestamp;
}

export type QuarterlySummary = BinomialSummary & {
  quarter: 1 | 2 | 3 | 4;
};

export type ConditionSessionTrend = {
  condition: Condition;
  sessions: number;
  zSlopePerSession: number | null;
  ordinalZCorrelation: number | null;
};

export type QuarterlyTrendResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  quarters: QuarterlySummary[];
  byCondition: ConditionSessionTrend[];
};

export function quarterlyTrend(
  observations: readonly LayerASessionObservation[],
  experimentStartDate: string,
  experimentDays = 365,
): QuarterlyTrendResult {
  if (!Number.isInteger(experimentDays) || experimentDays <= 0) {
    throw new RangeError('experimentDays must be a positive integer');
  }
  const start = parseIsoDate(experimentStartDate);
  const usable = usableExploratorySessions(observations).map((item, originalIndex) => {
    if (item.date === undefined) throw new TypeError('quarterlyTrend requires date on every observation');
    const current = parseIsoDate(item.date);
    const dayIndex = Math.trunc((current - start) / 86_400_000);
    if (dayIndex < 0 || dayIndex >= experimentDays) {
      throw new RangeError('observation date is outside the experiment window');
    }
    const quarter = Math.min(3, Math.floor((dayIndex * 4) / experimentDays)) as 0 | 1 | 2 | 3;
    return { item, originalIndex, quarter, dayIndex };
  });

  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    quarters: ([0, 1, 2, 3] as const).map((quarter) => ({
      quarter: (quarter + 1) as 1 | 2 | 3 | 4,
      ...summarizeObservations(
        usable.filter((entry) => entry.quarter === quarter).map((entry) => entry.item),
      ),
    })),
    byCondition: CONDITIONS.map((condition) => {
      const conditionRows = usable
        .filter((entry) => entry.item.condition === condition)
        .sort((a, b) => a.dayIndex - b.dayIndex || a.originalIndex - b.originalIndex);
      const ordinals = conditionRows.map((_, index) => index + 1);
      const zs = conditionRows.map((entry) => sessionZ(entry.item));
      return {
        condition,
        sessions: conditionRows.length,
        zSlopePerSession: ordinaryLeastSquaresSlope(ordinals, zs),
        ordinalZCorrelation: pearsonCorrelation(ordinals, zs),
      };
    }),
  };
}
