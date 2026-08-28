import { describe, expect, it } from 'vitest';
import { SeededTestRngProvider } from './seeded';

describe('SeededTestRngProvider', () => {
  it('is deterministic for the same seed and request sequence', async () => {
    const a = new SeededTestRngProvider('destiny-dice-test-seed');
    const b = new SeededTestRngProvider('destiny-dice-test-seed');

    const a1 = await a.getBytes(16);
    const a2 = await a.getBytes(8);
    const b1 = await b.getBytes(16);
    const b2 = await b.getBytes(8);

    expect(a1).toEqual(b1);
    expect(a2).toEqual(b2);
  });

  it('does not expose itself as ANU or RANDOM.ORG', () => {
    const provider = new SeededTestRngProvider('seed');
    expect(provider.source).toBe('local');
  });
});
