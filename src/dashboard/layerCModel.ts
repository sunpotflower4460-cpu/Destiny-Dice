import type { StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import { RNG_SOURCES, type RngSource } from '../rng/types';
import {
  analyzeInterimLayerC,
  type LayerCComparisonSummary,
  type LayerCPathwaySummary,
  type LayerCStratumSummary,
  type LayerCWishObservation,
} from '../stats';
import { buildWishLedgerRecords } from '../wish/projection';

export type AssignmentSourceArmCount = {
  total: number;
  practice: number;
  sealed: number;
};

export type LayerCDashboardModel = {
  analysisKind: 'interim';
  primaryOutcomePolicy: 'realized_vs_all_other_judged';
  totalWishes: number;
  assignedWishes: number;
  judgedWishes: number;
  awaitingJudgment: number;
  unassignedWishes: number;
  assignmentSourceCounts: Record<RngSource, AssignmentSourceArmCount>;
  comparison: LayerCComparisonSummary;
  sensitivityExcludingUndecidable: LayerCComparisonSummary;
  strata: {
    likelihood: LayerCStratumSummary[];
    influence: LayerCStratumSummary[];
  };
  pathways: LayerCPathwaySummary[];
};

function emptySourceCounts(): Record<RngSource, AssignmentSourceArmCount> {
  return {
    anu: { total: 0, practice: 0, sealed: 0 },
    randomorg: { total: 0, practice: 0, sealed: 0 },
    local: { total: 0, practice: 0, sealed: 0 },
  };
}

export function buildLayerCDashboardModel(
  entries: readonly StoredLedgerEntry[],
  registration: RegistrationPayload,
): LayerCDashboardModel | null {
  if (!registration.layerC.enabled) return null;

  const records = buildWishLedgerRecords(entries);
  const sourceCounts = emptySourceCounts();
  const observations: LayerCWishObservation[] = [];
  let assignedWishes = 0;
  let judgedWishes = 0;
  let awaitingJudgment = 0;
  let unassignedWishes = 0;

  for (const record of records) {
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
    observations.push({
      arm: record.assignment.arm,
      outcome: record.judgment.outcome,
      likelihood: record.wish.likelihood,
      influence: record.wish.influence,
      ...(record.judgment.pathway === undefined ? {} : { pathway: record.judgment.pathway }),
    });
  }

  const interim = analyzeInterimLayerC(observations);
  return {
    analysisKind: interim.analysisKind,
    primaryOutcomePolicy: interim.primaryOutcomePolicy,
    totalWishes: records.length,
    assignedWishes,
    judgedWishes,
    awaitingJudgment,
    unassignedWishes,
    assignmentSourceCounts: Object.fromEntries(
      RNG_SOURCES.map((source) => [source, sourceCounts[source]]),
    ) as Record<RngSource, AssignmentSourceArmCount>,
    comparison: interim.comparison,
    sensitivityExcludingUndecidable: interim.sensitivityExcludingUndecidable,
    strata: interim.strata,
    pathways: interim.pathways,
  };
}
