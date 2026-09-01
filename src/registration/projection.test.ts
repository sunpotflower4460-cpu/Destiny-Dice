import { describe, expect, it } from 'vitest';
import { LedgerService } from '../ledger/service';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { projectCurrentSchedule } from './projection';
import { RegistrationService } from './service';
import type { RegistrationInput, RegistrationPayload } from './types';

const input: RegistrationInput = {
  experimentId: 'exp-visibility',
  startDate: '2026-09-01',
  bitsPerDraw: 2048,
  sessionsPerDay: 2,
  dayBoundaryHour: 3,
  affirmationText: '今日の意図を丁寧に保つ',
  predictionByCondition: ['p1', 'p2', 'p3', 'p4', 'p5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'schedule-visibility',
  targetSeed: 'target-visibility',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

describe('projectCurrentSchedule', () => {
  it('returns only the requested current experiment day, never the full future schedules', async () => {
    const registration = await new RegistrationService(new LedgerService(new MemoryLedgerStore())).register(
      input,
      '2026-08-28T03:00:00.000Z',
    );

    const projection = await projectCurrentSchedule(registration.payload, '2026-09-02');
    expect(projection).toEqual({
      experimentDate: '2026-09-02',
      dayIndex: 1,
      condition: registration.payload.schedule[1],
      targets: expect.arrayContaining([expect.any(Number)]),
    });
    expect(projection?.targets).toHaveLength(2);
    expect(projection).not.toHaveProperty('schedule');
    expect(projection).not.toHaveProperty('targetSchedule');
  });

  it('refuses to regenerate targets when frozen targetAlgorithmVersion does not match', async () => {
    const registration = await new RegistrationService(new LedgerService(new MemoryLedgerStore())).register(
      input,
      '2026-08-28T03:00:00.000Z',
    );
    const payload = {
      ...registration.payload,
      targetAlgorithmVersion: 'not-sha256-counter-target-v1',
    } as unknown as RegistrationPayload;

    await expect(projectCurrentSchedule(payload, '2026-09-01')).rejects.toThrow('unsupported targetAlgorithmVersion');
  });

  it('returns null before start and after the frozen experiment window', async () => {
    const registration = await new RegistrationService(new LedgerService(new MemoryLedgerStore())).register(
      input,
      '2026-08-28T03:00:00.000Z',
    );
    await expect(projectCurrentSchedule(registration.payload, '2026-08-31')).resolves.toBeNull();
    await expect(projectCurrentSchedule(registration.payload, '2027-09-01')).resolves.toBeNull();
  });
});
