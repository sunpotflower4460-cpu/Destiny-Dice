import type { SQLiteDBConnection } from '@capacitor-community/sqlite';
import { LEDGER_ENTRY_TYPES, LEDGER_TABLE_NAME, type LedgerEntryType } from './schema';
import type { LedgerStore, PendingLedgerEntry, StoredLedgerEntry } from '../ledger/types';

function isLedgerEntryType(value: string): value is LedgerEntryType {
  return (LEDGER_ENTRY_TYPES as readonly string[]).includes(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function decodeRow(value: unknown): StoredLedgerEntry {
  if (!isRecord(value)) {
    throw new TypeError('SQLite ledger row must be an object');
  }

  const seq = value.seq;
  const type = value.type;
  const payloadJson = value.payload_json;
  const createdAt = value.created_at;
  const prevHash = value.prev_hash;
  const entryHash = value.entry_hash;

  if (!Number.isSafeInteger(seq) || (seq as number) <= 0) {
    throw new TypeError('SQLite ledger row has invalid seq');
  }
  if (typeof type !== 'string' || !isLedgerEntryType(type)) {
    throw new TypeError('SQLite ledger row has invalid type');
  }
  if (
    typeof payloadJson !== 'string' ||
    typeof createdAt !== 'string' ||
    typeof prevHash !== 'string' ||
    typeof entryHash !== 'string'
  ) {
    throw new TypeError('SQLite ledger row has invalid text columns');
  }

  return {
    seq: seq as number,
    type,
    payloadJson,
    createdAt,
    prevHash,
    entryHash,
  };
}

const SELECT_COLUMNS = 'seq, type, payload_json, created_at, prev_hash, entry_hash';

export class SqliteLedgerStore implements LedgerStore {
  private readonly db: SQLiteDBConnection;

  constructor(db: SQLiteDBConnection) {
    this.db = db;
  }

  async getLast(): Promise<StoredLedgerEntry | null> {
    const result = await this.db.query(
      `SELECT ${SELECT_COLUMNS} FROM ${LEDGER_TABLE_NAME} ORDER BY seq DESC LIMIT 1`,
    );
    const row = result.values?.[0];
    return row === undefined ? null : decodeRow(row);
  }

  async append(entry: PendingLedgerEntry): Promise<StoredLedgerEntry> {
    const result = await this.db.run(
      `INSERT INTO ${LEDGER_TABLE_NAME} (type, payload_json, created_at, prev_hash, entry_hash) VALUES (?, ?, ?, ?, ?)`,
      [entry.type, entry.payloadJson, entry.createdAt, entry.prevHash, entry.entryHash],
    );
    const seq = result.changes?.lastId;
    if (!Number.isSafeInteger(seq) || (seq as number) <= 0) {
      throw new Error('SQLite did not return a valid ledger lastId');
    }
    return {
      ...entry,
      seq: seq as number,
    };
  }

  async list(): Promise<StoredLedgerEntry[]> {
    const result = await this.db.query(`SELECT ${SELECT_COLUMNS} FROM ${LEDGER_TABLE_NAME} ORDER BY seq ASC`);
    return (result.values ?? []).map(decodeRow);
  }
}
