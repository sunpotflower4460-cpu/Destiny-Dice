import type { LedgerService } from '../ledger/service';
import type { JsonObject, StoredLedgerEntry } from '../ledger/types';
import { projectCurrentSchedule } from '../registration/projection';
import type { RegistrationPayload } from '../registration/types';
import type { RngService } from '../rng/service';
import { summarizeBitstream } from '../stats/core';
import { buildRitualRecord } from './ritual';
import type {
  Clock,
  ControlPayload,
  MoodRating,
  OrphanedPredictionSlot,
  PredictionPayload,
  SessionContext,
  SessionContextInput,
  SessionDraft,
  SessionPlan,
  SessionResult,
} from './types';

type SessionLedger = Pick<LedgerService, 'append' | 'appendConditionally' | 'list'>;
type SessionRng = Pick<RngService, 'getBits'>;

function parseObject(payloadJson: string, label: string): JsonObject {
  const parsed: unknown = JSON.parse(payloadJson);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error(`${label} payload is not an object`);
  }
  return parsed as JsonObject;
}

function parseRegistration(entry: StoredLedgerEntry): RegistrationPayload {
  if (entry.type !== 'registration') {
    throw new Error('ledger genesis is not a registration entry');
  }
  const parsed = parseObject(entry.payloadJson, 'registration') as Partial<RegistrationPayload>;
  if (
    typeof parsed.experimentId !== 'string' ||
    typeof parsed.startDate !== 'string' ||
    typeof parsed.days !== 'number' ||
    typeof parsed.bitsPerDraw !== 'number' ||
    typeof parsed.sessionsPerDay !== 'number' ||
    !Array.isArray(parsed.schedule) ||
    typeof parsed.targetSeed !== 'string'
  ) {
    throw new Error('registration payload is missing P4-required fields');
  }
  return parsed as RegistrationPayload;
}

function validateMood(mood: MoodRating, label: string): void {
  for (const [name, value] of [
    ['v', mood.v],
    ['e', mood.e],
  ] as const) {
    if (!Number.isInteger(value) || value < 1 || value > 10) {
      throw new RangeError(`${label}.${name} must be an integer from 1 through 10`);
    }
  }
}

function buildContext(input: SessionContextInput): SessionContext {
  if (!Number.isInteger(input.hour) || input.hour < 0 || input.hour > 23) {
    throw new RangeError('context.hour must be an integer from 0 through 23');
  }
  if (!Number.isInteger(input.dow) || input.dow < 0 || input.dow > 6) {
    throw new RangeError('context.dow must be an integer from 0 through 6');
  }
  if (!Number.isFinite(input.lunarPhase) || input.lunarPhase < 0 || input.lunarPhase > 1) {
    throw new RangeError('context.lunarPhase must be a finite number from 0 through 1');
  }
  if (input.sleep !== undefined && (!Number.isInteger(input.sleep) || input.sleep < 1 || input.sleep > 10)) {
    throw new RangeError('context.sleep must be an integer from 1 through 10 when present');
  }
  if (input.stateTag !== undefined && input.stateTag.length === 0) {
    throw new TypeError('context.stateTag must be non-empty when present');
  }

  return {
    hour: input.hour,
    dow: input.dow,
    lunarPhase: input.lunarPhase,
    ...(input.sleep === undefined ? {} : { sleep: input.sleep }),
    ...(input.stateTag === undefined ? {} : { stateTag: input.stateTag }),
  };
}

function parseControl(entry: StoredLedgerEntry): ControlPayload | null {
  if (entry.type !== 'control') return null;
  const parsed = parseObject(entry.payloadJson, 'control');
  if (typeof parsed.date !== 'string') {
    throw new Error(`control entry ${entry.seq} is missing date`);
  }
  return parsed as ControlPayload;
}

function parsePrediction(entry: StoredLedgerEntry): PredictionPayload {
  if (entry.type !== 'prediction') {
    throw new Error(`ledger entry ${entry.seq} is not prediction`);
  }
  return parseObject(entry.payloadJson, 'prediction') as PredictionPayload;
}

