import type { LedgerEntryType } from '../db/schema';

export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
export type JsonObject = { [key: string]: JsonValue };

export const GENESIS_PREV_HASH = '0'.repeat(64);

export type LedgerEntryInput = {
  type: LedgerEntryType;
  payload: JsonObject;
  createdAt: string;
};

export type PendingLedgerEntry = {
  type: LedgerEntryType;
  payloadJson: string;
  createdAt: string;
  prevHash: string;
  entryHash: string;
};

export type StoredLedgerEntry = PendingLedgerEntry & {
  seq: number;
};

export interface LedgerStore {
  getLast(): Promise<StoredLedgerEntry | null>;
  append(entry: PendingLedgerEntry): Promise<StoredLedgerEntry>;
  list(): Promise<StoredLedgerEntry[]>;
}
