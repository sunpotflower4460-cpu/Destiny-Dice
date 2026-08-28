import { LedgerService } from '../src/ledger/service';
import { MemoryLedgerStore } from '../src/ledger/memoryStore';
import { verifyChain } from '../src/ledger/verify';
import { RegistrationService } from '../src/registration/service';
import type { RegistrationInput } from '../src/registration/types';
import { RngService } from '../src/rng/service';
import { SeededRngProvider } from '../src/rng/testing/seeded';
import { SessionFlowService } from '../src/session/service';
import type { Clock } from '../src/session/types';

class SimulationClock implements Clock {
  private readonly timestamps = [
    '2026-09-01T00:00:00.000Z',
    '2026-09-01T01:10:00.000Z',
    '2026-09-01T01:10:01.000Z',
  ];

  now(): string {
    const timestamp = this.timestamps.shift();
    if (!timestamp) throw new Error('simulation clock exhausted');
    return timestamp;
  }
}

const registration: RegistrationInput = {
  experimentId: 'simulate-p4',
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
await new RegistrationService(ledger).register(registration, '2026-08-28T02:00:00.000Z');

const rng = new RngService([new SeededRngProvider('p4-offline-rng')]);
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

const entries = await ledger.list();
const verification = await verifyChain(entries);
if (!verification.ok) {
  throw new Error(`simulation ledger verification failed: ${verification.code}`);
}
if (result.predictionEntry.seq >= result.sessionEntry.seq) {
  throw new Error('simulation prediction ordering failed');
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
      headHash: result.sessionEntry.entryHash,
    },
    null,
    2,
  ),
);
