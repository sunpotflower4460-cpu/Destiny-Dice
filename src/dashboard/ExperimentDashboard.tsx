import { useEffect, useRef, useState } from 'react';
import type { RegistrationPayload } from '../registration/types';
import { EXPLORATORY_WARNING, type CumulativeDeviationPoint, type LayerCArmSummary } from '../stats';
import type { LayerCDashboardModel } from './layerCModel';
import type { LayerADashboardModel } from './model';
import './dashboard.css';

export type DashboardTab = 'home' | 'lab';

const SOURCE_LABELS = {
  anu: 'ANU quantum',
  randomorg: 'RANDOM.ORG',
  local: 'Local crypto',
} as const;

const PATHWAY_LABELS = {
  own_action: '自分の行動',
  other_person: '他人',
  chance_encounter: '偶然の出会い',
  unknown: '不明',
} as const;

const LIKELIHOOD_LABELS = ['よく起きそう', '五分五分', 'めったに'] as const;
const INFLUENCE_LABELS = {
  self: '自分で動かせる',
  mixed: '半々',
  external: '自分では動かせない',
} as const;

const MIRACLE_NULL_RATE = 0.0013498980316301;

function formatPercent(value: number | null, digits = 2): string {
  return value === null ? '—' : `${(value * 100).toFixed(digits)}%`;
}

function formatSignedPercent(value: number | null, digits = 1): string {
  if (value === null) return '—';
  const points = value * 100;
  return `${points >= 0 ? '+' : ''}${points.toFixed(digits)}pp`;
}

function formatNumber(value: number | null, digits = 2): string {
  if (value === null) return '—';
  if (!Number.isFinite(value)) return value > 0 ? '∞' : '—';
  return value.toFixed(digits);
}

function CumulativeDeviationCanvas({ points }: { points: readonly CumulativeDeviationPoint[] }) {
  const ref = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = ref.current;
    if (!canvas) return;
    const context = canvas.getContext('2d');
    if (!context) return;

    const width = canvas.width;
    const height = canvas.height;
    const left = 46;
    const right = 18;
    const top = 18;
    const bottom = 34;
    const plotWidth = width - left - right;
    const plotHeight = height - top - bottom;
    context.clearRect(0, 0, width, height);

    if (points.length === 0) {
      context.font = '16px system-ui, sans-serif';
      context.textAlign = 'center';
      context.fillText('ANUの有効セッションが記録されると、ここに累積偏差が現れます。', width / 2, height / 2);
      return;
    }

    const maxBits = points.at(-1)!.cumulativeBits;
    const maxMagnitude = Math.max(
      1,
      ...points.flatMap((point) => [Math.abs(point.deviation), point.envelope95]),
    );
    const x = (bits: number) => left + (bits / maxBits) * plotWidth;
    const y = (value: number) => top + plotHeight / 2 - (value / maxMagnitude) * (plotHeight * 0.45);

    context.lineWidth = 1;
    context.strokeStyle = 'rgba(31, 45, 39, 0.22)';
    context.beginPath();
    context.moveTo(left, y(0));
    context.lineTo(width - right, y(0));
    context.stroke();

    for (const sign of [-1, 1] as const) {
      context.setLineDash([6, 6]);
      context.strokeStyle = 'rgba(79, 102, 91, 0.5)';
      context.beginPath();
      points.forEach((point, index) => {
        const px = x(point.cumulativeBits);
        const py = y(sign * point.envelope95);
        if (index === 0) context.moveTo(px, py);
        else context.lineTo(px, py);
      });
      context.stroke();
    }

    context.setLineDash([]);
    context.lineWidth = 2.5;
    context.strokeStyle = '#203f35';
    context.beginPath();
    points.forEach((point, index) => {
      const px = x(point.cumulativeBits);
      const py = y(point.deviation);
      if (index === 0) context.moveTo(px, py);
      else context.lineTo(px, py);
    });
    context.stroke();

    context.fillStyle = '#66756d';
    context.font = '12px system-ui, sans-serif';
    context.textAlign = 'left';
    context.fillText('0 = 偶然中心', left, height - 10);
    context.textAlign = 'right';
    context.fillText(`${maxBits.toLocaleString()} bits`, width - right, height - 10);
  }, [points]);

  return <canvas className="deviation-canvas" ref={ref} width={900} height={300} aria-label="ANU有効セッションの累積偏差グラフ" />;
}

