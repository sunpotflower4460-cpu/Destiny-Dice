import type { LedgerService } from '../ledger/service';
import type { JsonObject, StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import type { RngService } from '../rng/service';
import {
  assertIsoDate,
  buildWishLedgerRecords,
  classifyPrimaryWishOutcome,
  projectDueJudgments,
  projectNormalWishRegistry,
  projectWishMoment,
} from './projection';
import type {
  AssignmentPayload,
  DueWishView,
  JudgmentPayload,
  PrimaryWishOutcome,
  WishClock,
  WishIdFactory,
  WishMomentPayload,
  WishMomentProjection,
  WishOutcome,
  WishPathway,
  WishPayload,
  WishRegistrationInput,
  WishRegistryProjection,
} from './types';

type WishLedger = Pick<LedgerService, 'append' | 'appendWithFollowUp' | 'list'>;
type WishRng = Pick<RngService, 'getAssignmentBit'>;

export const ASSIGNMENT_ARM_BY_BIT = {
  0: 'sealed',
  1: 'practice',
} as const;

function parseRegistration(entry: StoredLedgerEntry): RegistrationPayload {
  if (entry.type !== 'registration') throw new Error('ledger genesis is not registration');
  const parsed: unknown = JSON.parse(entry.payloadJson);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('registration payload is not an object');
  }
  const candidate = parsed as Partial<RegistrationPayload>;
  if (!candidate.layerC || typeof candidate.layerC !== 'object' || typeof candidate.layerC.enabled !== 'boolean') {
    throw new Error('registration payload is missing Layer C settings');
  }
  return candidate as RegistrationPayload;
}

function validateWishInput(input: WishRegistrationInput): void {
  if (typeof input.text !== 'string' || input.text.trim().length === 0) {
    throw new TypeError('wish text must be non-empty');
  }
  assertIsoDate(input.deadline, 'wish deadline');
  if (input.likelihood !== 1 && input.likelihood !== 2 && input.likelihood !== 3) {
    throw new RangeError('wish likelihood must be 1, 2, or 3');
  }
  if (input.influence !== 'self' && input.influence !== 'mixed' && input.influence !== 'external') {
    throw new RangeError('wish influence must be self, mixed, or external');
  }
}

function validateWishId(wishId: string): void {
  if (typeof wishId !== 'string' || wishId.trim().length === 0) {
    throw new TypeError('wishId must be non-empty');
  }
}

function validateJudgment(outcome: Exclude<WishOutcome, 'withdrawn'>, pathway?: WishPathway): void {
  if (outcome === 'realized' && pathway === undefined) {
    throw new Error('realized judgment requires a pathway');
  }
  if (outcome !== 'realized' && pathway !== undefined) {
    throw new Error('pathway is only allowed for realized judgments');
  }
}

function sameIds(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && a.every((value, index) => value === b[index]);
}

export type RegisteredWishResult = {
  wishEntry: StoredLedgerEntry;
  assignmentEntry: StoredLedgerEntry;
  wish: WishPayload;
  assignment: AssignmentPayload;
};

export class WishRegistryService {
  private readonly ledger: WishLedger;
  private readonly rng: WishRng;
  private readonly clock: WishClock;
  private readonly createWishId: WishIdFactory;
  private recoveryTail: Promise<void> = Promise.resolve();

  constructor(ledger: WishLedger, rng: WishRng, clock: WishClock, createWishId: WishIdFactory) {
    this.ledger = ledger;
    this.rng = rng;
    this.clock = clock;
    this.createWishId = createWishId;
  }

  async registerWish(input: WishRegistrationInput): Promise<RegisteredWishResult> {
    validateWishInput(input);
    const entries = await this.ledger.list();
    await this.assertLayerCEnabled(entries);

    const wishId = this.createWishId();
    validateWishId(wishId);
    if (buildWishLedgerRecords(entries).some((record) => record.wish.wishId === wishId)) {
      throw new Error(`wishId already exists: ${wishId}`);
    }

    const createdAt = this.clock.now();
    const wish: WishPayload = {
      wishId,
      text: input.text,
      deadline: input.deadline,
      likelihood: input.likelihood,
      influence: input.influence,
      createdAt,
    };

    const result = await this.ledger.appendWithFollowUp(
      { type: 'wish', payload: wish, createdAt },
      async (wishEntry) => {
        const random = await this.rng.getAssignmentBit();
        const committedAt = this.clock.now();
        const assignment: AssignmentPayload = {
          wishId,
          arm: ASSIGNMENT_ARM_BY_BIT[random.bit],
          rngSource: random.source,
          bit: random.bit,
          committedAt,
        };
        return { type: 'assignment', payload: assignment, createdAt: committedAt };
      },
    );

    if (result.followUp.seq !== result.first.seq + 1) {
      throw new Error('assignment must immediately follow wish during normal registration');
    }
    const assignment = JSON.parse(result.followUp.payloadJson) as AssignmentPayload;
    return {
      wishEntry: result.first,
      assignmentEntry: result.followUp,
      wish,
      assignment,
    };
  }

  recoverUnassignedWishes(): Promise<StoredLedgerEntry[]> {
    const operation = this.recoveryTail.then(() => this.recoverInside());
    this.recoveryTail = operation.then(
      () => undefined,
      () => undefined,
    );
    return operation;
  }

