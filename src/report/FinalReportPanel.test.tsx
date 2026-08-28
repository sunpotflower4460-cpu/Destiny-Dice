import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { LedgerService } from '../ledger/service';
import { MemoryLedgerStore } from '../ledger/memoryStore';
import { RegistrationService } from '../registration/service';
import type { RegistrationInput } from '../registration/types';
import { FinalReportPanel } from './FinalReportPanel';

const input: RegistrationInput = {
  experimentId: 'p10-panel-test',
  startDate: '2026-01-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1', 'P2', 'P3', 'P4', 'P5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p10-panel-schedule',
  targetSeed: 'p10-panel-target',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

async function registeredFixture() {
  const ledger = new LedgerService(new MemoryLedgerStore());
  const registration = await new RegistrationService(ledger).register(input, '2025-12-31T15:00:00.000Z');
  return { entries: await ledger.list(), registration: registration.payload };
}

describe('FinalReportPanel', () => {
  it('renders nothing during the registered 365-day window', async () => {
    const { entries, registration } = await registeredFixture();
    const html = renderToStaticMarkup(
      <FinalReportPanel entries={entries} registration={registration} currentExperimentDate="2026-12-31" />,
    );
    expect(html).toBe('');
    expect(html).not.toContain('Fisher');
    expect(html).not.toContain('Holm');
  });

  it('renders share/download report controls only after the experiment has ended', async () => {
    const { entries, registration } = await registeredFixture();
    const html = renderToStaticMarkup(
      <FinalReportPanel entries={entries} registration={registration} currentExperimentDate="2027-01-01" />,
    );
    expect(html).toContain('FINAL REPORT');
    expect(html).toContain('共有シートを開く');
    expect(html).toContain('Markdownを保存');
    expect(html).toContain('確証パート');
    expect(html).toContain('探索パート');
  });
});
