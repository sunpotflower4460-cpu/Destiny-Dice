import { describe, expect, it, vi } from 'vitest';
import { RandomOrgRngProvider } from './randomOrg';

describe('RandomOrgRngProvider', () => {
  it('requests byte-range integers using the official plain HTTP shape', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response('0\n127\n255\n', { status: 200 }),
    );
    const provider = new RandomOrgRngProvider({
      endpoint: 'https://example.test/random',
      fetchFn,
    });

    await expect(provider.getBytes(3)).resolves.toEqual(Uint8Array.from([0, 127, 255]));
    expect(fetchFn).toHaveBeenCalledTimes(1);
    const requestUrl = String(fetchFn.mock.calls[0]![0]);
    expect(requestUrl).toContain('num=3');
    expect(requestUrl).toContain('min=0');
    expect(requestUrl).toContain('max=255');
    expect(requestUrl).toContain('format=plain');
    expect(requestUrl).toContain('rnd=new');
  });

  it('rejects explicit RANDOM.ORG error payloads', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response('Error: quota exceeded\n', { status: 200 }),
    );
    const provider = new RandomOrgRngProvider({ endpoint: 'https://example.test/random', fetchFn });

    await expect(provider.getBytes(1)).rejects.toThrow('error response');
  });

  it('rejects the wrong number of returned bytes', async () => {
    const fetchFn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) =>
      new Response('1\n2\n', { status: 200 }),
    );
    const provider = new RandomOrgRngProvider({ endpoint: 'https://example.test/random', fetchFn });

    await expect(provider.getBytes(3)).rejects.toThrow('exactly 3 integers');
  });
});
