import { describe, expect, it } from 'vitest';
import { DEFAULT_DECISION_RULE } from '../registration/types';
import {
  analyzeControlQc,
  analyzeFinalLayerA,
  analyzeInterimLayerA,
  cumulativeDeviationSeries,
  type LayerASessionObservation,
} from './layerA';

function session(
  overrides: Partial<LayerASessionObservation> & Pick<LayerASessionObservation, 'condition' | 'nBits' | 'hits'>,
): LayerASessionObservation {
  return {
    rngSource: 'anu',
    ritualValid: true,
    ...overrides,
  };
}

describe('P5 Layer A boundaries', () => {
  it('keeps interim output structurally free of confirmatory p-values', () => {
    const result = analyzeInterimLayerA([
      session({ condition: 0, nBits: 1024, hits: 544 }),
      session({ condition: 0, nBits: 1024, hits: 1024, rngSource: 'local' }),
      session({ condition: 1, nBits: 1024, hits: 1024, ritualValid: false }),
    ]);

    expect(result.analysisKind).toBe('interim');
    expect(result.primarySample).toBe('anu_valid_only');
    expect(result.conditions[0]).toMatchObject({ nBits: 1024, hits: 544, z: 2 });
    expect('rawP' in result.conditions[0]!).toBe(false);
    expect('holmAdjustedP' in result.conditions[0]!).toBe(false);
    expect(result.sourceCounts.local).toEqual({ sessions: 1, bits: 1024 });
    expect(result.exclusions).toEqual({ fallbackSessions: 1, ritualInvalidSessions: 1 });
  });

  it('uses only ANU + ritual-valid sessions for final confirmatory decisions', () => {
    const result = analyzeFinalLayerA([
      session({ condition: 0, nBits: 16, hits: 16 }),
      session({ condition: 1, nBits: 4096, hits: 2048 }),
      session({ condition: 2, nBits: 16, hits: 16, rngSource: 'local' }),
      session({ condition: 3, nBits: 16, hits: 16, ritualValid: false }),
    ], DEFAULT_DECISION_RULE);

    expect(result.conditions[0]?.label).toBe('positive_pre_registered_result');
    expect(result.conditions[0]?.holmAdjustedP).not.toBeNull();
    expect(result.conditions[0]!.bf10).toBeGreaterThan(30);

    expect(result.conditions[1]?.label).toBe('negative_evidence');
    expect(result.conditions[1]!.bf10).toBeLessThan(1 / 30);

    expect(result.conditions[2]).toMatchObject({ nBits: 0, hits: 0, rawP: null, label: 'inconclusive' });
    expect(result.conditions[3]).toMatchObject({ nBits: 0, hits: 0, rawP: null, label: 'inconclusive' });
    expect(result.conditions[4]).toMatchObject({ nBits: 0, hits: 0, rawP: null, label: 'inconclusive' });
    expect(result.sourceCounts.local.sessions).toBe(1);
  });

  it('never substitutes fallback data to rescue an empty ANU primary sample', () => {
    const result = analyzeFinalLayerA([
      session({ condition: 4, nBits: 4096, hits: 4096, rngSource: 'randomorg' }),
    ], DEFAULT_DECISION_RULE);

    expect(result.conditions[4]).toMatchObject({
      sessions: 0,
      nBits: 0,
      hits: 0,
      rawP: null,
      holmAdjustedP: null,
      label: 'inconclusive',
    });
    expect(result.sourceCounts.randomorg).toEqual({ sessions: 1, bits: 4096 });
  });

  it('builds the cumulative bit-level deviation series and 95% chance envelope', () => {
    const points = cumulativeDeviationSeries([
      { nBits: 8, hits: 5 },
      { nBits: 8, hits: 3 },
    ]);

    expect(points[0]).toEqual({
      sessionIndex: 1,
      cumulativeBits: 8,
      cumulativeHits: 5,
      deviation: 1,
      envelope95: expect.closeTo(0.98 * Math.sqrt(8), 12),
    });
    expect(points[1]).toEqual({
      sessionIndex: 2,
      cumulativeBits: 16,
      cumulativeHits: 8,
      deviation: 0,
      envelope95: 3.92,
    });
  });

  it('summarizes machine-control QC without hiding source mix', () => {
    const qc = analyzeControlQc([
      { rngSource: 'anu', nBits: 8, hits: 8 },
      { rngSource: 'local', nBits: 8, hits: 0 },
    ]);

    expect(qc).toMatchObject({ sessions: 2, nBits: 16, hits: 8, hitRate: 0.5, z: 0 });
    expect(qc.sourceCounts).toEqual({
      anu: { sessions: 1, bits: 8 },
      randomorg: { sessions: 0, bits: 0 },
      local: { sessions: 1, bits: 8 },
    });
  });
});