function sessionIdentity(entry: StoredLedgerEntry): { date: string; seqInDay: number } | null {
  if (entry.type !== 'session') return null;
  const parsed = parseObject(entry.payloadJson, 'session');
  if (typeof parsed.date !== 'string' || typeof parsed.seqInDay !== 'number') {
    throw new Error(`session entry ${entry.seq} has invalid identity`);
  }
  return { date: parsed.date, seqInDay: parsed.seqInDay };
}

function predictionMatchesPlan(entry: StoredLedgerEntry, plan: SessionPlan): boolean {
  if (entry.type !== 'prediction') return false;
  const parsed = parseObject(entry.payloadJson, 'prediction');
  return (
    parsed.date === plan.experimentDate &&
    parsed.seqInDay === plan.seqInDay &&
    parsed.condition === plan.condition &&
    parsed.targetDir === plan.targetDir
  );
}

function normalizedProphecy(text: string | undefined): string | undefined {
  return text === undefined || text.length === 0 ? undefined : text;
}

function assertDraftMatchesCommittedPrediction(draft: SessionDraft, prediction: PredictionPayload): void {
  if (draft.confidence !== prediction.confidence) {
    throw new Error('retry draft confidence does not match the committed prediction');
  }
  if (normalizedProphecy(draft.prophecyText) !== normalizedProphecy(prediction.prophecyText)) {
    throw new Error('retry draft prophecy does not match the committed prediction');
  }
  if (draft.startedAt > prediction.committedAt) {
    throw new Error(
      'retry startedAt is after the committed prediction; original pre-prediction measurements cannot be reconstructed',
    );
  }
}

function findControl(entries: readonly StoredLedgerEntry[], experimentDate: string): StoredLedgerEntry | undefined {
  return entries.find((entry) => parseControl(entry)?.date === experimentDate);
}

function findSession(
  entries: readonly StoredLedgerEntry[],
  experimentDate: string,
  seqInDay: number,
): StoredLedgerEntry | undefined {
  return entries.find((entry) => {
    const identity = sessionIdentity(entry);
    return identity?.date === experimentDate && identity.seqInDay === seqInDay;
  });
}

export function findOrphanedPredictionSlot(
  entries: readonly StoredLedgerEntry[],
  experimentDate: string,
  seqInDay: number,
): OrphanedPredictionSlot | null {
  if (findSession(entries, experimentDate, seqInDay)) return null;
  let prediction: StoredLedgerEntry | undefined;
  for (const entry of entries) {
    if (entry.type !== 'prediction') continue;
    const payload = parsePrediction(entry);
    if (payload.date === experimentDate && payload.seqInDay === seqInDay) {
      prediction = entry;
    }
  }
  if (!prediction) return null;
  const payload = parsePrediction(prediction);
  return {
    experimentDate,
    seqInDay,
    predictionSeq: prediction.seq,
    committedAt: payload.committedAt,
  };
}

export class SessionFlowService {
  private readonly ledger: SessionLedger;
  private readonly rng: SessionRng;
  private readonly clock: Clock;
  private controlTail: Promise<void> = Promise.resolve();
  private sessionTail: Promise<void> = Promise.resolve();

  constructor(ledger: SessionLedger, rng: SessionRng, clock: Clock) {
    this.ledger = ledger;
    this.rng = rng;
    this.clock = clock;
  }

  /**
   * First activation for an experiment day commits at most one machine control.
   * The returned plan is only exposed after that control is durably appended.
   * seqInDay is persisted as 1..sessionsPerDay; UI may display it directly.
   */
  async prepareSession(experimentDate: string, seqInDay: number): Promise<SessionPlan> {
    const plan = await this.resolvePlan(experimentDate, seqInDay, true);
    await this.ensureDailyControl(experimentDate);
    return plan;
  }

  ensureDailyControl(experimentDate: string): Promise<StoredLedgerEntry> {
    const operation = this.controlTail.then(() => this.ensureDailyControlInside(experimentDate));
    this.controlTail = operation.then(
      () => undefined,
      () => undefined,
    );
    return operation;
  }

  async runSession(draft: SessionDraft): Promise<SessionResult> {
    const operation = this.sessionTail.then(() => this.runSessionInside(draft));
    this.sessionTail = operation.then(
      () => undefined,
      () => undefined,
    );
    return operation;
  }

