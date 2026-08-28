import { describe, expect, it } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
import { deriveGentleStreak } from './streak';

function session(seq: number, date: string, seqInDay = 1): StoredLedgerEntry {
  return {
    seq,
    type: 'session',
    payloadJson: JSON.stringify({ date, seqInDay }),
    createdAt: `${date}T12:00:00.000Z`,
    prevHash: '0'.repeat(64),
    entryHash: `${seq}`.padStart(64, '0'),
  };
}

describe('P9 gentle streak', () => {
  it('counts unique completed experiment days and the recent run without penalties', () => {
    const streak = deriveGentleStreak([
      session(1, '2026-09-01'),
      session(2, '2026-09-01', 2),
      session(3, '2026-09-02'),
      session(4, '2026-09-04'),
      session(5, '2026-09-05'),
    ], '2026-09-05');

    expect(streak).toEqual({
      completedDays: 4,
      recentRunDays: 2,
      latestCompletedDate: '2026-09-05',
      currentExperimentDayComplete: true,
      penaltyApplied: false,
    });
  });

  it('does not invent missing sessions or treat a rest day as experiment invalidation', () => {
    const streak = deriveGentleStreak([
      session(1, '2026-09-01'),
      session(2, '2026-09-03'),
    ], '2026-09-04');
    expect(streak.completedDays).toBe(2);
    expect(streak.recentRunDays).toBe(1);
    expect(streak.currentExperimentDayComplete).toBe(false);
    expect(streak.penaltyApplied).toBe(false);
  });
});
