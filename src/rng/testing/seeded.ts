import type { RngProvider } from '../types';

function seedToState(seed: string): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < seed.length; index += 1) {
    hash ^= seed.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash === 0 ? 0x6d2b79f5 : hash >>> 0;
}

/**
 * Deterministic test-only provider. It deliberately reports source='local'
 * and is not exported from src/rng/index.ts, preventing production code from
 * accidentally presenting seeded bytes as ANU/random.org data.
 */
export class SeededTestRngProvider implements RngProvider {
  readonly source = 'local' as const;
  private state: number;

  constructor(seed: string) {
    this.state = seedToState(seed);
  }

  async getBytes(byteLength: number): Promise<Uint8Array> {
    if (!Number.isInteger(byteLength) || byteLength <= 0) {
      throw new RangeError('seeded byteLength must be a positive integer');
    }

    const bytes = new Uint8Array(byteLength);
    for (let index = 0; index < byteLength; index += 1) {
      let value = this.state;
      value ^= value << 13;
      value ^= value >>> 17;
      value ^= value << 5;
      this.state = value >>> 0;
      bytes[index] = this.state & 0xff;
    }
    return bytes;
  }
}
