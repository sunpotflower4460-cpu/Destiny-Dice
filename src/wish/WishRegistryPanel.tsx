import { useMemo, useState, type FormEvent } from 'react';
import type {
  DueWishView,
  RegisteredWishResult,
  WishInfluence,
  WishLikelihood,
  WishPathway,
  WishRegistrationInput,
  WishRegistryProjection,
} from './index';
import './wish.css';

const DEADLINE_OPTIONS = [14, 28, 90] as const;

function addDaysIso(date: string, days: number): string {
  const timestamp = Date.parse(`${date}T00:00:00.000Z`);
  if (!Number.isFinite(timestamp)) throw new RangeError('currentExperimentDate must be a valid ISO date');
  return new Date(timestamp + days * 86_400_000).toISOString().slice(0, 10);
}

export type WishRegistryPanelProps = {
  currentExperimentDate: string;
  defaultDeadlineDays: 14 | 28 | 90;
  registry: WishRegistryProjection;
  dueJudgments: readonly DueWishView[];
  onRegister: (input: WishRegistrationInput) => Promise<RegisteredWishResult>;
  onWithdraw: (wishId: string) => Promise<void>;
  onJudge: (
    wishId: string,
    outcome: 'realized' | 'not_realized' | 'undecidable',
    pathway?: WishPathway,
  ) => Promise<void>;
  onRefresh: () => Promise<void>;
};

export function WishRegistryPanel({
  currentExperimentDate,
  defaultDeadlineDays,
  registry,
  dueJudgments,
  onRegister,
  onWithdraw,
  onJudge,
  onRefresh,
}: WishRegistryPanelProps) {
  const [text, setText] = useState('');
  const [deadlineDays, setDeadlineDays] = useState<14 | 28 | 90>(defaultDeadlineDays);
  const [likelihood, setLikelihood] = useState<WishLikelihood>(2);
  const [influence, setInfluence] = useState<WishInfluence>('mixed');
  const [submitting, setSubmitting] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const deadline = useMemo(() => addDaysIso(currentExperimentDate, deadlineDays), [currentExperimentDate, deadlineDays]);

  async function submit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setNotice(null);
    try {
      const result = await onRegister({ text, deadline, likelihood, influence });
      setText('');
      setNotice(
        result.assignment.arm === 'practice'
          ? `この願いは実践に選ばれました。毎日の願いタイムに現れます（source: ${result.assignment.rngSource}）。`
          : `この願いは封印されました。締切まで、そっと預かります（source: ${result.assignment.rngSource}）。`,
      );
      await onRefresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setSubmitting(false);
    }
  }

  async function runMutation(operation: () => Promise<void>): Promise<void> {
    setError(null);
    try {
      await operation();
      await onRefresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    }
  }

  return (
    <section className="wish-registry" aria-label="願いレジストリ">
      <header className="wish-heading">
        <div><p className="eyebrow">LAYER C / WISH REGISTRY</p><h2>小さな願いを、先に決めておく。</h2></div>
        <p>登録後は本文・締切・起きやすさ・影響可能性を編集できません。割付は登録直後に自動で決まります。</p>
      </header>

      <form className="wish-form" onSubmit={(event) => void submit(event)}>
        <label>願い（成功が一目で分かる文）
          <input required value={text} onChange={(event) => setText(event.target.value)} placeholder="例: 今月、探していた本が手に入る" />
        </label>
        {text.length > 0 && <p className="wish-hint">締切時に「実現した / しなかった」を迷わず選べる具体的な文にすると判定しやすくなります。</p>}
        <div className="wish-chip-group">
          <span>締切</span>
          {DEADLINE_OPTIONS.map((days) => (
            <button key={days} type="button" className={deadlineDays === days ? 'active' : ''} onClick={() => setDeadlineDays(days)}>
              {days === 14 ? '2週間' : days === 28 ? '1ヶ月' : '3ヶ月'}
            </button>
          ))}
          <small>{deadline}</small>
        </div>
        <div className="wish-form-grid">
          <label>起きやすさ
            <select value={likelihood} onChange={(event) => setLikelihood(Number(event.target.value) as WishLikelihood)}>
              <option value={1}>よく起きそう</option><option value={2}>五分五分</option><option value={3}>めったに</option>
            </select>
          </label>
          <label>自分で動かせる？
            <select value={influence} onChange={(event) => setInfluence(event.target.value as WishInfluence)}>
              <option value="self">動かせる</option><option value="mixed">半々</option><option value="external">動かせない</option>
            </select>
          </label>
        </div>
        <button className="wish-register-button" type="submit" disabled={submitting}>{submitting ? '登録・割付中…' : '願いを登録して自動割付'}</button>
        {notice && <p className="wish-notice">{notice}</p>}
        {error && <p className="error-text" role="alert">{error}</p>}
      </form>

      <div className="wish-summary-strip">
        <div><strong>{registry.practice.length}</strong><span>実践中</span></div>
        <div><strong>{registry.sealedCount}</strong><span>封印中（本文非表示）</span></div>
        <div><strong>{registry.dueCount}</strong><span>判定待ち</span></div>
        {registry.unassignedCount > 0 && <p>{registry.unassignedCount}件の割付が未完了です。再開時の自動復旧対象です。</p>}
      </div>

      <section className="wish-list" aria-labelledby="practice-wishes-heading">
        <h3 id="practice-wishes-heading">毎日の願いタイムに出る願い</h3>
        {registry.practice.length === 0 ? <p className="empty-state">現在表示できる実践群の願いはありません。</p> : registry.practice.map((wish) => (
          <article key={wish.wishId}>
            <div><strong>{wish.text}</strong><span>締切 {wish.deadline}</span></div>
            <button type="button" onClick={() => void runMutation(() => onWithdraw(wish.wishId))}>取り下げ</button>
          </article>
        ))}
        <p className="sealed-note">封印群の本文は締切前のこの画面には返されません。これは盲検ではなく、接触を減らすための非表示です。</p>
      </section>

      <section className="wish-due" aria-labelledby="due-heading">
        <h3 id="due-heading">判定待ち</h3>
        {dueJudgments.length === 0 ? <p className="empty-state">締切を迎えた願いはありません。</p> : dueJudgments.map((wish) => (
          <DueJudgmentCard key={wish.wishId} wish={wish} onJudge={(outcome, pathway) => runMutation(() => onJudge(wish.wishId, outcome, pathway))} />
        ))}
      </section>
    </section>
  );
}

