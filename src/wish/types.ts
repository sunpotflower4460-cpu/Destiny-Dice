import type { JsonObject } from '../ledger/types';
import type { RngSource } from '../rng/types';

export type WishLikelihood = 1 | 2 | 3;
export type WishInfluence = 'self' | 'mixed' | 'external';
export type WishArm = 'practice' | 'sealed';
export type WishOutcome = 'realized' | 'not_realized' | 'undecidable' | 'withdrawn';
export type WishPathway = 'own_action' | 'other_person' | 'chance_encounter' | 'unknown';

export type WishPayload = JsonObject & {
  wishId: string;
  text: string;
  deadline: string;
  likelihood: WishLikelihood;
  influence: WishInfluence;
  createdAt: string;
};

export type AssignmentPayload = JsonObject & {
  wishId: string;
  arm: WishArm;
  rngSource: RngSource;
  bit: 0 | 1;
  committedAt: string;
};

export type JudgmentPayload = JsonObject & {
  wishId: string;
  outcome: WishOutcome;
  pathway?: WishPathway;
  note?: string;
  judgedAt: string;
};

export type WishMomentPayload = JsonObject & {
  date: string;
  wishIdsShown: string[];
  seconds: number;
};

export type WishRegistrationInput = {
  text: string;
  deadline: string;
  likelihood: WishLikelihood;
  influence: WishInfluence;
};

export type PracticeWishView = {
  wishId: string;
  text: string;
  deadline: string;
  likelihood: WishLikelihood;
  influence: WishInfluence;
  createdAt: string;
};

export type DueWishView = PracticeWishView & {
  arm: WishArm;
  rngSource: RngSource;
};

export type WishRegistryProjection = {
  practice: PracticeWishView[];
  sealedCount: number;
  unassignedCount: number;
  dueCount: number;
};

export type WishMomentProjection = {
  date: string;
  wishes: PracticeWishView[];
};

export type PrimaryWishOutcome = 'realized' | 'not_realized';

export type WishClock = {
  now(): string;
};

export type WishIdFactory = () => string;
