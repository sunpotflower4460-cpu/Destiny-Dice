import { describe, expect, it } from 'vitest';
import {
  holmAdjust,
  oneSampleBayesFactor10,
  twoSidedBinomialNormalApproxP,
  wilsonInterval95,
} from './inference';

describe('P5 inference', () => {
  it('matches frozen one-sample Bayes factor golden values', () => {
    expect(oneSampleBayesFactor10(8, 8)).toBeCloseTo(28.4444444444, 9);
    expect(oneSampleBayesFactor10(4, 8)).toBeCloseTo(0.4063492063, 9);
  });

  it('returns BF10=1 for zero data', () => {
    expect(oneSampleBayesFactor10(0, 0)).toBeCloseTo(1, 12);
  });

  it('matches a known Holm step-down example in original order', () => {
    expect(holmAdjust([0.01, 0.04, 0.03, 0.002])).toEqual([0.03, 0.06, 0.06, 0.008]);
  });

  it('excludes null p-values from the Holm family and preserves them', () => {
    expect(holmAdjust([0.01, null, 0.04])).toEqual([0.02, null, 0.04]);
  });

  it('uses the P4a z statistic for the two-sided normal approximation', () => {
    expect(twoSidedBinomialNormalApproxP(544, 1024)).toBeCloseTo(0.0455001, 5);
    expect(twoSidedBinomialNormalApproxP(512, 1024)).toBeCloseTo(1, 7);
    expect(twoSidedBinomialNormalApproxP(0, 0)).toBeNull();
  });

  it('locks the 95% Wilson interval implementation', () => {
    expect(wilsonInterval95(512, 1024)).toEqual({
      lower: expect.closeTo(0.46943284426047427, 12),
      upper: expect.closeTo(0.5305671557395257, 12),
      level: 0.95,
      method: 'wilson',
    });
    expect(wilsonInterval95(0, 0)).toBeNull();
  });

  it('rejects malformed counts and p-values instead of repairing them', () => {
    expect(() => oneSampleBayesFactor10(9, 8)).toThrow();
    expect(() => wilsonInterval95(-1, 8)).toThrow();
    expect(() => holmAdjust([0.5, 1.1])).toThrow();
  });
});
