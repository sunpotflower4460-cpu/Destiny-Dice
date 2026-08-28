import type { RngProvider } from '../types';

export type CryptoLike = {
  getRandomValues<T extends ArrayBufferView>(array: T): T;
};

export class LocalCryptoRngProvider implements RngProvider {
  readonly source = 'local' as const;

  constructor(private readonly cryptoApi: CryptoLike = globalThis.crypto) {
    if (!cryptoApi?.getRandomValues) {
      throw new Error('crypto.getRandomValues is unavailable');
    }
  }

  async getBytes(byteLength: number): Promise<Uint8Array> {
    if (!Number.isInteger(byteLength) || byteLength <= 0) {
      throw new RangeError('local byteLength must be a positive integer');
    }

    // WebCrypto getRandomValues accepts at most 65,536 bytes per call.
    const output = new Uint8Array(byteLength);
    const maxChunk = 65_536;
    for (let offset = 0; offset < output.length; offset += maxChunk) {
      this.cryptoApi.getRandomValues(output.subarray(offset, Math.min(offset + maxChunk, output.length)));
    }
    return output;
  }
}
