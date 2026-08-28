import type { RegistrationInput } from './types';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export function assertIsoDate(value: string, label: string): void {
  if (!ISO_DATE.test(value)) throw new TypeError(`${label} must be YYYY-MM-DD`);
  const [yearText, monthText, dayText] = value.split('-');
  const year = Number(yearText);
  const month = Number(monthText);
  const day = Number(dayText);
  const date = new Date(Date.UTC(year, month - 1, day));
  const normalized = [
    String(date.getUTCFullYear()).padStart(4, '0'),
    String(date.getUTCMonth() + 1).padStart(2, '0'),
    String(date.getUTCDate()).padStart(2, '0'),
  ].join('-');
  if (normalized !== value) throw new TypeError(`${label} must be a valid calendar date`);
}

export function assertIanaTimeZone(value: string): void {
  if (!value.trim()) throw new TypeError('timeZone must be non-empty');
  try {
    new Intl.DateTimeFormat('en-US', { timeZone: value }).format(0);
  } catch {
    throw new TypeError('timeZone must be a valid IANA timezone');
  }
}

export function validateRegistrationInput(input: RegistrationInput): void {
  if (!input.experimentId.trim()) throw new TypeError('experimentId must be non-empty');
  assertIsoDate(input.startDate, 'startDate');
  if (![1024, 2048, 4096].includes(input.bitsPerDraw)) throw new RangeError('bitsPerDraw is invalid');
  if (![1, 2, 3].includes(input.sessionsPerDay)) throw new RangeError('sessionsPerDay is invalid');
  if (!Number.isInteger(input.dayBoundaryHour) || input.dayBoundaryHour < 0 || input.dayBoundaryHour > 23) {
    throw new RangeError('dayBoundaryHour must be an integer from 0 through 23');
  }
  if (!input.affirmationText.trim()) throw new TypeError('affirmationText must be non-empty');
  if (input.predictionByCondition.length !== 5 || input.predictionByCondition.some((value) => !value.trim())) {
    throw new TypeError('predictionByCondition must contain five non-empty predictions');
  }
  assertIanaTimeZone(input.timeZone);
  if (!input.scheduleSeed.trim()) throw new TypeError('scheduleSeed must be non-empty');
  if (!input.targetSeed.trim()) throw new TypeError('targetSeed must be non-empty');
  if (![14, 28, 90].includes(input.layerC.defaultDeadlineDays)) {
    throw new RangeError('layerC.defaultDeadlineDays is invalid');
  }
  if (input.buildId !== undefined && !input.buildId.trim()) {
    throw new TypeError('buildId must be omitted or non-empty');
  }
}
