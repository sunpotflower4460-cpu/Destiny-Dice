import { describe, expect, it } from 'vitest';
import { buildRitualRecord } from './ritual';

describe('P4 ritual compliance', () => {
  it('requires 60 seconds for P1 pull-only', () => {
    expect(buildRitualRecord(0, { seconds: 59 }).valid).toBe(false);
    expect(buildRitualRecord(0, { seconds: 60 }).valid).toBe(true);
  });

  it('requires at least 30 characters for P2 intention writing', () => {
    const short = 'あ'.repeat(29);
    const enough = 'あ'.repeat(30);
    expect(buildRitualRecord(1, { seconds: 20, text: short })).toMatchObject({
      kind: 'intention_writing',
      textLen: 29,
      valid: false,
    });
    expect(buildRitualRecord(1, { seconds: 20, text: enough })).toMatchObject({
      textLen: 30,
      valid: true,
    });
  });

  it('requires timer completion for P3 and P4', () => {
    expect(buildRitualRecord(2, { seconds: 299 }).valid).toBe(false);
    expect(buildRitualRecord(2, { seconds: 300 }).valid).toBe(true);
    expect(buildRitualRecord(3, { seconds: 179 }).valid).toBe(false);
    expect(buildRitualRecord(3, { seconds: 180 }).valid).toBe(true);
  });

  it('requires P2 text plus the P3/P4 timer duration for P5', () => {
    expect(buildRitualRecord(4, { seconds: 480, text: 'あ'.repeat(29) }).valid).toBe(false);
    expect(buildRitualRecord(4, { seconds: 479, text: 'あ'.repeat(30) }).valid).toBe(false);
    expect(buildRitualRecord(4, { seconds: 480, text: 'あ'.repeat(30) }).valid).toBe(true);
  });

  it('rejects invalid elapsed seconds rather than repairing them', () => {
    expect(() => buildRitualRecord(0, { seconds: -1 })).toThrow('finite non-negative');
    expect(() => buildRitualRecord(0, { seconds: Number.NaN })).toThrow('finite non-negative');
  });
});
