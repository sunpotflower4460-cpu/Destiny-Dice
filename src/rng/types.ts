export const RNG_SOURCES = ['anu', 'randomorg', 'local'] as const;

export type RngSource = (typeof RNG_SOURCES)[number];

export type RandomBits = {
  bitsHex: string;
  nBits: number;
  source: RngSource;
};

export type AssignmentBit = {
  bit: 0 | 1;
  source: RngSource;
};

/**
 * Provider contract is byte-oriented on purpose.
 * Layer A draw sizes (1024/2048/4096 bits) are byte aligned; Layer C's
 * one-bit assignment is derived unbiasedly from one uniform provider byte.
 */
export interface RngProvider {
  readonly source: RngSource;
  getBytes(byteLength: number): Promise<Uint8Array>;
}

export type RngAttemptFailure = {
  source: RngSource;
  error: string;
};

export class RngExhaustedError extends Error {
  readonly attempts: readonly RngAttemptFailure[];

  constructor(attempts: readonly RngAttemptFailure[]) {
    super(`All RNG providers failed: ${attempts.map((attempt) => attempt.source).join(' -> ')}`);
    this.name = 'RngExhaustedError';
    this.attempts = attempts;
  }
}
