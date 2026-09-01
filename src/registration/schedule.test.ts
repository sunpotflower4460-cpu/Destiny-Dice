import { describe, expect, it } from 'vitest';
import { assertTargetAlgorithmVersion, generateConditionSchedule, generateTargetSchedule } from './schedule';

function counts(values: readonly number[]): number[] {
  return [0, 1, 2, 3, 4].map((condition) => values.filter((value) => value === condition).length);
}

describe('registration schedules', () => {
  it('creates a balanced 365-day condition schedule deterministically', async () => {
    const first = await generateConditionSchedule(365, 'schedule-seed');
    const second = await generateConditionSchedule(365, 'schedule-seed');
    expect(second).toEqual(first);
    expect(first).toHaveLength(365);
    expect(counts(first)).toEqual([73, 73, 73, 73, 73]);

    for (let offset = 0; offset < first.length; offset += 5) {
      expect([...first.slice(offset, offset + 5)].sort()).toEqual([0, 1, 2, 3, 4]);
    }
  });

  it('matches frozen cross-platform golden vectors', async () => {
    expect((await generateConditionSchedule(15, 'schedule-seed')).slice(0, 15)).toEqual([
      3, 4, 2, 0, 1,
      2, 4, 0, 3, 1,
      2, 3, 4, 0, 1,
    ]);
    expect((await generateTargetSchedule(8, 3, 'target-seed')).slice(0, 24)).toEqual([
      0, 0, 0, 1, 1, 0, 1, 1,
      0, 0, 1, 0, 1, 1, 0, 1,
      0, 0, 1, 1, 0, 1, 1, 1,
    ]);
  });

  it('creates deterministic targets independently from condition generation', async () => {
    const targets = await generateTargetSchedule(365, 3, 'target-seed');
    expect(targets).toHaveLength(1095);
    expect(await generateTargetSchedule(365, 3, 'target-seed')).toEqual(targets);
    expect(await generateTargetSchedule(365, 3, 'different-target-seed')).not.toEqual(targets);
  });

  it('rejects a frozen targetAlgorithmVersion that is not the v1 generator', () => {
    expect(() => assertTargetAlgorithmVersion('sha256-counter-target-v1')).not.toThrow();
    expect(() => assertTargetAlgorithmVersion('other-algo')).toThrow('unsupported targetAlgorithmVersion');
  });
});
