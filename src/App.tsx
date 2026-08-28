import { type FormEvent, useEffect, useState } from 'react';
import {
  ExperimentDashboard,
  buildLayerADashboardModel,
  buildLayerCDashboardModel,
  type LayerADashboardModel,
  type LayerCDashboardModel,
} from './dashboard';
import { getApplicationLedgerService } from './ledger/appService';
import { verifyChain } from './ledger/verify';
import {
  DEFAULT_DECISION_RULE,
  RegistrationService,
  createSecureSeed,
  type BitsPerDraw,
  type RegistrationInput,
  type RegistrationPayload,
  type SessionsPerDay,
} from './registration';
import './App.css';

type AppState =
  | { kind: 'loading' }
  | { kind: 'ready' }
  | {
      kind: 'registered';
      payload: RegistrationPayload;
      genesisHash: string;
      dashboard: LayerADashboardModel;
      layerC: LayerCDashboardModel | null;
    }
  | { kind: 'error'; message: string };

const CONDITION_LABELS = ['P1 引くだけ', 'P2 意図書き', 'P3 アファメーション', 'P4 祈り', 'P5 フルコンボ'];

function defaultTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}

function parseRegistration(payloadJson: string): RegistrationPayload {
  const parsed: unknown = JSON.parse(payloadJson);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('registration payload is not an object');
  }
  const candidate = parsed as Partial<RegistrationPayload>;
  if (typeof candidate.experimentId !== 'string' || typeof candidate.startDate !== 'string') {
    throw new Error('registration payload is missing required identity fields');
  }
  return candidate as RegistrationPayload;
}

