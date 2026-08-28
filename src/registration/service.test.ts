import { describe, expect, it } from 'vitest';
import { computeLedgerEntryHash } from '../ledger/hash';
import { LedgerService } from '../ledger/service';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { GENESIS_PREV_HASH } from '../ledger/types';
import { verifyChain } from '../ledger/verify';
import { RegistrationService } from './service';
import type { RegistrationInput, RegistrationPayload } from './types';

const input: RegistrationInput = {
  experimentId: 'exp-2026',
  startDate: '2026-09-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の意図に向き合う',
  predictionByCondition: ['P1は基準', 'P2は上がる', 'P3は上がる', 'P4は上がる', 'P5が最も上がる'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'schedule-seed',
  targetSeed: 'target-seed',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

const CREATED_AT = '2026-08-28T02:00:00.000Z';

describe('RegistrationService', () => {
  it('writes one frozen registration genesis containing protocol provenance', async () => {
    const store = new MemoryLedgerStore();
    const ledger = new LedgerService(store);
    const service = new RegistrationService(ledger);

    const result = await service.register(input, CREATED_AT);
    expect(result.seq).toBe(1);
    expect(result.genesisHash).toMatch(/^[0-9a-f]{64}$/);

    const entries = await ledger.list();
    expect(await verifyChain(entries)).toEqual({ ok: true, entries: 1, headHash: result.genesisHash });
    const payload = JSON.parse(entries[0]!.payloadJson) as RegistrationPayload;

    expect(payload.protocolVersion).toBe('2.1');
    expect(payload.canonicalizationVersion).toBe('rfc8785-jcs-v1');
    expect(payload.scheduleAlgorithmVersion).toBe('sha256-counter-fy-v1');
    expect(payload.targetAlgorithmVersion).toBe('sha256-counter-target-v1');
    expect(payload.rngPolicyVersion).toBe('rng-policy-v1');
    expect(payload.analysisPlanVersion).toBe('analysis-plan-v2.1');
    expect(payload.statsVersion).toBe('stats-plan-v1');
    expect(payload.appVersion).toBe('0.0.0');
    expect(payload.timeZone).toBe('Asia/Tokyo');
    expect(payload.targetSeed).toBe('target-seed');
    expect(payload.schedule).toHaveLength(365);
    expect(payload.decisionRuleA).toEqual({ pThresh: 0.01, bfPos: 30, bfNeg: 1 / 30 });
    expect(payload.layerC.withdrawalPolicy).toBe('count_as_fail');
    expect(payload.layerC.decisionRuleC).toEqual(payload.decisionRuleA);
  });

  it('commits predictions, decision rule, and Layer C settings into the genesis hash', async () => {
    const result = await new RegistrationService(new LedgerService(new MemoryLedgerStore())).register(input, CREATED_AT);
    const predictionChanged: RegistrationPayload = {
      ...result.payload,
      predictionByCondition: ['changed', ...result.payload.predictionByCondition.slice(1)],
    };
    const decisionChanged: RegistrationPayload = {
      ...result.payload,
      decisionRuleA: { ...result.payload.decisionRuleA, pThresh: 0.02 },
    };
    const layerCChanged: RegistrationPayload = {
      ...result.payload,
      layerC: { ...result.payload.layerC, enabled: false },
    };

    for (const payload of [predictionChanged, decisionChanged, layerCChanged]) {
      const hash = await computeLedgerEntryHash({
        type: 'registration',
        payload,
        createdAt: CREATED_AT,
        prevHash: GENESIS_PREV_HASH,
      });
      expect(hash).not.toBe(result.genesisHash);
    }
  });

  it('cannot register twice on the same ledger', async () => {
    const service = new RegistrationService(new LedgerService(new MemoryLedgerStore()));
    await service.register(input, CREATED_AT);
    await expect(service.register(input, '2026-08-28T02:01:00.000Z')).rejects.toThrow(
      'only one genesis registration',
    );
  });
});
