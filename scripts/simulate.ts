import { buildLayerCDashboardModel } from '../src/dashboard/layerCModel';
import { LedgerService } from '../src/ledger/service';
import { MemoryLedgerStore } from '../src/ledger/memoryStore';
import { verifyChain } from '../src/ledger/verify';
import { RegistrationService } from '../src/registration/service';
import type { RegistrationInput } from '../src/registration/types';
import { RngService } from '../src/rng/service';
import { SeededTestRngProvider } from '../src/rng/testing/seeded';
import { SessionFlowService } from '../src/session/service';
import type { Clock } from '../src/session/types';
import { WishRegistryService } from '../src/wish/service';
import type { WishClock } from '../src/wish/types';

class SimulationClock implements Clock {
  private readonly timestamps = [
    '2026-09-01T00:00:00.000Z',
    '2026-09-01T01:10:00.000Z',
    '2026-09-01T01:10:01.000Z',
  ];

  now(): string {
    const timestamp = this.timestamps.shift();
    if (!timestamp) throw new Error('simulation session clock exhausted');
    return timestamp;
  }
}

class WishSimulationClock implements WishClock {
  private readonly timestamps = [
    '2026-09-01T01:11:00.000Z',
    '2026-09-01T01:11:01.000Z',
    '2026-09-01T01:11:02.000Z',
    '2026-09-01T01:11:03.000Z',
    '2026-09-01T01:12:00.000Z',
    '2026-09-29T01:00:00.000Z',
    '2026-09-29T01:00:01.000Z',
  ];

  now(): string {
    const timestamp = this.timestamps.shift();
    if (!timestamp) throw new Error('simulation wish clock exhausted');
    return timestamp;
  }
}

const registration: RegistrationInput = {
  experimentId: 'simulate-p8',
  startDate: '2026-09-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1', 'P2', 'P3', 'P4', 'P5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'simulate-schedule-seed',
  targetSeed: 'simulate-target-seed',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

const ledger = new LedgerService(new MemoryLedgerStore());
const registrationResult = await new RegistrationService(ledger).register(registration, '2026-08-28T02:00:00.000Z');

const rng = new RngService([new SeededTestRngProvider('p4-offline-rng')]);
const sessions = new SessionFlowService(ledger, rng, new SimulationClock());
const plan = await sessions.prepareSession('2026-09-01', 1);
const result = await sessions.runSession({
  experimentDate: '2026-09-01',
  seqInDay: 1,
  moodPre: { v: 5, e: 6 },
  ritual: {
    seconds: 600,
    text: '今日の的が実現した状態を具体的に思い描いて文章として十分な長さで記録する',
  },
  moodPost: { v: 6, e: 6 },
  confidence: 64,
  prophecyText: '少し届く感じがする',
  context: { hour: 10, dow: 2, lunarPhase: 0.42 },
  startedAt: '2026-09-01T01:00:00.000Z',
});

const assignmentBits = [1, 0] as const;
let assignmentIndex = 0;
const wishIds = ['simulate-wish-practice', 'simulate-wish-sealed'] as const;
let wishIdIndex = 0;
const wishes = new WishRegistryService(
  ledger,
  {
    getAssignmentBit: async () => {
      const bit = assignmentBits[assignmentIndex];
      assignmentIndex += 1;
      if (bit === undefined) throw new Error('simulation assignment RNG exhausted');
      return { bit, source: 'local' as const };
    },
  },
  new WishSimulationClock(),
  () => {
    const wishId = wishIds[wishIdIndex];
    wishIdIndex += 1;
    if (!wishId) throw new Error('simulation wish ID fixture exhausted');
    return wishId;
  },
);
const practiceWish = await wishes.registerWish({
  text: '9月中に探していた本が手に入る',
  deadline: '2026-09-29',
  likelihood: 2,
  influence: 'mixed',
});
const sealedWish = await wishes.registerWish({
  text: '9月中に偶然うれしい知らせが届く',
  deadline: '2026-09-29',
  likelihood: 2,
  influence: 'external',
});
const wishMoment = await wishes.projectWishMoment('2026-09-01');
await wishes.recordWishMoment('2026-09-01', wishMoment.wishes.map((wish) => wish.wishId), 30);
await wishes.judgeWish('simulate-wish-practice', '2026-09-29', 'realized', 'chance_encounter');
await wishes.judgeWish('simulate-wish-sealed', '2026-09-29', 'not_realized');

const entries = await ledger.list();
const verification = await verifyChain(entries);
if (!verification.ok) {
  throw new Error(`simulation ledger verification failed: ${verification.code}`);
}
if (result.predictionEntry.seq >= result.sessionEntry.seq) {
  throw new Error('simulation prediction ordering failed');
}
if (practiceWish.assignmentEntry.seq !== practiceWish.wishEntry.seq + 1) {
  throw new Error('simulation practice wish assignment ordering failed');
}
if (sealedWish.assignmentEntry.seq !== sealedWish.wishEntry.seq + 1) {
  throw new Error('simulation sealed wish assignment ordering failed');
}
if (wishMoment.wishes.map((wish) => wish.wishId).join(',') !== 'simulate-wish-practice') {
  throw new Error('simulation wish moment projection violated sealed boundary');
}

const layerC = buildLayerCDashboardModel(entries, registrationResult.payload);
if (!layerC) throw new Error('simulation Layer C dashboard unexpectedly disabled');
if (layerC.comparison.practice.realizationRate !== 1 || layerC.comparison.sealed.realizationRate !== 0) {
  throw new Error('simulation Layer C realization-rate aggregation failed');
}
if ('fisherTwoSidedP' in layerC) {
  throw new Error('simulation interim Layer C dashboard leaked confirmatory Fisher p-value');
}

console.log(
  JSON.stringify(
    {
      ok: true,
      networkRequests: 0,
      entryTypes: entries.map((entry) => entry.type),
      condition: plan.condition,
      targetDir: plan.targetDir,
      predictionSeq: result.predictionEntry.seq,
      sessionSeq: result.sessionEntry.seq,
      rngSource: result.payload.rngSource,
      hits: result.payload.hits,
      nBits: result.payload.nBits,
      z: result.payload.z,
      practiceWishSeq: practiceWish.wishEntry.seq,
      practiceAssignmentSeq: practiceWish.assignmentEntry.seq,
      sealedWishSeq: sealedWish.wishEntry.seq,
      sealedAssignmentSeq: sealedWish.assignmentEntry.seq,
      wishMomentCount: wishMoment.wishes.length,
      layerCPracticeRate: layerC.comparison.practice.realizationRate,
      layerCSealedRate: layerC.comparison.sealed.realizationRate,
      layerCBf10: layerC.comparison.bf10,
      headHash: entries.at(-1)!.entryHash,
    },
    null,
    2,
  ),
);
