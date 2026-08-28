export type AnchorPayload = {
  genesisHash: string;
  headHash: string;
  headSeq: number;
  protocolVersion: string;
};

export type NotaryResult =
  | { status: 'published'; weekIndex: number; payload: AnchorPayload }
  | { status: 'skipped'; reason: 'disabled' | 'endpoint_missing' | 'outside_experiment' | 'already_attempted'; weekIndex?: number }
  | { status: 'failed'; weekIndex: number; error: string };

export type NotaryAttemptStore = {
  getLastAttemptedWeek(genesisHash: string): number | null;
  setLastAttemptedWeek(genesisHash: string, weekIndex: number): void;
};

export type NotaryFetch = typeof fetch;
