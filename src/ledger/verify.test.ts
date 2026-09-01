import { describe, expect, it } from 'vitest';
import { LedgerService } from './service';
import { MemoryLedgerStore } from './memoryStore';
import { GENESIS_PREV_HASH, type StoredLedgerEntry } from './types';
import { verifyChain } from './verify';

async function buildFixture(): Promise<StoredLedgerEntry[]> {
  const store = new MemoryLedgerStore();
  const service = new LedgerService(store);
  await service.append(
    'registration',
    { experimentId: 'exp-1', meta: { b: 2, a: 1 } },
    '2026-08-28T01:00:00.000Z',
  );
  await service.append('control', { date: '2026-08-28', hits: 512 }, '2026-08-28T01:01:00.000Z');
  await service.append(
    'prediction',
    { date: '2026-08-28', confidence: 70 },
    '2026-08-28T01:02:00.000Z',
  );
  await service.append(
    'session',
    { date: '2026-08-28', bitsHex: 'aabbccdd', predictionSeq: 3 },
    '2026-08-28T01:03:00.000Z',
  );
  return store.list();
}

function clone(entries: readonly StoredLedgerEntry[]): StoredLedgerEntry[] {
  return entries.map((entry) => ({ ...entry }));
}

describe('verifyChain', () => {
  it('accepts an intact canonical chain', async () => {
    const entries = await buildFixture();
    await expect(verifyChain(entries)).resolves.toEqual({
      ok: true,
      entries: 4,
      headHash: entries[3]!.entryHash,
    });
  });

  it('detects payload mutation', async () => {
    const entries = clone(await buildFixture());
    entries[1]!.payloadJson = '{"date":"tampered","hits":512}';
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'entry_hash_mismatch', seq: 2 });
  });

  it('detects type mutation', async () => {
    const entries = clone(await buildFixture());
    entries[1]!.type = 'wish';
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'entry_hash_mismatch', seq: 2 });
  });

  it('detects createdAt mutation', async () => {
    const entries = clone(await buildFixture());
    entries[1]!.createdAt = '2026-08-28T09:09:09.000Z';
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'entry_hash_mismatch', seq: 2 });
  });

  it('detects prevHash mutation', async () => {
    const entries = clone(await buildFixture());
    entries[1]!.prevHash = 'f'.repeat(64);
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'invalid_prev_hash', seq: 2 });
  });

  it('detects deletion of an interior entry', async () => {
    const entries = clone(await buildFixture());
    entries.splice(1, 1);
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'invalid_seq', seq: 3 });
  });

  it('detects insertion of an unchained entry', async () => {
    const entries = clone(await buildFixture());
    const inserted: StoredLedgerEntry = {
      ...entries[1]!,
      seq: 2,
      prevHash: 'f'.repeat(64),
    };
    entries.splice(1, 0, inserted);
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'invalid_prev_hash', seq: 2 });
  });

  it('detects reordering', async () => {
    const entries = clone(await buildFixture());
    [entries[1], entries[2]] = [entries[2]!, entries[1]!];
    await expect(verifyChain(entries)).resolves.toMatchObject({ ok: false, code: 'invalid_seq', seq: 3 });
  });

  it('detects an incorrect genesis prevHash', async () => {
    const entries = clone(await buildFixture());
    entries[0]!.prevHash = '1'.repeat(64);
    expect(entries[0]!.prevHash).not.toBe(GENESIS_PREV_HASH);
    await expect(verifyChain(entries)).resolves.toMatchObject({
      ok: false,
      code: 'invalid_genesis_prev_hash',
      seq: 1,
    });
  });

  it('detects semantically equivalent but non-canonical stored payload JSON', async () => {
    const entries = clone(await buildFixture());
    entries[1]!.payloadJson = '{"hits":512,"date":"2026-08-28"}';
    await expect(verifyChain(entries)).resolves.toMatchObject({
      ok: false,
      code: 'non_canonical_payload',
      seq: 2,
    });
  });

  it('rejects a hash-valid session whose predictionSeq does not precede the session', async () => {
    const store = new MemoryLedgerStore();
    const service = new LedgerService(store);
    await service.append('registration', { experimentId: 'exp-order' }, '2026-08-28T01:00:00.000Z');
    await service.append('prediction', { date: '2026-08-28' }, '2026-08-28T01:02:00.000Z');
    await service.append(
      'session',
      { date: '2026-08-28', bitsHex: 'aabbccdd', predictionSeq: 3 },
      '2026-08-28T01:03:00.000Z',
    );
    await expect(verifyChain(await store.list())).resolves.toMatchObject({
      ok: false,
      code: 'invalid_prediction_binding',
      seq: 3,
    });
  });

  it('rejects a hash-valid session that does not bind an earlier prediction', async () => {
    const store = new MemoryLedgerStore();
    const service = new LedgerService(store);
    await service.append('registration', { experimentId: 'exp-bind' }, '2026-08-28T01:00:00.000Z');
    await service.append(
      'session',
      { date: '2026-08-28', bitsHex: 'aabbccdd', predictionSeq: 1 },
      '2026-08-28T01:03:00.000Z',
    );
    await expect(verifyChain(await store.list())).resolves.toMatchObject({
      ok: false,
      code: 'invalid_prediction_binding',
      seq: 2,
    });
  });
});
