import { describe, expect, it } from 'vitest';
import { decideTodaySessionPresentation } from './sessionRecovery';
import type { OrphanedPredictionSlot, SessionPlan } from '../session/types';

const plan: SessionPlan = {
  experimentDate: '2026-09-01',
  dayIndex: 0,
  seqInDay: 1,
  condition: 0,
  targetDir: 1,
};

const orphan: OrphanedPredictionSlot = {
  experimentDate: '2026-09-01',
  seqInDay: 1,
  predictionSeq: 3,
  committedAt: '2026-09-01T01:10:00.000Z',
};

describe('decideTodaySessionPresentation', () => {
  it('abandons an orphaned prediction when this process has no recoverable startedAt', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: 1,
        orphanForNextSeq: orphan,
        inFlight: null,
        previousPlan: null,
        inFlightSessionCommitted: false,
        now: '2026-09-01T02:00:00.000Z',
      }),
    ).toEqual({ kind: 'abandon', orphan });
  });

  it('reuses the in-process startedAt when it still precedes the committed prediction', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: 1,
        orphanForNextSeq: orphan,
        inFlight: { experimentDate: '2026-09-01', seqInDay: 1, startedAt: '2026-09-01T01:00:00.000Z' },
        previousPlan: plan,
        inFlightSessionCommitted: false,
        now: '2026-09-01T02:00:00.000Z',
      }),
    ).toEqual({ kind: 'prepare', startedAt: '2026-09-01T01:00:00.000Z' });
  });

  it('abandons when a later startedAt cannot reconstruct pre-prediction measurements', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: 1,
        orphanForNextSeq: orphan,
        inFlight: { experimentDate: '2026-09-01', seqInDay: 1, startedAt: '2026-09-01T02:00:00.000Z' },
        previousPlan: plan,
        inFlightSessionCommitted: false,
        now: '2026-09-01T02:00:01.000Z',
      }),
    ).toEqual({ kind: 'abandon', orphan });
  });

  it('keeps the current plan after the session is committed so wish-moment UI can finish', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: null,
        orphanForNextSeq: null,
        inFlight: { experimentDate: '2026-09-01', seqInDay: 1, startedAt: '2026-09-01T01:00:00.000Z' },
        previousPlan: plan,
        inFlightSessionCommitted: true,
        now: '2026-09-01T02:00:00.000Z',
      }),
    ).toEqual({ kind: 'preserve', plan, startedAt: '2026-09-01T01:00:00.000Z' });
  });

  it('does not mint a new startedAt for the same in-flight slot before prediction', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: 1,
        orphanForNextSeq: null,
        inFlight: { experimentDate: '2026-09-01', seqInDay: 1, startedAt: '2026-09-01T01:00:00.000Z' },
        previousPlan: plan,
        inFlightSessionCommitted: false,
        now: '2026-09-01T02:00:00.000Z',
      }),
    ).toEqual({ kind: 'prepare', startedAt: '2026-09-01T01:00:00.000Z' });
  });

  it('starts a fresh slot with now when nothing is in flight', () => {
    expect(
      decideTodaySessionPresentation({
        experimentDate: '2026-09-01',
        nextSeqInDay: 1,
        orphanForNextSeq: null,
        inFlight: null,
        previousPlan: null,
        inFlightSessionCommitted: false,
        now: '2026-09-01T02:00:00.000Z',
      }),
    ).toEqual({ kind: 'prepare', startedAt: '2026-09-01T02:00:00.000Z' });
  });
});
