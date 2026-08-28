import { describe, expect, it, vi } from 'vitest';
import { RngService } from './service';
import { RngExhaustedError, type RngProvider, type RngSource } from './types';

function fakeProvider(
  source: RngSource,
  implementation: (byteLength: number) => Promise<Uint8Array>,
): RngProvider {
  return {
    source,
    getBytes: vi.fn(implementation),
  };
}

describe('RngService', () => {
  it('uses ANU when the primary provider succeeds', async () => {
    const anu = fakeProvider('anu', async () => Uint8Array.from([0x12, 0xab]));
    const randomOrg = fakeProvider('randomorg', async () => Uint8Array.from([0xff, 0xff]));
    const local = fakeProvider('local', async () => Uint8Array.from([0xee, 0xee]));
    const service = new RngService([anu, randomOrg, local]);

    await expect(service.getBits(16)).resolves.toEqual({
      bitsHex: '12ab',
      nBits: 16,
      source: 'anu',
    });
    expect(randomOrg.getBytes).not.toHaveBeenCalled();
    expect(local.getBytes).not.toHaveBeenCalled();
  });

  it('falls back from ANU to RANDOM.ORG and records the actual source', async () => {
    const anu = fakeProvider('anu', async () => {
      throw new Error('ANU offline');
    });
    const randomOrg = fakeProvider('randomorg', async () => Uint8Array.from([0x01]));
    const local = fakeProvider('local', async () => Uint8Array.from([0x02]));
    const service = new RngService([anu, randomOrg, local]);

    await expect(service.getBits(8)).resolves.toEqual({
      bitsHex: '01',
      nBits: 8,
      source: 'randomorg',
    });
    expect(local.getBytes).not.toHaveBeenCalled();
  });

  it('falls back to local crypto when both external providers fail', async () => {
    const anu = fakeProvider('anu', async () => {
      throw new Error('ANU offline');
    });
    const randomOrg = fakeProvider('randomorg', async () => {
      throw new Error('quota exhausted');
    });
    const local = fakeProvider('local', async () => Uint8Array.from([0xfe]));
    const service = new RngService([anu, randomOrg, local]);

    await expect(service.getBits(8)).resolves.toEqual({
      bitsHex: 'fe',
      nBits: 8,
      source: 'local',
    });
  });

  it('returns one unbiased assignment bit with its actual source', async () => {
    const anu = fakeProvider('anu', async () => {
      throw new Error('ANU offline');
    });
    const randomOrg = fakeProvider('randomorg', async () => Uint8Array.from([0xa5]));
    const service = new RngService([anu, randomOrg]);

    await expect(service.getAssignmentBit()).resolves.toEqual({
      bit: 1,
      source: 'randomorg',
    });
  });

  it('rejects non-byte-aligned Layer A requests', async () => {
    const service = new RngService([fakeProvider('local', async () => Uint8Array.from([0]))]);
    await expect(service.getBits(7)).rejects.toThrow('positive multiple of 8');
  });

  it('fails loudly with every provider attempt preserved', async () => {
    const service = new RngService([
      fakeProvider('anu', async () => {
        throw new Error('a');
      }),
      fakeProvider('randomorg', async () => {
        throw new Error('b');
      }),
      fakeProvider('local', async () => {
        throw new Error('c');
      }),
    ]);

    try {
      await service.getBits(8);
      throw new Error('expected getBits to fail');
    } catch (error) {
      expect(error).toBeInstanceOf(RngExhaustedError);
      const exhausted = error as RngExhaustedError;
      expect(exhausted.attempts).toEqual([
        { source: 'anu', error: 'a' },
        { source: 'randomorg', error: 'b' },
        { source: 'local', error: 'c' },
      ]);
    }
  });
});
