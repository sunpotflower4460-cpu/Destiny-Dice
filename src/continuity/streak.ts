import type { StoredLedgerEntry } from '../ledger/types';
import { addCalendarDays } from './time';

export type GentleStreak = {
  completedDays: number;
  recentRunDays: number;
  latestCompletedDate: string | null;
  currentExperimentDayComplete: boolean;
  penaltyApplied: false;
};

function sessionDate(entry: StoredLedgerEntry): string | null {
  if (entry.type !== 'session') return null;
  const parsed: unknown = JSON.parse(entry.payloadJson);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error(`session entry ${entry.seq} payload is not an object`);
  }
  const date = (parsed as { date?: unknown }).date;
  if (typeof date !== 'string') throw new Error(`session entry ${entry.seq} is missing date`);
  return date;
}

export function deriveGentleStreak(
  entries: readonly StoredLedgerEntry[],
  currentExperimentDate: string,
): GentleStreak {
  const dates = new Set<string>();
  for (const entry of entries) {
    const date = sessionDate(entry);
    if (date !== null && date <= currentExperimentDate) dates.add(date);
  }

  const ordered = [...dates].sort();
  const latestCompletedDate = ordered.at(-1) ?? null;
  let recentRunDays = 0;
  if (latestCompletedDate !== null) {
    let cursor = latestCompletedDate;
    while (dates.has(cursor)) {
      recentRunDays += 1;
      cursor = addCalendarDays(cursor, -1);
    }
  }

  return {
    completedDays: dates.size,
    recentRunDays,
    latestCompletedDate,
    currentExperimentDayComplete: dates.has(currentExperimentDate),
    penaltyApplied: false,
  };
}
