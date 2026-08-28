import type { StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import { RNG_SOURCES, type RngSource } from '../rng/types';
import {
  analyzeExploratoryLayerC,
  analyzeInterimLayerC,
  type LayerCComparisonSummary,
  type LayerCPathwaySummary,
  type LayerCStratumSummary,
  type LayerCWishObservation,
} from '../stats';
import { assertIsoDate, buildWishLedgerRecords } from '../wish/projection';

export type AssignmentSourceArmCount = {
  total: number;
  practice: number;
  sealed: number;
};

export type LayerCDashboardModel = {
  analysisKind: 'interim';
  primaryOutcomePolicy: 'realized_vs_all_other_judged';
  experimentEndDate: string;
  totalWishes: number;
  assignedWishes: number;
  judgedWishes: number;
  primaryEligibleJudgedWishes: number;
  postExperimentDeadlineWishes: number;
  awaitingJudgment: number;
  unassignedWishes: number;
  assignmentSourceCounts: Record<RngSource, AssignmentSourceArmCount>;
  comparison: LayerCComparisonSummary;
  sensitivityExcludingUndecidable: LayerCComparisonSummary;
  exploratoryAnalysisKind: 'exploratory';
  exploratoryWarning: string;
  strata: {
    likelihood: LayerCStratumSummary[];
    influence: LayerCStratumSummary[];
  };
  pathways: LayerCPathwaySummary[];
};

const DAY_MS = 86_400_000;

function emptySourceCounts(): Record<RngSource, AssignmentSourceArmCount> {
  return {
    anu: { total: 0, practice: 0, sealed: 0 },
    randomorg: { total: 0, practice: 0, sealed: 0 },
    local: { total: 0, practice: 0, sealed: 0 },
  };
}

function deriveExperimentEndDate(registration: RegistrationPayload): string {
  assertIsoDate(registration.startDate, 'registration startDate');
  if (!Number.isInteger(registration.days) || registration.days <= 0) {
    throw new RangeError('registration days must be a positive integer');
  }
  const startMs = Date.parse(`${registration.startDate}T00:00:00.000Z`);
  return new Date(startMs + (registration.days - 1) * DAY_MS).toISOString().slice(0, 10);
}

export function buildLayerCDashboardModel(
  entries: readonly StoredLedgerEntry[],
  registration: RegistrationPayload,
): LayerCDashboardModel | null {
  if (!registration.layerC.enabled) return null;

  const records = buildWishLedgerRecords(entries);
  const experimentEndDate = deriveExperimentEndDate(registration);
  const sourceCounts = emptySourceCounts();
  const observations: LayerCWishObservation[] = [];
  let assignedWishes = 0;
  let judgedWishes = 0;
  let primaryEligibleJudgedWishes = 0;
  let postExperimentDeadlineWishes = 0;
  let awaitingJudgment = 0;
  let unassignedWishes = 0;

  for (const record of records) {
    const deadlineAfterExperiment = record.wish.deadline > experimentEndDate;
    if (deadlineAfterExperiment) postExperimentDeadlineWishes += 1;

    if (!record.assignment) {
      if (record.judgment) throw new Error(`judgment exists without assignment for wish ${record.wish.wishId}`);
      unassignedWishes += 1;
      continue;
    }

    assignedWishes += 1;
    const source = sourceCounts[record.assignment.rngSource];
    source.total += 1;
    source[record.assignment.arm] += 1;

    if (!record.judgment) {
      awaitingJudgment += 1;
      continue;
    }

    judgedWishes += 1;
    if (deadlineAfterExperiment) continue;

    primaryEligibleJudgedWishes += 1;
    observations.push({
      arm: record.assignment.arm,
      outcome: record.judgment.outcome,
      likelihood: record.wish.likelihood,
      influence: record.wish.influence,
      ...(record.judgment.pathway === undefined ? {} : { pathway: record.judgment.pathway }),
    });
  }

  const interim = analyzeInterimLayerC(observations);
  const exploratory = analyzeExploratoryLayerC(observations);
  return {
    analysisKind: interim.analysisKind,
    primaryOutcomePolicy: interim.primaryOutcomePolicy,
    experimentEndDate,
    totalWishes: records.length,
    assignedWishes,
    judgedWishes,
    primaryEligibleJudgedWishes,
    postExperimentDeadlineWishes,
    awaitingJudgment,
    unassignedWishes,
    assignmentSourceCounts: Object.fromEntries(
      RNG_SOURCES.map((source) => [source, sourceCounts[source]]),
    ) as Record<RngSource, AssignmentSourceArmCount>,
    comparison: interim.comparison,
    sensitivityExcludingUndecidable: interim.sensitivityExcludingUndecidable,
    exploratoryAnalysisKind: exploratory.analysisKind,
    exploratoryWarning: exploratory.warning,
    strata: exploratory.strata,
    pathways: exploratory.pathways,
  };
}
