import { describe, expect, it } from 'vitest';
import {
  approximateLunarPhase,
  experimentDayBoundaryInstant,
  resolveExperimentDate,
  systemContext,
  zonedDateTimeToInstant,
} from './time';

describe('P9 frozen experiment-day semantics', () => {
  it('keeps 02:59 in the previous experiment day and flips exactly at 03:00 in Asia/Tokyo', () => {
    expect(resolveExperimentDate('2026-09-01T17:59:59.000Z', 'Asia/Tokyo', 3)).toBe('2026-09-01');
    expect(resolveExperimentDate('2026-09-01T18:00:00.000Z', 'Asia/Tokyo', 3)).toBe('2026-09-02');
  });

  it('uses the registered timezone rather than the runtime/device timezone', () => {
    const instant = '2026-09-01T18:00:00.000Z';
    expect(resolveExperimentDate(instant, 'Asia/Tokyo', 3)).toBe('2026-09-02');
    expect(resolveExperimentDate(instant, 'America/Los_Angeles', 3)).toBe('2026-09-01');
  });

  it('converts the frozen wall-clock boundary to an absolute instant', () => {
    expect(experimentDayBoundaryInstant('2026-09-02', 'Asia/Tokyo', 3)).toBe('2026-09-01T18:00:00.000Z');
    expect(zonedDateTimeToInstant('2026-09-02', '20:30', 'Asia/Tokyo')).toBe('2026-09-02T11:30:00.000Z');
  });

  it('uses Intl timezone rules across DST and rejects a nonexistent wall-clock time', () => {
    expect(zonedDateTimeToInstant('2026-03-08', '03:00', 'America/New_York')).toBe('2026-03-08T07:00:00.000Z');
    expect(() => zonedDateTimeToInstant('2026-03-08', '02:30', 'America/New_York')).toThrow('does not resolve exactly');
  });

  it('derives deterministic session context in the frozen timezone', () => {
    const context = systemContext('2026-09-01T18:30:00.000Z', 'Asia/Tokyo');
    expect(context.hour).toBe(3);
    expect(context.dow).toBe(3); // 2026-09-02 Wednesday
    expect(context.lunarPhase).toBeGreaterThanOrEqual(0);
    expect(context.lunarPhase).toBeLessThan(1);
  });

  it('anchors the exploratory lunar phase approximation at the documented reference new moon', () => {
    expect(approximateLunarPhase('2000-01-06T18:14:00.000Z')).toBeCloseTo(0, 12);
    expect(approximateLunarPhase('2000-01-21T12:36:50.000Z')).toBeCloseTo(0.5, 4);
  });
});
