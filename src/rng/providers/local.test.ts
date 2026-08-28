import { describe, expect, it } from 'vitest';
import { LocalCryptoRngProvider, type RandomFill } from './local';

describe('LocalCryptoRngProvider', () => {
  it('uses an injected random fill function and remains labeled local', async () => {
    let next = 1;
    const fillRandomValues: RandomFill = (bytes) => {
      for (let index = 0; index < bytes.length; index += 1) {
        bytes[index] = next;
        next += 1;
      }
    };
    const provider = new LocalCryptoRngProvider(fillRandomValues);

    expect(provider.source).toBe('local');
    await expect(provider.getBytes(4)).resolves.toEqual(Uint8Array.from([1, 2, 3, 4]));
  });
});
