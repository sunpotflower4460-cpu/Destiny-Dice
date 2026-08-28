import { describe, expect, it } from 'vitest';
import type { PlannedNotification } from './notifications';
import {
  requestPermissionAndSync,
  syncPlannedNotifications,
  type LocalNotificationsPort,
  type NotificationPermissionState,
} from './scheduler';

function plan(id: number): PlannedNotification {
  return {
    id,
    kind: 'daily_reminder',
    at: '2026-09-01T11:00:00.000Z',
    title: 'today',
    body: 'body',
    extra: { kind: 'daily_reminder' },
  };
}

class FakeNotifications implements LocalNotificationsPort {
  permission: NotificationPermissionState = 'prompt';
  pending = [{ id: 3 }, { id: 10_000 }, { id: 10_004 }, { id: 90_000 }];
  cancelled: number[] = [];
  scheduled: number[] = [];
  requestCalls = 0;

  async checkPermissions() {
    return { display: this.permission };
  }

  async requestPermissions() {
    this.requestCalls += 1;
    this.permission = 'granted';
    return { display: this.permission };
  }

  async getPending() {
    return { notifications: this.pending };
  }

  async cancel(options: { notifications: Array<{ id: number }> }) {
    this.cancelled.push(...options.notifications.map((item) => item.id));
  }

  async schedule(options: { notifications: Array<{ id: number }> }) {
    this.scheduled.push(...options.notifications.map((item) => item.id));
    return { notifications: options.notifications };
  }
}

describe('P9 notification synchronization', () => {
  it('does not request permission or touch pending notifications implicitly', async () => {
    const notifications = new FakeNotifications();
    const result = await syncPlannedNotifications(notifications, [plan(10_000)]);
    expect(result).toEqual({ permission: 'prompt', cancelled: 0, scheduled: 0 });
    expect(notifications.requestCalls).toBe(0);
    expect(notifications.cancelled).toEqual([]);
    expect(notifications.scheduled).toEqual([]);
  });

  it('cancels only the reserved Intention Dice IDs before replacing the plan', async () => {
    const notifications = new FakeNotifications();
    notifications.permission = 'granted';
    const result = await syncPlannedNotifications(notifications, [plan(10_000), plan(10_001)]);
    expect(result).toEqual({ permission: 'granted', cancelled: 2, scheduled: 2 });
    expect(notifications.cancelled).toEqual([10_000, 10_004]);
    expect(notifications.scheduled).toEqual([10_000, 10_001]);
  });

  it('requests permission only through the explicit request path and then schedules', async () => {
    const notifications = new FakeNotifications();
    const result = await requestPermissionAndSync(notifications, [plan(10_000)]);
    expect(notifications.requestCalls).toBe(1);
    expect(result.permission).toBe('granted');
    expect(notifications.scheduled).toEqual([10_000]);
  });
});
