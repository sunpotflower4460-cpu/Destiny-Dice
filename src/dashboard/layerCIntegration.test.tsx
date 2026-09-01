import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import type { StoredLedgerEntry } from '../ledger/types';
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
  type RegistrationPayload,
} from '../registration/types';
import { ExperimentDashboard } from './ExperimentDashboard';
import { buildLayerCDashboardModel } from './layerCModel';
import { buildLayerADashboardModel } from './model';

const registration: RegistrationPayload = {
  experimentId: 'p8-layer-c-render',
  startDate: '2026-09-01',
  days: EXPERIMENT_DAYS,
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: 'calm and focused',
  predictionByCondition: ['p1', 'p2', 'p3', 'p4', 'p5'],
  decisionRuleA: DEFAULT_DECISION_RULE,
  layerC: {
    enabled: true,
    defaultDeadlineDays: 28,
    withdrawalPolicy: 'count_as_fail',
    decisionRuleC: DEFAULT_DECISION_RULE,
    notarize: false,
  },
  schedule: Array.from({ length: EXPERIMENT_DAYS }, (_, index) => index % 5),
  scheduleSeed: 'p8-schedule-seed',
  analysisPlanVersion: ANALYSIS_PLAN_VERSION,
  protocolVersion: PROTOCOL_VERSION,
  canonicalizationVersion: CANONICALIZATION_VERSION,
  scheduleAlgorithmVersion: SCHEDULE_ALGORITHM_VERSION,
  targetAlgorithmVersion: TARGET_ALGORITHM_VERSION,
  targetSeed: 'p8-target-seed',
  timeZone: 'Asia/Tokyo',
  rngPolicyVersion: RNG_POLICY_VERSION,
  statsVersion: STATS_VERSION,
  appVersion: APP_VERSION,
};

function entry(seq: number, type: StoredLedgerEntry['type'], payload: Record<string, unknown>): StoredLedgerEntry {
  return {
    seq,
    type,
    payloadJson: JSON.stringify(payload),
    createdAt: '2026-09-01T00:00:00.000Z',
    prevHash: '0'.repeat(64),
    entryHash: `${seq}`.padStart(64, '0'),
  };
}

function wishRows(): StoredLedgerEntry[] {
  return [
    entry(1, 'wish', { wishId: 'p1', text: '実践願い', deadline: '2026-09-30', likelihood: 1, influence: 'self', createdAt: '2026-09-01T00:00:00.000Z' }),
    entry(2, 'assignment', { wishId: 'p1', arm: 'practice', rngSource: 'anu', bit: 1, committedAt: '2026-09-01T00:00:01.000Z' }),
    entry(3, 'judgment', { wishId: 'p1', outcome: 'realized', pathway: 'own_action', judgedAt: '2026-10-01T00:00:00.000Z' }),
    entry(4, 'wish', { wishId: 's1', text: 'UIへ漏れてはいけない封印本文', deadline: '2026-09-30', likelihood: 3, influence: 'external', createdAt: '2026-09-01T00:00:02.000Z' }),
    entry(5, 'assignment', { wishId: 's1', arm: 'sealed', rngSource: 'local', bit: 0, committedAt: '2026-09-01T00:00:03.000Z' }),
    entry(6, 'judgment', { wishId: 's1', outcome: 'not_realized', judgedAt: '2026-10-01T00:00:00.000Z' }),
  ];
}

describe('P8 Layer C lab integration', () => {
  it('renders baseline, BF, strata, pathways and assignment source without exposing Fisher p or wish text', () => {
    const entries = wishRows();
    const layerA = buildLayerADashboardModel(entries, registration);
    const layerC = buildLayerCDashboardModel(entries, registration);
    const html = renderToStaticMarkup(
      <ExperimentDashboard
        registration={registration}
        genesisHash={'a'.repeat(64)}
        model={layerA}
        layerCModel={layerC}
        initialTab="lab"
      />,
    );

    expect(html).toContain('LAB / LAYER C');
    expect(html).toContain('封印群があなたのベースライン');
    expect(html).toContain('BF₁₀');
    expect(html).toContain('Fisher p値は最終解析まで非表示');
    expect(html).toContain('層別の探索集計');
    expect(html).toContain('実現した願いの経路');
    expect(html).toContain('Local crypto');
    expect(html).not.toContain('UIへ漏れてはいけない封印本文');
    expect(html).not.toContain('実践願い');
    expect(JSON.stringify(layerC)).not.toContain('fisherTwoSidedP');
  });
});
