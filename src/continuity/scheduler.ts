import { MAX_PENDING_LOCAL_NOTIFICATIONS, type PlannedNotification } from './notifications';

export const MANAGED_NOTIFICATION_ID_MIN = 10_000;
export const MANAGED_NOTIFICATION_ID_MAX = MANAGED_NOTIFICATION_ID_MIN + MAX_PENDING_LOCAL_NOTIFICATIONS - 1;

export type NotificationPermissionState = 'prompt' | 'prompt-with-rationale' | 'granted' | 'denied';

export type LocalNotificationsPort = {
  checkPermissions(): Promise<{ display: NotificationPermissionState }>;
  requestPermissions(): Promise<{ display: NotificationPermissionState }>;
  getPending(): Promise<{ notifications: Array<{ id: number }> }>;
  cancel(options: { notifications: Array<{ id: number }> }): Promise<void>;
  schedule(options: {
    notifications: Array<{
      id: number;
      title: string;
      body: string;
      schedule: { at: Date };
      extra: PlannedNotification['extra'];
    }>;
  }): Promise<{ notifications: Array<{ id: number }> }>;
};

export type NotificationSyncResult = {
  permission: NotificationPermissionState;
  cancelled: number;
  scheduled: number;
};

function managedPendingIds(pending: readonly { id: number }[]): Array<{ id: number }> {
  return pending.filter(
    ({ id }) => Number.isInteger(id) && id >= MANAGED_NOTIFICATION_ID_MIN && id <= MANAGED_NOTIFICATION_ID_MAX,
  );
}

async function replaceManagedNotifications(
  port: LocalNotificationsPort,
  plans: readonly PlannedNotification[],
  permission: NotificationPermissionState,
): Promise<NotificationSyncResult> {
  if (permission !== 'granted') {
    return { permission, cancelled: 0, scheduled: 0 };
  }

  const pending = await port.getPending();
  const managed = managedPendingIds(pending.notifications);
  if (managed.length > 0) {
    await port.cancel({ notifications: managed });
  }

  if (plans.length > 0) {
    await port.schedule({
      notifications: plans.map((plan) => ({
        id: plan.id,
        title: plan.title,
        body: plan.body,
        schedule: { at: new Date(plan.at) },
        extra: plan.extra,
      })),
    });
  }

  return { permission, cancelled: managed.length, scheduled: plans.length };
}

/** Refreshes only Intention Dice's reserved notification ID range. */
export async function syncPlannedNotifications(
  port: LocalNotificationsPort,
  plans: readonly PlannedNotification[],
): Promise<NotificationSyncResult> {
  const permission = await port.checkPermissions();
  return replaceManagedNotifications(port, plans, permission.display);
}

/** Permission is requested only after an explicit user action. */
export async function requestPermissionAndSync(
  port: LocalNotificationsPort,
  plans: readonly PlannedNotification[],
): Promise<NotificationSyncResult> {
  const permission = await port.requestPermissions();
  return replaceManagedNotifications(port, plans, permission.display);
}
