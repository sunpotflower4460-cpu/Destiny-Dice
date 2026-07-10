// DESIGN.md §5 のスキーマ通り。ledger は追記専用の単一チェーン（AGENTS.md 不変ルール1）。
// type が許容する8種（DESIGN.md §5・§6 P2「全8タイプ対応」）。
export const LEDGER_ENTRY_TYPES = [
  'registration',
  'control',
  'prediction',
  'session',
  'wishmoment',
  'wish',
  'assignment',
  'judgment',
] as const;

export type LedgerEntryType = (typeof LEDGER_ENTRY_TYPES)[number];

export const LEDGER_TABLE_NAME = 'ledger';

export const CREATE_LEDGER_TABLE_SQL = `
CREATE TABLE IF NOT EXISTS ${LEDGER_TABLE_NAME} (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  type TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  prev_hash TEXT NOT NULL,
  entry_hash TEXT NOT NULL
);
`.trim();
