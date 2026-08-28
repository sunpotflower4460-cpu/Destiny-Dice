import type { StoredLedgerEntry } from '../ledger/types';
import type { SessionsPerDay } from '../registration/types';
import { buildWishLedgerRecords } from '../wish/projection';
import type { WishDeadlineCandidate } from './notifications';

export type DailySessionProgress = {
  completedSessions: number;
  nextSeqInDay: number | null;
  complete: boolean;
};

function sessionIdentity(entry: StoredLedgerEntry): { date: string; seqInDay: number } | null {
  if (entry.type !== 'session') return null;
  const parsed: unknown = JSON.parse(entry.payloadJson);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error(`session entry ${entry.seq} payload is not an object`);
  }
  const payload = parsed as { date?: unknown; seqInDay?: unknown };
  if (typeof payload.date !== 'string' || !Number.isInteger(payload.seqInDay)) {
    throw new Error(`session entry ${entry.seq} has invalid date/seqInDay`);
  }
  return { date: payload.date, seqInDay: payload.seqInDay as number };
}

export function deriveDailySessionProgress(
  entries: readonly StoredLedgerEntry[],
  experimentDate: string,
  sessionsPerDay: SessionsPerDay,
): DailySessionProgress {
  const completed = new Set<number>();
  for (const entry of entries) {
    const identity = sessionIdentity(entry);
    if (!identity || identity.date !== experimentDate) continue;
    if (identity.seqInDay < 1 || identity.seqInDay > sessionsPerDay) {
      throw new Error(`session entry ${entry.seq} seqInDay is outside registered range`);
    }
    if (completed.has(identity.seqInDay)) {
      throw new Error(`duplicate committed session for ${experimentDate} #${identity.seqInDay}`);
    }
    completed.add(identity.seqInDay);
  }

  let nextSeqInDay: number | null = null;
  for (let seq = 1; seq <= sessionsPerDay; seq += 1) {
    if (!completed.has(seq)) {
      nextSeqInDay = seq;
      break;
    }
  }

  return {
    completedSessions: completed.size,
    nextSeqInDay,
    complete: nextSeqInDay === null,
  };
}

/**
 * Notification scheduling receives only identity/deadline state; wish text is
 * deliberately excluded so sealed content cannot leak into notification payloads.
 */
export function buildWishDeadlineCandidates(entries: readonly StoredLedgerEntry[]): WishDeadlineCandidate[] {
  return buildWishLedgerRecords(entries).map((record) => ({
    wishId: record.wish.wishId,
    deadline: record.wish.deadline,
    assigned: record.assignment !== null,
    judged: record.judgment !== null,
  }));
}
