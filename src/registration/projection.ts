import { assertTargetAlgorithmVersion, generateTargetSchedule } from './schedule';
import type { Condition, RegistrationPayload, TargetDirection } from './types';
import { assertIsoDate } from './validation';

function dayIndex(startDate: string, experimentDate: string): number {
  assertIsoDate(startDate, 'startDate');
  assertIsoDate(experimentDate, 'experimentDate');
  const start = Date.parse(`${startDate}T00:00:00.000Z`);
  const current = Date.parse(`${experimentDate}T00:00:00.000Z`);
  return Math.trunc((current - start) / 86_400_000);
}

export type CurrentScheduleProjection = {
  experimentDate: string;
  dayIndex: number;
  condition: Condition;
  targets: TargetDirection[];
};

/**
 * Normal application UI receives only the current experiment day's assignment.
 * The full frozen condition schedule remains in the ledger for audit/export,
 * while future target bits are regenerated only inside this projection and not returned.
 */
export async function projectCurrentSchedule(
  registration: RegistrationPayload,
  currentExperimentDate: string,
): Promise<CurrentScheduleProjection | null> {
  const index = dayIndex(registration.startDate, currentExperimentDate);
  if (index < 0 || index >= registration.days) return null;

  const condition = registration.schedule[index];
  if (condition === undefined || condition < 0 || condition > 4) {
    throw new Error('Frozen registration schedule is invalid');
  }

  assertTargetAlgorithmVersion(registration.targetAlgorithmVersion);
  const allTargets = await generateTargetSchedule(
    registration.days,
    registration.sessionsPerDay,
    registration.targetSeed,
  );
  const targetStart = index * registration.sessionsPerDay;
  const targets = allTargets.slice(targetStart, targetStart + registration.sessionsPerDay);
  if (targets.length !== registration.sessionsPerDay) {
    throw new Error('Frozen target schedule is incomplete');
  }

  return {
    experimentDate: currentExperimentDate,
    dayIndex: index,
    condition: condition as Condition,
    targets,
  };
}
