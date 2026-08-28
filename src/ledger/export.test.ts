import { describe, expect, it } from 'vitest';
import { exportLedgerCsv, exportLedgerJson, importLedgerCsv, importLedgerJson } from './export';
import { MemoryLedgerStore } from './memoryStore';
import { LedgerService } from './service';

async function makeEntries() {
  const store = new MemoryLedgerStore();
  const service = new LedgerService(store);
  await service.append(
    'registration',
    { experimentId: 'exp,"quoted"', note: 'line1\nline2' },
    '2026-08-28T01:00:00.000Z',
  );
  await service.append(
    'session',
    { bitsHex: 'deadbeef00112233', nBits: 64, ritual: { text: 'comma, quote " and slash \\' } },
    '2026-08-28T01:01:00.000Z',
  );
  return store.list();
}

describe('ledger export', () => {
  it('round-trips JSON without losing canonical payload strings or raw bits', async () => {
    const entries = await makeEntries();
    const json = exportLedgerJson(entries);

    expect(json).toContain('deadbeef00112233');
    expect(importLedgerJson(json)).toEqual(entries);
  });

  it('round-trips RFC4180-style CSV including commas and quotes', async () => {
    const entries = await makeEntries();
    const csv = exportLedgerCsv(entries);

    expect(csv).toContain('deadbeef00112233');
    expect(importLedgerCsv(csv)).toEqual(entries);
  });
});
