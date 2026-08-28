import type { StoredLedgerEntry } from '../ledger/types';
import type { Condition, RegistrationPayload } from '../registration/types';
import type { RngSource } from '../rng/types';
import {
  analyzeControlQc,
  analyzeInterimLayerA,
  cumulativeDeviationSeries,
  zScore,
  type ConfidenceInterval,
  type ControlQcResult,
  type CumulativeDeviationPoint,
  type LayerAControlObservation,
  type LayerASessionObservation,
  type SourceCounts,
} from '../stats';

export const CONDITION_LABELS = [
  'P1 引くだけ',
  'P2 意図書き',
  'P3 アファメーション',
  'P4 祈り',
  'P5 フルコンボ',
] as const;

const RNG_SOURCES: readonly RngSource[] = ['anu', 'randomorg', 'local'];

export type DashboardConditionCard = {
  condition: Condition;
  label: string;
  sessions: number;
  nBits: number;
  hits: number;
  expectedHits: number;
  hitRate: number | null;
  chanceHitRate: 0.5;
  z: number | null;
  ci95: ConfidenceInterval | null;
  bf10: number;
};

export type MiracleLogItem = {
  date: string;
  seqInDay: number;
  condition: Condition;
  conditionLabel: string;
  z: number;
  hits: number;
  expectedHits: number;
  nBits: number;
  rngSource: RngSource;
  primaryQuantumSample: boolean;
};

export type CalendarDay = {
  date: string;
  recordedSessions: number;
  condition: Condition | null;
  conditionLabel: string | null;
  resonance: boolean;
  miracle: boolean;
  rngSources: RngSource[];
};

export type LayerADashboardModel = {
  analysisKind: 'interim';
  primarySample: 'anu_valid_only';
  conditionCards: DashboardConditionCard[];
  sourceCounts: SourceCounts;
  fallbackSessions: number;
  ritualInvalidSessions: number;
  cumulativeDeviation: CumulativeDeviationPoint[];
  controlQc: ControlQcResult;
  miracles: MiracleLogItem[];
  calendar: CalendarDay[];
};

type ParsedSession = LayerASessionObservation & {
  ledgerSeq: number;
  date: string;
  seqInDay: number;
  z: number;
};

