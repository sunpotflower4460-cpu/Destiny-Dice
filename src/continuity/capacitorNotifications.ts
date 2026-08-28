import { LocalNotifications } from '@capacitor/local-notifications';
import type { LocalNotificationsPort } from './scheduler';

/** Native/web Capacitor adapter kept outside the pure scheduling logic. */
export const capacitorLocalNotificationsPort = LocalNotifications as unknown as LocalNotificationsPort;
