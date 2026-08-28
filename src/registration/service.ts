import type { LedgerService } from '../ledger/service';
import { generateConditionSchedule } from './schedule';
import {
  ANALYSIS_PLAN_VERSION,
  APP_VERSION,
  CANONICALIZATION_VERSION,
  DEFAULT_DECISION_RULE,
  EXPERIMENT_DAYS,
  PROTOCOL_VERSION,
  RNG_POLICY_VERSION,
  SCHEDULE_ALGORITHM_VERSION,
  STATS_VERSION,
  TARGET_ALGORITHM_VERSION,
  type RegistrationInput,
  type RegistrationPayload,
  type RegistrationResult,
} from './types';
import { validateRegistrationInput } from './validation';

export class RegistrationService {
  private readonly ledger: LedgerService;

  constructor(ledger: LedgerService) {
    this.ledger = ledger;
  }

  async register(input: RegistrationInput, createdAt: string): Promise<RegistrationResult> {
    validateRegistrationInput(input);

    const schedule = await generateConditionSchedule(EXPERIMENT_DAYS, input.scheduleSeed);
    const layerC = {
      enabled: input.layerC.enabled,
      defaultDeadlineDays: input.layerC.defaultDeadlineDays,
      withdrawalPolicy: 'count_as_fail' as const,
      decisionRuleC: { ...DEFAULT_DECISION_RULE },
      notarize: input.layerC.notarize,
    };

    const basePayload = {
      experimentId: input.experimentId.trim(),
      startDate: input.startDate,
      days: EXPERIMENT_DAYS,
      bitsPerDraw: input.bitsPerDraw,
      sessionsPerDay: input.sessionsPerDay,
      dayBoundaryHour: input.dayBoundaryHour,
      affirmationText: input.affirmationText.trim(),
      predictionByCondition: input.predictionByCondition.map((value) => value.trim()),
      decisionRuleA: { ...DEFAULT_DECISION_RULE },
      layerC,
      schedule,
      scheduleSeed: input.scheduleSeed,
      analysisPlanVersion: ANALYSIS_PLAN_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      canonicalizationVersion: CANONICALIZATION_VERSION,
      scheduleAlgorithmVersion: SCHEDULE_ALGORITHM_VERSION,
      targetAlgorithmVersion: TARGET_ALGORITHM_VERSION,
      targetSeed: input.targetSeed,
      timeZone: input.timeZone,
      rngPolicyVersion: RNG_POLICY_VERSION,
      statsVersion: STATS_VERSION,
      appVersion: APP_VERSION,
    } satisfies RegistrationPayload;

    const payload: RegistrationPayload = input.buildId
      ? { ...basePayload, buildId: input.buildId }
      : basePayload;

    const entry = await this.ledger.append('registration', payload, createdAt);
    return {
      payload,
      genesisHash: entry.entryHash,
      seq: entry.seq,
    };
  }
}
