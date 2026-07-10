import { describe, expect, it } from 'vitest';
import { CREATE_LEDGER_TABLE_SQL, LEDGER_ENTRY_TYPES, LEDGER_TABLE_NAME } from './schema';

describe('ledger schema (P0 dummy test)', () => {
  it('defines all 8 entry types from DESIGN.md §5', () => {
    expect(LEDGER_ENTRY_TYPES).toHaveLength(8);
    expect(LEDGER_ENTRY_TYPES).toEqual([
      'registration',
      'control',
      'prediction',
      'session',
      'wishmoment',
      'wish',
      'assignment',
      'judgment',
    ]);
  });

  it('creates the ledger table with the columns from DESIGN.md §5', () => {
    expect(CREATE_LEDGER_TABLE_SQL).toContain(`CREATE TABLE IF NOT EXISTS ${LEDGER_TABLE_NAME}`);
    expect(CREATE_LEDGER_TABLE_SQL).toContain('seq INTEGER PRIMARY KEY AUTOINCREMENT');
    expect(CREATE_LEDGER_TABLE_SQL).toContain('type TEXT NOT NULL');
    expect(CREATE_LEDGER_TABLE_SQL).toContain('payload_json TEXT NOT NULL');
    expect(CREATE_LEDGER_TABLE_SQL).toContain('created_at TEXT NOT NULL');
    expect(CREATE_LEDGER_TABLE_SQL).toContain('prev_hash TEXT NOT NULL');
    expect(CREATE_LEDGER_TABLE_SQL).toContain('entry_hash TEXT NOT NULL');
  });
});
