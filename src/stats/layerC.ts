import type { DecisionRule } from '../registration/types';
import { EXPLORATORY_WARNING } from './exploratory';
import type { ConfirmatoryLabel } from './layerA';
import { wilsonInterval95, type ConfidenceInterval } from './inference';
import { logGamma } from './math';

export const LAYER_C_ARMS = ['practice', 'sealed'] as const;
export const LAYER_C_OUTCOMES = ['realized', 'not_realized', 'undecidable', 'withdrawn'] as const;
export const LAYER_C_PATHWAYS = ['own_action', 'other_person', 'chance_encounter', 'unknown'] as const;
export const LAYER_C_LIKELIHOODS = [1, 2, 3] as const;
export const LAYER_C_INFLUENCES = ['self', 'mixed', 'external'] as const;

export type LayerCArm = (typeof LAYER_C_ARMS)[number];
export type LayerCOutcome = (typeof LAYER_C_OUTCOMES)[number];
export type LayerCPathway = (typeof LAYER_C_PATHWAYS)[number];
export type LayerCLikelihood = (typeof LAYER_C_LIKELIHOODS)[number];
export type LayerCInfluence = (typeof LAYER_C_INFLUENCES)[number];

export type LayerCWishObservation = {
  arm: LayerCArm;
  outcome: LayerCOutcome;
  likelihood: LayerCLikelihood;
  influence: LayerCInfluence;
  pathway?: LayerCPathway;
};

export type LayerCArmSummary = {
  arm: LayerCArm;
  n: number;
  realized: number;
  notRealized: number;
  realizationRate: number | null;
  ci95: ConfidenceInterval | null;
  withdrawn: number;
  undecidable: number;
};

export type LayerCComparisonSummary = {
  baselineArm: 'sealed';
  practice: LayerCArmSummary;
  sealed: LayerCArmSummary;
  riskDifference: number | null;
  bf10: number;
};

export type LayerCStratumSummary = {
  key: string;
  label: string;
  comparison: LayerCComparisonSummary;
};

export type LayerCPathwaySummary = {
  pathway: LayerCPathway;
  practice: number;
  sealed: number;
  total: number;
  practiceShare: number | null;
  sealedShare: number | null;
};

export type InterimLayerCResult = {
  analysisKind: 'interim';
  primaryOutcomePolicy: 'realized_vs_all_other_judged';
  comparison: LayerCComparisonSummary;
  sensitivityExcludingUndecidable: LayerCComparisonSummary;
};

export type ExploratoryLayerCResult = {
  analysisKind: 'exploratory';
  warning: typeof EXPLORATORY_WARNING;
  strata: {
    likelihood: LayerCStratumSummary[];
    influence: LayerCStratumSummary[];
  };
  pathways: LayerCPathwaySummary[];
};

export type FinalLayerCResult = {
  analysisKind: 'final_confirmatory';
  primaryOutcomePolicy: 'realized_vs_all_other_judged';
  comparison: LayerCComparisonSummary;
  fisherTwoSidedP: number | null;
  label: ConfirmatoryLabel;
  evidenceGrade: '★★';
  limitation: 'randomized_non_blinded_self_judgment';
};

function validateCount(n: number, y: number, label: string): void {
  if (!Number.isInteger(n) || n < 0) throw new RangeError(`${label} n must be a non-negative integer`);
  if (!Number.isInteger(y) || y < 0 || y > n) {
    throw new RangeError(`${label} y must be an integer from 0 through n`);
  }
}

function logBeta(a: number, b: number): number {
  return logGamma(a) + logGamma(b) - logGamma(a + b);
}

function logChoose(n: number, k: number): number {
  if (!Number.isInteger(n) || n < 0 || !Number.isInteger(k) || k < 0 || k > n) {
    return Number.NEGATIVE_INFINITY;
  }
  return logGamma(n + 1) - logGamma(k + 1) - logGamma(n - k + 1);
}

function logSumExp(values: readonly number[]): number {
  if (values.length === 0) return Number.NEGATIVE_INFINITY;
  const max = Math.max(...values);
  if (max === Number.NEGATIVE_INFINITY) return max;
  return max + Math.log(values.reduce((sum, value) => sum + Math.exp(value - max), 0));
}

