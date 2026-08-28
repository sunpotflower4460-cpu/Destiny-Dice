import { writeFileSync } from 'node:fs';
import { exportLedgerJson } from '../src/ledger/export';
import { LedgerService } from '../src/ledger/service';
import { MemoryLedgerStore } from '../src/ledger/memoryStore';
import { verifyChain } from '../src/ledger/verify';
import { RegistrationService } from '../src/registration/service';
import { generateTargetSchedule } from '../src/registration/schedule';
import type { Condition, RegistrationInput, TargetDirection } from '../src/registration/types';
import { buildFinalReportModel, renderFinalReportMarkdown } from '../src/report';
import { SeededTestRngProvider } from '../src/rng/testing/seeded';
import { summarizeBitstream } from '../src/stats/core';

const DAY_MS = 86_400_000;
const START_DATE = '2026-09-01';
const WISH_SEED = 'p10-wish-seed-1';

type Options = {
  days: number;
  effect: number;
  condition: Condition;
  wishN: number;
  wishBase: number;
  wishEffect: number;
  exportPath?: string;
  reportPath?: string;
};

function numberArg(args: string[], name: string, fallback: number): number {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  const raw = args[index + 1];
  if (raw === undefined) throw new Error(`${name} requires a value`);
  const value = Number(raw);
  if (!Number.isFinite(value)) throw new Error(`${name} must be numeric`);
  return value;
}

function stringArg(args: string[], name: string): string | undefined {
  const index = args.indexOf(name);
  if (index < 0) return undefined;
  const value = args[index + 1];
  if (!value) throw new Error(`${name} requires a value`);
  return value;
}

function parseOptions(args: string[]): Options {
  const days = numberArg(args, '--days', 365);
  const effect = numberArg(args, '--effect', 0);
  const condition = numberArg(args, '--condition', 2);
  const wishN = numberArg(args, '--wish-n', 120);
  const wishBase = numberArg(args, '--wish-base', 0.3);
  const wishEffect = numberArg(args, '--wish-effect', 0);
  if (!Number.isInteger(days) || days < 1 || days > 365) throw new RangeError('--days must be 1..365');
  if (!Number.isInteger(condition) || condition < 0 || condition > 4) throw new RangeError('--condition must be 0..4');
  if (!Number.isInteger(wishN) || wishN < 0) throw new RangeError('--wish-n must be a non-negative integer');
  if (effect < -0.49 || effect > 0.49) throw new RangeError('--effect must be between -0.49 and 0.49');
  if (wishBase < 0 || wishBase > 1 || wishBase + wishEffect < 0 || wishBase + wishEffect > 1) {
    throw new RangeError('wish probabilities must stay in 0..1');
  }
  return {
    days,
    effect,
    condition: condition as Condition,
    wishN,
    wishBase,
    wishEffect,
    ...(stringArg(args, '--export') ? { exportPath: stringArg(args, '--export') } : {}),
    ...(stringArg(args, '--report') ? { reportPath: stringArg(args, '--report') } : {}),
  };
}

function isoDate(dayIndex: number): string {
  return new Date(Date.parse(`${START_DATE}T00:00:00.000Z`) + dayIndex * DAY_MS).toISOString().slice(0, 10);
}

function timestamp(date: string, hour: number, minute = 0, second = 0): string {
  return `${date}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}.000Z`;
}

function bitsHexForHits(nBits: number, hits: number, target: TargetDirection): string {
  if (!Number.isInteger(hits) || hits < 0 || hits > nBits || nBits % 4 !== 0) throw new RangeError('invalid synthetic hit fixture');
  const hitBit = target === 1 ? '1' : '0';
  const missBit = target === 1 ? '0' : '1';
  const bits = hitBit.repeat(hits) + missBit.repeat(nBits - hits);
  let hex = '';
  for (let index = 0; index < bits.length; index += 4) hex += Number.parseInt(bits.slice(index, index + 4), 2).toString(16);
  return hex;
}

