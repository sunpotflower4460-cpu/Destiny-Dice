export type RateGateOptions = {
  minIntervalMs: number;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
};

const defaultSleep = (ms: number): Promise<void> =>
  new Promise((resolve) => {
    setTimeout(resolve, ms);
  });

/**
 * Serializes provider calls and enforces a configurable minimum interval.
 * No service-specific interval is hard-coded here; Gate 0 explicitly froze
 * provider throttling as adapter configuration because ANU's service policy is changing.
 */
export class RateGate {
  private readonly minIntervalMs: number;
  private readonly now: () => number;
  private readonly sleep: (ms: number) => Promise<void>;
  private tail: Promise<void> = Promise.resolve();
  private lastStartedAt: number | undefined;

  constructor(options: RateGateOptions) {
    if (!Number.isFinite(options.minIntervalMs) || options.minIntervalMs < 0) {
      throw new RangeError('minIntervalMs must be a finite non-negative number');
    }

    this.minIntervalMs = options.minIntervalMs;
    this.now = options.now ?? Date.now;
    this.sleep = options.sleep ?? defaultSleep;
  }

  run<T>(task: () => Promise<T>): Promise<T> {
    const run = this.tail.then(async () => {
      if (this.lastStartedAt !== undefined) {
        const remaining = this.lastStartedAt + this.minIntervalMs - this.now();
        if (remaining > 0) {
          await this.sleep(remaining);
        }
      }

      this.lastStartedAt = this.now();
      return task();
    });

    this.tail = run.then(
      () => undefined,
      () => undefined,
    );

    return run;
  }
}
