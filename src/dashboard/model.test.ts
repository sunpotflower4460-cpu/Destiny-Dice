import { describe, expect, it } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
import {
  ANALYSIS_PLAN_VERSION,
  APP_VERSION,
  CANONICALIZATION_VERSION,
  DEFAULT_DECISION_RULE,
  EXPERIMENT_DAYS,
  PROTOCOL_VERSION,
  RNG_POLICY_VERSION,
  SCHEDULE_ALGORITHM_VERSION,
  STATS_VERSION,
  TARGET_ALGORITHM_VERSION,
  type RegistrationPayload,
} from '../registration/types';
import { buildLayerADashboardModel } from './model';

const registration: RegistrationPayload = {
  experimentId: 'dashboard-test',
  startDate: '2026-09-01',
  days: EXPERIMENT_DAYS,
  bitsPerDraw: 1024,
  sessionsPerDay: 2,
  dayBoundaryHour: 3,
  affirmationText: 'test affirmation',
  predictionByCondition: ['a', 'b', 'c', 'd', 'e'],
  decisionRuleA: DEFAULT_DECISION_RULE,
  layerC: {
    enabled: true,
    defaultDeadlineDays: 28,
    withdrawalPolicy: 'count_as_fail',
    decisionRuleC: DEFAULT_DECISION_RULE,
    notarize: false,
  },
  schedule: Array.from({ length: EXPERIMENT_DAYS }, (_, index) => index % 5),
  scheduleSeed: 'schedule-seed',
  analysisPlanVersion: ANALYSIS_PLAN_VERSION,
  protocolVersion: PROTOCOL_VERSION,
  canonicalizationVersion: CANONICALIZATION_VERSION,
  scheduleAlgorithmVersion: SCHEDULE_ALGORITHM_VERSION,
  targetAlgorithmVersion: TARGET_ALGORITHM_VERSION,
  targetSeed: 'target-seed',
  timeZone: 'Asia/Tokyo',
  rngPolicyVersion: RNG_POLICY_VERSION,
  statsVersion: STATS_VERSION,
  appVersion: APP_VERSION,
};

function entry(seq: number, type: StoredLedgerEntry['type'], payload: Record<string, unknown>): StoredLedgerEntry {
  return {
    seq,
    type,
    payloadJson: JSON.stringify(payload),
    createdAt: `2026-09-01T00:00:0${Math.min(seq, 9)}.000Z`,
    prevHash: '0'.repeat(64),
    entryHash: `${seq}`.padStart(64, '0'),
  };
}

function sessionPayload(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    date: '2026-09-01',
    seqInDay: 1,
    condition: 0,
    targetDir: 1,
    rngSource: 'anu',
    predictionSeq: 2,
    bitsHex: '00',
    nBits: 1024,
    hits: 544,
    z: 2,
    ritual: { kind: 'pull_only', seconds: 60, valid: true },
    moodPre: { v: 5, e: 5 },
    moodPost: { v: 5, e: 5 },
    context: { hour: 9, dow: 2, lunarPhase: 0.5 },
    startedAt: '2026-09-01T00:00:00.000Z',
    completedAt: '2026-09-01T00:01:00.000Z',
    ...overrides,
  };
}

describe('P6 dashboard projection', () => {
  it('uses the P5 interim boundary for cards and never exposes confirmatory p-values', () => {
    const model = buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload()),
      entry(3, 'session', sessionPayload({ seqInDay: 2, rngSource: 'local', hits: 1024, z: 32 })),
      entry(4, 'session', sessionPayload({ date: '2026-09-02', condition: 1, ritual: { kind: 'intention_writing', seconds: 90, valid: false }, hits: 1024, z: 32 })),
    ], registration);

    expect(model.analysisKind).toBe('interim');
    expect(model.primarySample).toBe('anu_valid_only');
    expect(model.primarySessions).toBe(1);
    expect(model.validSessions).toBe(2);
    expect(model.conditionCards[0]).toMatchObject({ sessions: 1, nBits: 1024, hits: 544, expectedHits: 512, z: 2 });
    expect('rawP' in model.conditionCards[0]!).toBe(false);
    expect('holmAdjustedP' in model.conditionCards[0]!).toBe(false);
    expect(model.sourceCounts.local.sessions).toBe(1);
    expect(model.fallbackSessions).toBe(1);
    expect(model.ritualInvalidSessions).toBe(1);
  });

  it('keeps fallback miracles visible but marks them outside the primary quantum sample', () => {
    const model = buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload({ rngSource: 'local', hits: 1024, z: 32 })),
    ], registration);

    expect(model.miracles).toEqual([
      expect.objectContaining({ rngSource: 'local', primaryQuantumSample: false, expectedHits: 512 }),
    ]);
    expect(model.conditionCards[0]?.sessions).toBe(0);
  });

  it('does not leak future registered conditions through the 365-day calendar', () => {
    const model = buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload()),
    ], registration);

    expect(model.calendar).toHaveLength(365);
    expect(model.calendar[0]).toMatchObject({ date: '2026-09-01', condition: 0, recordedSessions: 1 });
    expect(model.calendar[1]).toMatchObject({ date: '2026-09-02', condition: null, conditionLabel: null, recordedSessions: 0 });
    expect(model.calendar[1]?.condition).not.toBe(registration.schedule[1]);
  });

  it('summarizes overall and per-source machine-control QC', () => {
    const model = buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'control', { date: '2026-09-01', rngSource: 'anu', nBits: 8, hits: 8, z: 2.82842712474619 }),
      entry(3, 'control', { date: '2026-09-02', rngSource: 'local', nBits: 8, hits: 0, z: -2.82842712474619 }),
    ], registration);

    expect(model.controlQc).toMatchObject({ sessions: 2, nBits: 16, hits: 8, hitRate: 0.5, z: 0 });
    expect(model.controlQc.bySource.anu).toMatchObject({ sessions: 1, hits: 8 });
    expect(model.controlQc.bySource.local).toMatchObject({ sessions: 1, hits: 0 });
  });

  it('rejects a session whose recorded z no longer matches the frozen P4a computation', () => {
    expect(() => buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload({ z: 9 })),
    ], registration)).toThrow('recorded z does not match frozen stats core');
  });

  it('rejects session records outside the frozen experiment window', () => {
    expect(() => buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload({ date: '2027-09-01' })),
    ], registration)).toThrow('outside the experiment window');
  });

  it('rejects a recorded condition that disagrees with the preregistered schedule', () => {
    expect(() => buildLayerADashboardModel([
      entry(1, 'registration', registration),
      entry(2, 'session', sessionPayload({ condition: 4 })),
    ], registration)).toThrow('does not match registered schedule');
  });
});