function App() {
  const [state, setState] = useState<AppState>({ kind: 'loading' });
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [experimentId, setExperimentId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [bitsPerDraw, setBitsPerDraw] = useState<BitsPerDraw>(1024);
  const [sessionsPerDay, setSessionsPerDay] = useState<SessionsPerDay>(1);
  const [dayBoundaryHour, setDayBoundaryHour] = useState(3);
  const [affirmationText, setAffirmationText] = useState('');
  const [predictions, setPredictions] = useState<[string, string, string, string, string]>(['', '', '', '', '']);
  const [timeZone, setTimeZone] = useState(defaultTimeZone);
  const [layerCEnabled, setLayerCEnabled] = useState(true);
  const [defaultDeadlineDays, setDefaultDeadlineDays] = useState<14 | 28 | 90>(28);
  const [notarize, setNotarize] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void getApplicationLedgerService()
      .then(async (ledger) => {
        const entries = await ledger.list();
        if (cancelled) return;
        if (entries.length === 0) {
          setState({ kind: 'ready' });
          return;
        }
        const verification = await verifyChain(entries);
        if (!verification.ok) {
          setState({ kind: 'error', message: `台帳検証エラー: ${verification.code}` });
          return;
        }
        const genesis = entries[0]!;
        const payload = parseRegistration(genesis.payloadJson);
        setState({
          kind: 'registered',
          payload,
          genesisHash: genesis.entryHash,
          dashboard: buildLayerADashboardModel(entries, payload),
          layerC: buildLayerCDashboardModel(entries, payload),
        });
      })
      .catch((error: unknown) => {
        if (!cancelled) setState({ kind: 'error', message: error instanceof Error ? error.message : String(error) });
      });
    return () => {
      cancelled = true;
    };
  }, []);

  function updatePrediction(index: number, value: string): void {
    setPredictions((current) => {
      const next = [...current] as [string, string, string, string, string];
      next[index] = value;
      return next;
    });
  }

  async function submitRegistration(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setSubmitting(true);
    setFormError(null);
    try {
      const input: RegistrationInput = {
        experimentId,
        startDate,
        bitsPerDraw,
        sessionsPerDay,
        dayBoundaryHour,
        affirmationText,
        predictionByCondition: predictions,
        timeZone,
        scheduleSeed: createSecureSeed(),
        targetSeed: createSecureSeed(),
        layerC: {
          enabled: layerCEnabled,
          defaultDeadlineDays,
          notarize,
        },
      };
      const ledger = await getApplicationLedgerService();
      const result = await new RegistrationService(ledger).register(input, new Date().toISOString());
      const entries = await ledger.list();
      setState({
        kind: 'registered',
        payload: result.payload,
        genesisHash: result.genesisHash,
        dashboard: buildLayerADashboardModel(entries, result.payload),
        layerC: buildLayerCDashboardModel(entries, result.payload),
      });
    } catch (error) {
      setFormError(error instanceof Error ? error.message : String(error));
    } finally {
      setSubmitting(false);
    }
  }

  if (state.kind === 'loading') {
    return <main className="registration-shell"><p>台帳を確認しています…</p></main>;
  }

  if (state.kind === 'error') {
    return (
      <main className="registration-shell">
        <section className="registration-card">
          <p className="eyebrow">Intention Dice</p>
          <h1>開始できませんでした</h1>
          <p className="error-text">{state.message}</p>
        </section>
      </main>
    );
  }

  if (state.kind === 'registered') {
    return (
      <ExperimentDashboard
        registration={state.payload}
        genesisHash={state.genesisHash}
        model={state.dashboard}
        layerCModel={state.layerC}
      />
    );
  }

  return (
    <main className="registration-shell">
      <form className="registration-card" onSubmit={(event) => void submitRegistration(event)}>
        <p className="eyebrow">ONE-YEAR PREREGISTRATION</p>
        <h1>実験を始める前に、条件を固定する</h1>
        <p className="lead">ここで確定した内容はgenesisへ記録され、この実験IDでは変更できません。</p>

        <fieldset>
          <legend>1. 実験の基本設定</legend>
          <label>Experiment ID<input required value={experimentId} onChange={(event) => setExperimentId(event.target.value)} placeholder="例: my-2026-experiment" /></label>
          <label>開始日<input required type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} /></label>
          <label>固定タイムゾーン<input required value={timeZone} onChange={(event) => setTimeZone(event.target.value)} /></label>
          <div className="form-row">
            <label>1回のbit数<select value={bitsPerDraw} onChange={(event) => setBitsPerDraw(Number(event.target.value) as BitsPerDraw)}><option value={1024}>1,024</option><option value={2048}>2,048</option><option value={4096}>4,096</option></select></label>
            <label>1日の回数<select value={sessionsPerDay} onChange={(event) => setSessionsPerDay(Number(event.target.value) as SessionsPerDay)}><option value={1}>1回</option><option value={2}>2回</option><option value={3}>3回</option></select></label>
            <label>日付境界<select value={dayBoundaryHour} onChange={(event) => setDayBoundaryHour(Number(event.target.value))}>{Array.from({ length: 24 }, (_, hour) => <option value={hour} key={hour}>{String(hour).padStart(2, '0')}:00</option>)}</select></label>
          </div>
        </fieldset>

        <fieldset>
          <legend>2. 実践と予言</legend>
          <label>アファメーション文<textarea required value={affirmationText} onChange={(event) => setAffirmationText(event.target.value)} rows={3} /></label>
          <p className="field-help">各条件について「1年後どうなると思うか」を、結果を見る前に固定します。</p>
          {CONDITION_LABELS.map((label, index) => (
            <label key={label}>{label}<input required value={predictions[index]} onChange={(event) => updatePrediction(index, event.target.value)} /></label>
          ))}
        </fieldset>

        <fieldset>
          <legend>3. Layer C</legend>
          <label className="check-row"><input type="checkbox" checked={layerCEnabled} onChange={(event) => setLayerCEnabled(event.target.checked)} />願いのランダム化比較を有効にする</label>
          <label>願いの既定締切<select value={defaultDeadlineDays} onChange={(event) => setDefaultDeadlineDays(Number(event.target.value) as 14 | 28 | 90)}><option value={14}>2週間</option><option value={28}>4週間</option><option value={90}>3ヶ月</option></select></label>
          <label className="check-row"><input type="checkbox" checked={notarize} onChange={(event) => setNotarize(event.target.checked)} />週次チェーンヘッド公証をONにする</label>
        </fieldset>

        <section className="decision-box">
          <strong>確証判定ルールも同時に固定</strong>
          <span>陽性: Holm補正後 p &lt; {DEFAULT_DECISION_RULE.pThresh} かつ BF₁₀ &gt; {DEFAULT_DECISION_RULE.bfPos}</span>
          <span>陰性証拠: BF₁₀ &lt; 1/{Math.round(1 / DEFAULT_DECISION_RULE.bfNeg)}</span>
        </section>

        {formError && <p className="error-text" role="alert">{formError}</p>}
        <label className="lock-confirm"><input required type="checkbox" />365日schedule・target seed・判定ルールを固定し、この実験IDでは変更しないことを確認しました。</label>
        <button className="lock-button" disabled={submitting} type="submit">{submitting ? '固定しています…' : 'この条件で実験をロック'}</button>
      </form>
    </main>
  );
}

export default App;
