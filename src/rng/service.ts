import type { AssignmentBit, RandomBits, RngAttemptFailure, RngProvider } from './types';
import { RngExhaustedError } from './types';

function bytesToHex(bytes: Uint8Array): string {
  return Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
}

function validateByteLength(byteLength: number): void {
  if (!Number.isInteger(byteLength) || byteLength <= 0) {
    throw new RangeError('byteLength must be a positive integer');
  }
}

export class RngService {
  private readonly providers: readonly RngProvider[];

  constructor(providers: readonly RngProvider[]) {
    if (providers.length === 0) {
      throw new Error('At least one RNG provider is required');
    }
    this.providers = providers;
  }

  async getBits(nBits: number): Promise<RandomBits> {
    if (!Number.isInteger(nBits) || nBits <= 0 || nBits % 8 !== 0) {
      throw new RangeError('nBits must be a positive multiple of 8');
    }

    const { bytes, provider } = await this.acquireBytes(nBits / 8);
    return {
      bitsHex: bytesToHex(bytes),
      nBits,
      source: provider.source,
    };
  }

  /**
   * Returns one unbiased bit for Layer C assignment.
   * Providers are byte-oriented, so parity of one uniform byte is used; exactly
   * 128 byte values map to each arm.
   */
  async getAssignmentBit(): Promise<AssignmentBit> {
    const { bytes, provider } = await this.acquireBytes(1);
    return {
      bit: (bytes[0]! & 1) as 0 | 1,
      source: provider.source,
    };
  }

  private async acquireBytes(byteLength: number): Promise<{ bytes: Uint8Array; provider: RngProvider }> {
    validateByteLength(byteLength);
    const failures: RngAttemptFailure[] = [];

    for (const provider of this.providers) {
      try {
        const bytes = await provider.getBytes(byteLength);
        if (!(bytes instanceof Uint8Array)) {
          throw new TypeError(`${provider.source} returned a non-Uint8Array payload`);
        }
        if (bytes.byteLength !== byteLength) {
          throw new Error(`${provider.source} returned ${bytes.byteLength} bytes; expected ${byteLength}`);
        }
        return { bytes, provider };
      } catch (error) {
        failures.push({
          source: provider.source,
          error: error instanceof Error ? error.message : String(error),
        });
      }
    }

    throw new RngExhaustedError(failures);
  }
}