function ritual(condition: Condition) {
  if (condition === 0) return { kind: 'pull_only', seconds: 60, valid: true } as const;
  if (condition === 1) return { kind: 'intention_writing', seconds: 120, textLen: 40, text: '今日の的が実現した状態を具体的に思い描いて十分な長さで記録する文章です。', valid: true } as const;
  if (condition === 2) return { kind: 'affirmation', seconds: 300, valid: true } as const;
  if (condition === 3) return { kind: 'prayer', seconds: 180, valid: true } as const;
  return { kind: 'full_combo', seconds: 480, textLen: 40, text: '今日の的が実現した状態を具体的に思い描いて十分な長さで記録する文章です。', valid: true } as const;
}

async function sha256(text: string): Promise<string> {
  const bytes = new TextEncoder().encode(text);
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return [...new Uint8Array(digest)].map((value) => value.toString(16).padStart(2, '0')).join('');
}

async function appendSessions(ledger: LedgerService, registration: Awaited<ReturnType<RegistrationService['register']>>['payload'], options: Options): Promise<void> {
  const targets = await generateTargetSchedule(registration.days, registration.sessionsPerDay, registration.targetSeed);
  for (let dayIndex = 0; dayIndex < options.days; dayIndex += 1) {
    const date = isoDate(dayIndex);
    const condition = registration.schedule[dayIndex] as Condition;
    const controlBits = bitsHexForHits(registration.bitsPerDraw, registration.bitsPerDraw / 2, 1);
    const controlStats = summarizeBitstream(controlBits, registration.bitsPerDraw, 1);
    await ledger.append('control', {
      date,
      rngSource: 'local',
      bitsHex: controlBits,
      nBits: registration.bitsPerDraw,
      hits: controlStats.hits,
      z: controlStats.z,
    }, timestamp(date, 4));

    const targetDir = targets[dayIndex]!;
    const prediction = await ledger.append('prediction', {
      date,
      seqInDay: 1,
      condition,
      targetDir,
      confidence: 50 + (dayIndex % 41),
      prophecyText: `synthetic prediction ${dayIndex + 1}`,
      committedAt: timestamp(date, 8, 1),
    }, timestamp(date, 8, 1));

    const hitRate = condition === options.condition ? 0.5 + options.effect : 0.5;
    const hits = Math.round(registration.bitsPerDraw * hitRate);
    const bitsHex = bitsHexForHits(registration.bitsPerDraw, hits, targetDir);
    const stats = summarizeBitstream(bitsHex, registration.bitsPerDraw, targetDir);
    await ledger.append('session', {
      date,
      seqInDay: 1,
      condition,
      targetDir,
      rngSource: 'anu',
      predictionSeq: prediction.seq,
      bitsHex,
      nBits: registration.bitsPerDraw,
      hits: stats.hits,
      z: stats.z,
      ritual: ritual(condition),
      moodPre: { v: 4 + (dayIndex % 5), e: 4 + ((dayIndex + 2) % 5) },
      moodPost: { v: 5 + (dayIndex % 4), e: 5 + ((dayIndex + 1) % 4) },
      context: {
        hour: 8 + (dayIndex % 12),
        dow: new Date(`${date}T00:00:00.000Z`).getUTCDay(),
        lunarPhase: (dayIndex % 30) / 30,
        stateTag: ['calm', 'scattered', 'tired', 'energized'][dayIndex % 4]!,
      },
      startedAt: timestamp(date, 8),
      completedAt: timestamp(date, 8, 10),
    }, timestamp(date, 8, 10));
  }
}

