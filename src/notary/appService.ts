import { WeeklyNotaryService } from './service';
import type { NotaryAttemptStore } from './types';

const PREFIX = 'intention-dice:notary-week:';

export class LocalStorageNotaryAttemptStore implements NotaryAttemptStore {
  getLastAttemptedWeek(genesisHash: string): number | null {
    if (typeof window === 'undefined') return null;
    try {
      const raw = window.localStorage.getItem(`${PREFIX}${genesisHash}`);
      if (raw === null) return null;
      const value = Number(raw);
      return Number.isInteger(value) && value >= 0 ? value : null;
    } catch {
      return null;
    }
  }

  setLastAttemptedWeek(genesisHash: string, weekIndex: number): void {
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(`${PREFIX}${genesisHash}`, String(weekIndex));
    } catch {
      // Scheduling state is best-effort. The public endpoint is idempotent.
    }
  }
}

let applicationNotary: WeeklyNotaryService | null = null;

export function getApplicationNotaryService(): WeeklyNotaryService {
  if (!applicationNotary) {
    const raw = import.meta.env.VITE_NOTARY_ENDPOINT?.trim();
    applicationNotary = new WeeklyNotaryService(raw ? raw : null, new LocalStorageNotaryAttemptStore());
  }
  return applicationNotary;
}
