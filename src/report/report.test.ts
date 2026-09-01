import { describe, expect, it } from 'vitest';
import { exportLedgerJson } from '../ledger/export';
import { LedgerService } from '../ledger/service';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { RegistrationService } from '../registration/service';
import type { RegistrationInput } from '../registration/types';
import { loadVerifiedExperimentExport } from './export';
import { buildFinalReportModel } from './model';
import { renderFinalReportMarkdown } from './markdown';

const registrationInput: RegistrationInput = {
  experimentId: 'p10-report-test',
  startDate: '2026-01-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1予言', 'P2予言', 'P3予言', 'P4予言', 'P5予言'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p10-report-schedule-seed',
  targetSeed: 'p10-report-target-seed',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

async function fixture() {
  const ledger = new LedgerService(new MemoryLedgerStore());
  const registration = await new RegistrationService(ledger).register(
    registrationInput,
    '2025-12-31T15:00:00.000Z',
  );

  await ledger.append('wish', {
    wishId: 'eligible-judged',
    text: '実験終了までに判定できる願い',
    deadline: '2026-12-31',
    likelihood: 2,
    influence: 'self',
    createdAt: '2026-01-01T01:00:00.000Z',
  }, '2026-01-01T01:00:00.000Z');
  await ledger.append('assignment', {
    wishId: 'eligible-judged',
    arm: 'practice',
    rngSource: 'local',
    bit: 1,
    committedAt: '2026-01-01T01:00:01.000Z',
  }, '2026-01-01T01:00:01.000Z');
  await ledger.append('judgment', {
    wishId: 'eligible-judged',
    outcome: 'realized',
    pathway: 'own_action',
    judgedAt: '2026-12-31T12:00:00.000Z',
  }, '2026-12-31T12:00:00.000Z');

  await ledger.append('wish', {
    wishId: 'eligible-missing',
    text: '締切済みだが判定が欠測の願い',
    deadline: '2026-12-31',
    likelihood: 3,
    influence: 'external',
    createdAt: '2026-01-02T01:00:00.000Z',
  }, '2026-01-02T01:00:00.000Z');
  await ledger.append('assignment', {
    wishId: 'eligible-missing',
    arm: 'sealed',
    rngSource: 'randomorg',
    bit: 0,
    committedAt: '2026-01-02T01:00:01.000Z',
  }, '2026-01-02T01:00:01.000Z');

  await ledger.append('wish', {
    wishId: 'post-experiment',
    text: '実験終了後に締切が来る願い',
    deadline: '2027-01-01',
    likelihood: 1,
    influence: 'mixed',
    createdAt: '2026-12-20T01:00:00.000Z',
  }, '2026-12-20T01:00:00.000Z');
  await ledger.append('assignment', {
    wishId: 'post-experiment',
    arm: 'sealed',
    rngSource: 'anu',
    bit: 0,
    committedAt: '2026-12-20T01:00:01.000Z',
  }, '2026-12-20T01:00:01.000Z');
  await ledger.append('judgment', {
    wishId: 'post-experiment',
    outcome: 'not_realized',
    judgedAt: '2027-01-01T12:00:00.000Z',
  }, '2027-01-01T12:00:00.000Z');

  return { ledger, registration };
}

describe('P10 final report', () => {
  it('uses only deadline-reached-and-judged wishes in the final Layer C denominator', async () => {
    const { ledger, registration } = await fixture();
    const model = buildFinalReportModel(await ledger.list(), registration.payload);
    expect(model.experimentEndDate).toBe('2026-12-31');
    expect(model.confirmatory.layerC.eligibleJudgedWishes).toBe(1);
    expect(model.confirmatory.layerC.eligibleUnjudgedWishes).toBe(1);
    expect(model.confirmatory.layerC.postExperimentDeadlineWishes).toBe(1);
    expect(model.confirmatory.layerC.result?.comparison.practice.n).toBe(1);
    expect(model.confirmatory.layerC.result?.comparison.sealed.n).toBe(0);
    expect(model.confirmatory.layerC.result?.fisherTwoSidedP).toBeNull();
  });

  it('keeps confirmatory Markdown free of exploratory-derived sections and warnings', async () => {
    const { ledger, registration } = await fixture();
    const markdown = renderFinalReportMarkdown(buildFinalReportModel(await ledger.list(), registration.payload));
    const [confirmatory, exploratory] = markdown.split('## 2. 探索パート');
    expect(confirmatory).toContain('## 1. 確証パート');
    expect(confirmatory).toContain('Holm補正p');
    expect(confirmatory).toContain('Fisher両側p');
    expect(confirmatory).toContain('偶然なら 0pp');
    expect(confirmatory).toContain('偶然モデルとの中立点 1');
    expect(confirmatory).not.toContain('探索的分析です');
    expect(confirmatory).not.toContain('dose-response');
    expect(confirmatory).not.toContain('手応え×z相関');
    expect(exploratory).toContain('探索的分析です');
    expect(exploratory).toContain('偶然なら同じ分析session数で約');
    expect(exploratory).toContain('偶然なら 0、非盲検');
    expect(exploratory).toContain('層別の探索集計');
    expect(exploratory).toContain('dose×z 相関');
    expect(exploratory).toContain('（偶然なら 0）');
    expect(exploratory).toContain('### 8. 実現までの日数');
    expect(exploratory).toContain('測定不能');
    expect(exploratory).toContain('### 9. 願い文の特徴');
  });

  it('verifies an exported chain before rebuilding the report and rejects tampering', async () => {
    const { ledger, registration } = await fixture();
    const entries = await ledger.list();
    const exported = exportLedgerJson(entries);
    const verified = await loadVerifiedExperimentExport(exported);
    expect(verified.registration.experimentId).toBe(registration.payload.experimentId);
    expect(renderFinalReportMarkdown(buildFinalReportModel(verified.entries, verified.registration)))
      .toBe(renderFinalReportMarkdown(buildFinalReportModel(entries, registration.payload)));

    const tampered = JSON.parse(exported) as Array<Record<string, unknown>>;
    tampered[1]!.payloadJson = JSON.stringify({ tampered: true });
    await expect(loadVerifiedExperimentExport(JSON.stringify(tampered))).rejects.toThrow(/ledger verification failed/);
  });
});
