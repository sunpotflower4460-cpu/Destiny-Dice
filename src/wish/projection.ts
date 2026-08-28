import type { StoredLedgerEntry } from '../ledger/types';
import type { RngSource } from '../rng/types';
import type {
  AssignmentPayload,
  DueWishView,
  JudgmentPayload,
  PracticeWishView,
  PrimaryWishOutcome,
  WishArm,
  WishInfluence,
  WishLikelihood,
  WishMomentProjection,
  WishOutcome,
  WishPayload,
  WishRegistryProjection,
} from './types';

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
const RNG_SOURCES: readonly RngSource[] = ['anu', 'randomorg', 'local'];
const INFLUENCES: readonly WishInfluence[] = ['self', 'mixed', 'external'];
const ARMS: readonly WishArm[] = ['practice', 'sealed'];
const OUTCOMES: readonly WishOutcome[] = ['realized', 'not_realized', 'undecidable', 'withdrawn'];

export type WishLedgerRecord = {
  wishEntry: StoredLedgerEntry;
  wish: WishPayload;
  assignmentEntry: StoredLedgerEntry | null;
  assignment: AssignmentPayload | null;
  judgmentEntry: StoredLedgerEntry | null;
  judgment: JudgmentPayload | null;
};

function asObject(value: unknown, label: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${label} must be an object`);
  }
  return value as Record<string, unknown>;
}

function parsePayload(entry: StoredLedgerEntry): Record<string, unknown> {
  return asObject(JSON.parse(entry.payloadJson), `${entry.type} payload`);
}

function asNonEmptyString(value: unknown, label: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new TypeError(`${label} must be a non-empty string`);
  }
  return value;
}

export function assertIsoDate(value: string, label: string): void {
  if (!DATE_RE.test(value)) throw new RangeError(`${label} must be YYYY-MM-DD`);
  const parsed = new Date(`${value}T00:00:00.000Z`);
  if (!Number.isFinite(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== value) {
    throw new RangeError(`${label} must be a valid calendar date`);
  }
}

function parseLikelihood(value: unknown): WishLikelihood {
  if (value !== 1 && value !== 2 && value !== 3) {
    throw new RangeError('wish likelihood must be 1, 2, or 3');
  }
  return value;
}

function parseInfluence(value: unknown): WishInfluence {
  if (!INFLUENCES.includes(value as WishInfluence)) {
    throw new RangeError('wish influence must be self, mixed, or external');
  }
  return value as WishInfluence;
}

function parseWish(entry: StoredLedgerEntry): WishPayload {
  const payload = parsePayload(entry);
  const wishId = asNonEmptyString(payload.wishId, 'wishId');
  const text = asNonEmptyString(payload.text, 'wish text');
  const deadline = asNonEmptyString(payload.deadline, 'wish deadline');
  assertIsoDate(deadline, 'wish deadline');
  const createdAt = asNonEmptyString(payload.createdAt, 'wish createdAt');
  return {
    wishId,
    text,
    deadline,
    likelihood: parseLikelihood(payload.likelihood),
    influence: parseInfluence(payload.influence),
    createdAt,
  };
}

function parseAssignment(entry: StoredLedgerEntry): AssignmentPayload {
  const payload = parsePayload(entry);
  const arm = payload.arm;
  const rngSource = payload.rngSource;
  const bit = payload.bit;
  if (!ARMS.includes(arm as WishArm)) throw new RangeError('assignment arm must be practice or sealed');
  if (!RNG_SOURCES.includes(rngSource as RngSource)) throw new RangeError('assignment rngSource is invalid');
  if (bit !== 0 && bit !== 1) throw new RangeError('assignment bit must be 0 or 1');
  return {
    wishId: asNonEmptyString(payload.wishId, 'assignment wishId'),
    arm: arm as WishArm,
    rngSource: rngSource as RngSource,
    bit,
    committedAt: asNonEmptyString(payload.committedAt, 'assignment committedAt'),
  };
}

function parseJudgment(entry: StoredLedgerEntry): JudgmentPayload {
  const payload = parsePayload(entry);
  const outcome = payload.outcome;
  if (!OUTCOMES.includes(outcome as WishOutcome)) throw new RangeError('judgment outcome is invalid');
  const pathway = payload.pathway;
  if (
    pathway !== undefined &&
    pathway !== 'own_action' &&
    pathway !== 'other_person' &&
    pathway !== 'chance_encounter' &&
    pathway !== 'unknown'
  ) {
    throw new RangeError('judgment pathway is invalid');
  }
  return {
    wishId: asNonEmptyString(payload.wishId, 'judgment wishId'),
    outcome: outcome as WishOutcome,
    ...(pathway === undefined ? {} : { pathway }),
    ...(payload.note === undefined ? {} : { note: asNonEmptyString(payload.note, 'judgment note') }),
    judgedAt: asNonEmptyString(payload.judgedAt, 'judgment judgedAt'),
  };
}

function toPracticeView(record: WishLedgerRecord): PracticeWishView {
  return {
    wishId: record.wish.wishId,
    text: record.wish.text,
    deadline: record.wish.deadline,
    likelihood: record.wish.likelihood,
    influence: record.wish.influence,
    createdAt: record.wish.createdAt,
  };
}

export function buildWishLedgerRecords(entries: readonly StoredLedgerEntry[]): WishLedgerRecord[] {
  const wishes = new Map<string, WishLedgerRecord>();

  for (const entry of entries) {
    if (entry.type !== 'wish') continue;
    const wish = parseWish(entry);
    if (wishes.has(wish.wishId)) throw new Error(`duplicate wishId: ${wish.wishId}`);
    wishes.set(wish.wishId, {
      wishEntry: entry,
      wish,
      assignmentEntry: null,
      assignment: null,
      judgmentEntry: null,
      judgment: null,
    });
  }

  for (const entry of entries) {
    if (entry.type === 'assignment') {
      const assignment = parseAssignment(entry);
      const record = wishes.get(assignment.wishId);
      if (!record) throw new Error(`assignment references missing wish: ${assignment.wishId}`);
      if (record.assignment) throw new Error(`duplicate assignment for wish: ${assignment.wishId}`);
      if (entry.seq <= record.wishEntry.seq) throw new Error('assignment must be appended after wish');
      const expectedArm: WishArm = assignment.bit === 1 ? 'practice' : 'sealed';
      if (assignment.arm !== expectedArm) throw new Error('assignment arm does not match frozen bit mapping');
      record.assignmentEntry = entry;
      record.assignment = assignment;
    }

    if (entry.type === 'judgment') {
      const judgment = parseJudgment(entry);
      const record = wishes.get(judgment.wishId);
      if (!record) throw new Error(`judgment references missing wish: ${judgment.wishId}`);
      if (record.judgment) throw new Error(`duplicate judgment for wish: ${judgment.wishId}`);
      if (entry.seq <= record.wishEntry.seq) throw new Error('judgment must be appended after wish');
      record.judgmentEntry = entry;
      record.judgment = judgment;
    }
  }

  return [...wishes.values()].sort((a, b) => a.wishEntry.seq - b.wishEntry.seq);
}

export function projectNormalWishRegistry(
  entries: readonly StoredLedgerEntry[],
  currentExperimentDate: string,
): WishRegistryProjection {
  assertIsoDate(currentExperimentDate, 'currentExperimentDate');
  const records = buildWishLedgerRecords(entries);
  const practice: PracticeWishView[] = [];
  let sealedCount = 0;
  let unassignedCount = 0;
  let dueCount = 0;

  for (const record of records) {
    if (!record.assignment) {
      unassignedCount += 1;
      continue;
    }
    if (record.judgment) continue;
    if (record.wish.deadline <= currentExperimentDate) {
      dueCount += 1;
      continue;
    }
    if (record.assignment.arm === 'practice') practice.push(toPracticeView(record));
    else sealedCount += 1;
  }

  return { practice, sealedCount, unassignedCount, dueCount };
}

export function projectWishMoment(
  entries: readonly StoredLedgerEntry[],
  currentExperimentDate: string,
): WishMomentProjection {
  const registry = projectNormalWishRegistry(entries, currentExperimentDate);
  return { date: currentExperimentDate, wishes: registry.practice };
}

export function projectDueJudgments(
  entries: readonly StoredLedgerEntry[],
  currentExperimentDate: string,
): DueWishView[] {
  assertIsoDate(currentExperimentDate, 'currentExperimentDate');
  return buildWishLedgerRecords(entries)
    .filter((record) => record.assignment && !record.judgment && record.wish.deadline <= currentExperimentDate)
    .map((record) => ({
      ...toPracticeView(record),
      arm: record.assignment!.arm,
      rngSource: record.assignment!.rngSource,
    }));
}

export function classifyPrimaryWishOutcome(outcome: WishOutcome): PrimaryWishOutcome {
  return outcome === 'realized' ? 'realized' : 'not_realized';
}
