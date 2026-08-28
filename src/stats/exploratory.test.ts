import { describe, expect, it } from 'vitest';
import {
  EXPLORATORY_WARNING,
  doseResponse,
  predictionCalibration,
  quarterlyTrend,
} from './exploratory';
import type { LayerASessionObservation } from './layerA';

function observation(
  condition: 0 | 1 | 2 | 3 | 4,
  hits: number,
  extras: Partial<LayerASessionObservation> = {},
): LayerASessionObservation {
  return {
    condition,
    rngSource: 'anu',
    ritualValid: true,
    nBits: 8,
    hits,
    ...extras,
  };
}

describe('P5 exploratory Layer A analysis', () => {
  it('locks a five-band confidence calibration golden example', () => {
    const result = predictionCalibration([
      observation(0, 4, { confidence: 10 }),
      observation(0, 5, { confidence: 30 }),
      observation(0, 6, { confidence: 50 }),
      observation(0, 7, { confidence: 70 }),
      observation(0, 8, { confidence: 90 }),
    ]);

    expect(result.analysisKind).toBe('exploratory');
    expect(result.warning).toBe(EXPLORATORY_WARNING);
    expect(result.confidenceZCorrelation).toBeCloseTo(1, 12);
    expect(result.bins.map((bin) => [bin.minConfidence, bin.maxConfidence, bin.hitRate])).toEqual([
      [0, 20, 0.5],
      [21, 40, 0.625],
      [41, 60, 0.75],
      [61, 80, 0.875],
      [81, 100, 1],
    ]);
  });

  it('keeps fallback sessions available to exploratory calibration while excluding invalid rituals', () => {
    const result = predictionCalibration([
      observation(0, 6, { confidence: 80, rngSource: 'local' }),
      observation(0, 8, { confidence: 100, ritualValid: false }),
    ]);

    expect(result.sessions).toBe(1);
    expect(result.bins[3]?.nBits).toBe(8);
    expect(result.bins[4]?.nBits).toBe(0);
  });

  it('summarizes the frozen P1 < P2/P3/P4 < P5 dose ordering', () => {
    const result = doseResponse([
      observation(0, 4, { ritualSeconds: 60 }),
      observation(1, 5, { ritualSeconds: 60 }),
      observation(1, 6, { ritualSeconds: 120 }),
      observation(4, 7, { ritualSeconds: 480 }),
    ]);

    expect(result.groups.map((group) => [group.dose, group.nBits, group.hitRate])).toEqual([
      [0, 8, 0.5],
      [1, 16, 11 / 16],
      [2, 8, 0.875],
    ]);
    expect(result.doseZCorrelation).toBeGreaterThan(0.8);
    expect(result.durationByCondition[1]?.ritualSecondsZCorrelation).toBeCloseTo(1, 12);
  });

  it('splits the frozen 365-day window into four quarters and reports session-order trend', () => {
    const result = quarterlyTrend([
      observation(0, 4, { date: '2026-09-01' }),
      observation(0, 5, { date: '2026-12-02' }),
      observation(0, 6, { date: '2027-03-03' }),
      observation(0, 7, { date: '2027-06-02' }),
    ], '2026-09-01');

    expect(result.quarters.map((quarter) => [quarter.quarter, quarter.sessions, quarter.hitRate])).toEqual([
      [1, 1, 0.5],
      [2, 1, 0.625],
      [3, 1, 0.75],
      [4, 1, 0.875],
    ]);
    expect(result.byCondition[0]?.sessions).toBe(4);
    expect(result.byCondition[0]?.zSlopePerSession).toBeGreaterThan(0);
    expect(result.byCondition[0]?.ordinalZCorrelation).toBeCloseTo(1, 12);
  });

  it('rejects dates outside the registered experiment window', () => {
    expect(() => quarterlyTrend([
      observation(0, 4, { date: '2027-09-01' }),
    ], '2026-09-01')).toThrow('outside the experiment window');
  });
});
