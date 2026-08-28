import { describe, expect, it } from 'vitest';
import { LocalCryptoRngProvider, type CryptoLike } from './local';

describe('LocalCryptoRngProvider', () => {
  it('uses injected crypto.getRandomValues and remains labeled local', async () => {
    let next = 1;
    const cryptoApi: CryptoLike = {
      getRandomValues<T extends ArrayBufferView>(array: T): T {
        const bytes = new Uint8Array(array.buffer, array.byteOffset, array.byteLength);
        for (let index = 0; index < bytes.length; index += 1) {
          bytes[index] = next;
          next += 1;
        }
        return array;
      },
    };
    const provider = new LocalCryptoRngProvider(cryptoApi);

    expect(provider.source).toBe('local');
    await expect(provider.getBytes(4)).resolves.toEqual(Uint8Array.from([1, 2, 3, 4]));
  });
});
