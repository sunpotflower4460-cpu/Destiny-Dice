import { useState } from 'react';
import type { RegistrationPayload } from '../registration/types';
import { exportLedgerCsv, exportLedgerJson } from './export';
import type { StoredLedgerEntry } from './types';
import { verifyChain, type VerifyChainResult } from './verify';

export function ledgerExportFilename(experimentId: string, format: 'json' | 'csv'): string {
  return `intention-dice-${experimentId}-ledger.${format}`;
}

export function describeVerifyResult(result: VerifyChainResult): string {
  if (result.ok) {
    return `チェーンは改竄検知に通りました（${result.entries} entries / head ${result.headHash}）。ローカルhash chainは改竄検知可能（tamper-evident）であり、外部anchorなしでは完全改竄不能ではありません。`;
  }
  const seq = result.seq === undefined ? '' : ` / seq ${result.seq}`;
  return `検証に失敗しました: ${result.code}${seq} — ${result.message}`;
}

export function downloadTextFile(filename: string, mime: string, text: string): void {
  const blob = new Blob([text], { type: `${mime};charset=utf-8` });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.rel = 'noopener';
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export function LedgerExportPanel({
  entries,
  registration,
}: {
  entries: readonly StoredLedgerEntry[];
  registration: RegistrationPayload;
}) {
  const [busy, setBusy] = useState<'json' | 'csv' | 'verify' | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function exportFile(format: 'json' | 'csv'): Promise<void> {
    setBusy(format);
    setMessage(null);
    try {
      const body = format === 'json' ? exportLedgerJson(entries) : exportLedgerCsv(entries);
      const mime = format === 'json' ? 'application/json' : 'text/csv';
      downloadTextFile(ledgerExportFilename(registration.experimentId, format), mime, body);
      setMessage(`${format.toUpperCase()} を保存ダイアログへ渡しました。生ビット（bitsHex）を含む追記台帳そのものです。`);
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setBusy(null);
    }
  }

  async function runVerify(): Promise<void> {
    setBusy('verify');
    setMessage(null);
    try {
      const result = await verifyChain(entries);
      setMessage(describeVerifyResult(result));
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setBusy(null);
    }
  }

  return (
    <section className="runtime-card ledger-export-card" aria-labelledby="ledger-export-heading">
      <p className="eyebrow">LEDGER EXPORT</p>
      <h2 id="ledger-export-heading">台帳を、そのまま持ち出す。</h2>
      <p>
        JSON / CSV は追記専用チェーンの全エントリです。削除や編集の経路はありません。
        セッションとコントロールの生ビットは payload に含まれます。
      </p>
      <p className="quiet-note">
        いま {entries.length} 件。起動時にも連鎖を見ていますが、ここからいつでも再実行できます。
      </p>
      <div className="notification-controls">
        <button type="button" disabled={busy !== null} onClick={() => void runVerify()}>
          {busy === 'verify' ? '検証しています…' : '改竄検知を実行'}
        </button>
        <button type="button" disabled={busy !== null} onClick={() => void exportFile('json')}>
          {busy === 'json' ? '書き出しています…' : 'JSONを保存'}
        </button>
        <button type="button" disabled={busy !== null} onClick={() => void exportFile('csv')}>
          {busy === 'csv' ? '書き出しています…' : 'CSVを保存'}
        </button>
      </div>
      {message && <p className="quiet-note" role="status">{message}</p>}
    </section>
  );
}
