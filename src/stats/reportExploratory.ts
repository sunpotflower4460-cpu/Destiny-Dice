import type { Condition } from '../registration/types';
import { RNG_SOURCES, type RngSource } from '../rng/types';
import { zScore } from './core';
import { EXPLORATORY_WARNING } from './exploratory';
import { CONDITIONS, summarizeObservations, type BinomialSummary, type LayerASessionObservation } from './layerA';
import { pearsonCorrelation } from './math';

export type MeanCi95 = {
  n: number;
  mean: number | null;
  ci95: { low: number; high: number } | null;
};

export type LayerBSessionObservation = {
  condition: Condition;
  ritualValid: boolean;
  moodPreV: number;
  moodPreE: number;
  moodPostV: number;
  moodPostE: number;
};

export type LayerBConditionSummary = {
  condition: Condition;
  valenceChange: MeanCi95;
  energyChange: MeanCi95;
};

export type LayerBExploratoryResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  conditions: LayerBConditionSummary[];
  limitation: 'non_blinded_placebo_included';
};

export type RichSessionObservation = LayerASessionObservation & {
  moodPreV: number;
  moodPreE: number;
  hour: number;
  dow: number;
  lunarPhase: number;
  stateTag?: string;
};

export type StateDependenceResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  sessions: number;
  correlations: {
    moodPreVWithZ: number | null;
    moodPreEWithZ: number | null;
    hourWithZ: number | null;
    lunarPhaseWithZ: number | null;
  };
  byDow: Array<{ dow: number; summary: BinomialSummary }>;
  byStateTag: Array<{ stateTag: string; summary: BinomialSummary }>;
};

export type MiracleProfileResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  resonanceThreshold: '|z|>=2';
  targetMiracleThreshold: 'z>=3';
  analyzedSessions: number;
  resonanceSessions: number;
  targetMiracleSessions: number;
  byCondition: Record<Condition, number>;
  bySource: Record<RngSource, number>;
  averageMoodPreV: number | null;
  averageMoodPreE: number | null;
  averageHour: number | null;
  averageConfidence: number | null;
};

function validateMood(value: number, label: string): void {
  if (!Number.isFinite(value) || value < 1 || value > 10) {
    throw new RangeError(`${label} must be in 1..10`);
  }
}

function validateRichSession(item: RichSessionObservation): void {
  validateMood(item.moodPreV, 'moodPreV');
  validateMood(item.moodPreE, 'moodPreE');
  if (!Number.isInteger(item.hour) || item.hour < 0 || item.hour > 23) {
    throw new RangeError('hour must be an integer in 0..23');
  }
  if (!Number.isInteger(item.dow) || item.dow < 0 || item.dow > 6) {
    throw new RangeError('dow must be an integer in 0..6');
  }
  if (!Number.isFinite(item.lunarPhase) || item.lunarPhase < 0 || item.lunarPhase > 1) {
    throw new RangeError('lunarPhase must be in 0..1');
  }
}

function mean(values: readonly number[]): number | null {
  if (values.length === 0) return null;
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

export function meanNormal95(values: readonly number[]): MeanCi95 {
  if (values.some((value) => !Number.isFinite(value))) {
    throw new TypeError('meanNormal95 values must be finite');
  }
  if (values.length === 0) return { n: 0, mean: null, ci95: null };
  const average = mean(values)!;
  if (values.length < 2) return { n: 1, mean: average, ci95: null };
  const variance = values.reduce((sum, value) => sum + (value - average) ** 2, 0) / (values.length - 1);
  const margin = 1.96 * Math.sqrt(variance / values.length);
  return { n: values.length, mean: average, ci95: { low: average - margin, high: average + margin } };
}

export function analyzeLayerBMood(observations: readonly LayerBSessionObservation[]): LayerBExploratoryResult {
  for (const item of observations) {
    validateMood(item.moodPreV, 'moodPreV');
    validateMood(item.moodPreE, 'moodPreE');
    validateMood(item.moodPostV, 'moodPostV');
    validateMood(item.moodPostE, 'moodPostE');
  }
  const usable = observations.filter((item) => item.ritualValid);
  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    conditions: CONDITIONS.map((condition) => {
      const rows = usable.filter((item) => item.condition === condition);
      return {
        condition,
        valenceChange: meanNormal95(rows.map((item) => item.moodPostV - item.moodPreV)),
        energyChange: meanNormal95(rows.map((item) => item.moodPostE - item.moodPreE)),
      };
    }),
    limitation: 'non_blinded_placebo_included',
  };
}

export function stateDependence(observations: readonly RichSessionObservation[]): StateDependenceResult {
  observations.forEach(validateRichSession);
  const usable = observations.filter((item) => item.ritualValid);
  const zs = usable.map((item) => zScore(item.hits, item.nBits));
  const stateTags = [...new Set(usable.flatMap((item) => item.stateTag ? [item.stateTag] : []))].sort();
  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    sessions: usable.length,
    correlations: {
      moodPreVWithZ: pearsonCorrelation(usable.map((item) => item.moodPreV), zs),
      moodPreEWithZ: pearsonCorrelation(usable.map((item) => item.moodPreE), zs),
      hourWithZ: pearsonCorrelation(usable.map((item) => item.hour), zs),
      lunarPhaseWithZ: pearsonCorrelation(usable.map((item) => item.lunarPhase), zs),
    },
    byDow: Array.from({ length: 7 }, (_, dow) => ({
      dow,
      summary: summarizeObservations(usable.filter((item) => item.dow === dow)),
    })),
    byStateTag: stateTags.map((stateTag) => ({
      stateTag,
      summary: summarizeObservations(usable.filter((item) => item.stateTag === stateTag)),
    })),
  };
}

export function miracleProfile(observations: readonly RichSessionObservation[]): MiracleProfileResult {
  observations.forEach(validateRichSession);
  const usable = observations.filter((item) => item.ritualValid);
  const resonances = usable.filter((item) => Math.abs(zScore(item.hits, item.nBits)) >= 2);
  const targetMiracles = usable.filter((item) => zScore(item.hits, item.nBits) >= 3);
  const byCondition = Object.fromEntries(CONDITIONS.map((condition) => [condition, resonances.filter((item) => item.condition === condition).length])) as Record<Condition, number>;
  const bySource = Object.fromEntries(RNG_SOURCES.map((source) => [source, resonances.filter((item) => item.rngSource === source).length])) as Record<RngSource, number>;
  const confidences = resonances.flatMap((item) => item.confidence === undefined ? [] : [item.confidence]);
  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    resonanceThreshold: '|z|>=2',
    targetMiracleThreshold: 'z>=3',
    analyzedSessions: usable.length,
    resonanceSessions: resonances.length,
    targetMiracleSessions: targetMiracles.length,
    byCondition,
    bySource,
    averageMoodPreV: mean(resonances.map((item) => item.moodPreV)),
    averageMoodPreE: mean(resonances.map((item) => item.moodPreE)),
    averageHour: mean(resonances.map((item) => item.hour)),
    averageConfidence: mean(confidences),
  };
}
