import { useEffect, useMemo, useRef, useState } from 'react';
import { WishMoment, type WishMomentProjection } from '../wish';
import { buildRitualRecord } from './ritual';
import type { SessionContextInput, SessionDraft, SessionPlan, SessionResult } from './types';
import './SessionFlow.css';

const CONDITION_NAMES = ['引くだけ', '意図書き', 'アファメーション', '祈り', 'フルコンボ'] as const;

function timerRequirement(condition: SessionPlan['condition']): number {
  if (condition === 0) return 60;
  if (condition === 2) return 300;
  if (condition === 3) return 180;
  if (condition === 4) return 480;
  return 0;
}

function needsText(condition: SessionPlan['condition']): boolean {
  return condition === 1 || condition === 4;
}

export type SessionFlowProps = {
  plan: SessionPlan;
  affirmationText: string;
  startedAt: string;
  context: SessionContextInput;
  onDraw: (draft: SessionDraft) => Promise<SessionResult>;
  loadWishMoment?: (experimentDate: string) => Promise<WishMomentProjection>;
  recordWishMoment?: (experimentDate: string, wishIdsShown: readonly string[], seconds: number) => Promise<void>;
  onFinish?: () => void | Promise<void>;
  finishLabel?: string;
};

type Stage = 'moodPre' | 'ritual' | 'moodPost' | 'prediction' | 'draw' | 'result' | 'wishMoment' | 'feedback';

