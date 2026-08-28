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
import { zScore } from '../stats';
import { ExperimentDashboard } from './ExperimentDashboard';
import { buildLayerADashboardModel } from './model';

const registration: RegistrationPayload = {
  experimentId: 'p6-365-render',
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
  scheduleSeed: 'p6-schedule-seed',
  analysisPlanVersion: ANALYSIS_PLAN_VERSION,
  protocolVersion: PROTOCOL_VERSION,
  canonicalizationVersion: CANONICALIZATION_VERSION,
  scheduleAlgorithmVersion: SCHEDULE_ALGORITHM_VERSION,
  targetAlgorithmVersion: TARGET_ALGORITHM_VERSION,
  targetSeed: 'p6-target-seed',
  timeZone: 'Asia/Tokyo',
  rngPolicyVersion: RNG_POLICY_VERSION,
  statsVersion: STATS_VERSION,
  appVersion: APP_VERSION,
};

function isoDate(offset: number): string {
  return new Date(Date.parse('2026-09-01T00:00:00.000Z') + offset * 86_400_000).toISOString().slice(0, 10);
}

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

function simulatedEntries(): StoredLedgerEntry[] {
  const entries: StoredLedgerEntry[] = [entry(1, 'registration', registration)];
  let seq = 2;
  for (let day = 0; day < EXPERIMENT_DAYS; day += 1) {
    const date = isoDate(day);
    const controlHits = 512 + ((day % 5) - 2);
    entries.push(entry(seq, 'control', {
      date,
      rngSource: day % 41 === 0 ? 'local' : 'anu',
      bitsHex: '00',
      nBits: 1024,
      hits: controlHits,
      z: zScore(controlHits, 1024),
    }));
    seq += 1;

    const hits = day % 97 === 0 ? 560 : 512 + ((day % 7) - 3);
    const ritualValid = day % 53 !== 0;
    entries.push(entry(seq, 'session', {
      date,
      seqInDay: 1,
      condition: day % 5,
      targetDir: day % 2,
      rngSource: day % 37 === 0 ? 'local' : 'anu',
      predictionSeq: Math.max(1, seq - 1),
      bitsHex: '00',
      nBits: 1024,
      hits,
      z: zScore(hits, 1024),
      ritual: { kind: 'pull_only', seconds: 60 + (day % 120), valid: ritualValid },
      moodPre: { v: 5, e: 5 },
      moodPost: { v: 5, e: 5 },
      context: { hour: 9, dow: day % 7, lunarPhase: (day % 30) / 30 },
      startedAt: `${date}T00:00:00.000Z`,
      completedAt: `${date}T00:02:00.000Z`,
    }));
    seq += 1;
  }
  return entries;
}

describe('P6 365-day dashboard rendering', () => {
  it('server-renders a stable Lab structure without exposing final p-value fields', () => {
    const model = buildLayerADashboardModel(simulatedEntries(), registration);
    const html = renderToStaticMarkup(
      <ExperimentDashboard
        registration={registration}
        genesisHash={'a'.repeat(64)}
        model={model}
        initialTab="lab"
      />,
    );

    const signature = {
      calendarCells: html.match(/role="gridcell"/g)?.length ?? 0,
      conditionCards: html.match(/class="condition-card"/g)?.length ?? 0,
      miracleRows: html.match(/class="source-pill/g)?.length ?? 0,
      hasCanvas: html.includes('aria-label="ANU有効セッションの累積偏差グラフ"'),
      hasChanceExpectation: html.includes('偶然なら 50.0%') && html.includes('偶然期待'),
      hasNoPeekBoundary: html.includes('p値は最終日まで非表示'),
      leaksFinalFields: html.includes('rawP') || html.includes('holmAdjustedP'),
      sourceCounts: model.sourceCounts,
      fallbackSessions: model.fallbackSessions,
      ritualInvalidSessions: model.ritualInvalidSessions,
      firstCalendarDate: model.calendar[0]?.date,
      lastCalendarDate: model.calendar.at(-1)?.date,
    };

    expect(signature).toMatchInlineSnapshot(`
      {
        "calendarCells": 365,
        "conditionCards": 5,
        "fallbackSessions": 10,
        "firstCalendarDate": "2026-09-01",
        "hasCanvas": true,
        "hasChanceExpectation": true,
        "hasNoPeekBoundary": true,
        "lastCalendarDate": "2027-08-31",
        "leaksFinalFields": false,
        "miracleRows": 3,
        "ritualInvalidSessions": 7,
        "sourceCounts": {
          "anu": {
            "bits": 363520,
            "sessions": 355,
          },
          "local": {
            "bits": 10240,
            "sessions": 10,
          },
          "randomorg": {
            "bits": 0,
            "sessions": 0,
          },
        },
      }
    `);
  });
});
