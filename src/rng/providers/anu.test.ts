import { describe, expect, it, vi } from 'vitest';
import { AnuRngProvider } from './anu';

describe('AnuRngProvider', () => {
  it('requests uint8 bytes from the configured endpoint without real network access', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response(JSON.stringify({ data: [0, 127, 255] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const provider = new AnuRngProvider({
      endpoint: 'https://example.test/anu',
      fetchFn,
    });

    await expect(provider.getBytes(3)).resolves.toEqual(Uint8Array.from([0, 127, 255]));
    expect(fetchFn).toHaveBeenCalledTimes(1);
    const requestUrl = String(fetchFn.mock.calls[0]![0]);
    expect(requestUrl).toContain('length=3');
    expect(requestUrl).toContain('type=uint8');
  });

  it('rejects malformed byte payloads so the service can fall back', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response(JSON.stringify({ data: [0, 999] }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
    const provider = new AnuRngProvider({ endpoint: 'https://example.test/anu', fetchFn });

    await expect(provider.getBytes(2)).rejects.toThrow('invalid byte');
  });

  it('surfaces HTTP failures instead of disguising them as quantum data', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response('unavailable', { status: 503 }),
    );
    const provider = new AnuRngProvider({ endpoint: 'https://example.test/anu', fetchFn });

    await expect(provider.getBytes(1)).rejects.toThrow('HTTP 503');
  });
});
