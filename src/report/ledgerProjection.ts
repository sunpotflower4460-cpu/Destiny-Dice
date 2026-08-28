import type { StoredLedgerEntry } from '../ledger/types';
import type { Condition, RegistrationPayload, TargetDirection } from '../registration/types';
import { RNG_SOURCES, type RngSource } from '../rng/types';
import { summarizeBitstream, type LayerCWishObservation, type RichSessionObservation } from '../stats';
import { buildWishLedgerRecords } from '../wish/projection';

const DAY_MS = 86_400_000;
const CONDITIONS = [0, 1, 2, 3, 4] as const satisfies readonly Condition[];

export type ReportSession = RichSessionObservation & {
  date: string;
  seqInDay: number;
  targetDir: TargetDirection;
  predictionSeq: number;
  moodPostV: number;
  moodPostE: number;
};

export type FinalLayerCProjection = {
  observations: LayerCWishObservation[];
  textRows: Array<{ arm: 'practice' | 'sealed'; outcome: string; text: string }>;
  eligibleUnjudgedWishes: number;
  postExperimentDeadlineWishes: number;
  unassignedWishes: number;
  assignmentSourceCounts: Record<RngSource, number>;
};

type ParsedPrediction = {
  seq: number;
  date: string;
  seqInDay: number;
  condition: Condition;
  targetDir: TargetDirection;
  confidence: number;
};

function asObject(value: unknown, label: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new TypeError(`${label} must be an object`);
  return value as Record<string, unknown>;
}

function asString(value: unknown, label: string): string {
  if (typeof value !== 'string' || value.length === 0) throw new TypeError(`${label} must be a non-empty string`);
  return value;
}

function asInteger(value: unknown, label: string): number {
  if (!Number.isInteger(value)) throw new TypeError(`${label} must be an integer`);
  return value as number;
}

function asFinite(value: unknown, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new TypeError(`${label} must be finite`);
  return value;
}

function asBoolean(value: unknown, label: string): boolean {
  if (typeof value !== 'boolean') throw new TypeError(`${label} must be boolean`);
  return value;
}

function asCondition(value: unknown): Condition {
  const condition = asInteger(value, 'condition');
  if (!CONDITIONS.includes(condition as Condition)) throw new RangeError('condition must be 0..4');
  return condition as Condition;
}

function asTarget(value: unknown): TargetDirection {
  if (value !== 0 && value !== 1) throw new RangeError('targetDir must be 0 or 1');
  return value;
}

function asSource(value: unknown): RngSource {
  if (!RNG_SOURCES.includes(value as RngSource)) throw new RangeError('rngSource is invalid');
  return value as RngSource;
}

function parsePayload(entry: StoredLedgerEntry): Record<string, unknown> {
  return asObject(JSON.parse(entry.payloadJson), `${entry.type} payload`);
}

function parsePrediction(entry: StoredLedgerEntry): ParsedPrediction {
  const payload = parsePayload(entry);
  const confidence = asInteger(payload.confidence, 'prediction confidence');
  if (confidence < 0 || confidence > 100) throw new RangeError('prediction confidence must be 0..100');
  return {
    seq: entry.seq,
    date: asString(payload.date, 'prediction date'),
    seqInDay: asInteger(payload.seqInDay, 'prediction seqInDay'),
    condition: asCondition(payload.condition),
    targetDir: asTarget(payload.targetDir),
    confidence,
  };
}

function parseMood(value: unknown, label: string): { v: number; e: number } {
  const mood = asObject(value, label);
  const v = asInteger(mood.v, `${label}.v`);
  const e = asInteger(mood.e, `${label}.e`);
  if (v < 1 || v > 10 || e < 1 || e > 10) throw new RangeError(`${label} values must be 1..10`);
  return { v, e };
}

function parseIsoDate(date: string, label: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) throw new TypeError(`${label} must be YYYY-MM-DD`);
  const ms = Date.parse(`${date}T00:00:00.000Z`);
  if (!Number.isFinite(ms) || new Date(ms).toISOString().slice(0, 10) !== date) throw new RangeError(`${label} must be a valid date`);
  return ms;
}

export function finalExperimentDate(registration: RegistrationPayload): string {
  const start = parseIsoDate(registration.startDate, 'registration startDate');
  return new Date(start + (registration.days - 1) * DAY_MS).toISOString().slice(0, 10);
}

