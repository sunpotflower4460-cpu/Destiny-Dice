import { Capacitor } from '@capacitor/core';
import { CapacitorSQLite, SQLiteConnection, type SQLiteDBConnection } from '@capacitor-community/sqlite';
import { defineCustomElements as defineJeepSqliteElements } from 'jeep-sqlite/loader';
import { CREATE_LEDGER_TABLE_SQL } from './schema';

const DB_NAME = 'intention_dice';

const sqlite = new SQLiteConnection(CapacitorSQLite);

let webStoreReady: Promise<void> | undefined;

// Web は jeep-sqlite (Stencil Web Component + sql.js wasm) 経由でのみ動作する。
// AGENTS.md §8: 正確なAPIはインストール済みバージョン(jeep-sqlite/loader, @capacitor-community/sqlite)で確認済み。
// 注意: jeep-sqlite@2.8.0 は内部に sql.js@1.11.0 向けのグルーコードを同梱しているため、
// public/assets/sql-wasm.wasm は node_modules/sql.js(1.11.0固定)からコピーしたものでないと
// WebAssembly.instantiate() が LinkError で落ちる（jeep-sqlite/sql.js のバージョンアップ時は要再検証）。
function ensureWebStore(): Promise<void> {
  if (!webStoreReady) {
    webStoreReady = (async () => {
      defineJeepSqliteElements(window);
      if (!document.querySelector('jeep-sqlite')) {
        document.body.appendChild(document.createElement('jeep-sqlite'));
      }
      await customElements.whenDefined('jeep-sqlite');
      await sqlite.initWebStore();
    })();
  }
  return webStoreReady;
}

export type DbInitResult = { ok: true } | { ok: false; error: string };

/**
 * Returns the initialized application database connection.
 * Ledger INSERTs are intentionally not exposed here; P2 writes go through
 * SqliteLedgerStore -> LedgerService so the single-writer hash chain invariant
 * cannot be bypassed by normal application code.
 */
export async function getDatabaseConnection(): Promise<SQLiteDBConnection> {
  if (Capacitor.getPlatform() === 'web') {
    await ensureWebStore();
  }

  const isConn = await sqlite.isConnection(DB_NAME, false);
  const db: SQLiteDBConnection = isConn.result
    ? await sqlite.retrieveConnection(DB_NAME, false)
    : await sqlite.createConnection(DB_NAME, false, 'no-encryption', 1, false);

  const isOpen = await db.isDBOpen();
  if (!isOpen.result) {
    await db.open();
  }
  await db.execute(CREATE_LEDGER_TABLE_SQL);
  return db;
}

export async function initDatabase(): Promise<DbInitResult> {
  try {
    await getDatabaseConnection();
    return { ok: true };
  } catch (err) {
    return { ok: false, error: err instanceof Error ? err.message : String(err) };
  }
}