  /**
   * All pre-result user measurements arrive before this method is called.
   * The method appends prediction, re-reads and verifies that committed row,
   * and only then calls measured RNG. There is no measured-RNG method exposed
   * by this service that can bypass the committed prediction check.
   */
  private async runSessionInside(draft: SessionDraft): Promise<SessionResult> {
    validateMood(draft.moodPre, 'moodPre');
    validateMood(draft.moodPost, 'moodPost');
    if (!Number.isInteger(draft.confidence) || draft.confidence < 0 || draft.confidence > 100) {
      throw new RangeError('confidence must be an integer from 0 through 100');
    }
    if (typeof draft.startedAt !== 'string' || draft.startedAt.length === 0) {
      throw new TypeError('startedAt must be a non-empty exact timestamp string');
    }

    const plan = await this.resolvePlan(draft.experimentDate, draft.seqInDay, true);
    await this.assertDailyControlCommitted(plan.experimentDate);

    const ritual = buildRitualRecord(plan.condition, draft.ritual);
    const context = buildContext(draft.context);
    const predictionEntry = await this.commitOrReusePrediction(plan, draft);

    const registration = await this.loadRegistration();
    const random = await this.rng.getBits(registration.bitsPerDraw);
    if (random.nBits !== registration.bitsPerDraw) {
      throw new Error(`measured RNG returned ${random.nBits} bits; expected ${registration.bitsPerDraw}`);
    }
    const stats = summarizeBitstream(random.bitsHex, random.nBits, plan.targetDir);
    const completedAt = this.clock.now();
    const payload = {
      date: plan.experimentDate,
      seqInDay: plan.seqInDay,
      condition: plan.condition,
      targetDir: plan.targetDir,
      rngSource: random.source,
      predictionSeq: predictionEntry.seq,
      bitsHex: random.bitsHex,
      nBits: random.nBits,
      hits: stats.hits,
      z: stats.z,
      ritual,
      moodPre: draft.moodPre,
      moodPost: draft.moodPost,
      context,
      startedAt: draft.startedAt,
      completedAt,
    } as const;

    const sessionEntry = await this.ledger.appendConditionally(
      (entries) => findSession(entries, plan.experimentDate, plan.seqInDay) === undefined,
      async () => ({ type: 'session', payload, createdAt: completedAt }),
    );
    if (!sessionEntry) {
      throw new Error(`session ${plan.experimentDate} #${plan.seqInDay} is already committed`);
    }
    if (predictionEntry.seq >= sessionEntry.seq) {
      throw new Error('prediction must be committed before session');
    }

    return { plan, predictionEntry, sessionEntry, payload };
  }

  private async commitOrReusePrediction(plan: SessionPlan, draft: SessionDraft): Promise<StoredLedgerEntry> {
    const appended = await this.ledger.appendConditionally(
      (entries) =>
        findSession(entries, plan.experimentDate, plan.seqInDay) === undefined &&
        entries.find((entry) => predictionMatchesPlan(entry, plan)) === undefined,
      async () => {
        const committedAt = this.clock.now();
        const payload: PredictionPayload = {
          date: plan.experimentDate,
          seqInDay: plan.seqInDay,
          condition: plan.condition,
          targetDir: plan.targetDir,
          confidence: draft.confidence,
          committedAt,
          ...(draft.prophecyText === undefined || draft.prophecyText.length === 0
            ? {}
            : { prophecyText: draft.prophecyText }),
        };
        return { type: 'prediction', payload, createdAt: committedAt };
      },
    );
    if (appended) {
      await this.assertPredictionCommitted(appended, parsePrediction(appended));
      return appended;
    }

    const latest = await this.ledger.list();
    if (findSession(latest, plan.experimentDate, plan.seqInDay)) {
      throw new Error(`session ${plan.experimentDate} #${plan.seqInDay} is already committed`);
    }
    const existing = latest.find((entry) => predictionMatchesPlan(entry, plan));
    if (!existing) {
      throw new Error('prediction append was skipped without an existing matching prediction');
    }
    const payload = parsePrediction(existing);
    assertDraftMatchesCommittedPrediction(draft, payload);
    await this.assertPredictionCommitted(existing, payload);
    return existing;
  }