function parseSession(entry: StoredLedgerEntry, predictions: ReadonlyMap<number, ParsedPrediction>): ReportSession {
  const payload = parsePayload(entry);
  const ritual = asObject(payload.ritual, 'session ritual');
  const context = asObject(payload.context, 'session context');
  const moodPre = parseMood(payload.moodPre, 'moodPre');
  const moodPost = parseMood(payload.moodPost, 'moodPost');
  const predictionSeq = asInteger(payload.predictionSeq, 'session predictionSeq');
  const prediction = predictions.get(predictionSeq);
  if (!prediction || prediction.seq >= entry.seq) throw new Error('session requires a prior committed prediction');

  const date = asString(payload.date, 'session date');
  const seqInDay = asInteger(payload.seqInDay, 'session seqInDay');
  const condition = asCondition(payload.condition);
  const targetDir = asTarget(payload.targetDir);
  if (prediction.date !== date || prediction.seqInDay !== seqInDay || prediction.condition !== condition || prediction.targetDir !== targetDir) {
    throw new Error('session does not match its committed prediction');
  }

  const nBits = asInteger(payload.nBits, 'session nBits');
  const hits = asInteger(payload.hits, 'session hits');
  const frozen = summarizeBitstream(asString(payload.bitsHex, 'session bitsHex'), nBits, targetDir);
  const recordedZ = asFinite(payload.z, 'session z');
  if (frozen.hits !== hits || Math.abs(frozen.z - recordedZ) > 1e-12) throw new Error('session bits/hits/z do not match frozen stats core');

  return {
    date,
    seqInDay,
    condition,
    targetDir,
    predictionSeq,
    rngSource: asSource(payload.rngSource),
    ritualValid: asBoolean(ritual.valid, 'ritual valid'),
    ritualSeconds: asInteger(ritual.seconds, 'ritual seconds'),
    nBits,
    hits,
    confidence: prediction.confidence,
    moodPreV: moodPre.v,
    moodPreE: moodPre.e,
    moodPostV: moodPost.v,
    moodPostE: moodPost.e,
    hour: asInteger(context.hour, 'context hour'),
    dow: asInteger(context.dow, 'context dow'),
    lunarPhase: asFinite(context.lunarPhase, 'context lunarPhase'),
    ...(typeof context.stateTag === 'string' && context.stateTag.length > 0 ? { stateTag: context.stateTag } : {}),
  };
}

export function projectReportSessions(entries: readonly StoredLedgerEntry[], registration: RegistrationPayload): ReportSession[] {
  const predictions = new Map(entries.filter((entry) => entry.type === 'prediction').map((entry) => {
    const parsed = parsePrediction(entry);
    return [parsed.seq, parsed] as const;
  }));
  const sessions = entries.filter((entry) => entry.type === 'session').map((entry) => parseSession(entry, predictions));
  const start = parseIsoDate(registration.startDate, 'registration startDate');
  const seen = new Set<string>();
  for (const session of sessions) {
    const dayIndex = (parseIsoDate(session.date, 'session date') - start) / DAY_MS;
    if (!Number.isInteger(dayIndex) || dayIndex < 0 || dayIndex >= registration.days) throw new RangeError('session date is outside experiment window');
    if (registration.schedule[dayIndex] !== session.condition) throw new Error('session condition violates frozen schedule');
    if (session.seqInDay < 1 || session.seqInDay > registration.sessionsPerDay) throw new RangeError('session seqInDay is outside registration');
    const key = `${session.date}:${session.seqInDay}`;
    if (seen.has(key)) throw new Error(`duplicate session slot ${key}`);
    seen.add(key);
  }
  return sessions;
}

export function projectFinalLayerC(entries: readonly StoredLedgerEntry[], registration: RegistrationPayload): FinalLayerCProjection {
  const result: FinalLayerCProjection = {
    observations: [],
    textRows: [],
    eligibleUnjudgedWishes: 0,
    postExperimentDeadlineWishes: 0,
    unassignedWishes: 0,
    assignmentSourceCounts: { anu: 0, randomorg: 0, local: 0 },
  };
  if (!registration.layerC.enabled) return result;
  const endDate = finalExperimentDate(registration);
  for (const record of buildWishLedgerRecords(entries)) {
    if (!record.assignment) {
      result.unassignedWishes += 1;
      continue;
    }
    result.assignmentSourceCounts[record.assignment.rngSource] += 1;
    if (record.wish.deadline > endDate) {
      result.postExperimentDeadlineWishes += 1;
      continue;
    }
    if (!record.judgment) {
      result.eligibleUnjudgedWishes += 1;
      continue;
    }
    result.observations.push({
      arm: record.assignment.arm,
      outcome: record.judgment.outcome,
      likelihood: record.wish.likelihood,
      influence: record.wish.influence,
      ...(record.judgment.pathway === undefined ? {} : { pathway: record.judgment.pathway }),
    });
    result.textRows.push({ arm: record.assignment.arm, outcome: record.judgment.outcome, text: record.wish.text });
  }
  return result;
}
