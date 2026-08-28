import { describe, expect, it } from 'vitest';
import { analyzeLayerBMood, meanNormal95, miracleProfile, stateDependence, type RichSessionObservation } from './reportExploratory';

const sessions: RichSessionObservation[] = [
  { condition: 0, rngSource: 'anu', ritualValid: true, nBits: 1024, hits: 512, confidence: 10, moodPreV: 4, moodPreE: 5, hour: 8, dow: 1, lunarPhase: 0.1, stateTag: 'calm' },
  { condition: 1, rngSource: 'anu', ritualValid: true, nBits: 1024, hits: 544, confidence: 90, moodPreV: 8, moodPreE: 9, hour: 20, dow: 2, lunarPhase: 0.9, stateTag: 'energized' },
  { condition: 2, rngSource: 'local', ritualValid: true, nBits: 1024, hits: 560, confidence: 80, moodPreV: 7, moodPreE: 8, hour: 18, dow: 2, lunarPhase: 0.8, stateTag: 'energized' },
];

describe('P10 report exploratory statistics', () => {
  it('has a fixed normal-approximation 95% mean CI golden', () => {
    const result = meanNormal95([1, 2, 3]);
    expect(result.n).toBe(3);
    expect(result.mean).toBe(2);
    expect(result.ci95?.low).toBeCloseTo(0.8683934723883333, 12);
    expect(result.ci95?.high).toBeCloseTo(3.131606527611667, 12);
    expect(meanNormal95([])).toEqual({ n: 0, mean: null, ci95: null });
  });

  it('summarizes paired mood changes by condition without using invalid rituals', () => {
    const result = analyzeLayerBMood([
      { condition: 0, ritualValid: true, moodPreV: 4, moodPreE: 5, moodPostV: 6, moodPostE: 6 },
      { condition: 0, ritualValid: true, moodPreV: 5, moodPreE: 5, moodPostV: 6, moodPostE: 7 },
      { condition: 0, ritualValid: false, moodPreV: 1, moodPreE: 1, moodPostV: 10, moodPostE: 10 },
    ]);
    expect(result.conditions[0]?.valenceChange.mean).toBe(1.5);
    expect(result.conditions[0]?.energyChange.mean).toBe(1.5);
    expect(result.conditions[0]?.valenceChange.n).toBe(2);
    expect(result.limitation).toBe('non_blinded_placebo_included');
  });

  it('computes state-dependence correlations and grouped summaries', () => {
    const result = stateDependence(sessions);
    expect(result.sessions).toBe(3);
    expect(result.correlations.moodPreVWithZ).toBeCloseTo(0.8386278693775346, 12);
    expect(result.byDow[2]?.summary.sessions).toBe(2);
    expect(result.byStateTag.map((item) => item.stateTag)).toEqual(['calm', 'energized']);
  });

  it('profiles resonance and target-miracle sessions with source disclosure', () => {
    const result = miracleProfile(sessions);
    expect(result.resonanceSessions).toBe(2);
    expect(result.targetMiracleSessions).toBe(1);
    expect(result.bySource.anu).toBe(1);
    expect(result.bySource.local).toBe(1);
    expect(result.averageConfidence).toBe(85);
  });
});
