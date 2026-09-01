import type { JsonObject, StoredLedgerEntry } from '../ledger/types';
import type { Condition, TargetDirection } from '../registration/types';
import type { RngSource } from '../rng/types';

export type MoodRating = JsonObject & {
  v: number;
  e: number;
};

export type RitualInput = {
  seconds: number;
  text?: string;
};

export type RitualRecord = JsonObject & {
  kind: 'pull_only' | 'intention_writing' | 'affirmation' | 'prayer' | 'full_combo';
  seconds: number;
  valid: boolean;
  textLen?: number;
  text?: string;
};

export type SessionContextInput = {
  hour: number;
  dow: number;
  lunarPhase: number;
  sleep?: number;
  stateTag?: string;
};

export type SessionContext = JsonObject & {
  hour: number;
  dow: number;
  lunarPhase: number;
  sleep?: number;
  stateTag?: string;
};

export type PredictionPayload = JsonObject & {
  date: string;
  seqInDay: number;
  condition: Condition;
  targetDir: TargetDirection;
  confidence: number;
  prophecyText?: string;
  committedAt: string;
};

export type ControlPayload = JsonObject & {
  date: string;
  rngSource: RngSource;
  bitsHex: string;
  nBits: number;
  hits: number;
  z: number;
};

export type SessionPayload = JsonObject & {
  date: string;
  seqInDay: number;
  condition: Condition;
  targetDir: TargetDirection;
  rngSource: RngSource;
  predictionSeq: number;
  bitsHex: string;
  nBits: number;
  hits: number;
  z: number;
  ritual: RitualRecord;
  moodPre: MoodRating;
  moodPost: MoodRating;
  context: SessionContext;
  startedAt: string;
  completedAt: string;
};

export type SessionPlan = {
  experimentDate: string;
  dayIndex: number;
  seqInDay: number;
  condition: Condition;
  targetDir: TargetDirection;
};

export type OrphanedPredictionSlot = {
  experimentDate: string;
  seqInDay: number;
  predictionSeq: number;
  committedAt: string;
};

export type SessionDraft = {
  experimentDate: string;
  seqInDay: number;
  moodPre: MoodRating;
  ritual: RitualInput;
  moodPost: MoodRating;
  confidence: number;
  prophecyText?: string;
  context: SessionContextInput;
  startedAt: string;
};

export type SessionResult = {
  plan: SessionPlan;
  predictionEntry: StoredLedgerEntry;
  sessionEntry: StoredLedgerEntry;
  payload: SessionPayload;
};

export interface Clock {
  now(): string;
}
