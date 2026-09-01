import type { OrphanedPredictionSlot, SessionPlan } from '../session/types';

export type InFlightSession = {
  experimentDate: string;
  seqInDay: number;
  startedAt: string;
};

export type TodaySessionDecision =
  | { kind: 'idle' }
  | { kind: 'abandon'; orphan: OrphanedPredictionSlot }
  | { kind: 'prepare'; startedAt: string }
  | { kind: 'preserve'; plan: SessionPlan; startedAt: string };

/**
 * Same-process recovery vs true restart abandonment.
 * Process-local startedAt can finish an orphan prediction; a new process cannot
 * reconstruct Layer B order, so that slot stays missing.
 */
export function decideTodaySessionPresentation(input: {
  experimentDate: string;
  nextSeqInDay: number | null;
  orphanForNextSeq: OrphanedPredictionSlot | null;
  inFlight: InFlightSession | null;
  previousPlan: SessionPlan | null;
  inFlightSessionCommitted: boolean;
  now: string;
}): TodaySessionDecision {
  const { experimentDate, nextSeqInDay, orphanForNextSeq, inFlight, previousPlan, inFlightSessionCommitted, now } =
    input;

  if (
    inFlight &&
    inFlightSessionCommitted &&
    previousPlan &&
    previousPlan.experimentDate === inFlight.experimentDate &&
    previousPlan.seqInDay === inFlight.seqInDay
  ) {
    return { kind: 'preserve', plan: previousPlan, startedAt: inFlight.startedAt };
  }

  if (nextSeqInDay === null) return { kind: 'idle' };

  if (orphanForNextSeq) {
    const recoverable =
      inFlight !== null &&
      inFlight.experimentDate === experimentDate &&
      inFlight.seqInDay === nextSeqInDay &&
      inFlight.startedAt <= orphanForNextSeq.committedAt;
    if (recoverable && inFlight) {
      return { kind: 'prepare', startedAt: inFlight.startedAt };
    }
    return { kind: 'abandon', orphan: orphanForNextSeq };
  }

  const startedAt =
    inFlight && inFlight.experimentDate === experimentDate && inFlight.seqInDay === nextSeqInDay
      ? inFlight.startedAt
      : now;
  return { kind: 'prepare', startedAt };
}
