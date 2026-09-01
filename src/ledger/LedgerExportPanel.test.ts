import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { RegistrationService } from '../registration/service';
import type { RegistrationInput } from '../registration/types';
import {
  LedgerExportPanel,
  describeVerifyResult,
  ledgerExportFilename,
} from './LedgerExportPanel';
import { MemoryLedgerStore } from './memoryStore';
import { LedgerService } from './service';
import { verifyChain } from './verify';

const input: RegistrationInput = {
  experimentId: 'p11-export-panel',
  startDate: '2026-01-01',
  bitsPerDraw: 1024,
  sessionsPerDay: 1,
  dayBoundaryHour: 3,
  affirmationText: '私は落ち着いて今日の的に意図を向ける',
  predictionByCondition: ['P1', 'P2', 'P3', 'P4', 'P5'],
  timeZone: 'Asia/Tokyo',
  scheduleSeed: 'p11-export-schedule',
  targetSeed: 'p11-export-target',
  layerC: { enabled: true, defaultDeadlineDays: 28, notarize: false },
};

describe('LedgerExportPanel', () => {
  it('names JSON and CSV files from the frozen experimentId', () => {
    expect(ledgerExportFilename('p11-export-panel', 'json')).toBe('intention-dice-p11-export-panel-ledger.json');
    expect(ledgerExportFilename('p11-export-panel', 'csv')).toBe('intention-dice-p11-export-panel-ledger.csv');
  });

  it('describes a passing verify as tamper-evident, not tamper-proof', async () => {
    const ledger = new LedgerService(new MemoryLedgerStore());
    await new RegistrationService(ledger).register(input, '2025-12-31T15:00:00.000Z');
    const result = await verifyChain(await ledger.list());
    expect(result.ok).toBe(true);
    const text = describeVerifyResult(result);
    expect(text).toContain('改竄検知に通りました');
    expect(text).toContain('tamper-evident');
    expect(text).toContain('完全改竄不能ではありません');
    expect(text).not.toContain('完全改竄不能です');
  });

  it('renders on-demand verify and JSON/CSV export controls', async () => {
    const ledger = new LedgerService(new MemoryLedgerStore());
    const registration = await new RegistrationService(ledger).register(input, '2025-12-31T15:00:00.000Z');
    const html = renderToStaticMarkup(
      createElement(LedgerExportPanel, {
        entries: await ledger.list(),
        registration: registration.payload,
      }),
    );
    expect(html).toContain('LEDGER EXPORT');
    expect(html).toContain('改竄検知を実行');
    expect(html).toContain('JSONを保存');
    expect(html).toContain('CSVを保存');
    expect(html).toContain('生ビット');
    expect(html).toContain('いま 1 件');
  });
});
