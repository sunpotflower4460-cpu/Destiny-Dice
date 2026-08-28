import { describe, expect, it } from 'vitest';
import { LEDGER_ENTRY_TYPES } from '../db/schema';
import { MemoryLedgerStore } from './memoryStore';
import { LedgerService } from './service';
import { GENESIS_PREV_HASH } from './types';

describe('LedgerService', () => {
  it('supports all eight ledger entry types and stores canonical payload JSON', async () => {
    const store = new MemoryLedgerStore();
    const service = new LedgerService(store);

    for (const [index, type] of LEDGER_ENTRY_TYPES.entries()) {
      await service.append(type, { z: index, a: type }, `2026-08-28T01:00:0${index}.000Z`);
    }

    const entries = await service.list();
    expect(entries.map((entry) => entry.type)).toEqual([...LEDGER_ENTRY_TYPES]);
    expect(entries[0]?.prevHash).toBe(GENESIS_PREV_HASH);
    expect(entries[0]?.payloadJson).toBe('{"a":"registration","z":0}');
    for (let index = 1; index < entries.length; index += 1) {
      expect(entries[index]?.prevHash).toBe(entries[index - 1]?.entryHash);
    }
  });

  it('requires registration as genesis and forbids a second registration', async () => {
    const service = new LedgerService(new MemoryLedgerStore());

    await expect(service.append('control', { date: '2026-08-28' }, '2026-08-28T01:00:00.000Z')).rejects.toThrow(
      'first ledger entry must be registration',
    );
    await service.append('registration', { experimentId: 'exp-1' }, '2026-08-28T01:00:01.000Z');
    await expect(
      service.append('registration', { experimentId: 'exp-2' }, '2026-08-28T01:00:02.000Z'),
    ).rejects.toThrow('only one genesis registration');

    await expect(service.append('control', { date: '2026-08-28' }, '2026-08-28T01:00:03.000Z')).resolves.toMatchObject({
      seq: 2,
      type: 'control',
    });
  });

  it('serializes concurrent appends so they cannot fork the chain head', async () => {
    const store = new MemoryLedgerStore();
    const service = new LedgerService(store);
    await service.append('registration', { experimentId: 'exp-1' }, '2026-08-28T01:00:00.000Z');

    const appended = await Promise.all([
      service.append('control', { order: 1 }, '2026-08-28T01:00:01.000Z'),
      service.append('prediction', { order: 2 }, '2026-08-28T01:00:02.000Z'),
      service.append('session', { order: 3 }, '2026-08-28T01:00:03.000Z'),
    ]);

    expect(appended.map((entry) => entry.seq)).toEqual([2, 3, 4]);
    const entries = await store.list();
    expect(entries[2]?.prevHash).toBe(entries[1]?.entryHash);
    expect(entries[3]?.prevHash).toBe(entries[2]?.entryHash);
  });
});
