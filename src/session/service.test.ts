import { describe, expect, it } from 'vitest';
import { LedgerService } from '../ledger/service';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { verifyChain } from '../ledger/verify';
import { RegistrationService } from '../registration/service';
import type { RegistrationInput } from '../registration/types';
import type { RandomBits } from '../rng/types';
import { summarizeBitstream } from '../stats/core';
import { SessionFlowService } from './service';
import type { Clock, SessionDraft } from './types';

const registrationInput: RegistrationInput = {
  experimentId: 'p4-e2e',
  startDate: '2026-09-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1', 'P2', 'P3', 'P4', 'P5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p4-schedule-seed',
  targetSeed: 'p4-target-seed',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

class QueueClock implements Clock {
  private readonly values: string[];

  constructor(values: string[]) {
    this.values = [...values];
  }

  now(): string {
    const value = this.values.shift();
    if (!value) throw new Error('clock exhausted');
    return value;
  }
}

class QueueRng {
  readonly calls: number[] = [];
  private readonly draws: RandomBits[];
  private readonly beforeCall?: (callNumber: number) => Promise<void>;

  constructor(draws: RandomBits[], beforeCall?: (callNumber: number) => Promise<void>) {
    this.draws = [...draws];
    this.beforeCall = beforeCall;
  }

  async getBits(nBits: number): Promise<RandomBits> {
    this.calls.push(nBits);
    await this.beforeCall?.(this.calls.length);
    const draw = this.draws.shift();
    if (!draw) throw new Error('rng exhausted');
    return draw;
  }
}

async function createRegisteredLedger(): Promise<LedgerService> {
  const ledger = new LedgerService(new MemoryLedgerStore());
  await new RegistrationService(ledger).register(registrationInput, '2026-08-28T02:00:00.000Z');
  return ledger;
}

function draft(planDate = '2026-09-01'): SessionDraft {
  return {
    experimentDate: planDate,
    seqInDay: 1,
    moodPre: { v: 6, e: 5 },
    ritual: { seconds: 600, text: '今日の的が実現した状態を具体的に思い描いて文章として十分な長さで記録する' },
    moodPost: { v: 7, e: 6 },
    confidence: 72,
    prophecyText: '今日は少し届く感じがする',
    context: { hour: 10, dow: 2, lunarPhase: 0.42, sleep: 7, stateTag: 'calm' },
    startedAt: '2026-09-01T01:00:00.000Z',
  };
}

function localDraw(bitsHex: string): RandomBits {
  return { bitsHex, nBits: 1024, source: 'local' };
}

describe('SessionFlowService', () => {
  it('creates control -> prediction -> session and verifies prediction before measured RNG', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng(
      [localDraw('aa'.repeat(128)), localDraw('ff'.repeat(128))],
      async (callNumber) => {
        if (callNumber !== 2) return;
        const entries = await ledger.list();
        expect(entries.at(-1)?.type).toBe('prediction');
      },
    );
    const clock = new QueueClock([
      '2026-09-01T00:00:00.000Z',
      '2026-09-01T01:10:00.000Z',
      '2026-09-01T01:10:01.000Z',
    ]);
    const service = new SessionFlowService(ledger, rng, clock);

    const plan = await service.prepareSession('2026-09-01', 1);
    const samePlan = await service.prepareSession('2026-09-01', 1);
    expect(samePlan).toEqual(plan);
    expect(rng.calls).toEqual([1024]);

    const result = await service.runSession(draft());
    const entries = await ledger.list();
    expect(entries.map((entry) => entry.type)).toEqual(['registration', 'control', 'prediction', 'session']);
    expect(result.predictionEntry.seq).toBeLessThan(result.sessionEntry.seq);
    expect(result.payload.predictionSeq).toBe(result.predictionEntry.seq);
    expect(result.payload.rngSource).toBe('local');
    expect(result.payload.targetDir).toBe(plan.targetDir);
    expect(result.payload.condition).toBe(plan.condition);
    expect(result.payload.ritual.valid).toBe(true);
    expect(rng.calls).toEqual([1024, 1024]);

    const expectedStats = summarizeBitstream('ff'.repeat(128), 1024, plan.targetDir);
    expect(result.payload.hits).toBe(expectedStats.hits);
    expect(result.payload.z).toBe(expectedStats.z);
    expect(await verifyChain(entries)).toEqual({
      ok: true,
      entries: 4,
      headHash: result.sessionEntry.entryHash,
    });
  });

  it('serializes concurrent daily-control preparation to one RNG acquisition', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('aa'.repeat(128))]);
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock(['2026-09-01T00:00:00.000Z']),
    );

    const [first, second] = await Promise.all([
      service.prepareSession('2026-09-01', 1),
      service.prepareSession('2026-09-01', 1),
    ]);
    expect(second).toEqual(first);
    expect(rng.calls).toEqual([1024]);
    expect((await ledger.list()).filter((entry) => entry.type === 'control')).toHaveLength(1);
  });

  it('rejects controls outside the frozen experiment window before RNG acquisition', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('aa'.repeat(128))]);
    const service = new SessionFlowService(ledger, rng, new QueueClock([]));

    await expect(service.ensureDailyControl('2026-08-31')).rejects.toThrow('outside the frozen experiment window');
    expect(rng.calls).toEqual([]);
    expect((await ledger.list()).filter((entry) => entry.type === 'control')).toHaveLength(0);
  });

  it('rejects measured sessions when daily control has not been committed', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('ff'.repeat(128))]);
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock(['2026-09-01T01:10:00.000Z']),
    );

    await expect(service.runSession(draft())).rejects.toThrow('daily control must be committed');
    expect(rng.calls).toEqual([]);
  });

  it('does not acquire measured RNG if prediction append fails', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('aa'.repeat(128)), localDraw('ff'.repeat(128))]);
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock(['2026-09-01T00:00:00.000Z']),
    );
    await service.prepareSession('2026-09-01', 1);
    expect(rng.calls).toHaveLength(1);

    const rejectingLedger = {
      list: () => ledger.list(),
      append: (...args: Parameters<LedgerService['append']>) => {
        if (args[0] === 'prediction') return Promise.reject(new Error('prediction write failed'));
        return ledger.append(...args);
      },
      appendConditionally: (
        shouldAppend: Parameters<LedgerService['appendConditionally']>[0],
        entryFactory: Parameters<LedgerService['appendConditionally']>[1],
      ) => ledger.appendConditionally(shouldAppend, entryFactory),
    };
    const failingService = new SessionFlowService(
      rejectingLedger,
      rng,
      new QueueClock(['2026-09-01T01:10:00.000Z']),
    );

    await expect(failingService.runSession(draft())).rejects.toThrow('prediction write failed');
    expect(rng.calls).toHaveLength(1);
  });

  it('rejects a second session for the same experiment date and sequence without new RNG', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('aa'.repeat(128)), localDraw('ff'.repeat(128))]);
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock([
        '2026-09-01T00:00:00.000Z',
        '2026-09-01T01:10:00.000Z',
        '2026-09-01T01:10:01.000Z',
      ]),
    );
    await service.prepareSession('2026-09-01', 1);
    await service.runSession(draft());

    await expect(service.runSession(draft())).rejects.toThrow('already committed');
    expect(rng.calls).toEqual([1024, 1024]);
  });

  it('serializes concurrent measured sessions to one prediction and one session', async () => {
    const ledger = await createRegisteredLedger();
    const rng = new QueueRng([localDraw('aa'.repeat(128)), localDraw('ff'.repeat(128)), localDraw('00'.repeat(128))]);
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock([
        '2026-09-01T00:00:00.000Z',
        '2026-09-01T01:10:00.000Z',
        '2026-09-01T01:10:01.000Z',
      ]),
    );
    await service.prepareSession('2026-09-01', 1);

    const results = await Promise.allSettled([service.runSession(draft()), service.runSession(draft())]);
    const fulfilled = results.filter((result) => result.status === 'fulfilled');
    const rejected = results.filter((result) => result.status === 'rejected');
    expect(fulfilled).toHaveLength(1);
    expect(rejected).toHaveLength(1);
    expect(rejected[0]).toMatchObject({ status: 'rejected' });
    if (rejected[0]?.status === 'rejected') {
      expect(String(rejected[0].reason)).toMatch(/already committed/);
    }

    const types = (await ledger.list()).map((entry) => entry.type);
    expect(types).toEqual(['registration', 'control', 'prediction', 'session']);
    expect(rng.calls).toEqual([1024, 1024]);
  });

  it('reuses a committed prediction if measured RNG fails before the session is appended', async () => {
    const ledger = await createRegisteredLedger();
    const draws = [localDraw('aa'.repeat(128)), localDraw('ff'.repeat(128))];
    const rng = {
      calls: [] as number[],
      async getBits(nBits: number) {
        this.calls.push(nBits);
        if (this.calls.length === 2) throw new Error('measured rng failed');
        const draw = draws.shift();
        if (!draw) throw new Error('rng exhausted');
        return draw;
      },
    };
    const service = new SessionFlowService(
      ledger,
      rng,
      new QueueClock([
        '2026-09-01T00:00:00.000Z',
        '2026-09-01T01:10:00.000Z',
        '2026-09-01T01:10:01.000Z',
        '2026-09-01T01:10:02.000Z',
      ]),
    );
    await service.prepareSession('2026-09-01', 1);
    await expect(service.runSession(draft())).rejects.toThrow('measured rng failed');
    expect((await ledger.list()).map((entry) => entry.type)).toEqual(['registration', 'control', 'prediction']);

    const result = await service.runSession(draft());
    const entries = await ledger.list();
    expect(entries.map((entry) => entry.type)).toEqual(['registration', 'control', 'prediction', 'session']);
    expect(result.payload.predictionSeq).toBe(entries.find((entry) => entry.type === 'prediction')?.seq);
    expect(result.payload.rngSource).toBe('local');
    expect(rng.calls).toEqual([1024, 1024, 1024]);
  });
});
