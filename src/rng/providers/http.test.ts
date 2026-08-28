import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchWithTimeout, type FetchLike } from './http';

afterEach(() => {
  vi.useRealTimers();
});

describe('fetchWithTimeout', () => {
  it('aborts a stalled provider request at the configured timeout', async () => {
    vi.useFakeTimers();

    const fetchFn: FetchLike = vi.fn(async (_input, init) =>
      new Promise<Response>((_resolve, reject) => {
        init?.signal?.addEventListener('abort', () => {
          reject(new DOMException('aborted', 'AbortError'));
        });
      }),
    );

    const pending = fetchWithTimeout(fetchFn, 'https://example.test/stalled', {}, 250);
    await vi.advanceTimersByTimeAsync(250);

    await expect(pending).rejects.toMatchObject({ name: 'AbortError' });
  });
});
