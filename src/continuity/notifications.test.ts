import { describe, expect, it } from 'vitest';
import { planLocalNotifications, type NotificationRegistration } from './notifications';

const registration = {
  startDate: '2026-09-01',
  days: 365,
  timeZone: 'Asia/Tokyo',
  dayBoundaryHour: 3,
} satisfies NotificationRegistration;

describe('P9 local notification scheduling', () => {
  it('schedules deadline notifications at the frozen experiment-day boundary and never includes wish text', () => {
    const plans = planLocalNotifications({
      registration,
      now: '2026-09-01T00:00:00.000Z',
      wishes: [
        { wishId: 'sealed-secret', deadline: '2026-09-02', assigned: true, judged: false },
        { wishId: 'already-done', deadline: '2026-09-02', assigned: true, judged: true },
        { wishId: 'unassigned', deadline: '2026-09-02', assigned: false, judged: false },
      ],
    });

    expect(plans).toHaveLength(1);
    expect(plans[0]).toMatchObject({
      kind: 'wish_deadline',
      at: '2026-09-01T18:00:00.000Z',
      extra: { kind: 'wish_deadline', wishId: 'sealed-secret' },
    });
    expect(JSON.stringify(plans)).not.toContain('secret wish text');
  });

  it('maps a user-selected daily reminder time into the frozen timezone for each experiment day', () => {
    const plans = planLocalNotifications({
      registration,
      now: '2026-09-01T00:00:00.000Z',
      wishes: [],
      dailyReminderTime: '20:00',
      maxPending: 3,
    });

    expect(plans.map((plan) => plan.at)).toEqual([
      '2026-09-01T11:00:00.000Z',
      '2026-09-02T11:00:00.000Z',
      '2026-09-03T11:00:00.000Z',
    ]);
    expect(plans.every((plan) => plan.kind === 'daily_reminder')).toBe(true);
  });

  it('places reminder times before the boundary in the following calendar morning of the same experiment day', () => {
    const plans = planLocalNotifications({
      registration,
      now: '2026-09-01T00:00:00.000Z',
      wishes: [],
      dailyReminderTime: '01:00',
      maxPending: 1,
    });
    expect(plans[0]?.at).toBe('2026-09-01T16:00:00.000Z');
  });

  it('keeps only the nearest platform-safe pending notifications and prioritizes a deadline at equal time', () => {
    const plans = planLocalNotifications({
      registration,
      now: '2026-08-31T00:00:00.000Z',
      wishes: [{ wishId: 'due', deadline: '2026-09-01', assigned: true, judged: false }],
      dailyReminderTime: '03:00',
      maxPending: 2,
    });
    expect(plans).toHaveLength(2);
    expect(plans[0]?.kind).toBe('wish_deadline');
    expect(plans[1]?.kind).toBe('daily_reminder');
    expect(plans[0]?.at).toBe(plans[1]?.at);
  });
});