/**
 * Two-sided Fisher exact test for a 2x2 table with practice as group 1 and
 * sealed as group 2. Tables with an empty arm have no comparative p-value.
 */
export function fisherExactTwoSided(n1: number, y1: number, n2: number, y2: number): number | null {
  validateCount(n1, y1, 'group1');
  validateCount(n2, y2, 'group2');
  if (n1 === 0 || n2 === 0) return null;

  const total = n1 + n2;
  const totalRealized = y1 + y2;
  const minX = Math.max(0, n1 - (total - totalRealized));
  const maxX = Math.min(n1, totalRealized);
  const denominator = logChoose(total, n1);
  const logProbabilities: Array<{ x: number; logP: number }> = [];

  for (let x = minX; x <= maxX; x += 1) {
    logProbabilities.push({
      x,
      logP: logChoose(totalRealized, x) + logChoose(total - totalRealized, n1 - x) - denominator,
    });
  }

  const observed = logProbabilities.find((item) => item.x === y1);
  if (!observed) throw new Error('observed 2x2 table is outside Fisher support');
  const tolerance = 1e-12;
  const included = logProbabilities
    .filter((item) => item.logP <= observed.logP + tolerance)
    .map((item) => item.logP);
  return Math.min(1, Math.exp(logSumExp(included)));
}

/**
 * Frozen Layer C Bayes factor from DESIGN.md v2.0:
 * independent Beta(1,1) arm rates versus one common Beta(1,1) rate.
 */
export function twoGroupBayesFactor10(n1: number, y1: number, n2: number, y2: number): number {
  validateCount(n1, y1, 'group1');
  validateCount(n2, y2, 'group2');
  const logBf =
    logBeta(1 + y1, 1 + n1 - y1) +
    logBeta(1 + y2, 1 + n2 - y2) -
    logBeta(1 + y1 + y2, 1 + n1 + n2 - y1 - y2);
  return Math.exp(logBf);
}

function validateObservation(observation: LayerCWishObservation): void {
  if (!LAYER_C_ARMS.includes(observation.arm)) throw new RangeError('Layer C arm is invalid');
  if (!LAYER_C_OUTCOMES.includes(observation.outcome)) throw new RangeError('Layer C outcome is invalid');
  if (!LAYER_C_LIKELIHOODS.includes(observation.likelihood)) throw new RangeError('Layer C likelihood is invalid');
  if (!LAYER_C_INFLUENCES.includes(observation.influence)) throw new RangeError('Layer C influence is invalid');
  if (observation.pathway !== undefined && !LAYER_C_PATHWAYS.includes(observation.pathway)) {
    throw new RangeError('Layer C pathway is invalid');
  }
  if (observation.outcome === 'realized' && observation.pathway === undefined) {
    throw new Error('realized Layer C outcome requires pathway');
  }
  if (observation.outcome !== 'realized' && observation.pathway !== undefined) {
    throw new Error('pathway is only valid for realized Layer C outcomes');
  }
}

function validateObservations(observations: readonly LayerCWishObservation[]): void {
  observations.forEach(validateObservation);
}

function armSummary(observations: readonly LayerCWishObservation[], arm: LayerCArm): LayerCArmSummary {
  const rows = observations.filter((item) => item.arm === arm);
  const realized = rows.filter((item) => item.outcome === 'realized').length;
  return {
    arm,
    n: rows.length,
    realized,
    notRealized: rows.length - realized,
    realizationRate: rows.length === 0 ? null : realized / rows.length,
    ci95: wilsonInterval95(realized, rows.length),
    withdrawn: rows.filter((item) => item.outcome === 'withdrawn').length,
    undecidable: rows.filter((item) => item.outcome === 'undecidable').length,
  };
}

export function summarizeLayerCComparison(
  observations: readonly LayerCWishObservation[],
): LayerCComparisonSummary {
  validateObservations(observations);
  const practice = armSummary(observations, 'practice');
  const sealed = armSummary(observations, 'sealed');
  return {
    baselineArm: 'sealed',
    practice,
    sealed,
    riskDifference:
      practice.realizationRate === null || sealed.realizationRate === null
        ? null
        : practice.realizationRate - sealed.realizationRate,
    bf10: twoGroupBayesFactor10(practice.n, practice.realized, sealed.n, sealed.realized),
  };
}

