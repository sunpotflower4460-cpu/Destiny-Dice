import { useCallback, useEffect, useRef, useState } from 'react';
import { capacitorLocalNotificationsPort } from '../continuity/capacitorNotifications';
import { planLocalNotifications } from '../continuity/notifications';
import { buildWishDeadlineCandidates, deriveDailySessionProgress, type DailySessionProgress } from '../continuity/runtime';
import {
  requestPermissionAndSync,
  syncPlannedNotifications,
  type NotificationPermissionState,
} from '../continuity/scheduler';
import { deriveGentleStreak, type GentleStreak } from '../continuity/streak';
import { resolveExperimentDate, systemContext } from '../continuity/time';
import {
  ExperimentDashboard,
  buildLayerADashboardModel,
  buildLayerCDashboardModel,
  type LayerADashboardModel,
  type LayerCDashboardModel,
} from '../dashboard';
import { getApplicationLedgerService } from '../ledger/appService';
import type { LedgerService } from '../ledger/service';
import type { StoredLedgerEntry } from '../ledger/types';
import { projectCurrentSchedule } from '../registration/projection';
import type { RegistrationPayload } from '../registration/types';
import { getApplicationRngConfiguration, getApplicationRngService } from '../rng/appService';
import {
  SessionFlow,
  SessionFlowService,
  type SessionContextInput,
  type SessionDraft,
  type SessionPlan,
  type SessionResult,
} from '../session';
import {
  createSecureWishId,
  WishRegistryPanel,
  WishRegistryService,
  type DueWishView,
  type RegisteredWishResult,
  type WishPathway,
  type WishRegistrationInput,
  type WishRegistryProjection,
} from '../wish';
import './ExperimentRuntime.css';

type RuntimeTab = 'today' | 'wishes' | 'records';
type NotificationUiState = NotificationPermissionState | 'checking' | 'unavailable' | 'error';

type RuntimeServices = {
  ledger: LedgerService;
  session: SessionFlowService;
  wish: WishRegistryService;
};

type RuntimeSnapshot = {
  entries: StoredLedgerEntry[];
  currentExperimentDate: string;
  activeDayIndex: number | null;
  progress: DailySessionProgress;
  streak: GentleStreak;
  plan: SessionPlan | null;
  startedAt: string;
  context: SessionContextInput;
  dashboard: LayerADashboardModel;
  layerC: LayerCDashboardModel | null;
  wishRegistry: WishRegistryProjection | null;
  dueJudgments: DueWishView[];
};

const systemClock = {
  now: () => new Date().toISOString(),
};

function reminderStorageKey(experimentId: string): string {
  return `intention-dice:daily-reminder:${experimentId}`;
}

function readReminderTime(experimentId: string): string | null {
  if (typeof window === 'undefined') return null;
  try {
    const value = window.localStorage.getItem(reminderStorageKey(experimentId));
    return value && /^\d{2}:\d{2}$/.test(value) ? value : null;
  } catch {
    return null;
  }
}

function storeReminderTime(experimentId: string, value: string | null): void {
  if (typeof window === 'undefined') return;
  try {
    if (value === null) window.localStorage.removeItem(reminderStorageKey(experimentId));
    else window.localStorage.setItem(reminderStorageKey(experimentId), value);
  } catch {
    // Notification preference persistence is best-effort and never changes the ledger.
  }
}

function endDate(registration: RegistrationPayload): string {
  const start = Date.parse(`${registration.startDate}T00:00:00.000Z`);
  return new Date(start + (registration.days - 1) * 86_400_000).toISOString().slice(0, 10);
}

