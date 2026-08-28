import { Sha256CounterStream, type Sha256Digest } from './counterStream';
import type { Condition, SessionsPerDay, TargetDirection } from './types';

export const CONDITION_DOMAIN = 'condition-schedule-v1';
export const TARGET_DOMAIN = 'target-schedule-v1';

export async function generateConditionSchedule(
  days: number,
  seed: string,
  digest?: Sha256Digest,
): Promise<Condition[]> {
  if (!Number.isInteger(days) || days <= 0) throw new RangeError('days must be a positive integer');
  const stream = new Sha256CounterStream(CONDITION_DOMAIN, seed, digest);
  const schedule: Condition[] = [];

  while (schedule.length < days) {
    const block: Condition[] = [0, 1, 2, 3, 4];
    for (let index = block.length - 1; index > 0; index -= 1) {
      const swapIndex = await stream.nextBoundedInt(index + 1);
      [block[index], block[swapIndex]] = [block[swapIndex]!, block[index]!];
    }
    const remaining = days - schedule.length;
    schedule.push(...block.slice(0, remaining));
  }

  return schedule;
}

export async function generateTargetSchedule(
  days: number,
  sessionsPerDay: SessionsPerDay,
  seed: string,
  digest?: Sha256Digest,
): Promise<TargetDirection[]> {
  if (!Number.isInteger(days) || days <= 0) throw new RangeError('days must be a positive integer');
  const stream = new Sha256CounterStream(TARGET_DOMAIN, seed, digest);
  const total = days * sessionsPerDay;
  const targets: TargetDirection[] = [];
  for (let index = 0; index < total; index += 1) {
    targets.push(await stream.nextBit());
  }
  return targets;
}
