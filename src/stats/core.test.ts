import { describe, expect, it } from 'vitest';
import { countHits, cumulativeDeviation, decodeBits, summarizeBitstream, zScore } from './core';

describe('stats core', () => {
  it('decodes hexadecimal bits deterministically MSB-first', () => {
    expect(Array.from(decodeBits('a5', 8))).toEqual([1, 0, 1, 0, 0, 1, 0, 1]);
    expect(Array.from(decodeBits('A5', 8))).toEqual([1, 0, 1, 0, 0, 1, 0, 1]);
  });

  it('counts target hits for HIGH and LOW without changing the raw bitstream', () => {
    expect(countHits('f1', 8, 1)).toBe(5);
    expect(countHits('f1', 8, 0)).toBe(3);
  });

  it('matches frozen z-score examples', () => {
    expect(zScore(512, 1024)).toBe(0);
    expect(zScore(544, 1024)).toBe(2);
    expect(zScore(480, 1024)).toBe(-2);
  });

  it('computes cumulative deviation from chance expectation', () => {
    expect(cumulativeDeviation(544, 1024)).toBe(32);
    expect(cumulativeDeviation(480, 1024)).toBe(-32);
  });

  it('summarizes a session with one frozen calculation path', () => {
    expect(summarizeBitstream('ff00', 16, 1)).toEqual({
      nBits: 16,
      hits: 8,
      z: 0,
      cumulativeDeviation: 0,
    });
    expect(summarizeBitstream('ffff', 16, 0)).toEqual({
      nBits: 16,
      hits: 0,
      z: -4,
      cumulativeDeviation: -8,
    });
  });

  it.each([
    ['', 8, 'length'],
    ['0', 8, 'length'],
    ['0000', 8, 'length'],
    ['0g', 8, 'hexadecimal'],
  ] as const)('rejects malformed bitsHex %j', (bitsHex, nBits, message) => {
    expect(() => decodeBits(bitsHex, nBits)).toThrow(message);
  });

  it.each([0, -8, 7, 12, 8.5])('rejects invalid nBits=%s', (nBits) => {
    expect(() => decodeBits('00', nBits)).toThrow('positive multiple of 8');
  });

  it.each([-1, 9, 1.5])('rejects invalid hits=%s for nBits=8', (hits) => {
    expect(() => zScore(hits, 8)).toThrow('hits must be an integer');
  });

  it('rejects an invalid target value at runtime', () => {
    expect(() => countHits('00', 8, 2 as 0 | 1)).toThrow('target must be 0 or 1');
  });
});
