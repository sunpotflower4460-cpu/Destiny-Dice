import { describe, expect, it } from 'vitest';
import { RateGate } from './rateGate';

describe('RateGate', () => {
  it('enforces a configured legacy-style 70 second guard without hard-coding it', async () => {
    let now = 0;
    const sleeps: number[] = [];
    const gate = new RateGate({
      minIntervalMs: 70_000,
      now: () => now,
      sleep: async (ms) => {
        sleeps.push(ms);
        now += ms;
      },
    });

    await gate.run(async () => 'first');
    now = 1_000;
    await gate.run(async () => 'second');

    expect(sleeps).toEqual([69_000]);
  });

  it('serializes concurrent calls through one provider gate', async () => {
    let now = 0;
    const starts: number[] = [];
    const gate = new RateGate({
      minIntervalMs: 10,
      now: () => now,
      sleep: async (ms) => {
        now += ms;
      },
    });

    await Promise.all([
      gate.run(async () => {
        starts.push(now);
      }),
      gate.run(async () => {
        starts.push(now);
      }),
      gate.run(async () => {
        starts.push(now);
      }),
    ]);

    expect(starts).toEqual([0, 10, 20]);
  });
});
