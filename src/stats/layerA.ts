import type { Condition, DecisionRule } from '../registration/types';
import type { RngSource } from '../rng/types';
import { cumulativeDeviation, zScore } from './core';
import {
  holmAdjust,
  oneSampleBayesFactor10,
  twoSidedBinomialNormalApproxP,
  wilsonInterval95,
  type ConfidenceInterval,
} from './inference';

export const CONDITIONS = [0, 1, 2, 3, 4] as const satisfies readonly Condition[];
export const CHANCE_HIT_RATE = 0.5 as const;

export type LayerASessionObservation = {
  condition: Condition;
  rngSource: RngSource;
  ritualValid: boolean;
  nBits: number;
  hits: number;
  confidence?: number;
  ritualSeconds?: number;
  date?: string;
};

export type LayerAControlObservation = {
  rngSource: RngSource;
  nBits: number;
  hits: number;
  date?: string;
};

export type SourceCount = {
  sessions: number;
  bits: number;
};

export type SourceCounts = Record<RngSource, SourceCount>;

export type BinomialSummary = {
  sessions: number;
  nBits: number;
  hits: number;
  hitRate: number | null;
  chanceHitRate: typeof CHANCE_HIT_RATE;
  z: number | null;
  cumulativeDeviation: number;
  ci95: ConfidenceInterval | null;
  bf10: number;
};

export type InterimConditionSummary = BinomialSummary & {
  condition: Condition;
};

export type InterimLayerAResult = {
  analysisKind: 'interim';
  primarySample: 'anu_valid_only';
  conditions: InterimConditionSummary[];
  sourceCounts: SourceCounts;
  exclusions: {
    fallbackSessions: number;
    ritualInvalidSessions: number;
  };
};

export type ConfirmatoryLabel =
  | 'positive_pre_registered_result'
  | 'negative_evidence'
  | 'inconclusive';

export type FinalConditionResult = BinomialSummary & {
  condition: Condition;
  rawP: number | null;
  holmAdjustedP: number | null;
  label: ConfirmatoryLabel;
};

export type FinalLayerAResult = {
  analysisKind: 'final_confirmatory';
  primarySample: 'anu_valid_only';
  conditions: FinalConditionResult[];
  sourceCounts: SourceCounts;
  exclusions: {
    fallbackSessions: number;
    ritualInvalidSessions: number;
  };
};

function validateObservationCounts(hits: number, nBits: number): void {
  if (!Number.isInteger(nBits) || nBits <= 0 || nBits % 8 !== 0) {
    throw new RangeError('observation nBits must be a positive multiple of 8');
  }
  if (!Number.isInteger(hits) || hits < 0 || hits > nBits) {
    throw new RangeError('observation hits must be an integer from 0 through nBits');
  }
}

function validateSession(observation: LayerASessionObservation): void {
  if (!CONDITIONS.includes(observation.condition)) throw new RangeError('condition must be 0..4');
  validateObservationCounts(observation.hits, observation.nBits);
  if (observation.confidence !== undefined && (
    !Number.isInteger(observation.confidence) || observation.confidence < 0 || observation.confidence > 100
  )) {
    throw new RangeError('confidence must be an integer from 0 through 100');
  }
  if (observation.ritualSeconds !== undefined && (
    !Number.isInteger(observation.ritualSeconds) || observation.ritualSeconds < 0
  )) {
    throw new RangeError('ritualSeconds must be a non-negative integer');
  }
}

function validateControl(observation: LayerAControlObservation): void {
  validateObservationCounts(observation.hits, observation.nBits);
}

function emptySourceCounts(): SourceCounts {
  return {
    anu: { sessions: 0, bits: 0 },
    randomorg: { sessions: 0, bits: 0 },
    local: { sessions: 0, bits: 0 },
  };
}

export function countSources(
  observations: readonly Pick<LayerASessionObservation, 'rngSource' | 'nBits'>[],
): SourceCounts {
  const result = emptySourceCounts();
  for (const observation of observations) {
    const source = result[observation.rngSource];
    source.sessions += 1;
    source.bits += observation.nBits;
  }
  return result;
}

export function summarizeObservations(
  observations: readonly Pick<LayerASessionObservation, 'hits' | 'nBits'>[],
): BinomialSummary {
  const sessions = observations.length;
  const nBits = observations.reduce((sum, item) => sum + item.nBits, 0);
  const hits = observations.reduce((sum, item) => sum + item.hits, 0);
  if (nBits === 0) {
    return {
      sessions,
      nBits: 0,
      hits: 0,
      hitRate: null,
      chanceHitRate: CHANCE_HIT_RATE,
      z: null,
      cumulativeDeviation: 0,
      ci95: null,
      bf10: 1,
    };
  }

  return {
    sessions,
    nBits,
    hits,
    hitRate: hits / nBits,
    chanceHitRate: CHANCE_HIT_RATE,
    z: zScore(hits, nBits),
    cumulativeDeviation: cumulativeDeviation(hits, nBits),
    ci95: wilsonInterval95(hits, nBits),
    bf10: oneSampleBayesFactor10(hits, nBits),
  };
}

