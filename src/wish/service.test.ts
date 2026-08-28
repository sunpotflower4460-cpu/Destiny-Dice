import { describe, expect, it } from 'vitest';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { LedgerService } from '../ledger/service';
import { verifyChain } from '../ledger/verify';
import { RegistrationService } from '../registration/service';
import type { RegistrationInput } from '../registration/types';
import type { AssignmentBit } from '../rng/types';
import { buildWishLedgerRecords, projectDueJudgments, projectNormalWishRegistry, projectWishMoment } from './projection';
import { WishRegistryService } from './service';
import type { WishClock } from './types';

const registrationInput: RegistrationInput = {
  experimentId: 'p7-wishes',
  startDate: '2026-09-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の意図に向き合う',
  predictionByCondition: ['p1', 'p2', 'p3', 'p4', 'p5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p7-schedule-seed',
  targetSeed: 'p7-target-seed',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

class SequenceClock implements WishClock {
  private tick = 0;
  now(): string {
    const value = new Date(Date.parse('2026-09-01T00:00:00.000Z') + this.tick * 1000).toISOString();
    this.tick += 1;
    return value;
  }
}

class QueueAssignmentRng {
  readonly values: Array<AssignmentBit | Error>;
  calls = 0;
  constructor(values: Array<AssignmentBit | Error>) {
    this.values = values;
  }
  async getAssignmentBit(): Promise<AssignmentBit> {
    const value = this.values[this.calls];
    this.calls += 1;
    if (!value) throw new Error('assignment RNG fixture exhausted');
    if (value instanceof Error) throw value;
    return value;
  }
}

async function setup(layerCEnabled = true) {
  const store = new MemoryLedgerStore();
  const ledger = new LedgerService(store);
  await new RegistrationService(ledger).register(
    { ...registrationInput, layerC: { ...registrationInput.layerC, enabled: layerCEnabled } },
    '2026-08-28T00:00:00.000Z',
  );
  return { store, ledger };
}

describe('P7 WishRegistryService', () => {
  it('commits wish -> assignment as adjacent chain entries with frozen bit mapping', async () => {
    const { ledger } = await setup();
    const rng = new QueueAssignmentRng([{ bit: 1, source: 'anu' }]);
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => 'wish-1');

    const result = await service.registerWish({
      text: '9月中に探していた本が手に入る',
      deadline: '2026-09-30',
      likelihood: 2,
      influence: 'mixed',
    });

    expect(result.assignment).toMatchObject({ wishId: 'wish-1', bit: 1, arm: 'practice', rngSource: 'anu' });
    expect(result.assignmentEntry.seq).toBe(result.wishEntry.seq + 1);
    const entries = await ledger.list();
    expect(entries.map((entry) => entry.type)).toEqual(['registration', 'wish', 'assignment']);
    expect(await verifyChain(entries)).toMatchObject({ ok: true, entries: 3 });
  });

  it('leaves a failed post-wish assignment recoverable and recovery is idempotent', async () => {
    const { ledger } = await setup();
    const rng = new QueueAssignmentRng([
      new Error('temporary assignment failure'),
      { bit: 0, source: 'local' },
    ]);
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => 'wish-crash');

    await expect(service.registerWish({
      text: '今月、友人から連絡が来る',
      deadline: '2026-09-30',
      likelihood: 2,
      influence: 'external',
    })).rejects.toThrow('temporary assignment failure');

    expect((await ledger.list()).map((entry) => entry.type)).toEqual(['registration', 'wish']);
    const firstRecovery = await service.recoverUnassignedWishes();
    const secondRecovery = await service.recoverUnassignedWishes();
    expect(firstRecovery).toHaveLength(1);
    expect(secondRecovery).toHaveLength(0);
    expect(rng.calls).toBe(2);
    expect(buildWishLedgerRecords(await ledger.list())[0]?.assignment).toMatchObject({ arm: 'sealed', rngSource: 'local', bit: 0 });
    expect((await ledger.list()).filter((entry) => entry.type === 'assignment')).toHaveLength(1);
  });

  it('does not double-assign or consume a second RNG value when recovery overlaps normal registration', async () => {
    const { ledger } = await setup();
    let release!: (value: AssignmentBit) => void;
    let markStarted!: () => void;
    let calls = 0;
    const started = new Promise<void>((resolve) => { markStarted = resolve; });
    const rng = {
      getAssignmentBit: async () => {
        calls += 1;
        markStarted();
        return new Promise<AssignmentBit>((resolve) => { release = resolve; });
      },
    };
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => 'race-wish');
    const registration = service.registerWish({
      text: '復旧競合を検証する願い',
      deadline: '2026-09-30',
      likelihood: 2,
      influence: 'mixed',
    });
    await started;
    const recovery = service.recoverUnassignedWishes();
    release({ bit: 1, source: 'anu' });
    await registration;
    expect(await recovery).toHaveLength(0);
    expect(calls).toBe(1);
    expect((await ledger.list()).filter((entry) => entry.type === 'assignment')).toHaveLength(1);
  });

  it('never returns sealed text to normal registry or wish moment before deadline, but reveals it when due', async () => {
    const { ledger } = await setup();
    const rng = new QueueAssignmentRng([
      { bit: 1, source: 'anu' },
      { bit: 0, source: 'randomorg' },
    ]);
    let id = 0;
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => `wish-${++id}`);
    await service.registerWish({ text: '実践側の願い', deadline: '2026-09-15', likelihood: 1, influence: 'self' });
    await service.registerWish({ text: '絶対に事前表示してはいけない封印本文', deadline: '2026-09-15', likelihood: 3, influence: 'external' });

    const before = projectNormalWishRegistry(await ledger.list(), '2026-09-10');
    const moment = projectWishMoment(await ledger.list(), '2026-09-10');
    expect(before.practice.map((wish) => wish.text)).toEqual(['実践側の願い']);
    expect(before.sealedCount).toBe(1);
    expect(JSON.stringify(before)).not.toContain('絶対に事前表示してはいけない封印本文');
    expect(JSON.stringify(moment)).not.toContain('絶対に事前表示してはいけない封印本文');

    const due = projectDueJudgments(await ledger.list(), '2026-09-15');
    expect(due.map((wish) => [wish.text, wish.arm])).toEqual([
      ['実践側の願い', 'practice'],
      ['絶対に事前表示してはいけない封印本文', 'sealed'],
    ]);
  });

  it('records wish moment only for the complete eligible practice projection', async () => {
    const { ledger } = await setup();
    const rng = new QueueAssignmentRng([
      { bit: 1, source: 'anu' },
      { bit: 0, source: 'anu' },
    ]);
    let id = 0;
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => `moment-${++id}`);
    await service.registerWish({ text: '見せる願い', deadline: '2026-10-01', likelihood: 2, influence: 'mixed' });
    await service.registerWish({ text: '封印する願い', deadline: '2026-10-01', likelihood: 2, influence: 'mixed' });

    await expect(service.recordWishMoment('2026-09-10', ['moment-1', 'moment-2'], 30)).rejects.toThrow(
      'exactly the currently eligible practice wishes',
    );
    const entry = await service.recordWishMoment('2026-09-10', ['moment-1'], 30);
    expect(entry.type).toBe('wishmoment');
    expect(JSON.parse(entry.payloadJson)).toEqual({ date: '2026-09-10', seconds: 30, wishIdsShown: ['moment-1'] });
  });

  it('creates deadline judgments, requires realized pathway, and treats withdrawn as not realized', async () => {
    const { ledger } = await setup();
    const rng = new QueueAssignmentRng([
      { bit: 1, source: 'anu' },
      { bit: 1, source: 'anu' },
    ]);
    let id = 0;
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => `judge-${++id}`);
    await service.registerWish({ text: '判定する願い', deadline: '2026-09-05', likelihood: 2, influence: 'self' });
    await service.registerWish({ text: '取り下げる願い', deadline: '2026-10-05', likelihood: 2, influence: 'self' });

    await expect(service.judgeWish('judge-1', '2026-09-05', 'realized')).rejects.toThrow('requires a pathway');
    const judgment = await service.judgeWish('judge-1', '2026-09-05', 'realized', 'own_action');
    const withdrawal = await service.withdrawWish('judge-2');
    expect(JSON.parse(judgment.payloadJson)).toMatchObject({ wishId: 'judge-1', outcome: 'realized', pathway: 'own_action' });
    expect(JSON.parse(withdrawal.payloadJson)).toMatchObject({ wishId: 'judge-2', outcome: 'withdrawn' });
    expect(service.primaryOutcomeFor('withdrawn')).toBe('not_realized');
    expect(service.primaryOutcomeFor('undecidable')).toBe('not_realized');
    expect(service.primaryOutcomeFor('realized')).toBe('realized');
  });

  it('serializes concurrent judgments so a wish can receive only one judgment entry', async () => {
    const { ledger } = await setup();
    const service = new WishRegistryService(
      ledger,
      new QueueAssignmentRng([{ bit: 1, source: 'anu' }]),
      new SequenceClock(),
      () => 'judgment-race',
    );
    await service.registerWish({ text: '二重判定を防ぐ願い', deadline: '2026-09-05', likelihood: 2, influence: 'mixed' });

    const outcomes = await Promise.allSettled([
      service.judgeWish('judgment-race', '2026-09-05', 'not_realized'),
      service.judgeWish('judgment-race', '2026-09-05', 'undecidable'),
    ]);
    expect(outcomes.filter((result) => result.status === 'fulfilled')).toHaveLength(1);
    expect(outcomes.filter((result) => result.status === 'rejected')).toHaveLength(1);
    expect((await ledger.list()).filter((entry) => entry.type === 'judgment')).toHaveLength(1);
  });

  it('rejects semantically malformed judgment entries when rebuilding the ledger projection', async () => {
    const { ledger } = await setup();
    const service = new WishRegistryService(
      ledger,
      new QueueAssignmentRng([{ bit: 1, source: 'anu' }]),
      new SequenceClock(),
      () => 'malformed-judgment',
    );
    await service.registerWish({ text: '不正判定を検知する願い', deadline: '2026-09-05', likelihood: 2, influence: 'mixed' });
    await ledger.append(
      'judgment',
      { wishId: 'malformed-judgment', outcome: 'realized', judgedAt: '2026-09-05T00:00:00.000Z' },
      '2026-09-05T00:00:00.000Z',
    );
    expect(() => buildWishLedgerRecords(await ledger.list())).rejects.toThrow('realized judgment must include pathway');
  });

  it('rejects Layer C writes when the experiment registered Layer C disabled', async () => {
    const { ledger } = await setup(false);
    const service = new WishRegistryService(
      ledger,
      new QueueAssignmentRng([{ bit: 1, source: 'anu' }]),
      new SequenceClock(),
      () => 'disabled-wish',
    );
    await expect(service.registerWish({
      text: '登録不可',
      deadline: '2026-09-30',
      likelihood: 2,
      influence: 'mixed',
    })).rejects.toThrow('Layer C is disabled');
  });

  it('holds the ledger single-writer slot across assignment RNG so unrelated appends cannot interleave', async () => {
    const { ledger } = await setup();
    let release!: (value: AssignmentBit) => void;
    let started!: () => void;
    const startedPromise = new Promise<void>((resolve) => { started = resolve; });
    const rng = {
      getAssignmentBit: async () => {
        started();
        return new Promise<AssignmentBit>((resolve) => { release = resolve; });
      },
    };
    const service = new WishRegistryService(ledger, rng, new SequenceClock(), () => 'locked-wish');

    const registerPromise = service.registerWish({
      text: '連続appendを検証する願い',
      deadline: '2026-09-30',
      likelihood: 2,
      influence: 'mixed',
    });
    await startedPromise;
    const unrelated = ledger.append('control', { date: '2026-09-01', rngSource: 'local', bitsHex: '00', nBits: 8, hits: 4, z: 0 }, '2026-09-01T00:00:10.000Z');
    release({ bit: 1, source: 'local' });
    await registerPromise;
    await unrelated;

    expect((await ledger.list()).map((entry) => entry.type)).toEqual(['registration', 'wish', 'assignment', 'control']);
  });
});
