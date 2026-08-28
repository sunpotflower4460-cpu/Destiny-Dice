import { describe, expect, it } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
import { buildWishDeadlineCandidates, deriveDailySessionProgress } from './runtime';

function entry(seq: number, type: StoredLedgerEntry['type'], payload: object): StoredLedgerEntry {
  return {
    seq,
    type,
    payloadJson: JSON.stringify(payload),
    createdAt: '2026-09-01T00:00:00.000Z',
    prevHash: '0'.repeat(64),
    entryHash: String(seq).padStart(64, '0'),
  };
}

describe('P9 runtime continuity projections', () => {
  it('resumes at the first missing registered session without filling gaps', () => {
    const entries = [
      entry(1, 'session', { date: '2026-09-01', seqInDay: 1 }),
      entry(2, 'session', { date: '2026-09-01', seqInDay: 3 }),
      entry(3, 'session', { date: '2026-08-31', seqInDay: 2 }),
    ];
    expect(deriveDailySessionProgress(entries, '2026-09-01', 3)).toEqual({
      completedSessions: 2,
      nextSeqInDay: 2,
      complete: false,
    });
  });

  it('reports the day complete when every registered sequence exists', () => {
    const entries = [
      entry(1, 'session', { date: '2026-09-01', seqInDay: 1 }),
      entry(2, 'session', { date: '2026-09-01', seqInDay: 2 }),
    ];
    expect(deriveDailySessionProgress(entries, '2026-09-01', 2).complete).toBe(true);
  });

  it('builds deadline candidates without carrying wish text into the notification layer', () => {
    const entries = [
      entry(1, 'wish', {
        wishId: 'sealed-1',
        text: 'this must remain sealed',
        deadline: '2026-09-14',
        likelihood: 2,
        influence: 'external',
        createdAt: '2026-09-01T00:00:00.000Z',
      }),
      entry(2, 'assignment', {
        wishId: 'sealed-1',
        arm: 'sealed',
        rngSource: 'local',
        bit: 0,
        committedAt: '2026-09-01T00:00:01.000Z',
      }),
    ];
    const candidates = buildWishDeadlineCandidates(entries);
    expect(candidates).toEqual([
      { wishId: 'sealed-1', deadline: '2026-09-14', assigned: true, judged: false },
    ]);
    expect(JSON.stringify(candidates)).not.toContain('this must remain sealed');
  });
});