async function appendWishes(ledger: LedgerService, options: Options): Promise<void> {
  if (options.wishN === 0) return;
  const provider = new SeededTestRngProvider(WISH_SEED);
  const bytes = await provider.getBytes(options.wishN * 2);
  const deadline = isoDate(364);
  for (let index = 0; index < options.wishN; index += 1) {
    const wishId = `synthetic-wish-${String(index + 1).padStart(3, '0')}`;
    const bit = (bytes[index * 2]! & 1) as 0 | 1;
    const arm = bit === 1 ? 'practice' : 'sealed';
    const wish = await ledger.append('wish', {
      wishId,
      text: `${index + 1}番目の検証用マイクロ願いが締切までに実現する`,
      deadline,
      likelihood: ((index % 3) + 1) as 1 | 2 | 3,
      influence: (['self', 'mixed', 'external'] as const)[index % 3]!,
      createdAt: timestamp(START_DATE, 9),
    }, timestamp(START_DATE, 9));
    const assignment = await ledger.append('assignment', {
      wishId,
      arm,
      rngSource: 'local',
      bit,
      committedAt: timestamp(START_DATE, 9, 0, 1),
    }, timestamp(START_DATE, 9, 0, 1));
    if (assignment.seq !== wish.seq + 1) throw new Error('synthetic wish assignment lost adjacency');

    const probability = options.wishBase + (arm === 'practice' ? options.wishEffect : 0);
    const realized = bytes[index * 2 + 1]! / 256 < probability;
    await ledger.append('judgment', {
      wishId,
      outcome: realized ? 'realized' : 'not_realized',
      ...(realized ? { pathway: (['own_action', 'other_person', 'chance_encounter', 'unknown'] as const)[index % 4]! } : {}),
      judgedAt: timestamp(deadline, 12),
    }, timestamp(deadline, 12));
  }
}

const options = parseOptions(process.argv.slice(2));
const registrationInput: RegistrationInput = {
  experimentId: 'p10-synthetic-final',
  startDate: START_DATE,
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1は基準付近', 'P2は少し上がる', 'P3は上がる', 'P4は上がる', 'P5は最も上がる'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p10-schedule-seed-v1',
  targetSeed: 'p10-target-seed-v1',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

const ledger = new LedgerService(new MemoryLedgerStore());
const registered = await new RegistrationService(ledger).register(registrationInput, '2026-08-28T02:00:00.000Z');
await appendSessions(ledger, registered.payload, options);
await appendWishes(ledger, options);
const entries = await ledger.list();
const verification = await verifyChain(entries);
if (!verification.ok) throw new Error(`simulation ledger verification failed: ${verification.code}`);
const report = buildFinalReportModel(entries, registered.payload);
const markdown = renderFinalReportMarkdown(report);
const reportSha256 = await sha256(markdown);

if (options.effect > 0 && options.days === 365 && report.confirmatory.layerA.conditions[options.condition]!.label !== 'positive_pre_registered_result') {
  throw new Error(`expected Layer A condition ${options.condition} to meet the positive frozen rule`);
}
if (options.wishEffect > 0 && options.wishN === 120 && report.confirmatory.layerC.result?.label !== 'positive_pre_registered_result') {
  throw new Error('expected Layer C to meet the positive frozen rule for the fixed P10 seed');
}
if (options.exportPath) writeFileSync(options.exportPath, exportLedgerJson(entries), 'utf8');
if (options.reportPath) writeFileSync(options.reportPath, markdown, 'utf8');

console.log(JSON.stringify({
  ok: true,
  synthetic: true,
  networkRequests: 0,
  days: options.days,
  effect: options.effect,
  condition: options.condition,
  wishN: options.wishN,
  wishBase: options.wishBase,
  wishEffect: options.wishEffect,
  entries: entries.length,
  layerALabels: report.confirmatory.layerA.conditions.map((item) => item.label),
  layerCLabel: report.confirmatory.layerC.result?.label ?? null,
  layerCPracticeRate: report.confirmatory.layerC.result?.comparison.practice.realizationRate ?? null,
  layerCSealedRate: report.confirmatory.layerC.result?.comparison.sealed.realizationRate ?? null,
  layerCFisherP: report.confirmatory.layerC.result?.fisherTwoSidedP ?? null,
  layerCBf10: report.confirmatory.layerC.result?.comparison.bf10 ?? null,
  reportSha256,
  headHash: report.headHash,
}, null, 2));