function asObject(value: unknown, label: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${label} must be an object`);
  }
  return value as Record<string, unknown>;
}

function asInteger(value: unknown, label: string): number {
  if (!Number.isInteger(value)) throw new TypeError(`${label} must be an integer`);
  return value as number;
}

function asFiniteNumber(value: unknown, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new TypeError(`${label} must be a finite number`);
  }
  return value;
}

function asString(value: unknown, label: string): string {
  if (typeof value !== 'string' || value.length === 0) throw new TypeError(`${label} must be a non-empty string`);
  return value;
}

function asCondition(value: unknown): Condition {
  const condition = asInteger(value, 'condition');
  if (condition < 0 || condition > 4) throw new RangeError('condition must be 0..4');
  return condition as Condition;
}

function asRngSource(value: unknown): RngSource {
  if (!RNG_SOURCES.includes(value as RngSource)) throw new RangeError('rngSource must be anu, randomorg, or local');
  return value as RngSource;
}

function parsePayload(entry: StoredLedgerEntry): Record<string, unknown> {
  const parsed: unknown = JSON.parse(entry.payloadJson);
  return asObject(parsed, `${entry.type} payload`);
}

function assertRecordedZ(hits: number, nBits: number, recordedZ: number): void {
  const expected = zScore(hits, nBits);
  if (Math.abs(expected - recordedZ) > Number.EPSILON * Math.max(1, Math.abs(expected)) * 8) {
    throw new Error(`recorded z does not match frozen stats core: expected ${expected}, got ${recordedZ}`);
  }
}

function parseSession(entry: StoredLedgerEntry): ParsedSession {
  const payload = parsePayload(entry);
  const ritual = asObject(payload.ritual, 'session ritual');
  const nBits = asInteger(payload.nBits, 'session nBits');
  const hits = asInteger(payload.hits, 'session hits');
  const z = asFiniteNumber(payload.z, 'session z');
  assertRecordedZ(hits, nBits, z);

  return {
    ledgerSeq: entry.seq,
    date: asString(payload.date, 'session date'),
    seqInDay: asInteger(payload.seqInDay, 'session seqInDay'),
    condition: asCondition(payload.condition),
    rngSource: asRngSource(payload.rngSource),
    ritualValid: ritual.valid === true,
    ritualSeconds: asInteger(ritual.seconds, 'ritual seconds'),
    nBits,
    hits,
    z,
  };
}

function parseControl(entry: StoredLedgerEntry): LayerAControlObservation {
  const payload = parsePayload(entry);
  const nBits = asInteger(payload.nBits, 'control nBits');
  const hits = asInteger(payload.hits, 'control hits');
  const z = asFiniteNumber(payload.z, 'control z');
  assertRecordedZ(hits, nBits, z);
  return {
    date: asString(payload.date, 'control date'),
    rngSource: asRngSource(payload.rngSource),
    nBits,
    hits,
  };
}

function isoDateAtOffset(startDate: string, offset: number): string {
  const timestamp = Date.parse(`${startDate}T00:00:00.000Z`);
  if (!Number.isFinite(timestamp)) throw new RangeError('registration startDate must be valid');
  return new Date(timestamp + offset * 86_400_000).toISOString().slice(0, 10);
}

function buildCalendar(registration: RegistrationPayload, sessions: readonly ParsedSession[]): CalendarDay[] {
  const byDate = new Map<string, ParsedSession[]>();
  for (const session of sessions) {
    const bucket = byDate.get(session.date) ?? [];
    bucket.push(session);
    byDate.set(session.date, bucket);
  }

  return Array.from({ length: registration.days }, (_, dayIndex) => {
    const date = isoDateAtOffset(registration.startDate, dayIndex);
    const rows = byDate.get(date) ?? [];
    const conditions = new Set(rows.map((row) => row.condition));
    if (conditions.size > 1) throw new Error(`multiple conditions recorded for ${date}`);
    const condition = rows[0]?.condition ?? null;
    return {
      date,
      recordedSessions: rows.length,
      condition,
      conditionLabel: condition === null ? null : CONDITION_LABELS[condition],
      resonance: rows.some((row) => Math.abs(row.z) >= 2),
      miracle: rows.some((row) => row.z >= 3),
      rngSources: RNG_SOURCES.filter((source) => rows.some((row) => row.rngSource === source)),
    };
  });
}

export function buildLayerADashboardModel(
  entries: readonly StoredLedgerEntry[],
  registration: RegistrationPayload,
): LayerADashboardModel {
  const sessions = entries.filter((entry) => entry.type === 'session').map(parseSession);
  const controls = entries.filter((entry) => entry.type === 'control').map(parseControl);
  const observations: LayerASessionObservation[] = sessions.map((session) => ({
    condition: session.condition,
    rngSource: session.rngSource,
    ritualValid: session.ritualValid,
    nBits: session.nBits,
    hits: session.hits,
    ritualSeconds: session.ritualSeconds,
    date: session.date,
  }));

  const interim = analyzeInterimLayerA(observations);
  const primary = sessions
    .filter((session) => session.rngSource === 'anu' && session.ritualValid)
    .sort((a, b) => a.ledgerSeq - b.ledgerSeq);

  return {
    analysisKind: 'interim',
    primarySample: interim.primarySample,
    conditionCards: interim.conditions.map((condition) => ({
      condition: condition.condition,
      label: CONDITION_LABELS[condition.condition],
      sessions: condition.sessions,
      nBits: condition.nBits,
      hits: condition.hits,
      expectedHits: condition.nBits / 2,
      hitRate: condition.hitRate,
      chanceHitRate: condition.chanceHitRate,
      z: condition.z,
      ci95: condition.ci95,
      bf10: condition.bf10,
    })),
    sourceCounts: interim.sourceCounts,
    fallbackSessions: interim.exclusions.fallbackSessions,
    ritualInvalidSessions: interim.exclusions.ritualInvalidSessions,
    cumulativeDeviation: cumulativeDeviationSeries(primary.map((session) => ({
      nBits: session.nBits,
      hits: session.hits,
    }))),
    controlQc: analyzeControlQc(controls),
    miracles: sessions
      .filter((session) => session.ritualValid && session.z >= 3)
      .sort((a, b) => a.ledgerSeq - b.ledgerSeq)
      .map((session) => ({
        date: session.date,
        seqInDay: session.seqInDay,
        condition: session.condition,
        conditionLabel: CONDITION_LABELS[session.condition],
        z: session.z,
        hits: session.hits,
        expectedHits: session.nBits / 2,
        nBits: session.nBits,
        rngSource: session.rngSource,
        primaryQuantumSample: session.rngSource === 'anu',
      })),
    calendar: buildCalendar(registration, sessions),
  };
}
