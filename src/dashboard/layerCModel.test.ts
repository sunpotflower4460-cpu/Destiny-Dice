import { describe, expect, it } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import { buildLayerCDashboardModel } from './layerCModel';

function entry(seq: number, type: StoredLedgerEntry['type'], payload: Record<string, unknown>): StoredLedgerEntry {
  return {
    seq,
    type,
    payloadJson: JSON.stringify(payload),
    createdAt: '2026-09-01T00:00:00.000Z',
    prevHash: '0'.repeat(64),
    entryHash: `${seq}`.padStart(64, '0'),
  };
}

function wishEntry(seq: number, wishId: string, text: string, likelihood = 2, influence = 'mixed'): StoredLedgerEntry {
  return entry(seq, 'wish', {
    wishId,
    text,
    deadline: '2026-09-30',
    likelihood,
    influence,
    createdAt: '2026-09-01T00:00:00.000Z',
  });
}

function assignmentEntry(
  seq: number,
  wishId: string,
  arm: 'practice' | 'sealed',
  rngSource: 'anu' | 'randomorg' | 'local',
): StoredLedgerEntry {
  return entry(seq, 'assignment', {
    wishId,
    arm,
    rngSource,
    bit: arm === 'practice' ? 1 : 0,
    committedAt: '2026-09-01T00:00:01.000Z',
  });
}

function judgmentEntry(
  seq: number,
  wishId: string,
  outcome: 'realized' | 'not_realized' | 'undecidable' | 'withdrawn',
  pathway?: 'own_action' | 'other_person' | 'chance_encounter' | 'unknown',
): StoredLedgerEntry {
  return entry(seq, 'judgment', {
    wishId,
    outcome,
    ...(pathway === undefined ? {} : { pathway }),
    judgedAt: '2026-10-01T00:00:00.000Z',
  });
}

function registration(enabled = true): RegistrationPayload {
  return { layerC: { enabled } } as RegistrationPayload;
}

describe('P8 Layer C dashboard projection', () => {
  it('aggregates judged wishes without leaking sealed text into the dashboard model', () => {
    const entries = [
      wishEntry(1, 'p1', '実践願い', 1, 'self'),
      assignmentEntry(2, 'p1', 'practice', 'anu'),
      judgmentEntry(3, 'p1', 'realized', 'own_action'),
      wishEntry(4, 's1', '締切前UIへ漏らしてはいけない封印本文', 3, 'external'),
      assignmentEntry(5, 's1', 'sealed', 'local'),
      judgmentEntry(6, 's1', 'not_realized'),
      wishEntry(7, 'p2', '取り下げ願い', 2, 'mixed'),
      assignmentEntry(8, 'p2', 'practice', 'randomorg'),
      judgmentEntry(9, 'p2', 'withdrawn'),
      wishEntry(10, 'pending', '未判定願い'),
      assignmentEntry(11, 'pending', 'sealed', 'anu'),
      wishEntry(12, 'unassigned', '復旧待ち願い'),
    ];

    const model = buildLayerCDashboardModel(entries, registration());
    expect(model).not.toBeNull();
    expect(model).toMatchObject({
      totalWishes: 5,
      assignedWishes: 4,
      judgedWishes: 3,
      awaitingJudgment: 1,
      unassignedWishes: 1,
    });
    expect(model!.comparison.practice).toMatchObject({ n: 2, realized: 1, notRealized: 1, withdrawn: 1 });
    expect(model!.comparison.sealed).toMatchObject({ n: 1, realized: 0, notRealized: 1 });
    expect(model!.assignmentSourceCounts).toEqual({
      anu: { total: 2, practice: 1, sealed: 1 },
      randomorg: { total: 1, practice: 1, sealed: 0 },
      local: { total: 1, practice: 0, sealed: 1 },
    });
    expect(JSON.stringify(model)).not.toContain('締切前UIへ漏らしてはいけない封印本文');
    expect(JSON.stringify(model)).not.toContain('実践願い');
    expect('fisherTwoSidedP' in model!).toBe(false);
  });

  it('returns null when Layer C was disabled at registration', () => {
    expect(buildLayerCDashboardModel([], registration(false))).toBeNull();
  });

  it('rejects a judgment that exists without a committed assignment', () => {
    const entries = [
      wishEntry(1, 'broken', '不正な願い'),
      judgmentEntry(2, 'broken', 'not_realized'),
    ];
    expect(() => buildLayerCDashboardModel(entries, registration())).toThrow('without assignment');
  });
});