  private async ensureDailyControlInside(experimentDate: string): Promise<StoredLedgerEntry> {
    const entries = await this.ledger.list();
    const registration = await this.loadRegistration(entries);
    const projection = await projectCurrentSchedule(registration, experimentDate);
    if (!projection) {
      throw new RangeError('experimentDate is outside the frozen experiment window');
    }

    const existing = findControl(entries, experimentDate);
    if (existing) return existing;

    const appended = await this.ledger.appendConditionally(
      (latest) => findControl(latest, experimentDate) === undefined,
      async () => {
        const random = await this.rng.getBits(registration.bitsPerDraw);
        if (random.nBits !== registration.bitsPerDraw) {
          throw new Error(`control RNG returned ${random.nBits} bits; expected ${registration.bitsPerDraw}`);
        }
        const stats = summarizeBitstream(random.bitsHex, random.nBits, 1);
        const payload: ControlPayload = {
          date: experimentDate,
          rngSource: random.source,
          bitsHex: random.bitsHex,
          nBits: random.nBits,
          hits: stats.hits,
          z: stats.z,
        };
        return { type: 'control', payload, createdAt: this.clock.now() };
      },
    );
    if (appended) return appended;

    const committed = findControl(await this.ledger.list(), experimentDate);
    if (!committed) {
      throw new Error(`daily control for ${experimentDate} was skipped without an existing control`);
    }
    return committed;
  }

  private async resolvePlan(
    experimentDate: string,
    seqInDay: number,
    rejectCompleted: boolean,
  ): Promise<SessionPlan> {
    const registration = await this.loadRegistration();
    if (!Number.isInteger(seqInDay) || seqInDay < 1 || seqInDay > registration.sessionsPerDay) {
      throw new RangeError(`seqInDay must be an integer from 1 through ${registration.sessionsPerDay}`);
    }
    if (rejectCompleted) {
      await this.assertSessionNotAlreadyCommitted(experimentDate, seqInDay);
    }
    const projection = await projectCurrentSchedule(registration, experimentDate);
    if (!projection) {
      throw new RangeError('experimentDate is outside the frozen experiment window');
    }
    const targetDir = projection.targets[seqInDay - 1];
    if (targetDir !== 0 && targetDir !== 1) {
      throw new Error('frozen target schedule is missing the requested session target');
    }
    return {
      experimentDate,
      dayIndex: projection.dayIndex,
      seqInDay,
      condition: projection.condition,
      targetDir,
    };
  }

  private async assertDailyControlCommitted(experimentDate: string): Promise<void> {
    const entries = await this.ledger.list();
    const control = entries.find((entry) => parseControl(entry)?.date === experimentDate);
    if (!control) {
      throw new Error('daily control must be committed before a measured session');
    }
  }

  private async assertSessionNotAlreadyCommitted(experimentDate: string, seqInDay: number): Promise<void> {
    const entries = await this.ledger.list();
    if (findSession(entries, experimentDate, seqInDay)) {
      throw new Error(`session ${experimentDate} #${seqInDay} is already committed`);
    }
  }

  private async assertPredictionCommitted(
    predictionEntry: StoredLedgerEntry,
    expected: PredictionPayload,
  ): Promise<void> {
    const entries = await this.ledger.list();
    const persisted = entries.find((entry) => entry.seq === predictionEntry.seq);
    if (!persisted) {
      throw new Error('prediction commit is not present in the ledger');
    }
    const payload = parsePrediction(persisted);
    if (
      payload.date !== expected.date ||
      payload.seqInDay !== expected.seqInDay ||
      payload.condition !== expected.condition ||
      payload.targetDir !== expected.targetDir
    ) {
      throw new Error('committed prediction does not match the requested measured session');
    }
  }

  private async loadRegistration(entries?: StoredLedgerEntry[]): Promise<RegistrationPayload> {
    const rows = entries ?? (await this.ledger.list());
    const genesis = rows[0];
    if (!genesis) {
      throw new Error('registration genesis is required before P4 session flow');
    }
    return parseRegistration(genesis);
  }
}