function HomePanel({ registration, genesisHash }: { registration: RegistrationPayload; genesisHash: string }) {
  return (
    <section className="zen-home" aria-labelledby="home-heading">
      <p className="eyebrow">INTENTION DICE</p>
      <h1 id="home-heading">静かに、続ける。</h1>
      <p className="zen-copy">結果を追いかける場所と、今日の実験をする場所を分けています。数字を見たい時だけ「ラボ」を開いてください。</p>
      <div className="zen-orb" aria-hidden="true"><span /></div>
      <dl className="zen-meta">
        <div><dt>Experiment</dt><dd>{registration.experimentId}</dd></div>
        <div><dt>期間</dt><dd>{registration.startDate} から {registration.days}日</dd></div>
        <div><dt>固定時刻系</dt><dd>{registration.timeZone} / {String(registration.dayBoundaryHour).padStart(2, '0')}:00境界</dd></div>
      </dl>
      <details className="audit-details">
        <summary>監査情報</summary>
        <div className="hash-box"><span>Genesis hash</span><code>{genesisHash}</code></div>
        <p className="quiet-note">未来のcondition / target scheduleは表示しません。台帳は改竄検知可能（tamper-evident）です。</p>
      </details>
    </section>
  );
}

function SourceSummary({ model }: { model: LayerADashboardModel }) {
  return (
    <section className="source-strip" aria-label="乱数ソース内訳">
      <div>
        <strong>{model.primarySessions}</strong>
        <span>ANU主要セッション</span>
      </div>
      <div>
        <strong>{model.sourceCounts.randomorg.sessions + model.sourceCounts.local.sessions}</strong>
        <span>fallback記録</span>
      </div>
      <div>
        <strong>{model.ritualInvalidSessions}</strong>
        <span>ritual無効</span>
      </div>
      <p>主要カードは <b>ANU + ritual valid</b> のみ。fallbackは消さず、ここ・QC・監査に残します。</p>
    </section>
  );
}