  async projectRegistry(currentExperimentDate: string): Promise<WishRegistryProjection> {
    return projectNormalWishRegistry(await this.ledger.list(), currentExperimentDate);
  }

  async projectWishMoment(currentExperimentDate: string): Promise<WishMomentProjection> {
    return projectWishMoment(await this.ledger.list(), currentExperimentDate);
  }

  async projectDueJudgments(currentExperimentDate: string): Promise<DueWishView[]> {
    return projectDueJudgments(await this.ledger.list(), currentExperimentDate);
  }

  async recordWishMoment(
    currentExperimentDate: string,
    wishIdsShown: readonly string[],
    seconds: number,
  ): Promise<StoredLedgerEntry> {
    assertIsoDate(currentExperimentDate, 'currentExperimentDate');
    if (!Number.isInteger(seconds) || seconds < 30 || seconds > 60) {
      throw new RangeError('wish moment seconds must be an integer from 30 through 60');
    }
    const projection = projectWishMoment(await this.ledger.list(), currentExperimentDate);
    const expectedIds = projection.wishes.map((wish) => wish.wishId);
    if (!sameIds(wishIdsShown, expectedIds)) {
      throw new Error('wish moment must show exactly the currently eligible practice wishes');
    }
    if (expectedIds.length === 0) {
      throw new Error('wish moment cannot be recorded when no practice wishes are eligible');
    }
    const createdAt = this.clock.now();
    const payload: WishMomentPayload = {
      date: currentExperimentDate,
      wishIdsShown: [...expectedIds],
      seconds,
    };
    return this.ledger.append('wishmoment', payload, createdAt);
  }

  async judgeWish(
    wishId: string,
    currentExperimentDate: string,
    outcome: Exclude<WishOutcome, 'withdrawn'>,
    pathway?: WishPathway,
    note?: string,
  ): Promise<StoredLedgerEntry> {
    validateWishId(wishId);
    assertIsoDate(currentExperimentDate, 'currentExperimentDate');
    validateJudgment(outcome, pathway);
    if (note !== undefined && note.trim().length === 0) throw new TypeError('judgment note must be non-empty when present');

    const entries = await this.ledger.list();
    const record = buildWishLedgerRecords(entries).find((candidate) => candidate.wish.wishId === wishId);
    if (!record) throw new Error(`wish not found: ${wishId}`);
    if (!record.assignment) throw new Error('wish must be assigned before judgment');
    if (record.judgment) throw new Error('wish already has a judgment');
    if (record.wish.deadline > currentExperimentDate) throw new Error('wish cannot be judged before its deadline');

    const judgedAt = this.clock.now();
    const payload: JudgmentPayload = {
      wishId,
      outcome,
      ...(pathway === undefined ? {} : { pathway }),
      ...(note === undefined ? {} : { note }),
      judgedAt,
    };
    return this.ledger.append('judgment', payload, judgedAt);
  }

  async withdrawWish(wishId: string, note?: string): Promise<StoredLedgerEntry> {
    validateWishId(wishId);
    if (note !== undefined && note.trim().length === 0) throw new TypeError('withdrawal note must be non-empty when present');
    const entries = await this.ledger.list();
    const record = buildWishLedgerRecords(entries).find((candidate) => candidate.wish.wishId === wishId);
    if (!record) throw new Error(`wish not found: ${wishId}`);
    if (!record.assignment) throw new Error('wish must be assigned before withdrawal');
    if (record.judgment) throw new Error('wish already has a judgment');

    const judgedAt = this.clock.now();
    const payload: JudgmentPayload = {
      wishId,
      outcome: 'withdrawn',
      ...(note === undefined ? {} : { note }),
      judgedAt,
    };
    return this.ledger.append('judgment', payload, judgedAt);
  }

  primaryOutcomeFor(outcome: WishOutcome): PrimaryWishOutcome {
    return classifyPrimaryWishOutcome(outcome);
  }

  private async recoverInside(): Promise<StoredLedgerEntry[]> {
    const initial = await this.ledger.list();
    await this.assertLayerCEnabled(initial);
    const pending = buildWishLedgerRecords(initial).filter((record) => !record.assignment);
    const recovered: StoredLedgerEntry[] = [];

    for (const record of pending) {
      const latest = await this.ledger.list();
      const current = buildWishLedgerRecords(latest).find((candidate) => candidate.wish.wishId === record.wish.wishId);
      if (!current || current.assignment) continue;
      const random = await this.rng.getAssignmentBit();
      const committedAt = this.clock.now();
      const payload: AssignmentPayload = {
        wishId: current.wish.wishId,
        arm: ASSIGNMENT_ARM_BY_BIT[random.bit],
        rngSource: random.source,
        bit: random.bit,
        committedAt,
      };
      recovered.push(await this.ledger.append('assignment', payload, committedAt));
    }
    return recovered;
  }

  private async assertLayerCEnabled(entries?: readonly StoredLedgerEntry[]): Promise<void> {
    const rows = entries ?? (await this.ledger.list());
    const genesis = rows[0];
    if (!genesis) throw new Error('registration genesis is required before Layer C');
    const registration = parseRegistration(genesis);
    if (!registration.layerC.enabled) throw new Error('Layer C is disabled for this registered experiment');
  }
}
