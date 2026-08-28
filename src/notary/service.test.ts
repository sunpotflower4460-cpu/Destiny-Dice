import { describe, expect, it, vi } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
import {
  ANALYSIS_PLAN_VERSION,
  APP_VERSION,
  CANONICALIZATION_VERSION,
  EXPERIMENT_DAYS,
  PROTOCOL_VERSION,
  RNG_POLICY_VERSION,
  SCHEDULE_ALGORITHM_VERSION,
  STATS_VERSION,
  TARGET_ALGORITHM_VERSION,
  type RegistrationPayload,
} from '../registration/types';
import { WeeklyNotaryService, buildAnchorPayload, experimentWeekIndex } from './service';
import type { NotaryAttemptStore } from './types';

const HASH_A = 'a'.repeat(64);
const HASH_B = 'b'.repeat(64);

function registration(notarize = true): RegistrationPayload {
  return {
    experimentId: 'exp-1',
    startDate: '2026-09-01',
    days: EXPERIMENT_DAYS,
    bitsPerDraw: 1024,
    sessionsPerDay: 1,
    dayBoundaryHour: 3,
    affirmationText: 'test',
    predictionByCondition: ['a', 'b', 'c', 'd', 'e'],
    decisionRuleA: { pThresh: 0.01, bfPos: 30, bfNeg: 1 / 30 },
    layerC: {
      enabled: true,
      defaultDeadlineDays: 28,
      withdrawalPolicy: 'count_as_fail',
      decisionRuleC: { pThresh: 0.01, bfPos: 30, bfNeg: 1 / 30 },
      notarize,
    },
    schedule: Array.from({ length: EXPERIMENT_DAYS }, (_, index) => index % 5),
    scheduleSeed: '11'.repeat(32),
    analysisPlanVersion: ANALYSIS_PLAN_VERSION,
    protocolVersion: PROTOCOL_VERSION,
    canonicalizationVersion: CANONICALIZATION_VERSION,
    scheduleAlgorithmVersion: SCHEDULE_ALGORITHM_VERSION,
    targetAlgorithmVersion: TARGET_ALGORITHM_VERSION,
    targetSeed: '22'.repeat(32),
    timeZone: 'Asia/Tokyo',
    rngPolicyVersion: RNG_POLICY_VERSION,
    statsVersion: STATS_VERSION,
    appVersion: APP_VERSION,
  };
}

function entries(): StoredLedgerEntry[] {
  return [{
    seq: 9,
    type: 'session',
    payloadJson: '{}',
    createdAt: '2026-09-01T04:00:00.000Z',
    prevHash: HASH_A,
    entryHash: HASH_B,
  }];
}

class MemoryAttempts implements NotaryAttemptStore {
  private readonly values = new Map<string, number>();
  getLastAttemptedWeek(genesisHash: string): number | null {
    return this.values.get(genesisHash) ?? null;
  }
  setLastAttemptedWeek(genesisHash: string, weekIndex: number): void {
    this.values.set(genesisHash, weekIndex);
  }
}

describe('weekly notary', () => {
  it('derives frozen experiment weeks and rejects dates outside the experiment', () => {
    const value = registration();
    expect(experimentWeekIndex(value, '2026-09-01')).toBe(0);
    expect(experimentWeekIndex(value, '2026-09-07')).toBe(0);
    expect(experimentWeekIndex(value, '2026-09-08')).toBe(1);
    expect(experimentWeekIndex(value, '2026-08-31')).toBeNull();
    expect(experimentWeekIndex(value, '2027-09-01')).toBeNull();
  });

  it('builds a privacy-minimal anchor from the ledger head only', () => {
    expect(buildAnchorPayload(entries(), HASH_A, PROTOCOL_VERSION)).toEqual({
      genesisHash: HASH_A,
      headHash: HASH_B,
      headSeq: 9,
      protocolVersion: '2.1',
    });
  });

  it('does not call a network when notarization is disabled or unconfigured', async () => {
    const fetchImpl = vi.fn<typeof fetch>();
    const attempts = new MemoryAttempts();
    const disabled = new WeeklyNotaryService('https://notary.example', attempts, fetchImpl);
    expect((await disabled.publishIfDue({
      registration: registration(false),
      currentExperimentDate: '2026-09-01',
      entries: entries(),
      genesisHash: HASH_A,
    })).status).toBe('skipped');

    const missing = new WeeklyNotaryService(null, attempts, fetchImpl);
    expect((await missing.publishIfDue({
      registration: registration(),
      currentExperimentDate: '2026-09-01',
      entries: entries(),
      genesisHash: HASH_A,
    })).status).toBe('skipped');
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it('publishes once per week and sends no experiment content beyond the anchor contract', async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response('{}', { status: 201 }));
    const service = new WeeklyNotaryService('https://notary.example/', new MemoryAttempts(), fetchImpl);
    const input = { registration: registration(), entries: entries(), genesisHash: HASH_A };

    const first = await service.publishIfDue({ ...input, currentExperimentDate: '2026-09-01' });
    expect(first.status).toBe('published');
    const second = await service.publishIfDue({ ...input, currentExperimentDate: '2026-09-05' });
    expect(second).toMatchObject({ status: 'skipped', reason: 'already_attempted', weekIndex: 0 });
    expect(fetchImpl).toHaveBeenCalledTimes(1);

    const [, request] = fetchImpl.mock.calls[0] ?? [];
    expect(JSON.parse(String(request?.body))).toEqual({
      genesisHash: HASH_A,
      headHash: HASH_B,
      headSeq: 9,
      protocolVersion: '2.1',
    });
  });

  it('records an offline attempt and waits until the next experiment week', async () => {
    const fetchImpl = vi.fn<typeof fetch>()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(new Response('{}', { status: 201 }));
    const service = new WeeklyNotaryService('https://notary.example', new MemoryAttempts(), fetchImpl);
    const input = { registration: registration(), entries: entries(), genesisHash: HASH_A };

    expect(await service.publishIfDue({ ...input, currentExperimentDate: '2026-09-01' }))
      .toMatchObject({ status: 'failed', weekIndex: 0, error: 'offline' });
    expect(await service.publishIfDue({ ...input, currentExperimentDate: '2026-09-02' }))
      .toMatchObject({ status: 'skipped', reason: 'already_attempted', weekIndex: 0 });
    expect((await service.publishIfDue({ ...input, currentExperimentDate: '2026-09-08' })).status)
      .toBe('published');
    expect(fetchImpl).toHaveBeenCalledTimes(2);
  });
});
