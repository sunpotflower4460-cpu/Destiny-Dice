import { logGamma, standardNormalCdf } from './math';
import { zScore } from './core';

export type ConfidenceInterval = {
  lower: number;
  upper: number;
  level: 0.95;
  method: 'wilson';
};

function validateCountPair(hits: number, nBits: number): void {
  if (!Number.isInteger(nBits) || nBits < 0) {
    throw new RangeError('nBits must be a non-negative integer');
  }
  if (!Number.isInteger(hits) || hits < 0 || hits > nBits) {
    throw new RangeError('hits must be an integer from 0 through nBits');
  }
}

/**
 * Wilson score interval for a binomial proportion at 95% confidence.
 * DESIGN.md requires a CI but does not prescribe the construction; stats-plan-v1
 * names and tests this implementation so it cannot change silently mid experiment.
 */
export function wilsonInterval95(hits: number, nBits: number): ConfidenceInterval | null {
  validateCountPair(hits, nBits);
  if (nBits === 0) return null;

  const z = 1.959963984540054;
  const p = hits / nBits;
  const z2 = z * z;
  const denominator = 1 + z2 / nBits;
  const center = (p + z2 / (2 * nBits)) / denominator;
  const halfWidth =
    (z / denominator) * Math.sqrt((p * (1 - p)) / nBits + z2 / (4 * nBits * nBits));

  return {
    lower: Math.max(0, center - halfWidth),
    upper: Math.min(1, center + halfWidth),
    level: 0.95,
    method: 'wilson',
  };
}

/**
 * Frozen confirmatory frequentist approximation: two-sided normal approximation
 * from the P4a z statistic, with no continuity correction.
 */
export function twoSidedBinomialNormalApproxP(hits: number, nBits: number): number | null {
  validateCountPair(hits, nBits);
  if (nBits === 0) return null;
  const z = Math.abs(zScore(hits, nBits));
  return Math.min(1, 2 * (1 - standardNormalCdf(z)));
}

/** Beta(1,1) alternative versus point null p=0.5. */
export function oneSampleBayesFactor10(hits: number, nBits: number): number {
  validateCountPair(hits, nBits);
  const misses = nBits - hits;
  const logBf =
    logGamma(1 + hits) +
    logGamma(1 + misses) -
    logGamma(2 + nBits) +
    nBits * Math.log(2);
  return Math.exp(logBf);
}

/**
 * Holm step-down adjusted p-values, returned in the caller's original order.
 * Null entries are preserved and excluded from the family size.
 */
export function holmAdjust(pValues: readonly (number | null)[]): (number | null)[] {
  const indexed = pValues.flatMap((value, index) => {
    if (value === null) return [];
    if (!Number.isFinite(value) || value < 0 || value > 1) {
      throw new RangeError('p-values must be null or finite numbers in [0, 1]');
    }
    return [{ index, value }];
  });

  indexed.sort((a, b) => a.value - b.value || a.index - b.index);
  const adjusted = new Array<number | null>(pValues.length).fill(null);
  let runningMax = 0;
  const m = indexed.length;

  for (let rank = 0; rank < indexed.length; rank += 1) {
    const item = indexed[rank]!;
    const candidate = Math.min(1, (m - rank) * item.value);
    runningMax = Math.max(runningMax, candidate);
    adjusted[item.index] = runningMax;
  }

  return adjusted;
}