function pathwayDistribution(observations: readonly LayerCWishObservation[]): LayerCPathwaySummary[] {
  const realized = observations.filter((item) => item.outcome === 'realized');
  const practiceTotal = realized.filter((item) => item.arm === 'practice').length;
  const sealedTotal = realized.filter((item) => item.arm === 'sealed').length;
  return LAYER_C_PATHWAYS.map((pathway) => {
    const practice = realized.filter((item) => item.arm === 'practice' && item.pathway === pathway).length;
    const sealed = realized.filter((item) => item.arm === 'sealed' && item.pathway === pathway).length;
    return {
      pathway,
      practice,
      sealed,
      total: practice + sealed,
      practiceShare: practiceTotal === 0 ? null : practice / practiceTotal,
      sealedShare: sealedTotal === 0 ? null : sealed / sealedTotal,
    };
  });
}

function stratum(
  observations: readonly LayerCWishObservation[],
  key: string,
  label: string,
  predicate: (item: LayerCWishObservation) => boolean,
): LayerCStratumSummary {
  return { key, label, comparison: summarizeLayerCComparison(observations.filter(predicate)) };
}

export function analyzeInterimLayerC(observations: readonly LayerCWishObservation[]): InterimLayerCResult {
  validateObservations(observations);
  const sensitivityRows = observations.filter((item) => item.outcome !== 'undecidable');
  return {
    analysisKind: 'interim',
    primaryOutcomePolicy: 'realized_vs_all_other_judged',
    comparison: summarizeLayerCComparison(observations),
    sensitivityExcludingUndecidable: summarizeLayerCComparison(sensitivityRows),
  };
}

export function analyzeExploratoryLayerC(
  observations: readonly LayerCWishObservation[],
): ExploratoryLayerCResult {
  validateObservations(observations);
  return {
    analysisKind: 'exploratory',
    warning: EXPLORATORY_WARNING,
    strata: {
      likelihood: LAYER_C_LIKELIHOODS.map((likelihood) =>
        stratum(observations, `likelihood-${likelihood}`, `起きやすさ ${likelihood}`, (item) => item.likelihood === likelihood),
      ),
      influence: LAYER_C_INFLUENCES.map((influence) =>
        stratum(observations, `influence-${influence}`, `影響可能性 ${influence}`, (item) => item.influence === influence),
      ),
    },
    pathways: pathwayDistribution(observations),
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

/**
 * Final confirmatory Layer C API. The caller must pass only wishes eligible for
 * the frozen final denominator (deadline reached by experiment end). P10 owns
 * that final-ledger cutoff projection; this function owns only frozen statistics.
 */
export function analyzeFinalLayerC(
  observations: readonly LayerCWishObservation[],
  decisionRule: DecisionRule,
): FinalLayerCResult {
  validateObservations(observations);
  validateDecisionRule(decisionRule);
  const comparison = summarizeLayerCComparison(observations);
  const fisherTwoSidedP = fisherExactTwoSided(
    comparison.practice.n,
    comparison.practice.realized,
    comparison.sealed.n,
    comparison.sealed.realized,
  );

  let label: ConfirmatoryLabel = 'inconclusive';
  if (
    fisherTwoSidedP !== null &&
    comparison.riskDifference !== null &&
    comparison.riskDifference > 0 &&
    fisherTwoSidedP < decisionRule.pThresh &&
    comparison.bf10 > decisionRule.bfPos
  ) {
    label = 'positive_pre_registered_result';
  } else if (
    comparison.practice.n > 0 &&
    comparison.sealed.n > 0 &&
    comparison.bf10 < decisionRule.bfNeg
  ) {
    label = 'negative_evidence';
  }

  return {
    analysisKind: 'final_confirmatory',
    primaryOutcomePolicy: 'realized_vs_all_other_judged',
    comparison,
    fisherTwoSidedP,
    label,
    evidenceGrade: '★★',
    limitation: 'randomized_non_blinded_self_judgment',
  };
}
