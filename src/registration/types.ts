import type { JsonObject } from '../ledger/types';

export const EXPERIMENT_DAYS = 365 as const;
export const CONDITION_COUNT = 5 as const;
export const PROTOCOL_VERSION = '2.1' as const;
export const CANONICALIZATION_VERSION = 'rfc8785-jcs-v1' as const;
export const SCHEDULE_ALGORITHM_VERSION = 'sha256-counter-fy-v1' as const;
export const TARGET_ALGORITHM_VERSION = 'sha256-counter-target-v1' as const;
export const RNG_POLICY_VERSION = 'rng-policy-v1' as const;
export const ANALYSIS_PLAN_VERSION = 'analysis-plan-v2.1' as const;
export const STATS_VERSION = 'stats-plan-v1' as const;
export const APP_VERSION = '0.0.0' as const;

export type Condition = 0 | 1 | 2 | 3 | 4;
export type TargetDirection = 0 | 1;
export type BitsPerDraw = 1024 | 2048 | 4096;
export type SessionsPerDay = 1 | 2 | 3;

export type DecisionRule = JsonObject & {
  pThresh: number;
  bfPos: number;
  bfNeg: number;
};

export const DEFAULT_DECISION_RULE: DecisionRule = {
  pThresh: 0.01,
  bfPos: 30,
  bfNeg: 1 / 30,
};

export type LayerCRegistration = JsonObject & {
  enabled: boolean;
  defaultDeadlineDays: 14 | 28 | 90;
  withdrawalPolicy: 'count_as_fail';
  decisionRuleC: DecisionRule;
  notarize: boolean;
};

export type RegistrationPayload = JsonObject & {
  experimentId: string;
  startDate: string;
  days: typeof EXPERIMENT_DAYS;
  bitsPerDraw: BitsPerDraw;
  sessionsPerDay: SessionsPerDay;
  dayBoundaryHour: number;
  affirmationText: string;
  predictionByCondition: string[];
  decisionRuleA: DecisionRule;
  layerC: LayerCRegistration;
  schedule: number[];
  scheduleSeed: string;
  analysisPlanVersion: typeof ANALYSIS_PLAN_VERSION;
  protocolVersion: typeof PROTOCOL_VERSION;
  canonicalizationVersion: typeof CANONICALIZATION_VERSION;
  scheduleAlgorithmVersion: typeof SCHEDULE_ALGORITHM_VERSION;
  targetAlgorithmVersion: typeof TARGET_ALGORITHM_VERSION;
  targetSeed: string;
  timeZone: string;
  rngPolicyVersion: typeof RNG_POLICY_VERSION;
  statsVersion: typeof STATS_VERSION;
  appVersion: typeof APP_VERSION;
  buildId?: string;
};

export type RegistrationInput = {
  experimentId: string;
  startDate: string;
  bitsPerDraw: BitsPerDraw;
  sessionsPerDay: SessionsPerDay;
  dayBoundaryHour: number;
  affirmationText: string;
  predictionByCondition: [string, string, string, string, string];
  timeZone: string;
  scheduleSeed: string;
  targetSeed: string;
  layerC: {
    enabled: boolean;
    defaultDeadlineDays: 14 | 28 | 90;
    notarize: boolean;
  };
  buildId?: string;
};

export type RegistrationResult = {
  payload: RegistrationPayload;
  genesisHash: string;
  seq: number;
};
