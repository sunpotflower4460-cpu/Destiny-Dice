const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const ISO_TIME = /^(\d{2}):(\d{2})$/;

export type ZonedParts = {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
};

function validateTimeZone(timeZone: string): void {
  if (typeof timeZone !== 'string' || timeZone.trim().length === 0) {
    throw new TypeError('timeZone must be a non-empty IANA timezone');
  }
  try {
    new Intl.DateTimeFormat('en-US', { timeZone }).format(0);
  } catch {
    throw new RangeError(`invalid IANA timezone: ${timeZone}`);
  }
}

function validateBoundaryHour(dayBoundaryHour: number): void {
  if (!Number.isInteger(dayBoundaryHour) || dayBoundaryHour < 0 || dayBoundaryHour > 23) {
    throw new RangeError('dayBoundaryHour must be an integer from 0 through 23');
  }
}

function parseInstant(instant: string | Date): Date {
  const date = instant instanceof Date ? new Date(instant.getTime()) : new Date(instant);
  if (!Number.isFinite(date.getTime())) throw new RangeError('instant must be a valid timestamp');
  return date;
}

function formatter(timeZone: string): Intl.DateTimeFormat {
  validateTimeZone(timeZone);
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    calendar: 'gregory',
    numberingSystem: 'latn',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  });
}

export function zonedParts(instant: string | Date, timeZone: string): ZonedParts {
  const parts = formatter(timeZone).formatToParts(parseInstant(instant));
  const values = new Map(parts.map((part) => [part.type, part.value]));
  const number = (name: string): number => {
    const value = Number(values.get(name));
    if (!Number.isInteger(value)) throw new Error(`Intl formatter did not return ${name}`);
    return value;
  };
  return {
    year: number('year'),
    month: number('month'),
    day: number('day'),
    hour: number('hour'),
    minute: number('minute'),
    second: number('second'),
  };
}

export function formatIsoDate(year: number, month: number, day: number): string {
  return `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function addCalendarDays(isoDate: string, days: number): string {
  if (!ISO_DATE.test(isoDate)) throw new RangeError('isoDate must be YYYY-MM-DD');
  if (!Number.isInteger(days)) throw new RangeError('days must be an integer');
  const timestamp = Date.parse(`${isoDate}T00:00:00.000Z`);
  if (!Number.isFinite(timestamp)) throw new RangeError('isoDate must be a real calendar date');
  const result = new Date(timestamp + days * 86_400_000).toISOString().slice(0, 10);
  const roundTrip = days === 0 ? result : isoDate;
  if (days === 0 && roundTrip !== isoDate) throw new RangeError('isoDate must be a real calendar date');
  return result;
}

export function resolveExperimentDate(
  instant: string | Date,
  timeZone: string,
  dayBoundaryHour: number,
): string {
  validateBoundaryHour(dayBoundaryHour);
  const local = zonedParts(instant, timeZone);
  const localDate = formatIsoDate(local.year, local.month, local.day);
  return local.hour < dayBoundaryHour ? addCalendarDays(localDate, -1) : localDate;
}

export function parseClockTime(value: string): { hour: number; minute: number } {
  const match = ISO_TIME.exec(value);
  if (!match) throw new RangeError('clock time must be HH:MM');
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour > 23 || minute > 59) throw new RangeError('clock time must be a real 24-hour time');
  return { hour, minute };
}

function wallClockStamp(parts: ZonedParts): number {
  return Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second);
}

/**
 * Converts a wall-clock time in the frozen IANA timezone to an absolute instant.
 * The iteration uses Intl as the timezone database, so DST offsets are not hard-coded.
 * Non-existent/ambiguous local wall times that cannot round-trip exactly are rejected.
 */
export function zonedDateTimeToInstant(
  isoDate: string,
  clockTime: string,
  timeZone: string,
): string {
  if (!ISO_DATE.test(isoDate)) throw new RangeError('isoDate must be YYYY-MM-DD');
  const { hour, minute } = parseClockTime(clockTime);
  validateTimeZone(timeZone);
  const [year, month, day] = isoDate.split('-').map(Number) as [number, number, number];
  const desired: ZonedParts = { year, month, day, hour, minute, second: 0 };
  let guess = Date.UTC(year, month - 1, day, hour, minute, 0);

  for (let iteration = 0; iteration < 6; iteration += 1) {
    const actual = zonedParts(new Date(guess), timeZone);
    const delta = wallClockStamp(desired) - wallClockStamp(actual);
    if (delta === 0) {
      return new Date(guess).toISOString();
    }
    guess += delta;
  }

  const finalParts = zonedParts(new Date(guess), timeZone);
  if (wallClockStamp(finalParts) !== wallClockStamp(desired)) {
    throw new RangeError(`wall-clock time ${isoDate} ${clockTime} does not resolve exactly in ${timeZone}`);
  }
  return new Date(guess).toISOString();
}

export function experimentDayBoundaryInstant(
  experimentDate: string,
  timeZone: string,
  dayBoundaryHour: number,
): string {
  validateBoundaryHour(dayBoundaryHour);
  return zonedDateTimeToInstant(experimentDate, `${String(dayBoundaryHour).padStart(2, '0')}:00`, timeZone);
}

export function systemContext(instant: string | Date, timeZone: string): { hour: number; dow: number; lunarPhase: number } {
  const date = parseInstant(instant);
  const local = zonedParts(date, timeZone);
  const localIso = formatIsoDate(local.year, local.month, local.day);
  const dow = new Date(`${localIso}T00:00:00.000Z`).getUTCDay();
  return { hour: local.hour, dow, lunarPhase: approximateLunarPhase(date) };
}

const SYNODIC_MONTH_DAYS = 29.530588853;
const REFERENCE_NEW_MOON_MS = Date.parse('2000-01-06T18:14:00.000Z');

/** Approximate synodic phase fraction: 0=new moon, 0.5=full moon. Exploratory context only. */
export function approximateLunarPhase(instant: string | Date): number {
  const elapsedDays = (parseInstant(instant).getTime() - REFERENCE_NEW_MOON_MS) / 86_400_000;
  const cycles = elapsedDays / SYNODIC_MONTH_DAYS;
  return ((cycles % 1) + 1) % 1;
}