export function ExperimentRuntime({
  registration,
  genesisHash,
}: {
  registration: RegistrationPayload;
  genesisHash: string;
}) {
  const [tab, setTab] = useState<RuntimeTab>('today');
  const [snapshot, setSnapshot] = useState<RuntimeSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [notificationState, setNotificationState] = useState<NotificationUiState>('checking');
  const [notificationMessage, setNotificationMessage] = useState<string | null>(null);
  const [dailyReminderTime, setDailyReminderTime] = useState<string | null>(() => readReminderTime(registration.experimentId));
  const servicesRef = useRef<Promise<RuntimeServices> | null>(null);

  const getServices = useCallback((): Promise<RuntimeServices> => {
    if (!servicesRef.current) {
      servicesRef.current = getApplicationLedgerService().then((ledger) => {
        const rng = getApplicationRngService();
        return {
          ledger,
          session: new SessionFlowService(ledger, rng, systemClock),
          wish: new WishRegistryService(ledger, rng, systemClock, createSecureWishId),
        };
      });
    }
    return servicesRef.current;
  }, []);

  const syncNotificationsFor = useCallback(async (
    entries: readonly StoredLedgerEntry[],
    now: string,
    reminderTime: string | null,
    requestPermission: boolean,
  ): Promise<void> => {
    const plans = planLocalNotifications({
      registration,
      now,
      wishes: buildWishDeadlineCandidates(entries),
      ...(reminderTime === null ? {} : { dailyReminderTime: reminderTime }),
    });

    try {
      const result = requestPermission
        ? await requestPermissionAndSync(capacitorLocalNotificationsPort, plans)
        : await syncPlannedNotifications(capacitorLocalNotificationsPort, plans);
      setNotificationState(result.permission);
      setNotificationMessage(
        result.permission === 'granted'
          ? `${result.scheduled}件のローカル通知を同期しました。`
          : result.permission === 'denied'
            ? '通知は端末設定で拒否されています。実験データには影響しません。'
            : '通知はまだ許可されていません。必要な時だけONにできます。',
      );
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : String(cause);
      const unavailable = /not supported|unavailable|not implemented/i.test(message);
      setNotificationState(unavailable ? 'unavailable' : 'error');
      setNotificationMessage(
        unavailable
          ? 'この環境ではローカル通知を利用できません。記録機能はそのまま使えます。'
          : `通知の同期に失敗しました: ${message}`,
      );
    }
  }, [registration]);

  const refresh = useCallback(async (options?: { syncNotifications?: boolean }): Promise<void> => {
    setRefreshing(true);
    setError(null);
    try {
      const services = await getServices();
      const now = systemClock.now();
      const currentExperimentDate = resolveExperimentDate(now, registration.timeZone, registration.dayBoundaryHour);
      let entries = await services.ledger.list();
      const currentSchedule = await projectCurrentSchedule(registration, currentExperimentDate);
      let progress = deriveDailySessionProgress(entries, currentExperimentDate, registration.sessionsPerDay);
      let plan: SessionPlan | null = null;

      if (currentSchedule && progress.nextSeqInDay !== null) {
        plan = await services.session.prepareSession(currentExperimentDate, progress.nextSeqInDay);
        entries = await services.ledger.list();
        progress = deriveDailySessionProgress(entries, currentExperimentDate, registration.sessionsPerDay);
      }

      if (registration.layerC.enabled) {
        const recovered = await services.wish.recoverUnassignedWishes();
        if (recovered.length > 0) entries = await services.ledger.list();
      }

      const wishRegistry = registration.layerC.enabled
        ? await services.wish.projectRegistry(currentExperimentDate)
        : null;
      const dueJudgments = registration.layerC.enabled
        ? await services.wish.projectDueJudgments(currentExperimentDate)
        : [];
      const startedAt = systemClock.now();
      const context = systemContext(startedAt, registration.timeZone);

      const next: RuntimeSnapshot = {
        entries,
        currentExperimentDate,
        activeDayIndex: currentSchedule?.dayIndex ?? null,
        progress,
        streak: deriveGentleStreak(entries, currentExperimentDate),
        plan,
        startedAt,
        context,
        dashboard: buildLayerADashboardModel(entries, registration),
        layerC: registration.layerC.enabled ? buildLayerCDashboardModel(entries, registration) : null,
        wishRegistry,
        dueJudgments,
      };
      setSnapshot(next);

      if (options?.syncNotifications !== false) {
        await syncNotificationsFor(entries, startedAt, dailyReminderTime, false);
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setRefreshing(false);
    }
  }, [dailyReminderTime, getServices, registration, syncNotificationsFor]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function requestNotifications(): Promise<void> {
    if (!snapshot) return;
    setNotificationState('checking');
    await syncNotificationsFor(snapshot.entries, systemClock.now(), dailyReminderTime, true);
  }

  async function changeReminderTime(value: string | null): Promise<void> {
    setDailyReminderTime(value);
    storeReminderTime(registration.experimentId, value);
    if (snapshot) {
      await syncNotificationsFor(snapshot.entries, systemClock.now(), value, false);
    }
  }

  async function runSession(draft: SessionDraft): Promise<SessionResult> {
    const services = await getServices();
    return services.session.runSession(draft);
  }

  async function loadWishMoment(experimentDate: string) {
    const services = await getServices();
    return services.wish.projectWishMoment(experimentDate);
  }

  async function recordWishMoment(experimentDate: string, wishIdsShown: readonly string[], seconds: number) {
    const services = await getServices();
    await services.wish.recordWishMoment(experimentDate, wishIdsShown, seconds);
  }

  async function registerWish(input: WishRegistrationInput): Promise<RegisteredWishResult> {
    const services = await getServices();
    return services.wish.registerWish(input);
  }

  async function withdrawWish(wishId: string): Promise<void> {
    const services = await getServices();
    await services.wish.withdrawWish(wishId);
  }

  async function judgeWish(
    wishId: string,
    outcome: 'realized' | 'not_realized' | 'undecidable',
    pathway?: WishPathway,
  ): Promise<void> {
    if (!snapshot) throw new Error('runtime snapshot is not ready');
    const services = await getServices();
    await services.wish.judgeWish(wishId, snapshot.currentExperimentDate, outcome, pathway);
  }

  if (error) {
    return (
      <main className="runtime-shell">
        <section className="runtime-card runtime-error">
          <p className="eyebrow">INTENTION DICE</p>
          <h1>実験ランタイムを開始できませんでした</h1>
          <p>{error}</p>
          <button type="button" onClick={() => void refresh()}>もう一度確認</button>
        </section>
      </main>
    );
  }

  if (!snapshot) {
    return <main className="runtime-shell"><p>今日の実験日と台帳を確認しています…</p></main>;
  }

  const experimentEnded = snapshot.currentExperimentDate > endDate(registration);
  const experimentNotStarted = snapshot.currentExperimentDate < registration.startDate;
  const anuConfigured = getApplicationRngConfiguration().anuConfigured;

  return (
    <main className="runtime-shell">
      <nav className="runtime-tabs" aria-label="実験ナビゲーション">
        <button className={tab === 'today' ? 'active' : ''} type="button" onClick={() => setTab('today')}>今日</button>
        {registration.layerC.enabled && (
          <button className={tab === 'wishes' ? 'active' : ''} type="button" onClick={() => setTab('wishes')}>
            願い{snapshot.dueJudgments.length > 0 ? ` (${snapshot.dueJudgments.length})` : ''}
          </button>
        )}
        <button className={tab === 'records' ? 'active' : ''} type="button" onClick={() => setTab('records')}>記録・ラボ</button>
      </nav>

      {tab === 'today' && (
        <section className="runtime-today">
          <header className="runtime-hero">
            <div>
              <p className="eyebrow">{snapshot.currentExperimentDate}</p>
              <h1>{snapshot.activeDayIndex === null ? '実験の時間を守る。' : `DAY ${snapshot.activeDayIndex + 1} / ${registration.days}`}</h1>
              <p>固定 timezone: {registration.timeZone} ／ 日付境界 {String(registration.dayBoundaryHour).padStart(2, '0')}:00</p>
            </div>
            <div className="runtime-streak" aria-label="やさしい継続記録">
              <strong>{snapshot.streak.completedDays}</strong><span>記録した日</span>
              <small>直近 {snapshot.streak.recentRunDays}日連続。空いた日があっても失格にはなりません。</small>
            </div>
          </header>

          <section className="runtime-card notification-card" aria-labelledby="notification-heading">
            <div>
              <p className="eyebrow">LOCAL REMINDERS</p>
              <h2 id="notification-heading">忘れない仕組みだけ、そっと置く。</h2>
              <p>願い締切と、希望した時刻の日次リマインダーを端末内で予約します。封印願いの本文は通知へ渡しません。</p>
            </div>
            <div className="notification-controls">
              {notificationState !== 'granted' && (
                <button type="button" disabled={notificationState === 'checking'} onClick={() => void requestNotifications()}>
                  {notificationState === 'checking' ? '確認中…' : '通知をONにする'}
                </button>
              )}
              <label>毎日リマインダー
                <select
                  value={dailyReminderTime ?? ''}
                  onChange={(event) => void changeReminderTime(event.target.value === '' ? null : event.target.value)}
                >
                  <option value="">OFF</option>
                  <option value="08:00">08:00</option>
                  <option value="12:00">12:00</option>
                  <option value="18:00">18:00</option>
                  <option value="20:00">20:00</option>
                  <option value="22:00">22:00</option>
                </select>
              </label>
            </div>
            {notificationMessage && <p className="quiet-note">{notificationMessage}</p>}
          </section>

          {!anuConfigured && (
            <section className="runtime-card runtime-source-note">
              <strong>ANU endpoint 未設定</strong>
              <p>現在は RANDOM.ORG → local crypto のfallbackで実行できます。fallbackは台帳に実ソースで残り、Layer A主要量子sampleには入りません。</p>
            </section>
          )}

          {experimentNotStarted && (
            <section className="runtime-card"><h2>開始日前です</h2><p>登録済み開始日は {registration.startDate}。それまではcontrolやsessionを作りません。</p></section>
          )}

          {experimentEnded && (
            <section className="runtime-card"><h2>365日の実験期間は終了しています</h2><p>既存台帳はそのまま保持されます。締切を迎える願いの判定は「願い」画面から続けられます。</p></section>
          )}

          {!experimentNotStarted && !experimentEnded && snapshot.progress.complete && (
            <section className="runtime-card runtime-complete">
              <p className="eyebrow">TODAY COMPLETE</p>
              <h2>今日の {registration.sessionsPerDay} セッションを記録しました。</h2>
              <p>抜けた日を後から埋める必要はありません。次のexperiment dayに、そのまま続けます。</p>
            </section>
          )}

          {snapshot.plan && (
            <SessionFlow
              key={`${snapshot.plan.experimentDate}-${snapshot.plan.seqInDay}`}
              plan={snapshot.plan}
              affirmationText={registration.affirmationText}
              startedAt={snapshot.startedAt}
              context={snapshot.context}
              onDraw={runSession}
              loadWishMoment={registration.layerC.enabled ? loadWishMoment : undefined}
              recordWishMoment={registration.layerC.enabled ? recordWishMoment : undefined}
              onFinish={() => refresh()}
              finishLabel={snapshot.plan.seqInDay < registration.sessionsPerDay ? '次のセッションへ' : '今日を完了'}
            />
          )}

          {refreshing && <p className="runtime-refreshing">台帳を更新しています…</p>}
        </section>
      )}

      {tab === 'wishes' && registration.layerC.enabled && snapshot.wishRegistry && (
        <WishRegistryPanel
          currentExperimentDate={snapshot.currentExperimentDate}
          defaultDeadlineDays={registration.layerC.defaultDeadlineDays}
          registry={snapshot.wishRegistry}
          dueJudgments={snapshot.dueJudgments}
          onRegister={registerWish}
          onWithdraw={withdrawWish}
          onJudge={judgeWish}
          onRefresh={() => refresh()}
        />
      )}

      {tab === 'records' && (
        <ExperimentDashboard
          registration={registration}
          genesisHash={genesisHash}
          model={snapshot.dashboard}
          layerCModel={snapshot.layerC}
        />
      )}
    </main>
  );
}
