export type Sha256Digest = (bytes: Uint8Array<ArrayBuffer>) => Promise<Uint8Array<ArrayBuffer>>;

export async function webCryptoSha256(bytes: Uint8Array<ArrayBuffer>): Promise<Uint8Array<ArrayBuffer>> {
  const digest = await crypto.subtle.digest('SHA-256', bytes.buffer);
  return new Uint8Array(digest);
}

export class Sha256CounterStream {
  private readonly domain: string;
  private readonly seed: string;
  private readonly digest: Sha256Digest;
  private readonly encoder = new TextEncoder();
  private counter = 0;
  private block: Uint8Array<ArrayBuffer> = new Uint8Array(0);
  private byteIndex = 0;
  private bitByte: number | null = null;
  private bitIndex = 8;

  constructor(domain: string, seed: string, digest: Sha256Digest = webCryptoSha256) {
    if (!domain) throw new TypeError('domain must be non-empty');
    if (!seed) throw new TypeError('seed must be non-empty');
    this.domain = domain;
    this.seed = seed;
    this.digest = digest;
  }

  async nextByte(): Promise<number> {
    if (this.byteIndex >= this.block.length) {
      const material = new Uint8Array(this.encoder.encode(`${this.domain}:${this.seed}:${this.counter}`));
      this.counter += 1;
      this.block = await this.digest(material);
      if (this.block.length !== 32) {
        throw new Error('SHA-256 digest must be exactly 32 bytes');
      }
      this.byteIndex = 0;
    }
    const value = this.block[this.byteIndex];
    this.byteIndex += 1;
    if (value === undefined) throw new Error('Counter stream byte unavailable');
    return value;
  }

  async nextBoundedInt(maxExclusive: number): Promise<number> {
    if (!Number.isInteger(maxExclusive) || maxExclusive < 1 || maxExclusive > 256) {
      throw new RangeError('maxExclusive must be an integer from 1 through 256');
    }
    const acceptanceLimit = 256 - (256 % maxExclusive);
    while (true) {
      const byte = await this.nextByte();
      if (byte < acceptanceLimit) return byte % maxExclusive;
    }
  }

  async nextBit(): Promise<0 | 1> {
    if (this.bitByte === null || this.bitIndex >= 8) {
      this.bitByte = await this.nextByte();
      this.bitIndex = 0;
    }
    // v1 consumes target bits most-significant-bit first. This ordering is part
    // of targetAlgorithmVersion='sha256-counter-target-v1'.
    const bit = ((this.bitByte >> (7 - this.bitIndex)) & 1) as 0 | 1;
    this.bitIndex += 1;
    return bit;
  }
}
