import type { RegistrationPayload } from '../registration/types';
import { addCalendarDays, parseClockTime, resolveExperimentDate, zonedDateTimeToInstant } from './time';

export const MAX_PENDING_LOCAL_NOTIFICATIONS = 64;

export type NotificationRegistration = Pick<
  RegistrationPayload,
  'startDate' | 'days' | 'timeZone' | 'dayBoundaryHour'
>;

export type WishDeadlineCandidate = {
  wishId: string;
  deadline: string;
  assigned: boolean;
  judged: boolean;
};

export type PlannedNotification = {
  id: number;
  kind: 'daily_reminder' | 'wish_deadline';
  at: string;
  title: string;
  body: string;
  extra: { kind: 'daily_reminder' } | { kind: 'wish_deadline'; wishId: string };
};

export type NotificationScheduleInput = {
  registration: NotificationRegistration;
  now: string | Date;
  wishes: readonly WishDeadlineCandidate[];
  dailyReminderTime?: string;
  maxPending?: number;
};

function experimentEndDate(registration: NotificationRegistration): string {
  if (!Number.isInteger(registration.days) || registration.days <= 0) {
    throw new RangeError('registration days must be a positive integer');
  }
  return addCalendarDays(registration.startDate, registration.days - 1);
}

function reminderInstantForExperimentDate(
  experimentDate: string,
  reminderTime: string,
  registration: NotificationRegistration,
): string {
  const { hour } = parseClockTime(reminderTime);
  const calendarDate = hour < registration.dayBoundaryHour ? addCalendarDays(experimentDate, 1) : experimentDate;
  return zonedDateTimeToInstant(calendarDate, reminderTime, registration.timeZone);
}

function deadlineInstant(deadline: string, registration: NotificationRegistration): string {
  return zonedDateTimeToInstant(
    deadline,
    `${String(registration.dayBoundaryHour).padStart(2, '0')}:00`,
    registration.timeZone,
  );
}

export function planLocalNotifications(input: NotificationScheduleInput): PlannedNotification[] {
  const maxPending = input.maxPending ?? MAX_PENDING_LOCAL_NOTIFICATIONS;
  if (!Number.isInteger(maxPending) || maxPending <= 0) throw new RangeError('maxPending must be a positive integer');
  const now = input.now instanceof Date ? input.now : new Date(input.now);
  if (!Number.isFinite(now.getTime())) throw new RangeError('now must be a valid timestamp');

  const { registration } = input;
  const currentExperimentDate = resolveExperimentDate(now, registration.timeZone, registration.dayBoundaryHour);
  const endDate = experimentEndDate(registration);
  const candidates: Omit<PlannedNotification, 'id'>[] = [];

  if (input.dailyReminderTime !== undefined && input.dailyReminderTime.length > 0) {
    parseClockTime(input.dailyReminderTime);
    let date = currentExperimentDate < registration.startDate ? registration.startDate : currentExperimentDate;
    while (date <= endDate) {
      const at = reminderInstantForExperimentDate(date, input.dailyReminderTime, registration);
      if (Date.parse(at) > now.getTime()) {
        candidates.push({
          kind: 'daily_reminder',
          at,
          title: '今日の実験',
          body: '2〜10分だけ、今日のセッションを記録しましょう。休んだ日があっても実験失格にはなりません。',
          extra: { kind: 'daily_reminder' },
        });
      }
      date = addCalendarDays(date, 1);
    }
  }

  for (const wish of input.wishes) {
    if (!wish.assigned || wish.judged) continue;
    const at = deadlineInstant(wish.deadline, registration);
    if (Date.parse(at) <= now.getTime()) continue;
    candidates.push({
      kind: 'wish_deadline',
      at,
      title: '願いの締切です',
      body: '締切を迎えた願いがあります。アプリを開いて、実現した／しなかった／判定不能を記録してください。',
      extra: { kind: 'wish_deadline', wishId: wish.wishId },
    });
  }

  candidates.sort((a, b) => {
    const time = Date.parse(a.at) - Date.parse(b.at);
    if (time !== 0) return time;
    if (a.kind !== b.kind) return a.kind === 'wish_deadline' ? -1 : 1;
    const aWish = a.extra.kind === 'wish_deadline' ? a.extra.wishId : '';
    const bWish = b.extra.kind === 'wish_deadline' ? b.extra.wishId : '';
    return aWish.localeCompare(bWish);
  });

  return candidates.slice(0, maxPending).map((candidate, index) => ({ ...candidate, id: 10_000 + index }));
}
