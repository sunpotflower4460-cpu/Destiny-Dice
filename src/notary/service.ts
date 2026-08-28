import type { RegistrationPayload } from '../registration/types';
import type { StoredLedgerEntry } from '../ledger/types';
import type { AnchorPayload, NotaryAttemptStore, NotaryFetch, NotaryResult } from './types';

const DAY_MS = 86_400_000;
const HASH_RE = /^[0-9a-f]{64}$/;

function dateIndex(startDate: string, currentDate: string): number {
  const start = Date.parse(`${startDate}T00:00:00.000Z`);
  const current = Date.parse(`${currentDate}T00:00:00.000Z`);
  if (!Number.isFinite(start) || !Number.isFinite(current)) throw new Error('invalid experiment date');
  return Math.round((current - start) / DAY_MS);
}

export function experimentWeekIndex(registration: RegistrationPayload, currentExperimentDate: string): number | null {
  const dayIndex = dateIndex(registration.startDate, currentExperimentDate);
  if (dayIndex < 0 || dayIndex >= registration.days) return null;
  return Math.floor(dayIndex / 7);
}

export function buildAnchorPayload(
  entries: readonly StoredLedgerEntry[],
  genesisHash: string,
  protocolVersion: string,
): AnchorPayload {
  const head = entries.at(-1);
  if (!head) throw new Error('cannot anchor an empty ledger');
  if (!HASH_RE.test(genesisHash)) throw new Error('invalid genesis hash');
  if (!HASH_RE.test(head.entry_hash)) throw new Error('invalid ledger head hash');
  return {
    genesisHash,
    headHash: head.entry_hash,
    headSeq: head.seq,
    protocolVersion,
  };
}

export class WeeklyNotaryService {
  constructor(
    private readonly endpoint: string | null,
    private readonly attempts: NotaryAttemptStore,
    private readonly fetchImpl: NotaryFetch = fetch,
    private readonly timeoutMs = 5_000,
  ) {}

  async publishIfDue(input: {
    registration: RegistrationPayload;
    currentExperimentDate: string;
    entries: readonly StoredLedgerEntry[];
    genesisHash: string;
  }): Promise<NotaryResult> {
    const { registration, currentExperimentDate, entries, genesisHash } = input;
    if (!registration.layerC.notarize) return { status: 'skipped', reason: 'disabled' };
    if (!this.endpoint) return { status: 'skipped', reason: 'endpoint_missing' };

    const weekIndex = experimentWeekIndex(registration, currentExperimentDate);
    if (weekIndex === null) return { status: 'skipped', reason: 'outside_experiment' };
    if (this.attempts.getLastAttemptedWeek(genesisHash) === weekIndex) {
      return { status: 'skipped', reason: 'already_attempted', weekIndex };
    }

    // The frozen v1 rule is "offline -> skip until next week". Mark the weekly
    // attempt before networking so repeated app opens cannot turn an outage into
    // an unbounded retry loop. Losing this local scheduling hint is harmless:
    // the Worker endpoint itself is idempotent.
    this.attempts.setLastAttemptedWeek(genesisHash, weekIndex);
    const payload = buildAnchorPayload(entries, genesisHash, registration.protocolVersion);
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      const response = await this.fetchImpl(`${this.endpoint.replace(/\/$/, '')}/anchors`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(payload),
        signal: controller.signal,
      });
      if (!response.ok) throw new Error(`anchor endpoint returned HTTP ${response.status}`);
      return { status: 'published', weekIndex, payload };
    } catch (cause) {
      const error = cause instanceof Error ? cause.message : String(cause);
      return { status: 'failed', weekIndex, error };
    } finally {
      clearTimeout(timer);
    }
  }
}
