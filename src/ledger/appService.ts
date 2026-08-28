import { getDatabaseConnection } from '../db/sqlite';
import { SqliteLedgerStore } from '../db/sqliteLedgerStore';
import { LedgerService } from './service';

let applicationLedger: Promise<LedgerService> | undefined;

/** One LedgerService instance per app runtime keeps the P2 single-writer queue global. */
export function getApplicationLedgerService(): Promise<LedgerService> {
  if (!applicationLedger) {
    applicationLedger = getDatabaseConnection().then((db) => new LedgerService(new SqliteLedgerStore(db)));
  }
  return applicationLedger;
}
