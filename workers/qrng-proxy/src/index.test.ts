import { describe, expect, it } from 'vitest';
import { createWorker, type WorkerEnv } from './index';

const GENESIS = 'a'.repeat(64);
const HEAD = 'b'.repeat(64);
type AnchorKv = WorkerEnv['ANCHOR_LOG'];

class MemoryKv implements AnchorKv {
  private readonly data = new Map<string, string>();
  async get(key: string): Promise<string | null> {
    return this.data.get(key) ?? null;
  }
  async put(key: string, value: string): Promise<void> {
    this.data.set(key, value);
  }
  async list({ prefix }: { prefix: string }): Promise<{ keys: { name: string }[] }> {
    return { keys: [...this.data.keys()].filter((key) => key.startsWith(prefix)).map((name) => ({ name })) };
  }
}

function env(): WorkerEnv {
  return { ANCHOR_LOG: new MemoryKv() };
}

describe('qrng-proxy public anchor log', () => {
  it('stores and publicly returns a server-timestamped anchor', async () => {
    const worker = createWorker(() => '2026-08-28T12:00:00.000Z');
    const bindings = env();
    const response = await worker.fetch(new Request('https://worker.example/anchors', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ genesisHash: GENESIS, headHash: HEAD, headSeq: 42, protocolVersion: '2.1' }),
    }), bindings);
    expect(response.status).toBe(201);

    const listed = await worker.fetch(new Request(`https://worker.example/anchors/${GENESIS}`), bindings);
    const body = await listed.json() as { anchors: unknown[]; description: string };
    expect(body.anchors).toEqual([{
      genesisHash: GENESIS,
      headHash: HEAD,
      headSeq: 42,
      protocolVersion: '2.1',
      receivedAt: '2026-08-28T12:00:00.000Z',
    }]);
    expect(body.description).toContain('do not make a local ledger tamper-proof');
  });

  it('is idempotent for the same genesis/head tuple', async () => {
    let calls = 0;
    const worker = createWorker(() => `2026-08-28T12:00:0${calls++}.000Z`);
    const bindings = env();
    const request = () => new Request('https://worker.example/anchors', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ genesisHash: GENESIS, headHash: HEAD, headSeq: 42, protocolVersion: '2.1' }),
    });
    expect((await worker.fetch(request(), bindings)).status).toBe(201);
    const duplicate = await worker.fetch(request(), bindings);
    expect(duplicate.status).toBe(200);
    const body = await duplicate.json() as { idempotent: boolean; anchor: { receivedAt: string } };
    expect(body.idempotent).toBe(true);
    expect(body.anchor.receivedAt).toBe('2026-08-28T12:00:00.000Z');
  });

  it('rejects malformed hashes and any unexpected privacy-bearing fields', async () => {
    const worker = createWorker(() => '2026-08-28T12:00:00.000Z');
    const bindings = env();
    const badHash = await worker.fetch(new Request('https://worker.example/anchors', {
      method: 'POST',
      body: JSON.stringify({ genesisHash: 'bad', headHash: HEAD, headSeq: 1, protocolVersion: '2.1' }),
    }), bindings);
    expect(badHash.status).toBe(400);

    const leakedText = await worker.fetch(new Request('https://worker.example/anchors', {
      method: 'POST',
      body: JSON.stringify({
        genesisHash: GENESIS,
        headHash: HEAD,
        headSeq: 1,
        protocolVersion: '2.1',
        wishText: 'must never leave the device',
      }),
    }), bindings);
    expect(leakedText.status).toBe(400);
  });
});