function ConditionCards({ model }: { model: LayerADashboardModel }) {
  return (
    <section className="lab-section" aria-labelledby="conditions-heading">
      <div className="section-heading">
        <div>
          <p className="eyebrow">LIVE / PRIMARY QUANTUM SAMPLE</p>
          <h2 id="conditions-heading">条件別の途中経過</h2>
        </div>
        <span className="no-peek-badge">p値は最終日まで非表示</span>
      </div>
      <div className="condition-grid">
        {model.conditionCards.map((card) => (
          <article className="condition-card" key={card.condition}>
            <div className="condition-title"><span>P{card.condition + 1}</span><h3>{card.label.replace(/^P\d\s/, '')}</h3></div>
            {card.nBits === 0 ? (
              <p className="empty-state">ANUの有効データはまだありません。</p>
            ) : (
              <>
                <div className="hero-stat">
                  <strong>{formatPercent(card.hitRate, 3)}</strong>
                  <span>hit率</span>
                  <small>偶然なら {formatPercent(card.chanceHitRate, 1)}</small>
                </div>
                <dl className="stat-grid">
                  <div><dt>hits</dt><dd>{card.hits.toLocaleString()}</dd><small>偶然期待 {card.expectedHits.toLocaleString()}</small></div>
                  <div><dt>z</dt><dd>{formatNumber(card.z, 3)}</dd><small>偶然中心 0</small></div>
                  <div><dt>95% CI</dt><dd>{card.ci95 ? `${formatPercent(card.ci95.lower)}–${formatPercent(card.ci95.upper)}` : '—'}</dd><small>偶然基準 50%</small></div>
                  <div><dt>BF₁₀</dt><dd>{formatNumber(card.bf10, 2)}</dd><small>偶然モデルとの中立点 1</small></div>
                </dl>
                <p className="sample-note">{card.sessions} sessions / {card.nBits.toLocaleString()} bits</p>
              </>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

function ControlPanel({ model }: { model: LayerADashboardModel }) {
  const qc = model.controlQc;
  return (
    <section className="lab-section control-panel" aria-labelledby="control-heading">
      <div className="section-heading">
        <div><p className="eyebrow">MACHINE CONTROL / C0</p><h2 id="control-heading">乱数そのもののQC</h2></div>
        <p>人の意図を入れない機械抽選です。</p>
      </div>
      {qc.nBits === 0 ? <p className="empty-state">control記録はまだありません。</p> : (
        <>
          <div className="control-overall">
            <strong>{formatPercent(qc.hitRate, 3)}</strong>
            <span>1の割合</span>
            <small>偶然なら 50.0% / z中心 0</small>
          </div>
          <div className="source-qc-grid">
            {(['anu', 'randomorg', 'local'] as const).map((source) => {
              const item = qc.bySource[source];
              return (
                <div key={source}>
                  <b>{SOURCE_LABELS[source]}</b>
                  <span>{item.sessions} sessions / {item.nBits.toLocaleString()} bits</span>
                  <span>{item.hitRate === null ? 'データなし' : `${formatPercent(item.hitRate, 3)}（偶然 50.0%）`}</span>
                </div>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}

function MiracleLog({ model }: { model: LayerADashboardModel }) {
  const expected = model.validSessions * MIRACLE_NULL_RATE;
  return (
    <section className="lab-section" aria-labelledby="miracle-heading">
      <div className="section-heading">
        <div><p className="eyebrow">MIRACLE LOG</p><h2 id="miracle-heading">的方向 z ≥ +3 の日</h2></div>
        <p>{model.miracles.length}件 <span className="chance-copy">／ 偶然なら同じ有効session数で約 {expected.toFixed(2)}件</span></p>
      </div>
      {model.miracles.length === 0 ? <p className="empty-state">まだミラクル基準を越えた記録はありません。ゼロもそのまま記録です。</p> : (
        <div className="miracle-list">
          {model.miracles.map((item) => (
            <article key={`${item.date}-${item.seqInDay}`}>
              <div><b>{item.date}</b><span>{item.conditionLabel} / #{item.seqInDay}</span></div>
              <div><strong>z {formatNumber(item.z, 2)}</strong><small>{item.hits.toLocaleString()} hits / 偶然期待 {item.expectedHits.toLocaleString()}</small></div>
              <span className={item.primaryQuantumSample ? 'source-pill source-pill--anu' : 'source-pill'}>{SOURCE_LABELS[item.rngSource]}{item.primaryQuantumSample ? '・主要sample' : '・fallback'}</span>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function ExperimentCalendar({ model }: { model: LayerADashboardModel }) {
  return (
    <section className="lab-section" aria-labelledby="calendar-heading">
      <div className="section-heading">
        <div><p className="eyebrow">365-DAY TRACE</p><h2 id="calendar-heading">記録カレンダー</h2></div>
        <p className="calendar-note">記録済みの日だけ着色。空欄は未記録または未来で、未来条件は表示しません。</p>
      </div>
      <div className="calendar-grid" role="grid" aria-label="365日の実験記録">
        {model.calendar.map((day) => {
          const fallback = day.rngSources.some((source) => source !== 'anu');
          const classes = ['calendar-day'];
          if (day.recordedSessions > 0) classes.push('calendar-day--recorded');
          if (day.resonance) classes.push('calendar-day--resonance');
          if (day.miracle) classes.push('calendar-day--miracle');
          if (fallback) classes.push('calendar-day--fallback');
          const detail = day.recordedSessions === 0
            ? `${day.date}: 未記録または未来`
            : `${day.date}: ${day.conditionLabel}, ${day.recordedSessions} session${day.miracle ? ', miracle' : day.resonance ? ', resonance' : ''}${fallback ? ', fallback含む' : ''}`;
          return <span className={classes.join(' ')} key={day.date} role="gridcell" aria-label={detail} title={detail} />;
        })}
      </div>
      <div className="calendar-legend" aria-label="カレンダー凡例">
        <span><i className="legend-dot legend-dot--recorded" />記録あり</span>
        <span><i className="legend-dot legend-dot--resonance" />共鳴 |z|≥2</span>
        <span><i className="legend-dot legend-dot--miracle" />ミラクル z≥3</span>
        <span><i className="legend-dot legend-dot--fallback" />fallback含む</span>
      </div>
    </section>
  );
}

function LayerCArmCard({ summary, baselineRate }: { summary: LayerCArmSummary; baselineRate: number | null }) {
  const isBaseline = summary.arm === 'sealed';
  return (
    <article className="condition-card">
      <div className="condition-title"><span>{isBaseline ? 'C0' : 'C1'}</span><h3>{isBaseline ? '封印群（ベースライン）' : '実践群'}</h3></div>
      {summary.n === 0 ? <p className="empty-state">判定済みの願いはまだありません。</p> : (
        <>
          <div className="hero-stat">
            <strong>{formatPercent(summary.realizationRate, 1)}</strong>
            <span>実現率</span>
            <small>{isBaseline ? 'あなたの反実仮想ベースライン' : `封印群ベースライン ${formatPercent(baselineRate, 1)}`}</small>
          </div>
          <dl className="stat-grid">
            <div><dt>実現</dt><dd>{summary.realized}/{summary.n}</dd><small>未実現 {summary.notRealized}</small></div>
            <div><dt>95% CI</dt><dd>{summary.ci95 ? `${formatPercent(summary.ci95.lower)}–${formatPercent(summary.ci95.upper)}` : '—'}</dd><small>自己判定・非盲検</small></div>
            <div><dt>取り下げ</dt><dd>{summary.withdrawn}</dd><small>主要解析では未実現</small></div>
            <div><dt>判定不能</dt><dd>{summary.undecidable}</dd><small>主要解析では未実現</small></div>
          </dl>
        </>
      )}
    </article>
  );
}

function LayerCPanel({ model }: { model: LayerCDashboardModel }) {
  const primary = model.comparison;
  const sensitivity = model.sensitivityExcludingUndecidable;
  return (
    <section className="lab-section" aria-labelledby="layer-c-heading">
      <div className="section-heading">
        <div><p className="eyebrow">LAB / LAYER C</p><h2 id="layer-c-heading">願いのランダム化比較</h2></div>
        <span className="no-peek-badge">Fisher p値は最終解析まで非表示</span>
      </div>
      <p><strong>封印群があなたのベースライン。</strong> 実践した願いと、実践UIから隔離した願いの実現率を同じ物差しで比べます。</p>
      <p className="quiet-note">証拠等級 ★★ — ランダム化比較ですが、実現判定は非盲検の自己判定です。</p>

      <section className="source-strip" aria-label="Layer C進捗">
        <div><strong>{model.totalWishes}</strong><span>登録願い</span></div>
        <div><strong>{model.judgedWishes}</strong><span>判定済み</span></div>
        <div><strong>{model.awaitingJudgment}</strong><span>未判定・締切前を含む</span></div>
        <p>未割付 {model.unassignedWishes}件。未割付はpractice/sealed集計へ入りません。</p>
      </section>

      <div className="condition-grid">
        <LayerCArmCard summary={primary.practice} baselineRate={primary.sealed.realizationRate} />
        <LayerCArmCard summary={primary.sealed} baselineRate={primary.sealed.realizationRate} />
      </div>

      <section className="source-strip" aria-label="Layer C比較要約">
        <div><strong>{formatSignedPercent(primary.riskDifference)}</strong><span>実践 − 封印</span></div>
        <div><strong>{formatNumber(primary.bf10, 2)}</strong><span>BF₁₀</span></div>
        <div><strong>1</strong><span>BFの中立点</span></div>
        <p>途中経過のBFは表示しますが、確証用Fisher p値と最終判定は実験終了時まで開示しません。</p>
      </section>

      <details className="audit-details">
        <summary>副次感度分析：判定不能だけを除外</summary>
        <p>取り下げは未実現のまま維持し、undecidableだけを除外した副次結果です。主要結果の代わりにはしません。</p>
        <div className="source-qc-grid">
          <div><b>実践群</b><span>{sensitivity.practice.realized}/{sensitivity.practice.n}</span><span>{formatPercent(sensitivity.practice.realizationRate, 1)}</span></div>
          <div><b>封印群</b><span>{sensitivity.sealed.realized}/{sensitivity.sealed.n}</span><span>{formatPercent(sensitivity.sealed.realizationRate, 1)}</span></div>
          <div><b>差</b><span>{formatSignedPercent(sensitivity.riskDifference)}</span><span>BF₁₀ {formatNumber(sensitivity.bf10, 2)}</span></div>
        </div>
      </details>

      <section aria-labelledby="assignment-source-heading">
        <div className="section-heading"><div><p className="eyebrow">ASSIGNMENT RNG</p><h3 id="assignment-source-heading">割付source</h3></div><p>fallbackもRCTから除外せず、実際のsourceをそのまま表示します。</p></div>
        <div className="source-qc-grid">
          {(['anu', 'randomorg', 'local'] as const).map((source) => {
            const count = model.assignmentSourceCounts[source];
            return <div key={source}><b>{SOURCE_LABELS[source]}</b><span>{count.total} wishes</span><span>実践 {count.practice} / 封印 {count.sealed}</span></div>;
          })}
        </div>
      </section>

      <section aria-labelledby="layer-c-strata-heading">
        <div className="section-heading"><div><p className="eyebrow">EXPLORATORY</p><h3 id="layer-c-strata-heading">層別の効きめ</h3></div></div>
        <p className="quiet-note">{EXPLORATORY_WARNING}</p>
        <div className="source-qc-grid">
          {model.strata.likelihood.map((item, index) => (
            <div key={item.key}><b>{LIKELIHOOD_LABELS[index]}</b><span>実践 {formatPercent(item.comparison.practice.realizationRate, 1)} / 封印 {formatPercent(item.comparison.sealed.realizationRate, 1)}</span><span>差 {formatSignedPercent(item.comparison.riskDifference)}</span></div>
          ))}
          {model.strata.influence.map((item) => {
            const key = item.key.replace('influence-', '') as keyof typeof INFLUENCE_LABELS;
            return <div key={item.key}><b>{INFLUENCE_LABELS[key]}</b><span>実践 {formatPercent(item.comparison.practice.realizationRate, 1)} / 封印 {formatPercent(item.comparison.sealed.realizationRate, 1)}</span><span>差 {formatSignedPercent(item.comparison.riskDifference)}</span></div>;
          })}
        </div>
      </section>

      <section aria-labelledby="pathways-heading">
        <div className="section-heading"><div><p className="eyebrow">EXPLORATORY / PATHWAYS</p><h3 id="pathways-heading">実現した願いの経路</h3></div><p>実現例だけの記述分布です。</p></div>
        <div className="source-qc-grid">
          {model.pathways.map((item) => (
            <div key={item.pathway}><b>{PATHWAY_LABELS[item.pathway]}</b><span>実践 {item.practice} ({formatPercent(item.practiceShare, 1)})</span><span>封印 {item.sealed} ({formatPercent(item.sealedShare, 1)})</span></div>
          ))}
        </div>
      </section>
    </section>
  );
}

function LabPanel({ model, layerCModel }: { model: LayerADashboardModel; layerCModel?: LayerCDashboardModel | null }) {
  return (
    <section className="lab-panel" aria-labelledby="lab-heading">
      <header className="lab-hero">
        <div><p className="eyebrow">LAB / LAYER A</p><h1 id="lab-heading">偶然と並べて、途中経過を見る。</h1></div>
        <p>これは途中経過です。確証用の頻度論p値とHolm判定は、実験終了時の最終解析まで開示しません。</p>
      </header>
      <SourceSummary model={model} />
      <ConditionCards model={model} />
      <section className="lab-section" aria-labelledby="deviation-heading">
        <div className="section-heading"><div><p className="eyebrow">CUMULATIVE DEVIATION</p><h2 id="deviation-heading">累積偏差</h2></div><p>実線 = 実測 / 破線 = 偶然なら95%程度が収まる ±0.98√bits</p></div>
        <CumulativeDeviationCanvas points={model.cumulativeDeviation} />
      </section>
      <ControlPanel model={model} />
      <MiracleLog model={model} />
      <ExperimentCalendar model={model} />
      {layerCModel && <LayerCPanel model={layerCModel} />}
    </section>
  );
}

export function ExperimentDashboard({
  registration,
  genesisHash,
  model,
  layerCModel,
  initialTab = 'home',
}: {
  registration: RegistrationPayload;
  genesisHash: string;
  model: LayerADashboardModel;
  layerCModel?: LayerCDashboardModel | null;
  initialTab?: DashboardTab;
}) {
  const [tab, setTab] = useState<DashboardTab>(initialTab);
  return (
    <main className="dashboard-shell">
      <nav className="dashboard-tabs" aria-label="メインナビゲーション">
        <button className={tab === 'home' ? 'active' : ''} type="button" onClick={() => setTab('home')}>ホーム</button>
        <button className={tab === 'lab' ? 'active' : ''} type="button" onClick={() => setTab('lab')}>ラボ</button>
      </nav>
      {tab === 'home'
        ? <HomePanel registration={registration} genesisHash={genesisHash} />
        : <LabPanel model={model} layerCModel={layerCModel} />}
    </main>
  );
}