export function SessionFlow({
  plan,
  affirmationText,
  startedAt,
  context,
  onDraw,
  loadWishMoment,
  recordWishMoment,
  onFinish,
  finishLabel = '完了',
}: SessionFlowProps) {
  const [stage, setStage] = useState<Stage>('moodPre');
  const [moodPre, setMoodPre] = useState({ v: 5, e: 5 });
  const [moodPost, setMoodPost] = useState({ v: 5, e: 5 });
  const [ritualText, setRitualText] = useState('');
  const [ritualSeconds, setRitualSeconds] = useState(0);
  const [timerRunning, setTimerRunning] = useState(false);
  const [confidence, setConfidence] = useState(50);
  const [prophecyText, setProphecyText] = useState('');
  const [holding, setHolding] = useState(false);
  const [drawing, setDrawing] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [result, setResult] = useState<SessionResult | null>(null);
  const [wishMoment, setWishMoment] = useState<WishMomentProjection | null>(null);
  const [loadingWishMoment, setLoadingWishMoment] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const holdTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!timerRunning) return;
    const timer = setInterval(() => setRitualSeconds((seconds) => seconds + 1), 1000);
    return () => clearInterval(timer);
  }, [timerRunning]);

  useEffect(() => () => {
    if (holdTimer.current) clearTimeout(holdTimer.current);
  }, []);

  const ritual = useMemo(
    () => buildRitualRecord(plan.condition, { seconds: ritualSeconds, ...(needsText(plan.condition) ? { text: ritualText } : {}) }),
    [plan.condition, ritualSeconds, ritualText],
  );
  const timerRequired = timerRequirement(plan.condition);
  const ritualReady = ritual.valid;

  function beginHold(): void {
    if (drawing || result) return;
    setHolding(true);
    holdTimer.current = setTimeout(() => {
      holdTimer.current = null;
      setHolding(false);
      void commitAndDraw();
    }, 3000);
  }

  function cancelHold(): void {
    if (holdTimer.current) clearTimeout(holdTimer.current);
    holdTimer.current = null;
    setHolding(false);
  }

  async function commitAndDraw(): Promise<void> {
    setDrawing(true);
    setError(null);
    try {
      const completed = await onDraw({
        experimentDate: plan.experimentDate,
        seqInDay: plan.seqInDay,
        moodPre,
        ritual: { seconds: ritualSeconds, ...(needsText(plan.condition) ? { text: ritualText } : {}) },
        moodPost,
        confidence,
        ...(prophecyText.length === 0 ? {} : { prophecyText }),
        context,
        startedAt,
      });
      setResult(completed);
      setStage('result');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setDrawing(false);
    }
  }

  async function continueAfterResult(): Promise<void> {
    if (!loadWishMoment || !recordWishMoment) {
      setStage('feedback');
      return;
    }
    setLoadingWishMoment(true);
    setError(null);
    try {
      const projection = await loadWishMoment(plan.experimentDate);
      if (projection.wishes.length === 0) {
        setStage('feedback');
        return;
      }
      setWishMoment(projection);
      setStage('wishMoment');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setLoadingWishMoment(false);
    }
  }

  async function completeWishMoment(seconds: number): Promise<void> {
    if (!wishMoment || !recordWishMoment) throw new Error('wish moment is not available');
    await recordWishMoment(
      plan.experimentDate,
      wishMoment.wishes.map((wish) => wish.wishId),
      seconds,
    );
    setStage('feedback');
  }

  async function finishSession(): Promise<void> {
    if (!onFinish || finishing) return;
    setFinishing(true);
    setError(null);
    try {
      await onFinish();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setFinishing(false);
    }
  }

  return (
    <section className="session-flow" aria-label="本日の実験セッション">
      <header className="session-header">
        <div>
          <p className="session-eyebrow">DAY {plan.dayIndex + 1} / SESSION {plan.seqInDay}</p>
          <h2>{CONDITION_NAMES[plan.condition]}</h2>
        </div>
        <div className="target-badge"><span>TARGET</span><strong>{plan.targetDir === 1 ? 'HIGH' : 'LOW'}</strong></div>
      </header>

      {stage === 'moodPre' && (
        <div className="session-step">
          <h3>いまの状態</h3>
          <p>結果を見る前の気分とエネルギーを記録します。</p>
          <MoodControls value={moodPre} onChange={setMoodPre} />
          <button type="button" onClick={() => setStage('ritual')}>この状態で実践へ</button>
        </div>
      )}

      {stage === 'ritual' && (
        <div className="session-step">
          <h3>今日の実践</h3>
          {plan.condition === 0 && <p>60秒、静かに待機します。</p>}
          {plan.condition === 1 && <p>「今日の的が実現した状態」を30文字以上で書きます。</p>}
          {plan.condition === 2 && <p>「{affirmationText}」を見ながら5分間続けます。</p>}
          {plan.condition === 3 && <p>今日の的に向けて3分間祈ります。</p>}
          {plan.condition === 4 && <p>30文字以上の意図書きの後、アファメーション＋祈りを合計8分続けます。</p>}

          {needsText(plan.condition) && (
            <label>意図テキスト<textarea rows={4} value={ritualText} onChange={(event) => setRitualText(event.target.value)} /></label>
          )}

          <div className="ritual-timer">
            <strong>{ritualSeconds}s</strong>
            {timerRequired > 0 && <span>/ {timerRequired}s</span>}
            <button type="button" onClick={() => setTimerRunning((running) => !running)}>
              {timerRunning ? '一時停止' : 'タイマー開始'}
            </button>
          </div>
          <p className={ritualReady ? 'valid-note' : 'quiet-note'}>
            {ritualReady ? '実践条件を満たしました。' : `現在は未完了として記録されます${needsText(plan.condition) ? `（${ritualText.length}/30文字）` : ''}。`}
          </p>
          <button type="button" disabled={!ritualReady} onClick={() => { setTimerRunning(false); setStage('moodPost'); }}>実践を完了</button>
        </div>
      )}

      {stage === 'moodPost' && (
        <div className="session-step">
          <h3>実践直後の状態</h3>
          <p>まだ抽選結果は見ません。実践そのものの前後差を記録します。</p>
          <MoodControls value={moodPost} onChange={setMoodPost} />
          <button type="button" onClick={() => setStage('prediction')}>手応えを記録</button>
        </div>
      )}

      {stage === 'prediction' && (
        <div className="session-step">
          <h3>抽選前の手応え</h3>
          <label>今日、的に届く確信 <strong>{confidence}</strong>/100
            <input type="range" min={0} max={100} value={confidence} onChange={(event) => setConfidence(Number(event.target.value))} />
          </label>
          <label>一言予言（任意）<input value={prophecyText} onChange={(event) => setProphecyText(event.target.value)} /></label>
          <p className="quiet-note">予言の値はこの画面ではまだ台帳へ書いていません。3秒長押しの抽選開始時に、乱数取得より先へ確定します。</p>
          <button type="button" onClick={() => setStage('draw')}>抽選の準備へ</button>
        </div>
      )}

      {stage === 'draw' && (
        <div className="session-step draw-step">
          <h3>{plan.targetDir === 1 ? 'HIGH' : 'LOW'} に意図を向ける</h3>
          <p>ボタンを3秒間押し続けると、先に予言を台帳へ確定し、その後だけ抽選します。</p>
          <button
            className={holding ? 'hold-button is-holding' : 'hold-button'}
            type="button"
            disabled={drawing}
            onPointerDown={beginHold}
            onPointerUp={cancelHold}
            onPointerLeave={cancelHold}
            onPointerCancel={cancelHold}
          >
            {drawing ? '取得中…' : holding ? 'そのまま…' : '3秒長押しで抽選'}
          </button>
          {error && <p className="session-error">{error}</p>}
        </div>
      )}

      {stage === 'result' && result && (
        <div className="session-step result-step">
          <p className="session-eyebrow">RESULT COMMITTED</p>
          <h3>{result.payload.hits.toLocaleString()} / {result.payload.nBits.toLocaleString()} hits</h3>
          <p className="result-z">z = {result.payload.z.toFixed(3)}</p>
          <p>偶然なら hit率 50%（期待 {Math.round(result.payload.nBits / 2).toLocaleString()} hits）</p>
          <p>RNG source: <strong>{result.payload.rngSource}</strong></p>
          <button type="button" disabled={loadingWishMoment} onClick={() => void continueAfterResult()}>
            {loadingWishMoment ? '願いタイムを確認中…' : '次へ'}
          </button>
          {error && <p className="session-error">{error}</p>}
        </div>
      )}

      {stage === 'wishMoment' && wishMoment && (
        <div className="session-step"><WishMoment projection={wishMoment} onComplete={completeWishMoment} /></div>
      )}

      {stage === 'feedback' && result && (
        <div className="session-step result-step">
          <p className="session-eyebrow">SESSION COMPLETE</p>
          {result.payload.z >= 3 && <p className="signal-label">ミラクル</p>}
          {result.payload.z < 3 && Math.abs(result.payload.z) >= 2 && <p className="signal-label">共鳴</p>}
          {Math.abs(result.payload.z) < 2 && <p>今日の記録を台帳へ保存しました。</p>}
          <p className="quiet-note">prediction seq {result.payload.predictionSeq} → session seq {result.sessionEntry.seq}</p>
          {onFinish && (
            <button type="button" disabled={finishing} onClick={() => void finishSession()}>
              {finishing ? '更新中…' : finishLabel}
            </button>
          )}
          {error && <p className="session-error">{error}</p>}
        </div>
      )}
    </section>
  );
}

function MoodControls({
  value,
  onChange,
}: {
  value: { v: number; e: number };
  onChange: (value: { v: number; e: number }) => void;
}) {
  return (
    <div className="mood-controls">
      <label>気分 <strong>{value.v}</strong>/10
        <input type="range" min={1} max={10} value={value.v} onChange={(event) => onChange({ ...value, v: Number(event.target.value) })} />
      </label>
      <label>エネルギー <strong>{value.e}</strong>/10
        <input type="range" min={1} max={10} value={value.e} onChange={(event) => onChange({ ...value, e: Number(event.target.value) })} />
      </label>
    </div>
  );
}
