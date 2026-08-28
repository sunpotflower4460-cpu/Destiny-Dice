import { importLedgerJson } from '../ledger/export';
import type { StoredLedgerEntry } from '../ledger/types';
import { verifyChain } from '../ledger/verify';
import {
  ANALYSIS_PLAN_VERSION,
  CANONICALIZATION_VERSION,
  CONDITION_COUNT,
  EXPERIMENT_DAYS,
  PROTOCOL_VERSION,
  RNG_POLICY_VERSION,
  STATS_VERSION,
  type RegistrationPayload,
} from '../registration/types';
import { assertIanaTimeZone, assertIsoDate } from '../registration/validation';

export type VerifiedExperimentExport = {
  entries: StoredLedgerEntry[];
  registration: RegistrationPayload;
};

function asObject(value: unknown, label: string): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new TypeError(`${label} must be an object`);
  return value as Record<string, unknown>;
}

export function parseFrozenRegistration(payloadJson: string): RegistrationPayload {
  const parsed = asObject(JSON.parse(payloadJson), 'registration payload');
  if (parsed.protocolVersion !== PROTOCOL_VERSION) throw new Error(`unsupported protocolVersion: ${String(parsed.protocolVersion)}`);
  if (parsed.canonicalizationVersion !== CANONICALIZATION_VERSION) throw new Error('canonicalizationVersion mismatch');
  if (parsed.analysisPlanVersion !== ANALYSIS_PLAN_VERSION) throw new Error('analysisPlanVersion mismatch');
  if (parsed.statsVersion !== STATS_VERSION) throw new Error('statsVersion mismatch');
  if (parsed.rngPolicyVersion !== RNG_POLICY_VERSION) throw new Error('rngPolicyVersion mismatch');
  if (parsed.days !== EXPERIMENT_DAYS) throw new Error(`registration days must be ${EXPERIMENT_DAYS}`);
  if (typeof parsed.experimentId !== 'string' || parsed.experimentId.length === 0) throw new TypeError('experimentId is required');
  if (typeof parsed.startDate !== 'string') throw new TypeError('startDate is required');
  assertIsoDate(parsed.startDate, 'startDate');
  if (typeof parsed.timeZone !== 'string') throw new TypeError('timeZone is required');
  assertIanaTimeZone(parsed.timeZone);
  if (!Array.isArray(parsed.schedule) || parsed.schedule.length !== EXPERIMENT_DAYS) throw new Error('registration schedule length mismatch');
  if (parsed.schedule.some((value) => !Number.isInteger(value) || (value as number) < 0 || (value as number) >= CONDITION_COUNT)) {
    throw new Error('registration schedule contains invalid condition');
  }
  if (!Array.isArray(parsed.predictionByCondition) || parsed.predictionByCondition.length !== CONDITION_COUNT) {
    throw new Error('predictionByCondition length mismatch');
  }
  const layerC = asObject(parsed.layerC, 'registration layerC');
  const decisionRuleA = asObject(parsed.decisionRuleA, 'registration decisionRuleA');
  const decisionRuleC = asObject(layerC.decisionRuleC, 'registration decisionRuleC');
  for (const [label, rule] of [['decisionRuleA', decisionRuleA], ['decisionRuleC', decisionRuleC]] as const) {
    if (typeof rule.pThresh !== 'number' || typeof rule.bfPos !== 'number' || typeof rule.bfNeg !== 'number') {
      throw new TypeError(`${label} is incomplete`);
    }
  }
  return parsed as unknown as RegistrationPayload;
}

export async function loadVerifiedExperimentExport(text: string): Promise<VerifiedExperimentExport> {
  const entries = importLedgerJson(text);
  if (entries.length === 0) throw new Error('experiment export is empty');
  const verification = await verifyChain(entries);
  if (!verification.ok) throw new Error(`ledger verification failed: ${verification.code}`);
  const genesis = entries[0]!;
  if (genesis.type !== 'registration') throw new Error('first ledger entry must be registration');
  return { entries, registration: parseFrozenRegistration(genesis.payloadJson) };
}
