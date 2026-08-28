import { describe, expect, it } from 'vitest';
import { canonicalizeJcs } from './canonicalize';

describe('canonicalizeJcs', () => {
  it('sorts object keys and uses ECMAScript JSON number serialization', () => {
    expect(
      canonicalizeJcs({
        z: [-0, 4.5, 0.002, 1e30, 1e-27],
        a: { y: true, x: null },
      }),
    ).toBe('{"a":{"x":null,"y":true},"z":[0,4.5,0.002,1e+30,1e-27]}');
  });

  it('preserves JSON string escaping without whitespace', () => {
    expect(canonicalizeJcs({ text: 'line1\n"line2"\\' })).toBe('{"text":"line1\\n\\"line2\\"\\\\"}');
  });

  it('rejects values outside the RFC 8785 JSON data model', () => {
    expect(() => canonicalizeJcs({ bad: Number.NaN })).toThrow('NaN or Infinity');
    expect(() => canonicalizeJcs({ bad: undefined })).toThrow('Unsupported JSON value type');
    expect(() => canonicalizeJcs({ bad: String.fromCharCode(0xd800) })).toThrow('unpaired high surrogate');
  });

  it('rejects cycles instead of silently producing unstable output', () => {
    const cyclic: Record<string, unknown> = {};
    cyclic.self = cyclic;
    expect(() => canonicalizeJcs(cyclic)).toThrow('cyclic');
  });
});
