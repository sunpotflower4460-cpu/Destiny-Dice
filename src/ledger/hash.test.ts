import { describe, expect, it } from 'vitest';
import { computeLedgerEntryHash, sha256Hex } from './hash';
import { GENESIS_PREV_HASH } from './types';

describe('ledger hashing', () => {
  it('matches the standard SHA-256 vector for abc', async () => {
    await expect(sha256Hex('abc')).resolves.toBe(
      'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
    );
  });

  it('binds type, payload, createdAt and prevHash into the entry hash', async () => {
    const base = {
      type: 'registration',
      payload: { experimentId: 'exp-1', nested: { b: 2, a: 1 } },
      createdAt: '2026-08-28T01:00:00.000Z',
      prevHash: GENESIS_PREV_HASH,
    } as const;

    const original = await computeLedgerEntryHash(base);
    expect(original).toMatch(/^[0-9a-f]{64}$/);
    await expect(computeLedgerEntryHash({ ...base, type: 'control' })).resolves.not.toBe(original);
    await expect(computeLedgerEntryHash({ ...base, payload: { experimentId: 'exp-2' } })).resolves.not.toBe(
      original,
    );
    await expect(computeLedgerEntryHash({ ...base, createdAt: '2026-08-28T01:00:01.000Z' })).resolves.not.toBe(
      original,
    );
    await expect(computeLedgerEntryHash({ ...base, prevHash: '1'.repeat(64) })).resolves.not.toBe(original);
  });
});
