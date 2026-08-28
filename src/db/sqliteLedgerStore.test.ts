import type { SQLiteDBConnection } from '@capacitor-community/sqlite';
import { describe, expect, it, vi } from 'vitest';
import { SqliteLedgerStore } from './sqliteLedgerStore';
import type { PendingLedgerEntry } from '../ledger/types';

const row = {
  seq: 1,
  type: 'registration',
  payload_json: '{"experimentId":"exp-1"}',
  created_at: '2026-08-28T01:00:00.000Z',
  prev_hash: '0'.repeat(64),
  entry_hash: 'a'.repeat(64),
};

describe('SqliteLedgerStore', () => {
  it('reads ledger rows in schema order and appends without exposing mutation APIs', async () => {
    const query = vi.fn(async (statement: string) => ({
      values: statement.includes('LIMIT 1') ? [row] : [row],
    }));
    const run = vi.fn(async () => ({
      changes: { changes: 1, lastId: 2, values: [] },
    }));
    const db = { query, run } as unknown as SQLiteDBConnection;
    const store = new SqliteLedgerStore(db);

    await expect(store.getLast()).resolves.toMatchObject({ seq: 1, type: 'registration' });
    await expect(store.list()).resolves.toHaveLength(1);

    const pending: PendingLedgerEntry = {
      type: 'control',
      payloadJson: '{"date":"2026-08-28"}',
      createdAt: '2026-08-28T01:01:00.000Z',
      prevHash: 'a'.repeat(64),
      entryHash: 'b'.repeat(64),
    };
    await expect(store.append(pending)).resolves.toEqual({ ...pending, seq: 2 });

    const insertSql = String(run.mock.calls[0]?.[0]);
    expect(insertSql).toContain('INSERT INTO ledger');
    expect(insertSql).not.toContain('UPDATE');
    expect(insertSql).not.toContain('DELETE');
  });
});
