export type Bit = 0 | 1;

export type SessionStats = {
  nBits: number;
  hits: number;
  z: number;
  cumulativeDeviation: number;
};

function validateNBits(nBits: number): void {
  if (!Number.isInteger(nBits) || nBits <= 0 || nBits % 8 !== 0) {
    throw new RangeError('nBits must be a positive multiple of 8');
  }
}

function validateBitsHex(bitsHex: string, nBits: number): void {
  validateNBits(nBits);
  if (typeof bitsHex !== 'string' || bitsHex.length !== nBits / 4) {
    throw new RangeError(`bitsHex length must be exactly ${nBits / 4} hex characters for ${nBits} bits`);
  }
  if (!/^[0-9a-fA-F]+$/.test(bitsHex)) {
    throw new TypeError('bitsHex must contain only hexadecimal characters');
  }
}

/**
 * Decode the persisted hexadecimal bitstream into 0/1 values.
 * Within each byte, bits are emitted most-significant-bit first. Hit counts do
 * not depend on ordering; this order is fixed only so sequence-based consumers
 * can reproduce the same stream across Web, Node and iOS.
 */
export function decodeBits(bitsHex: string, nBits: number): Uint8Array {
  validateBitsHex(bitsHex, nBits);
  const bits = new Uint8Array(nBits);
  let bitIndex = 0;

  for (let offset = 0; offset < bitsHex.length; offset += 2) {
    const byte = Number.parseInt(bitsHex.slice(offset, offset + 2), 16);
    for (let shift = 7; shift >= 0; shift -= 1) {
      bits[bitIndex] = (byte >> shift) & 1;
      bitIndex += 1;
    }
  }

  return bits;
}

export function countHits(bitsHex: string, nBits: number, target: Bit): number {
  if (target !== 0 && target !== 1) {
    throw new RangeError('target must be 0 or 1');
  }
  const bits = decodeBits(bitsHex, nBits);
  let ones = 0;
  for (const bit of bits) {
    ones += bit;
  }
  return target === 1 ? ones : nBits - ones;
}

export function cumulativeDeviation(hits: number, nBits: number): number {
  validateNBits(nBits);
  if (!Number.isInteger(hits) || hits < 0 || hits > nBits) {
    throw new RangeError('hits must be an integer from 0 through nBits');
  }
  return hits - nBits / 2;
}

export function zScore(hits: number, nBits: number): number {
  const deviation = cumulativeDeviation(hits, nBits);
  return deviation / Math.sqrt(nBits / 4);
}

export function summarizeBitstream(bitsHex: string, nBits: number, target: Bit): SessionStats {
  const hits = countHits(bitsHex, nBits, target);
  return {
    nBits,
    hits,
    z: zScore(hits, nBits),
    cumulativeDeviation: cumulativeDeviation(hits, nBits),
  };
}