function DueJudgmentCard({
  wish,
  onJudge,
}: {
  wish: DueWishView;
  onJudge: (outcome: 'realized' | 'not_realized' | 'undecidable', pathway?: WishPathway) => Promise<void>;
}) {
  const [realized, setRealized] = useState(false);
  return (
    <article className="due-card">
      <div className="due-meta"><span>締切 {wish.deadline}</span><span>{wish.arm === 'sealed' ? '封印群・締切到来で開封' : '実践群'}</span></div>
      <strong>{wish.text}</strong>
      {!realized ? (
        <div className="judgment-actions">
          <button type="button" onClick={() => setRealized(true)}>実現した ○</button>
          <button type="button" onClick={() => void onJudge('not_realized')}>しなかった ×</button>
          <button type="button" onClick={() => void onJudge('undecidable')}>判定不能</button>
        </div>
      ) : (
        <div className="pathway-actions">
          <span>どの経路でしたか？</span>
          <button type="button" onClick={() => void onJudge('realized', 'own_action')}>自分の行動</button>
          <button type="button" onClick={() => void onJudge('realized', 'other_person')}>他人</button>
          <button type="button" onClick={() => void onJudge('realized', 'chance_encounter')}>偶然の出会い</button>
          <button type="button" onClick={() => void onJudge('realized', 'unknown')}>不明</button>
        </div>
      )}
    </article>
  );
}
