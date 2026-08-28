import { useMemo, useState } from 'react';
import type { StoredLedgerEntry } from '../ledger/types';
import type { RegistrationPayload } from '../registration/types';
import { finalExperimentDate } from './ledgerProjection';
import { buildFinalReportModel } from './model';
import { renderFinalReportMarkdown } from './markdown';

export function FinalReportPanel({
  entries,
  registration,
  currentExperimentDate,
}: {
  entries: readonly StoredLedgerEntry[];
  registration: RegistrationPayload;
  currentExperimentDate: string;
}) {
  const experimentEnded = currentExperimentDate > finalExperimentDate(registration);
  const [message, setMessage] = useState<string | null>(null);
  const markdown = useMemo(() => {
    if (!experimentEnded) return null;
    return renderFinalReportMarkdown(buildFinalReportModel(entries, registration));
  }, [entries, experimentEnded, registration]);

  if (!experimentEnded || markdown === null) return null;
  const fileName = `intention-dice-${registration.experimentId}-final-report.md`;
  const downloadHref = `data:text/markdown;charset=utf-8,${encodeURIComponent(markdown)}`;

  async function share(): Promise<void> {
    try {
      if (typeof navigator.share === 'function') {
        await navigator.share({ title: 'Intention Dice 最終レポート', text: markdown! });
        setMessage('共有シートを開きました。');
        return;
      }
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(markdown!);
        setMessage('共有シート非対応のためMarkdownをコピーしました。');
        return;
      }
      setMessage('この環境では共有APIを利用できません。Markdownファイルを保存してください。');
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : String(cause));
    }
  }

  return (
    <section className="runtime-card final-report-card" aria-labelledby="final-report-heading">
      <p className="eyebrow">FINAL REPORT</p>
      <h2 id="final-report-heading">凍結したルールで、1年分を確定する。</h2>
      <p>確証パートと探索パートを分離したMarkdownです。同じledger exportからCLIでも再生成できます。</p>
      <div className="notification-controls">
        <button type="button" onClick={() => void share()}>共有シートを開く</button>
        <a href={downloadHref} download={fileName}>Markdownを保存</a>
      </div>
      {message && <p className="quiet-note">{message}</p>}
      <details>
        <summary>レポートをプレビュー</summary>
        <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{markdown}</pre>
      </details>
    </section>
  );
}
