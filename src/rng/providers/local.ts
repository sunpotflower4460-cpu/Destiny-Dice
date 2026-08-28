import type { RngProvider } from '../types';

export type RandomFill = (array: Uint8Array<ArrayBuffer>) => void;

export class LocalCryptoRngProvider implements RngProvider {
  readonly source = 'local' as const;
  private readonly fillRandomValues: RandomFill;

  constructor(fillRandomValues?: RandomFill) {
    if (fillRandomValues) {
      this.fillRandomValues = fillRandomValues;
      return;
    }

    if (!globalThis.crypto?.getRandomValues) {
      throw new Error('crypto.getRandomValues is unavailable');
    }

    this.fillRandomValues = (array) => {
      globalThis.crypto.getRandomValues(array);
    };
  }

  async getBytes(byteLength: number): Promise<Uint8Array> {
    if (!Number.isInteger(byteLength) || byteLength <= 0) {
      throw new RangeError('local byteLength must be a positive integer');
    }

    // WebCrypto getRandomValues accepts at most 65,536 bytes per call.
    // Allocate each chunk independently so TypeScript and WebCrypto both know
    // the backing store is ArrayBuffer rather than the broader ArrayBufferLike.
    const output = new Uint8Array(byteLength);
    const maxChunk = 65_536;
    for (let offset = 0; offset < output.length; offset += maxChunk) {
      const chunk = new Uint8Array(Math.min(maxChunk, output.length - offset));
      this.fillRandomValues(chunk);
      output.set(chunk, offset);
    }
    return output;
  }
}
