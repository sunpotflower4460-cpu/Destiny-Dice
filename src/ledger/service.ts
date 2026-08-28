import { LEDGER_ENTRY_TYPES, type LedgerEntryType } from '../db/schema';
import { canonicalizeJcs } from './canonicalize';
import { computeLedgerEntryHash } from './hash';
import {
  GENESIS_PREV_HASH,
  type JsonObject,
  type LedgerEntryInput,
  type LedgerStore,
  type StoredLedgerEntry,
} from './types';

function isLedgerEntryType(value: string): value is LedgerEntryType {
  return (LEDGER_ENTRY_TYPES as readonly string[]).includes(value);
}

export type LockedFollowUpResult = {
  first: StoredLedgerEntry;
  followUp: StoredLedgerEntry;
};

export class LedgerService {
  private readonly store: LedgerStore;
  private writeTail: Promise<void> = Promise.resolve();

  constructor(store: LedgerStore) {
    this.store = store;
  }

  append(type: LedgerEntryType, payload: JsonObject, createdAt: string): Promise<StoredLedgerEntry> {
    const operation = this.enqueueWrite(() => this.appendInsideCriticalSection(type, payload, createdAt));
    return operation;
  }

  /**
   * Commits one entry and, while retaining the same single-writer slot, derives
   * and commits its mandatory follow-up. If the factory fails, the first entry
   * remains durably committed and the caller can recover it later from ledger.
   */
  appendWithFollowUp(
    first: LedgerEntryInput,
    followUpFactory: (committedFirst: StoredLedgerEntry) => Promise<LedgerEntryInput>,
  ): Promise<LockedFollowUpResult> {
    return this.enqueueWrite(async () => {
      const committedFirst = await this.appendInsideCriticalSection(first.type, first.payload, first.createdAt);
      const followUp = await followUpFactory(committedFirst);
      const committedFollowUp = await this.appendInsideCriticalSection(
        followUp.type,
        followUp.payload,
        followUp.createdAt,
      );
      return { first: committedFirst, followUp: committedFollowUp };
    });
  }

  list(): Promise<StoredLedgerEntry[]> {
    return this.store.list();
  }

  private enqueueWrite<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.writeTail.then(operation);
    this.writeTail = result.then(
      () => undefined,
      () => undefined,
    );
    return result;
  }

  private async appendInsideCriticalSection(
    type: LedgerEntryType,
    payload: JsonObject,
    createdAt: string,
  ): Promise<StoredLedgerEntry> {
    if (!isLedgerEntryType(type)) {
      throw new TypeError(`Unsupported ledger entry type: ${String(type)}`);
    }
    if (typeof createdAt !== 'string' || createdAt.length === 0) {
      throw new TypeError('createdAt must be a non-empty exact timestamp string');
    }

    const head = await this.store.getLast();
    if (!head && type !== 'registration') {
      throw new Error('The first ledger entry must be registration');
    }
    if (head && type === 'registration') {
      throw new Error('A ledger chain may contain only one genesis registration entry');
    }

    const payloadJson = canonicalizeJcs(payload);
    const prevHash = head?.entryHash ?? GENESIS_PREV_HASH;
    const entryHash = await computeLedgerEntryHash({
      type,
      payload,
      createdAt,
      prevHash,
    });

    return this.store.append({
      type,
      payloadJson,
      createdAt,
      prevHash,
      entryHash,
    });
  }
}