function validateSessions(observations: readonly LayerASessionObservation[]): void {
  observations.forEach(validateSession);
}

function primarySessions(observations: readonly LayerASessionObservation[]): LayerASessionObservation[] {
  return observations.filter((item) => item.rngSource === 'anu' && item.ritualValid);
}

function exclusions(observations: readonly LayerASessionObservation[]) {
  return {
    fallbackSessions: observations.filter((item) => item.rngSource !== 'anu').length,
    ritualInvalidSessions: observations.filter((item) => !item.ritualValid).length,
  };
}

export function analyzeInterimLayerA(observations: readonly LayerASessionObservation[]): InterimLayerAResult {
  validateSessions(observations);
  const primary = primarySessions(observations);
  return {
    analysisKind: 'interim',
    primarySample: 'anu_valid_only',
    conditions: CONDITIONS.map((condition) => ({
      condition,
      ...summarizeObservations(primary.filter((item) => item.condition === condition)),
    })),
    sourceCounts: countSources(observations),
    exclusions: exclusions(observations),
  };
}

function validateDecisionRule(rule: DecisionRule): void {
  if (!Number.isFinite(rule.pThresh) || rule.pThresh <= 0 || rule.pThresh >= 1) {
    throw new RangeError('decisionRule.pThresh must be in (0, 1)');
  }
  if (!Number.isFinite(rule.bfPos) || rule.bfPos <= 1) {
    throw new RangeError('decisionRule.bfPos must be > 1');
  }
  if (!Number.isFinite(rule.bfNeg) || rule.bfNeg <= 0 || rule.bfNeg >= 1) {
    throw new RangeError('decisionRule.bfNeg must be in (0, 1)');
  }
}

export function analyzeFinalLayerA(
  observations: readonly LayerASessionObservation[],
  decisionRule: DecisionRule,
): FinalLayerAResult {
  validateSessions(observations);
  validateDecisionRule(decisionRule);
  const primary = primarySessions(observations);
  const summaries = CONDITIONS.map((condition) => {
    const summary = summarizeObservations(primary.filter((item) => item.condition === condition));
    return {
      condition,
      summary,
      rawP: summary.nBits === 0 ? null : twoSidedBinomialNormalApproxP(summary.hits, summary.nBits),
    };
  });
  const adjusted = holmAdjust(summaries.map((item) => item.rawP));

  return {
    analysisKind: 'final_confirmatory',
    primarySample: 'anu_valid_only',
    conditions: summaries.map((item, index) => {
      const holmAdjustedP = adjusted[index] ?? null;
      let label: ConfirmatoryLabel = 'inconclusive';
      if (
        item.summary.nBits > 0 &&
        holmAdjustedP !== null &&
        holmAdjustedP < decisionRule.pThresh &&
        item.summary.bf10 > decisionRule.bfPos
      ) {
        label = 'positive_pre_registered_result';
      } else if (item.summary.nBits > 0 && item.summary.bf10 < decisionRule.bfNeg) {
        label = 'negative_evidence';
      }
      return {
        condition: item.condition,
        ...item.summary,
        rawP: item.rawP,
        holmAdjustedP,
        label,
      };
    }),
    sourceCounts: countSources(observations),
    exclusions: exclusions(observations),
  };
}

export type ControlQcResult = BinomialSummary & {
  sourceCounts: SourceCounts;
};

export function analyzeControlQc(observations: readonly LayerAControlObservation[]): ControlQcResult {
  observations.forEach(validateControl);
  return {
    ...summarizeObservations(observations),
    sourceCounts: countSources(observations),
  };
}

export type CumulativeDeviationPoint = {
  sessionIndex: number;
  cumulativeBits: number;
  cumulativeHits: number;
  deviation: number;
  envelope95: number;
};

export function cumulativeDeviationSeries(
  observations: readonly Pick<LayerASessionObservation, 'hits' | 'nBits'>[],
): CumulativeDeviationPoint[] {
  let cumulativeBits = 0;
  let cumulativeHits = 0;
  return observations.map((item, index) => {
    validateObservationCounts(item.hits, item.nBits);
    cumulativeBits += item.nBits;
    cumulativeHits += item.hits;
    return {
      sessionIndex: index + 1,
      cumulativeBits,
      cumulativeHits,
      deviation: cumulativeHits - cumulativeBits / 2,
      envelope95: 0.98 * Math.sqrt(cumulativeBits),
    };
  });
}
