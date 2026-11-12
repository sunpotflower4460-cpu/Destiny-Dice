# Unity Garden — 記録・復旧・観察のデータ契約

設計版: **0.7 / designCandidate／Unity 実装・実機検証は未実施**。対象は新しい箱庭ゲームのみ。自然・天候・場所と配面の規則を保ち、来場者交代の保存・復帰、現在の採用版、監査用exportの境界を揃える。現行の組合せは[CURRENT_DESIGN.md](./CURRENT_DESIGN.md)を参照する。
この文書は既存の `DESIGN.md` / `PROTOCOL_FREEZE.md` の実験・台帳を変更しない。
数値パラメーター、ファイル I/O、C# の具体的な型は、後続の実装フェーズで検証する。

## 0. G1で実装する最小経路

展示版はlocal entropyだけを使い、天候・配面・出目・カードの材料を保存してから解釈する。v0.6の場所抽選はmapping材料の先頭を使い、独立した新しいroleや初期庭の乱数を増やさない。
「乱数の材料を保存できた」と「出目を表示した」は異なる段階である。

`PreparationStarted → [その期の材料がないときだけWeatherEntropyCommitted] → MappingEntropyCommitted → Prepared → IntentCommitted → AcquisitionStarted → DrawEntropyCommitted → OutcomeCommitted → Resolved → PresentationReceipt`

同じ天候期の材料があれば、その記録を再利用して配面へ進む。天候の選定と保存境界の詳細は§1.1、自然への作用は[WEATHER.md](./WEATHER.md)と[SIMULATION.md](./SIMULATION.md)を参照する。
任意の`CareFocusChanged`はrun単位で追記し、次のPreparationStartedがそれまでの最新の場所設定を固定する。配面公開後の意図編集とは別の操作であり、公開済みPreparedの作用場所や6面を変えない。詳細は§1.2とする。明示的な新庭操作では、別にRunSwitchRequestedを保存して§3.2のrun閉鎖経路へ入る。これも毎投の段階ではない。

通常経路で必要なのは、この順序、single-writer、純関数の解釈・計算、追記journal、保存結果の投影である。
外部provider、通信再試行、量子乱数へのfallbackはG1に入れず、後続gateで別に実装する。
終了・障害のイベントは例外時だけ追記する。各保存段階でユーザーへ確認画面を出さない。
以下のbuffer長・命名は実装用初期案であり、採用した値をrunの版付き設定に保存する。

## 1. 保存する目的と単位

自動記録の目的は、同じ庭を復元できることと、意図・抽選・自然の変化を後から見返せること。
プレイヤーへ各段階の保存確認を求めず、アプリケーション層が順序を強制する。

- `gameId`：このゲームの名前空間。既存の `experimentId` や Layer A/B/C を流用しない。
- `runId`：一つの庭の世代。新しい庭、ルール変更、展示の次の来場者は新しい ID にする。
- `roundId`：配面の準備開始から結果までの一意な ID。同じ ID の再開は新しい抽選ではない。
- `seq`：run 内で単調増加する記録順。壁時計の時刻変更で並び替えない。
- `worldRevision`：最後に確定した庭の版。`Resolved` と初期状態からだけ進める。
- `previousRunId`：来場者交代などで前に使っていたrunへの参照。庭の状態や意図を継承したことを意味しない。
- `importedFrom`：将来、別runの庭を取り込む場合の出所。G1〜G3では使わず、過去のrunを上書きしない。

ルール・計算方式・コンテンツの版を `RunStarted` で固定する。
版ラベルだけでなく、使った設定値・地図・カードと文章集の内容、または永続した内容参照とhashを保存する。
参照先は後から同名で差し替えられないものとし、保存データと一緒に復元・監査できるようにする。保存順は§9に従う。
版を変更する場合は新しい run を開始し、変更前後の集計を既定で混ぜない。
同じ端末の同じ人物かどうかは自動判定しない。展示の run は人物 ID を意味しない。

run内で未終端のroundは最大一つとする。正となる庭の参照は、最後の保存済み`Resolved.postState`、まだなければ`RunStarted`の初期状態から求める。
この参照は`sourceSeq / sourceEntryHash / worldRevision / stateHash`の組であり、最新のjournal headとは区別する。後着のPresentationReceiptやRecoveryContinuedは庭の版を進めない。
`PreparationStarted`はこの組を開始状態参照として固定し、`beforeRevision / beforeStateHash`を持つ。新しい準備の保存時とResolvedの保存時に、同じ庭の参照であることをwriterが照合する。
別の未終端roundがあれば新しいround IDの開始も拒否する。準備commandは一度だけroundIdを割り当ててcommandIdと保存し、同じcommandの再送はそのroundへ収束させる。二つの配面を同じ庭から並行解決しない。

### 1.1 天候を三つの正常解決投に固定する

`weatherPolicyVersion=weather-episodes.v0.4`は、一つのrunを初期`worldRevision=0`から始め、正常なResolvedの保存ごとに一つ進める。
G1〜G3の新しいrunは保存した静的mapから初期化し、initialStateProvenanceへmapの内容hashと展開版を残す。同runの復帰は保存済みsnapshot・revision・天候材料をそのまま復元する。別runの庭を引き継いで初期化する機能は後続の拡張とし、初期meanLight、個体の出自、累計、tickをどう扱うかを別の版で定義する。単にrevisionだけ0へ書き換えて引継ぎ対応済みにしない。

```text
weatherPeriodIndex = floor(beforeRevision / 3)
periodStep = beforeRevision % 3
```

一期は三つの正常解決投である。取消、準備中断、出目未成立、履歴閲覧、長押し、演出の省略・再提示、待機時間では進めない。未解決の回で天候の水だけを庭へ適用することもない。

候補順は`clear / cloudy / drizzle`に固定し、重みは初期期だけ`[256,0,0]`、以後の各期は`[96,96,64]`とする。初期の晴れは`selectionKind=initial_fixed`、以後は`independent_weighted`と記録する。初期をランダムに選んだ晴れとは表示しない。前の天候、庭の状態、意図、出目、カードを次の天候重みへ入力しない。同じ天候が次期も選ばれ得る。

初期期も含め、期ごとの生材料は**同run・同weatherPeriodIndexに一つのWeatherEntropyCommitted**へ保存する。このrun単位のrecordは`roundId=null`、`initiatedByPreparationSeq`で取得開始を許可したPreparationStartedへ結ぶ。roundを取消しても、この天候材料はその期に残す。

準備の具体的な順序は次のとおり。

1. writerが開始状態参照と未終端枠を照合し、期index・再利用する天候参照または新規weather取得IDをPreparationStartedへ保存する。
2. 当該期の記録がない場合だけ、role=weatherの独立32byteを取得。保存前には天候へ解釈せず、WeatherEntropyCommittedへ原本・profile hash・取得情報を保存する。
3. 保存済みの当該期材料からWeatherContextを純関数で導く。以前の準備で保存された材料でも同じ参照を使い、再取得しない。
4. mapping材料を別取得・保存し、その先頭の場所抽選とPreparationStartedに固定した設定から作用場所を解決する。その場所・現在の庭・確定した天候から、各recipeの作用と候補重みを求め、同じmapping bufferの続きから6面を生成する。
5. 同じWeatherContextとTargetContextを持つPreparedの永続化と画面の終状態対応を確認してから、天候・作用場所・6面・意図編集を公開する。天候や場所が未確定のPrepared、後からそれらだけを差し替えるPreparedは許可しない。

WeatherEntropyCommittedの保存でも、現在のrun/epoch・未終端準備・期待する期indexをwriterで検査する。取消が先に確定した準備への遅延取得結果を採用しない。材料保存が先なら、その後の取消やmapping失敗でも同じ期の材料を保持する。同一期の異なる材料が完全なrecordとして複数あれば、都合のよい方を選ばず検証障害とする。

天候の生材料を保存しただけでは庭のrevisionを進めない。WeatherContextはその投擲の入力条件であり、開始時snapshotの水・成長値を先に変更しない。天候とダイスの作用は、その回の40tickへ一緒に適用してResolvedへ保存する。

### 1.2 場所の選択は次の準備へ固定する

`targetPolicyVersion=target-sites.v0.6`は[target-profile.v0.6.json](./content/target-profile.v0.6.json)の`inlet / shade / meadow`を固定順に使う。各地点の4 anchorはそれぞれcell85 / cell135 / cell154を中心とする。初期庭の地形・水分・植物は従来の静的mapから作り、初期状態へ新たな乱数を加えない。

場所設定`CareFocusPreference`は`mode=auto / fixed`と`siteId`の組とする。autoではsiteId=null、fixedでは上記の三つのいずれかを必須とする。RunStartedに`defaultCareFocus={mode:auto,siteId:null}`を固定し、初回から設定入力を要求しない。場所設定は祈りや希望の`IntentCommitted.target`とは別の、実際の作用先を選ぶゲーム操作である。

変更コマンドは`commandId / runEpoch / mode / siteId / targetProfileHash`を持ち、Applicationの同じコマンド列からsingle-writerへ渡す。現在のrun・許可された操作状態・profile hashを検査し、`CareFocusChanged`を`roundId=null`で追記する。このrecordは庭のrevision、天候期、現在のroundの段階を進めない。同じcommandIdの再送は既存recordへ収束させる。同じ設定の再選択は新規recordを作らないno-opにできるが、新しい材料取得や準備開始を副作用にしない。

PreparationStartedを保存する同じ処理内で、それより前の最新のCareFocusChanged、なければRunStartedのdefaultCareFocusを選ぶ。`careFocusPreference / careFocusSourceRef={seq,entryHash}`とtargetProfileHashを固定する。Preparedはそのスナップショットだけを使い、後着の場所変更を参照しない。参照は同runのRunStartedまたは有効なCareFocusChangedに限り、profile hash・設定値・記録順を検証する。

保存済みmapping材料の先頭で`UniformBelow(3)`を一度呼び、固定順から`drawnSiteId`を得る。autoでは`resolvedSiteId=drawnSiteId`、fixedでは`resolvedSiteId=careFocusPreference.siteId`とする。fixedでも抽選とbyte消費を省略せず、drawnSiteIdと消費記録を残す。続く候補解決・重み・6面はresolvedSiteIdのanchorを使う。場所と6面は同じ準備アルゴリズム・同じmapping roleに属し、相互に独立とは主張しない。weather・outcome・oracleの別材料は維持する。

今回の場所が前回と同じ、種の空きがない、直近に同じ文章が出たという理由で場所を再抽選しない。fixedでもautoでも、種の候補条件と水・風・光の計算は同じ規則を使う。最初の場所抽選でmapping bufferが尽きた場合も通常のmapping_entropy_exhaustedとして準備を閉じ、都合のよい既定場所で補完しない。

v0.6の受付と表示境界は次のように定める。

1. Ready・Presentingでは、§3.2の交代準備・閉鎖待ちでない場合に次回分の場所設定だけを受け付ける。現在のPreparedと過去の演出の場所は保持する。autoへの変更では、次回の実際の場所はまだ不明と表示する。
2. Resolved保存直後に次の準備を先回りしない。過程再生の終了・省略、または保存済み終状態の静かな復元を完了してから次の準備へ進む。
3. coordinatorは場所操作の受付を閉じ、既に受け付けた場所コマンドの後へ準備開始を同じ直列列に置く。writerがその順に保存し、最新設定の選択とPreparationStartedの追記を一つの排他区間で行う。準備中の場所操作は無効とし、次回用のクリックとして予約しない。
4. 場所設定の保存成功前に、画面で受け付け済みと表示しない。保存失敗なら同じコマンドの保存・回復を先に行い、古い設定で次の準備へ進まない。保存後の画面では今回の場所と次回の設定を区別する。
5. 新しいPreparedが公開されたら、以後の場所変更はさらに次のPreparationStartedに適用する。場所変更自体は公開中roundの取消や配面再生成を要求しない。省略操作のreleaseを場所選択や次の投擲へ流用しない。

同runの復帰ではCareFocusChangedを含む保存済み順序から次回設定を復元し、既存Preparedは当時のTargetContextで復元する。途中で取消・未成立になったroundも書き換えず、新しいPreparationStartedがその時点の最新設定を固定する。来場者交代で作る新runはautoから始め、前のrunの設定や未処理コマンドを引き継がない。

## 2. 一回の状態遷移

以下の各段階は、single-writer が追記を永続化してから次へ進める。
保存の成功応答は、単にメモリー上のキューへ入ったことを意味してはならない。
表のCareFocusChangedは§1.2で許可した状態で任意に挿入するrun単位の設定記録であり、毎投の必須段階ではない。新庭への交代を保存するRunSwitchRequestedと、runを閉じるRunClosedは§3.2の別経路で扱う。

| 段階／イベント | 保存するもの | 保存後に許可する操作 |
|---|---|---|
| `CareFocusChanged`（任意・run単位） | 次の準備に使う場所設定、command ID、profile hash | 次回設定の表示。現在のround・庭・天候は変更しない |
| `PreparationStarted` | round ID、開始状態参照、場所設定と出所、配面規則、mapping取得 ID、天候期と取得/再利用の参照 | 必要なら初めて天候用乱数を取得。天候確定後に配面用取得 |
| `WeatherEntropyCommitted`（当該期に未保存のとき） | run単位の期index、32byte、取得元、開始準備への参照、天候profile hash | 当該期の天候を解釈。以後の準備でも同じ材料を使う |
| `MappingEntropyCommitted` | 配面用の生byte、取得元、材料形式・変換版、保存済み天候の参照 | 保存済みの場所設定・庭・天候とこの材料から、場所と6面を生成 |
| `Prepared` | 開始時の庭、確定WeatherContext・TargetContext、6面全体、生成根拠、mapping材料参照、実行時間幅 | 下記の表示条件を満たしてから天候・場所・配面公開、意図の編集 |
| `IntentCommitted` | 意図のスナップショット、方法、対応面、基準確率 | 取得開始の記録 |
| `AcquisitionStarted` | 取得 ID、参照する意図・配面、乱数ポリシー | 初めて出目・カード用乱数を取得 |
| `DrawEntropyCommitted` | outcomeのbufferと、oracleのbufferまたは失敗receiptを一つのrecordで保存 | 初めて出目・カードとして解釈 |
| `OutcomeCommitted` | 材料参照、出目、選択面、カードまたはカード未成立理由 | 同じ入力で自然モデルを計算 |
| `Resolved` | 完全な更新後状態、出来事、採用文、参照情報 | 結果演出、次回分の任意の場所設定。次の準備は終状態表示後 |
| `PresentationReceipt` | アプリが報告した提示段階、提示対象 | 補助的な提示履歴。次の回の必須gateではない |

意図を持たない場合も `IntentCommitted(kind=none)` を保存して同じ経路を通る。
長押し中の下書きは変更可能だが、確定後の変更は次の round に適用する。
`Prepared` の作用場所と6面は固定し、意図変更・次回分の場所設定・画面回転・バックグラウンド復帰で再生成しない。
その回の天候も同じ境界で固定する。前回の文章を読んだことや意図の変更を理由に、天候を選び直さない。
`PreparationStarted`から`Resolved`まではCoreの開始snapshotを固定する。配面公開後は公開中の庭もその開始状態に保ち、待機時間で進めない。
`OutcomeCommitted`後に別の計算用コピーを固定tick数だけ進める。未終端roundの所有と開始状態参照を再確認し、`Resolved`の永続化後にだけ`postState.worldRevision = beforeRevision + 1`を新しい庭として採用する。
対象位置・方向・シミュレーション時間幅も固定し、確定後の外部入力を混ぜない。
結果待ちの光や音は使えるが、保存前に出目・カード・庭の結果を先に見せない。

v0.6では前回の過程再生があれば終了または省略し、保存済み終状態を見せてから次のPreparationStartedを開始する。§1.2の順序で受け付け済み場所設定を先に処理する。6面公開・意図編集・投擲受付は、画面がPreparedの`beforeRevision / beforeStateHash`に対応する終状態であることを確認してから許可する。初回は保存済み初期状態との対応を確認する。
途中の補間画面は開始snapshotにしない。省略操作のreleaseを次の投擲へ流用せず、公開後の新しいpointer-down/upまたはkey-down/upを要求する。処理中の連打を次回分として予約しない。
この表示条件は保存済み庭と画面の対応であり、Receiptの保存や人の読了確認を新しいgateにしない。

## 3. 中断・再開・二重実行

復旧入口では保存済みRunSwitchRequested・RunClosedを先に確認し、交代中なら§3.2の非公開の完結処理へ進む。次の表はroundの保存段階ごとの扱いであり、交代要求を無視して旧runの通常画面を開く規則ではない。

| 再開時に存在する最後の段階 | 回復規則 |
|---|---|
| `PreparationStarted`のみ | `PreparationInterrupted`を追記。同じroundへ新しい材料を入れず、新roundで準備できる。当該期の保存済み天候が別の準備にあれば再利用 |
| 当該期の`WeatherEntropyCommitted`あり、mapping材料なし | 旧準備は中断として閉じ、新roundが同じ天候材料を再利用する。天候を再取得しない |
| `MappingEntropyCommitted` | PreparationStartedの場所設定と保存した材料・状態・版から、同じ場所抽選・6面生成を再開。新しい乱数を取得しない |
| `Prepared` | 同じ庭・天候・作用場所・6面を復元。未確定の意図は編集可能。後着の場所設定は次回分として保持 |
| `IntentCommitted` | 同じ意図を維持。新しい意図に置換せず、取得開始へ進める |
| `AcquisitionStarted`のみ | 真のプロセス終了後は`InterruptedUnresolved`を追記。同じ回を再取得しない |
| `DrawEntropyCommitted` | 保存したbufferと取得成否を解釈し、同じ出目・カードまたは未成立へ進める。未表示でも再取得しない |
| `OutcomeCommitted` | 保存した出目・カード・開始状態から同じ計算を実行する |
| `Resolved` | 保存した完全な更新後状態を復元する。入力をもう一度加算しない |
| 提示記録なし／提示途中 | 保存済み終状態と本文を静かに表示する。過去の音・注目cueを自動再生せず、集計から結果を外さない。表示の詳細はEXPERIENCE§5.3 |

roundの論理解決はResolvedで完了する。Receiptを失った回も、結果を復元してそのまま次へ進める。
Receiptは読了や次回開始の条件ではない。保存adapter自体の障害中は、通常どおり新規取得を止めて回復する。
同一プロセス内の一時停止と、プロセス終了後の復元は区別する。
取得処理が生きている間は、その処理の完了を待つ。別の取得を並行して始めない。
`PreparationInterrupted`は配面準備の未成立であり、出目の欠測数へ入れない。
weather取得が保存前に失敗した場合も`PreparationInterrupted(reason=weather_entropy_failed)`として準備を閉じ、失敗receiptを残す。真のprocess終了で未保存材料を回収できない場合はweather未確定の準備中断とし、別の準備で取得できる。記録済みの天候材料がある場合はこの経路で再取得せず、その材料を使う。保存adapterの障害中は新規取得せず、同じ材料の保存・末尾回復へ進む。
`InterruptedUnresolved`は出目取得開始後、永続化した材料から出目を確定できない状態である。
理由は`process_ended_before_material_commit / local_entropy_failed / outcome_entropy_exhausted`等を区別する。
予想が外れたという意味ではない。材料が有効に保存されていれば、Outcomeの記録前でも単に欠測へ戻さない。
欠測の run では庭を開始状態のまま保ち、新しい `roundId` で続けられる。
新しい round も履歴へ追記するため、欠測が成功例に置き換わることはない。
`AcquisitionStarted`前に明示的に終了した回は`AbandonedBeforeAcquisition`を追記する。
`IntentCommitted`があればそれも保持し、取消によって内容を編集したり出目を取得したりしない。
来場者交代でrunを終了するときも、取得開始前はこの終了記録を残す。
取得開始後は確定結果またはシステム上の未成立を記録してから、新runへ切り替える。
ユーザーが取消を押したことだけを理由に、既知の出目を欠測へ変更しない。
取得前の終了は未抽選であり、意図の一致率へ入れない。既存の記録は削除しない。

通常のroundイベントは`roundId + eventType`の一意性と合法な遷移をwriter/application層で検査する。
WeatherEntropyCommittedはrun単位の`runId + weatherPeriodIndex`で一意とし、元の準備が終端になっていても後の同一期Preparedから参照できる。
CareFocusChangedはrun単位の`runId + commandId`で一意とする。複数の設定変更を許すが、同commandの別内容を上書きしない。run単位の設定変更や後着Receiptで、既存roundの開始状態参照を最新journal headへ置き換えない。
提示記録だけは `roundId + presentationAttemptId + phase` を一意にする。ただしRunClosed後は新しいReceiptも追記せず、閉鎖前に確認できた提示記録を保持する。RunSwitchRequestedは§3.2のrun単位一意性に従う。
準備終了・取得前取消・出目未成立・正常解決は、同じroundで矛盾する終端を併存させない。
未終端枠を解放するのは、これらの終端recordを永続化した後だけ。正常解決以外では庭のrevisionを進めず、同じ検証済み状態から新しいroundを準備できる。
保存エラーを未成立の終端とみなして新roundを並行開始しない。部分末尾を回復して既存roundを閉じる経路、または§9.1の継続不能時に出所を明示した新runへ移る経路を使う。
完全なframeとhashを検証できれば、保存完了通知が返る前に終了していてもその記録を採用する。
連打・重複コールバック・遅れて返った取得結果は、既存の確定結果を置換できない。
終端状態になった round の遅延応答を新しい round へ流用しない。
計算や文章選定が失敗した場合も `OutcomeCommitted` を残し、乱数を引き直さない。

### 3.1 取消と非同期処理の順序

- 操作は`commandId / runEpoch / expectedRoundState`を持ち、Applicationの同じコマンド列へ渡す。
- `runEpoch`は現在の操作対象世代を表す。古い世代のcallbackを新runへ適用しない。
- Intentの保存中はUIを`committing`とし、終了記録の保存前に「取消済み」と表示しない。
- `AbandonedBeforeAcquisition`が先に保存されたら、そのroundのAcquisitionStartedを拒否する。
- AcquisitionStartedが先に保存されたら、取消は表示を閉じる要求として扱う。取得・保存の処理は正しく閉じる。
- 開始記録をwriterで保存した後、ロックを解放してlocal取得を行い、結果を同じwriterへ戻す。
- 保存完了callbackは直接次の取得を開始しない。現在のrun/round/状態を検査した次のコマンドを通す。
- 取得を待つ間にwriterのロックを保持しない。重複要求は既存commandの結果へ収束させる。
- 新規準備と解決のcommandは期待する開始状態参照も渡す。writer内で未終端roundと庭の参照を照合し、古いsnapshotに対する計画・遅延計算を採用しない。後着Receiptだけではこの照合を失敗させない。

来場者交代は次の§3.2の保存済み要求から続ける。旧runの通常画面を閉じることと、既存roundを内部で正しく終端化することを分ける。背景へ移るだけの場合は新runを作らず、同じ状態を保持する。

### 3.2 来場者交代を、中断しても一つの新しい庭へつなぐ

`RunSwitchRequested`は、新しい庭を始める明示操作を旧runへ保存する任意のrun単位recordである。通常の一投には追加しない。RunStartedの`runLifecycleVersion=run-switch.v0.7`として、受付済みの交代要求、旧runの閉鎖、次runのgenesisを次の順序へ固定する。この版を採用していない旧runへrecordや意味を黙って後付けせず、旧runの保存・復元は当時の版で扱う。

`RunSwitchRequested → 旧roundがあれば合法な終端 → RunClosed → 指定したtargetRunIdのRunStarted`

1. Applicationは同じコマンド列で交代要求を受け、通常の投擲・意図・場所変更の受付、自動の次準備、旧runの本文表示を止める。表示用のownerTokenと入力世代を失効させる。この時点の画面は中立の交代準備中であり、保存成功・新庭の開始完了とはまだ表示しない。
2. 次の庭用の内容を検証・durable保存する。新しい`targetRunId`はこのcommandへ一度だけ割り当てる。静的mapからの初期snapshot、採用するルール・各版、`contentManifest`、既定autoを`targetRunPlan`へまとめる。新genesis自身のseq/hash・保存/開始時刻・genesisRecoveryと、まだ確定していないswitchRequestRef・predecessorClosedRefはここへ先に作らない。
3. writerは旧runが閉鎖済みでなく、競合する交代要求がないことを検査し、`RunSwitchRequested(commandId, reason, targetRunId, targetRunPlan, plannedBy)`を保存する。内容原本の永続化はこの参照recordより先とする。保存済み要求は変更せず、再開時の同梱contentへ差し替えない。
4. 要求のdurable確認後は旧runを閉鎖待ちとして扱う。新しいPreparationStarted・Roll・Intent編集・CareFocusChangedを受け付けず、次項の既存roundの完結だけを内部で進める。旧runの本文・意図・結果を通常画面へ戻さない。
5. 旧roundが終端になったことを確認し、`RunClosed`へ`switchRequestRef / targetRunId / closingHeadRef / lastWorldRevision`を保存する。closingHeadRefはこのRunClosed直前の完全なseq/hash。閉鎖後の旧runへ通常recordや後着PresentationReceiptを追記しない。
6. `targetRunPlan`と旧RunClosedを照合して、固定したtargetRunIdへ一つのRunStartedを保存する。新genesisの`previousRunId / switchRequestRef / predecessorClosedRef`は同じ交代を指す。runをまたぐrecord参照は`{runId,seq,entryHash}`を必須とし、seqだけで別runのrecordへ結ばない。新runのseqは1、prevHashは64文字の0とし、旧runのheadを新runのprevHashへ流用しない。
7. 新runのgenesisと初期状態の保存・検証に成功してから、新しい庭の通常画面を開く。目標・方法・本文・操作時間・場所設定を旧runから継承せず、静的map・初期晴れ・既定autoの通常準備へ進む。旧runの片付けのために読了や追加の確認を求めない。

RunStarted・RunSwitchRequested・RunClosedはそれぞれ`runId + eventType`で一つとする。交代commandの再送は`runId + commandId`で同じ要求記録へ収束する。同commandで異なる内容を送った場合は不正な再送として拒否する。別commandが交代中に来ても第二のtargetRunIdを割り当てず、既に進行中の交代を返す。新runを通常公開した後に、その新runで明示された別の新庭操作は別の交代である。

targetRunPlanの固定対象は初期snapshot・ルール/計算版・content・既定値である。計画を作った実行元の`appVersion / buildId / platform`はRunSwitchRequestedの`plannedBy`へ保存する。新RunStartedの同項目は実際にgenesisを作った実行元を記録し、保存/開始時刻と同じくtargetRunPlanとの一致照合から除外する。要求後にアプリが更新されても古いbuildで作ったことにせず、genesis作成前に固定されたモデル・内容・保存版を現在の実装で扱えるかを検査する。対応できなければ同じ要求の回復待ちに留まり、新版の計算やcontentへ代用しない。

交代要求後に許可する旧roundの内部処理は、既存の保存段階だけで決める。

| 旧roundの保存段階 | 閉鎖待ちで行うこと |
|---|---|
| 未終端roundなし／Resolvedなど終端あり | 庭を再適用せず、次の準備や結果の再提示を行わずRunClosedへ進む |
| AcquisitionStartedより前 | AbandonedBeforeAcquisitionへswitchRequestRefを付けて終端化。意図・材料を変更せず、新しい出目取得を始めない |
| AcquisitionStartedあり、同processで取得中 | 既存の取得だけを待ち、その材料・失敗を元run/roundへ保存して閉じる。新しい取得を作らない |
| AcquisitionStartedあり、process終了後で材料なし | InterruptedUnresolvedを保存する。失った材料を回収できたことにしない |
| DrawEntropyCommitted／OutcomeCommittedあり | 同じ材料・入力・採用版から解釈・解決する。既知出目を交代の都合で未成立へ変更しない |
| 保存・計算・内容参照の障害 | 同じ回復経路へ留まる。成功したRunClosedや新genesisを作って飛ばさない |

UIの公開権と、旧runの内部処理所有権は別とする。交代準備の開始で古い表示callbackを無効にしても、既に許可した取得の材料保存まで無効にしない。内部callbackは旧runId・roundId・取得ID・保存段階をwriterで照合し、その旧roundへだけ適用する。新runの画面や庭へ結果を流用しない。Receiptの有無は閉鎖の必須条件にしない。

#### 交代中に終了した場合

起動時は通常UIを公開する前に、同gameIdの検証済みrunと交代参照を読み、保存済み交代要求を処理する。更新日時、端末時計、最後に開いた画面、破棄可能なactive-run cacheだけで公開runを選ばない。旧runの閉鎖と新genesisの相互参照、targetRunPlanとの一致を確認し、履歴の一つの継続先へ収束させる。矛盾する複数の継続先はintegrity_errorであり、最新らしい一方を選ばない。

| 終了境界 | 復帰時の扱い |
|---|---|
| RunSwitchRequestedの完全なframeがない | 交代は未受理。旧runの通常復元が起こり得る。未保存操作を再現・受理済みと表示しない |
| 要求は完全、旧round未終端 | 通常の旧本文を公開せず、保存済み要求から旧roundを完結する |
| 旧round終端済み、RunClosedなし | 終端を再適用せず、同じ要求から閉鎖を保存する |
| RunClosedあり、新RunStartedなし | 固定targetRunId・targetRunPlanを使って一つのgenesisを作る |
| 新RunStartedが完全、完了応答前に終了 | 既存genesisと対応を検証して新runを採用。第二のrunやgenesisを作らない |
| 新RunStartedのframeが途中 | §9.3で原本を保全して同じtargetRunIdへ回復。旧runの通常UIへ戻さない |

完全な要求frameを検証できれば、保存完了通知を返す前に終了していても受理済みとする。要求の保存が失敗した同processでは、中立の回復画面に留まり同commandの保存を回復する。新しい庭へ交代済みと表示したり、別のtargetRunIdで再試行したりしない。保存前のprocess終了だけは、記録されていない交代操作を保証できる境界ではない。運営は新しい庭の通常表示まで確認してから端末を次の来場者へ渡す。

これは通常の明示的な新庭操作の契約である。§9.1の検証障害で旧runを書込み不能にした場合の保全・救済を、正常なRunClosedや交代完了へ偽装しない。

## 4. 乱数の役割と監査

| role | 使用箇所 | 相互依存させない条件 |
|---|---|---|
| `weather` | 三つの正常解決投に使う天候を選ぶ | 当該期の最初のPreparationStarted後に別取得、材料保存後に解釈。期内は再利用 |
| `mapping` | 作用場所の抽選と、その場所の候補から6面を生成 | PreparationStarted後に取得。材料保存後、場所→候補・重み→6面の順に同じbufferを消費 |
| `outcome` | 確定した6面から1面を選ぶ | AcquisitionStarted後に新しく取得、材料保存後に解釈 |
| `oracle` | 6枚のオラクルから1枚を引く | outcomeと別bufferで取得、成功または失敗をbundleに保存 |
| `visual` | 粒子、装飾の揺れなど | 他 role の乱数列と庭の計算状態を変更しない |

新しい材料が必要な役割ごとに独立の取得を行い、bufferを共用しない。weatherの期内再利用は同一期を保つ規則であり、他roleへbyteを転用することではない。装飾用streamも分ける。
場所抽選と6面は一つのmapping roleの内部処理であり、同じbufferの異なる位置を順に使う。植物の誕生時の見た目は保存済み事実のprojectionとし、visualを含めた新しい乱数を取得しない。
別の取得で内容が偶然同じになっても再取得しない。値の違いを独立性の判定に使わない。
G1では有限の生byteをそのまま保存する。seedから展開する方式は採用時に別の材料形式・生成版を定義する。
将来の出目用 seed を配面生成時に作ったり、事前取得した出目を意図確定後に消費したりしない。
展示版はオフラインのローカル暗号乱数を想定し、`source=local` を正直に記録する。
テスト用 seed は `source=fixture` とし、実プレイの集計に混ぜない。

### 4.1 G1の有限buffer

初期案はweather用32byte/期、mapping用256byte/準備、outcome用32byte/投、oracle用32byte/投。長さは乱数ポリシーに保存する。
weatherは保存した先頭big-endian uint32へ既存の`UniformBelow(256)`を適用し、候補の累積重みで一つ選ぶ。256は2^32を割り切るため常に先頭wordを受理し、初期期も4byte消費・棄却0とする。未使用28byteを次期・他roleへ渡さない。初期期の重みも含め合計256を設定検証で要求し、byte長不足や不正設定を補完しない。この版にweather buffer全棄却の分岐はない。
outcomeを先に取得し、その成功後にoracleを別bufferへ取得する。成功した材料は一つのDrawEntropyCommittedへ保存する。
どちらも保存前には出目やカードに変換せず、Domain・UIへ生byteを渡さない。
片方の取得中に終了してbundleがなければ未解釈の未成立とする。メモリーにあった値を復元したことにしない。
同じprocess内でoutcome取得自体が失敗した場合は、失敗情報をInterruptedUnresolvedへ残す。
outcome成功後、oracleだけがlocal例外で失敗した場合は、outcome材料とoracleの失敗receiptをbundleに保存する。
その場合のOutcomeは`cardStatus=unavailable_local_failure`として出目を保全し、事実の基本文で庭を解決する。
この部分成功と、bundleを保存する前のprocess終了は区別する。後者の未保存材料は回収できない。
bundleが保存済みなら取得を再開せず、成功材料または記録済み失敗から同じ結果へ進める。
保存だけを再試行する場合も、同じ材料と取得情報を使う。保存エラーを理由にlocal乱数を取り直さない。

公平な D6 への変換は byte の `b >= 252` を棄却し、`1 + (b % 6)` とする。
棄却は不利な出目の引き直しではなく、剰余の偏りを除く変換処理である。
先頭から読み、最初の受理byteだけで決める。未使用byteを次のround・別roleへ持ち越さない。
oracleも独立bufferに同じ変換を適用し、固定した6枚の順序へ対応させる。
取得byte、使用offset、棄却数、変換版を記録し、同じ結果を再計算できるようにする。

mappingの`UniformBelow(n)`は、固定した候補順と連続するbig-endian uint32を使う初期案とする。
`n=1`はbyteを消費せず0。その他は`limit=2^32-(2^32 mod n)`未満のwordだけを受理し、`word mod n`を返す。
`2 <= n < 2^32`と中間64bit演算を検査する。Fisher–Yatesも同じmapping bufferから消費する。
v0.6はこのbufferのoffset0で最初に`UniformBelow(3)`を呼び、`inlet / shade / meadow`へ対応させる。`n=3`のlimitは4294967295で、word=4294967295だけを棄却する。固定場所でも同じ呼出しを行う。場所抽選の開始offset0、終了offset（排他的）、棄却word数を保存し、受理されたindexとdrawnSiteIdを原本から再現する。続く処理はその終了offsetから読み、先頭へ戻さない。
候補順・抽選順・shuffle方向は[GAMEPLAY.md](./GAMEPLAY.md)§5.1の`dynamic-faces.v0.6`に固定する。場所の1回に従来の14回を続け、通常経路は計15回のUniformBelow呼出しになる。列挙コンテナや辞書の順序を乱数の解釈へ混ぜない。v0.4の材料をv0.6のcursor規則で再解釈しない。

### 4.2 棄却がbufferを使い切った場合

有限bufferには、まれでも受理値が一つもない場合がある。テストfixtureで必ず扱う。
mappingが尽きたら`PreparationInterrupted(reason=mapping_entropy_exhausted)`とし、未公開の準備を終える。
outcomeが尽きたら`InterruptedUnresolved(reason=outcome_entropy_exhausted)`とし、その回の出目を補完しない。
**outcomeは成立しoracleだけが尽きた場合、出目を維持する。**
OutcomeCommittedに`cardStatus=unavailable_entropy_exhausted`, `cardId=null`と記録し、庭を解決して事実の基本文を使う。
カード未成立を表示・履歴で区別し、任意のカード名や象徴を代入しない。
この例外経路は新しいカード用乱数を取得せず、同じ生byteから常に同じ判断になる。
buffer不足と、候補条件違反・設定欠損・実装例外を混同しない。後者は設定や版の障害として止める。

配面候補の重みは「面の内容の出やすさ」であり、「完成した面の当たりやすさ」ではない。
各面が 1/6 でも、水が3面あれば水全体の確率は 3/6 になる。
オラクルは6枚から毎回復元抽出する。前回のカードも候補に戻り、連続して同じカードを引ける。
カード名や意味、カードと整数の対応は `deckVersion` で固定する。
カードの抽選結果を文に合わせて変更せず、実際の出来事に成立する文章を選ぶ。

### 4.3 外部providerは後続gate

G1はlocalから別providerへの自動fallbackを持たない。local取得失敗を別ソースの成功として隠さない。
外部providerを採用する版では、各`role + attemptId`の取得前記録、成功・失敗・不確実終了、実sourceを定義する。
再試行やfallbackが新しいsampleを作る場合は、その試行を保存し、先行応答との採用規則も固定する。
外部要求の一回性を根拠なく保証しない。既知出目をカード障害で失わない部分成功規則も検証してから有効にする。

## 5. DTO の契約

以下は C# の完成した型ではなく、保存・復旧に必要な論理フィールドである。
nullable と未取得を区別し、既知の値を便宜的な `0` や空文字で代用しない。

### 5.1 共通レコードと run

| DTO | 必須情報 |
|---|---|
| `JournalEntry` | `seq`, `type`, `createdAtUtc`, `payload`, `prevHash`, `entryHash` |
| `RecordMeta` | `schemaVersion`, `gameId`, `runId`, `roundId?`, `seq`。各 payload に含める |
| `RunStarted` | 初期状態、`rulesetVersion`, `simulationVersion`, `numericEncodingVersion`, `mappingPolicyVersion` |
| 同上・版情報 | `runLifecycleVersion`, `rngPolicyVersion`, `rngAlgorithmVersion`, `weatherPolicyVersion`, `targetPolicyVersion`, `appearanceProjectionVersion`, `deckVersion`, `messageVersion`, `groupingVersion`, `selectionPolicyVersion`, `appVersion`, `buildId` |
| 同上・識別 | `platform`, `previousRunId?`, `initialStateProvenance`, `startedAtUtc`。交代で作るrunは`switchRequestRef / predecessorClosedRef`を必須とする |
| 同上・場所の既定 | `defaultCareFocus={mode:auto,siteId:null}`。場所変更がない準備の参照元 |
| 同上・内容 | `contentManifest`：設定・地図・weather profile・target profile・deck・文章集の永続参照、内容hash、byte長。plant appearance profileは採用時だけ含む |
| 同上・未完genesisの保全 | `genesisRecovery?`：§9.3の未完genesis原本への診断参照。存在しない履歴やRecoveryContinuedを作らない |
| `RunSwitchRequested` | `commandId`, `reason`, `targetRunId`, `targetRunPlan`, `plannedBy={appVersion,buildId,platform}`。meta.roundId=null。同旧runで一つの交代要求 |
| `TargetRunPlan` | 新RunStarted用の初期snapshot、採用版、contentManifest、defaultCareFocus。新genesisの実行元app/build/platform、自身のseq/hash・保存/開始時刻・genesisRecovery・switchRequestRef・predecessorClosedRefはgenesis作成時に付ける |
| `RunClosed` | `closingHeadRef`, 終了理由、`lastWorldRevision`。交代では`switchRequestRef / targetRunId`を必須とし、進行中roundを残して通常終了しない |
| `WorldSnapshot` | simulation/weather profileとmapの内容hash、グリッド形状、地形・障害物・土壌容量、地表水・土中水・端数、植物状態、tickと累計、`worldRevision`, `stateHash` |

外観表現を未採用なら`appearanceProjectionVersion=null`、contentManifestの外観profile参照もなしとする。採用するrunは対応する版と原本hashを両方持ち、片方だけを保存しない。未採用を「出生条件の見た目を復元できる」と表示しない。

グリッドや植物状態の正確な要素は自然モデルの仕様で固定する。
浮動小数の丸め、列挙順、境界処理、時間刻みも数値表現・計算版に含める。
固定時間刻みだけを根拠に、Unity PhysX の端末間完全一致を主張しない。
異なる端末での完全一致は、同じ保存入力を使った監査で確認する実装上の受入条件とする。

`stateHash = SHA-256(JCS(WorldSnapshotからstateHashだけを除いたobject))`の小文字16進値とする。snapshotはSIMULATIONの状態項目を全て含み、セル配列はcellId昇順、個体配列はcellId・plantIdの順とする。整数は§9の範囲、IDはordinal比較を使う。
`Resolved.postStateHash`は`postState.stateHash`と同じ値。外側のrecord、hash自身、文章、演出trace、UI状態をsnapshot hashへ入れない。内容原本を識別するcontent hash、journal record全体のentryHashとは別の対象をhashする。

### 5.2 配面と意図

| DTO | 必須情報 |
|---|---|
| `CareFocusChanged` | `commandId`, `runEpoch`, `mode`, `siteId`, `targetProfileHash`。meta.roundId=null。autoはsiteId=null、fixedは有効なsiteId |
| `PreparationStarted` | 開始状態の`sourceSeq / sourceEntryHash / beforeRevision / beforeStateHash`、`commandId`, `preparationId`, 配面ポリシー、予定buffer長 |
| 同上・場所 | `careFocusPreference`, `careFocusSourceRef={seq,entryHash}`, `targetProfileHash`, `targetPolicyVersion`。保存時点の最新の有効な設定を固定 |
| 同上・天候 | `weatherPeriodIndex`, `weatherMode=acquire/reuse`, `weatherAcquisitionId?`, `weatherSourceRef?`, `weatherProfileHash`。acquireでは取得ID、reuseでは保存済み参照を必須とする |
| `WeatherEntropyCommitted` | `weatherPeriodIndex`, `initiatedByPreparationSeq`, `weatherReceipt`, `weatherProfileHash`, `weatherPolicyVersion`。meta.roundId=nullのrun単位record |
| `MappingEntropyCommitted` | `preparationSeq`, `weatherSourceRef`, `mappingReceipt`, 材料形式、変換版 |
| `Prepared` | `mappingEntropySeq`, PreparationStartedと同じ開始状態参照/hash、`faces[6]`, 候補重みと状態量、材料消費記録、`selectionAtPrepared`, `selectionSourceRef?` |
| 同上・天候 | `weatherContext`：下記WeatherContextを固定。MappingEntropyCommittedのweatherSourceRefと一致 |
| `WeatherContext` | `weatherPeriodIndex`, `periodStep`, `selectionKind`, `weatherId`, `weatherSourceRef={seq,entryHash}`, `weatherProfileHash`, `resolvedWeatherParameters`, `entropyConsumption` |
| Prepared・場所 | `targetContext`：下記TargetContextを固定。PreparationStartedの設定・参照と一致 |
| `TargetContext` | `targetPolicyVersion`, `targetProfileHash`, `careFocusPreference`, `careFocusSourceRef`, `drawnSiteId`, `resolvedSiteId`, `siteDrawConsumption` |
| Prepared・入力範囲 | `targetMode=site_anchors`, `anchorRefs`, `simulationStepCount`, `fixedDelta`, `preparedAtUtc`。4 anchorはresolvedSiteIdの中心セルへ対応 |
| `FaceAssignment` | `faceId=1..6`, `recipeId`, `elementId`, `resolvedParameters` |
| `resolvedParameters` | `amount`, `unitId`, `targetCells[]`, `cellWeights[]`, `radius`, `direction?`, `startTickOffset`, `durationTicks`, `injectionSchedule` |
| `IntentCommitted` | `preparedSeq`, `kind`, `target?`, `freeText?`, `method`, `committedAtUtc` |
| 同上・比較 | `matchingFaceIds?`, `chanceNumerator?`, `chanceDenominator?`, `eligibilityReason` |
| 同上・経過 | 実際に計測した長押し時間、前回提示文への参照、前回の意図からの継続か |
| 同上・選択 | `selectionChangedAfterPrepared`, `selectionMode=carried/selected/none`。開始時の目標はPreparedのmetadataを参照 |

WeatherContextの期index/stepはbeforeRevisionから導出した値と照合し、weatherSourceRefは同run・同一期の唯一のWeatherEntropyCommittedへ結ぶ。取得した準備のroundIdが違っても、同一期の再利用を許す。profile hashはRunStartedと一致し、weatherIdと消費範囲は保存材料から再現できなければならない。
resolvedWeatherParametersは[weather-profile.v0.4.json](./content/weather-profile.v0.4.json)を当該mapへ展開した日差し・風・降雨の対象/量/scheduleであり、UIの天候アイコンから推測しない。entropyConsumptionは先頭4byte・棄却0・変換版を残す。
静的mapから新規作成する初期セルのmeanLightは固定clearの光500と静的露出から作り、初期期のWeatherContextと照合する。同runの復帰では保存済みmeanLightを現在の空で作り直さない。RunStartedだけで天候抽選を省略する別経路は設けない。

TargetContextの設定・出所はPreparationStartedの固定値と一致させ、drawnSiteIdはMappingEntropyCommittedの先頭から検証する。`siteDrawConsumption`は`startByteOffset=0 / endByteOffsetExclusive / rejectedWordCount`を含む。resolvedSiteIdはauto/fixedの規則で決まり、anchorRefsと6面のresolvedParameters・候補重みが、その場所の同じ開始状態から解決されたことを照合する。場所抽選の消費範囲は全mapping消費記録の先頭部分と一致し、fixedでも空にしない。
後着のCareFocusChangedを現在のTargetContextへ取り込まず、設定record自体を庭のsnapshot hashへ加えない。場所が違っても今回選ばれた作用・状態が同じなら、自然状態も同じになり得る。設定変更を、必ず違う結果を生成するための隠しseedとして使わない。
旧runは当時のmapping policyと保存済みanchor・resolvedParametersを使う。v0.6の場所設定や先頭の場所抽選を後付けせず、旧固定anchorの結果を三地点方式の結果へ読み替えない。

`kind` は `none / face / element / gardenOutcome / freeText` とする。
`method` は選択された `asIs / intention / prayer / spoken / unknown` とする。
方法の選択は実行の自動検出ではない。音声録音・文字起こしがない状態でも使える。
意図を前回から維持する場合も、その回の配面に対する `matchingFaceIds` を確定し直す。
`gardenOutcome` の対象は出来事として残すが、面の一致率とは別の記録にする。
比較値を持つのはface/elementだけ。該当面なしは空配列・0/6、それ以外のkindは比較値をnullとする。
`selectionAtPrepared`は配面生成に使わない目標のmetadataとし、比較用の`{kind,target}`を保存する。同runでPreparedより前の最後のIntentCommittedからこの組を引き継ぎ、`selectionSourceRef={seq,entryHash}`を残す。none/freeTextはどちらも構造化した目標なしの`{kind:none,target:null}`へ対応させ、前の確定意図がなければ同じ既定値・参照nullとする。`selectionChangedAfterPrepared`はこの組と今回の確定意図から同じ規則で求めた組の一致だけで決める。kindは完全一致、faceのtargetは整数、element/gardenOutcomeは固定IDをordinal比較する。方法・本文だけの変更や編集クリック数を目標変更へ混ぜない。中断前の未確定draftはこの保存値を上書きせず、復元時も保存済みの元意図・基準と区別する。
6面すべての`resolvedParameters`を保存し、recipe名だけから後の版で値を推測しない。
`injectionSchedule`には総量をどのtickへ配るかを持たせ、総量と毎tick量を区別する。

### 5.3 取得と結果

| DTO | 必須情報 |
|---|---|
| `AcquisitionStarted` | `acquisitionId`, `intentSeq`, `preparedSeq`, 開始時刻、適用乱数ポリシー |
| `EntropyReceipt` | role、取得 ID、実 source、adapter版、開始・終了、`status=acquired/failed` |
| 同上・成功／失敗 | acquiredは`materialFormat=raw-bytes`, `rawBytesHex`, byte長。failedは材料null、固定した安全なerrorCode |
| `DrawEntropyCommitted` | `acquisitionSeq`, 成功した`outcomeReceipt`, 成功または失敗の`oracleReceipt`。一つのframeで保存 |
| `OutcomeCommitted` | `drawEntropySeq`, `faceId`, 選択面参照、`cardId?`, `cardStatus`, roleごとの消費範囲・棄却数・変換版 |
| `Resolved` | `outcomeSeq`, Preparedと同じ開始状態参照、完全な `postState`, `postStateHash`, `weatherApplicationEvidence`, `inputApplicationEvidence`, `events[]`, `message`, `presentationPlan`, 計算版 |
| `PresentationReceipt` | `resolvedSeq`, `presentationAttemptId`, `phase=presenter_started/presenter_completed`, アプリ報告時刻 |
| `InterruptedUnresolved` | `acquisitionSeq`, 検出時刻、理由、`outcomeKnown=false` |
| `PreparationInterrupted` | `preparationSeq`, `weatherSourceRef?`, `mappingEntropySeq?`, 理由と取得失敗receipt?。出目の欠測とは別 |
| `AbandonedBeforeAcquisition` | 準備・配面・意図の存在する参照、取消command ID、終了理由。交代による終端はswitchRequestRefを持つ |

`EntropyReceipt` に認証 token、API key、リクエストの秘密ヘッダーを保存しない。
`cardStatus`は`drawn / unavailable_entropy_exhausted / unavailable_local_failure`。cardがあるときだけテーマ選定を使う。
cardStatusがdrawn以外なら通常のfactual順位付けを行わず、その入力系統の基本文へ直接進む。
この分岐はfocus/primary/secondaryをnull、cardIdをnullとし、カードの紋章を表示しない。
基本文にも実適用の根拠が必要であり、根拠自体が欠ける障害を成功文で埋めない。
bundleが完全に保存される前の終了と、保存済み材料の未解釈を区別する。
検証済みbundleから再現できる出目は、Outcomeの追記前や庭・文章・演出の障害でも結果集計へ残す。
不正なframeや未対応の変換版から結果を推測せず、検証不能として別件数にする。
検証時は参照先の同一run/round・段階順と、材料から導いたface/card・棄却数の一致も確認する。

### 5.4 出来事・文章・つながり

| DTO | 必須情報 |
|---|---|
| `SimulationEvent` | `eventId`, `kind`, `ruleId/version`, 発生tickまたは集約区間firstTick/lastTick、対象 cell/植物 ID |
| 同上・根拠 | ダイス入力・天候の文脈参照、源別注入のsourceKind、判定に使った状態量と変化、参照する過去状態・出来事 |
| `WeatherApplicationEvidence` | Prepared.weatherContext/weatherSourceRef、実適用した天候の降雨tick/対象/量、各セルの基礎光・基礎風と合成場への参照、適用完了 |
| `InputApplicationEvidence` | 確定recipe・scheduleへの参照、実際に適用したtick/対象/量、適用完了。予定値と実適用を区別 |
| `ObservationGroup` | `groupId`, `kind`, 対象ID群、`firstTick`, `lastTick`, 元の`eventIds`, `aggregateEvidence`, `groupingVersion` |
| `SelectedMessage` | `templateId`, 実際の `text`, `locale`, 文章選定版、参照した `eventIds`, `cardId?`, `cardStatus` |
| 同上・選定 | `focusGroupId?`, `supportingGroupIds`, `predicateBindings`, `selectionPolicyVersion`, `scoreTuple?`, `recentMessageIdsCompared`, `recentResolvedSeqsCompared`, `fallbackUsed`, `fallbackReason?` |
| `PredicateBinding` | predicate名、具体的なsourceRefs、対象plant/cell、tickまたは区間、観測値、版付き閾値への参照 |
| `PresentationPlan` | `primaryGroupId?`, `secondaryGroupId?`, 参照した`observationGroups[]`, `groupingVersion` |

天候とダイスの適用証拠は別々に保持する。水収支も`weatherInjected / diceInjected / totalInjected`を分け、注入の合計を使って保存則を検査する。混合後の流れ・吸水・土中水に、記録していない由来比率を割り当てない。
光・風は天候の基礎場、上限適用後の合成場、ダイスが追加した実効差分を区別して残す。実効差分が0なら、天候だけによる光・風の記録でダイスの局所到達述語を成立させない。証拠の具体的な項目と合成順はSIMULATIONを正とする。

ObservationGroupは、実イベントを意味と場所でまとめた表示用の投影であり、新しい自然イベントや独立台帳ではない。
`observation-groups.v0.4`では同kindの4近傍成分を基本とし、WaterEnteredはsourceKind=dice/weatherごとに分離する。全域集約のSurfaceFlowObservedだけは一つのglobal groupにする。分離した流れへ集計値を複写せず、局所到達の根拠にしない。groupのIDと述語の束縛はGAMEPLAY§6〜7を正とする。
Resolvedには全SimulationEventと、文章・表示で参照するgroupだけを保存する。全候補groupは同じgroupingVersionから再生成できる。
`primaryGroupId=focusGroupId`とし、secondaryは最大一つ。言葉とFX/SEは同じ焦点を参照する。
input_pairの二つ目のgroupは必要な補助焦点として優先する。supportingGroupIdsは焦点以外の全ての述語根拠groupの参照であり、表示する一つのsecondaryとは区別する。
`predicateBindings`は文の各成立条件を具体的なイベント・入力・状態へ結び付け、group名だけを真偽の根拠にしない。
同じ対象を述べる複数条件はplant/cellを合わせ、各述語で定義した時点・区間の関係を満たすことを要求する。異なる場所の事実を組み合わせて成立させない。
v0.6で作用場所が変わっても、文章の根拠に地域名や選択設定だけを使わない。同じcell・plant・tickの実イベントで既存の述語を評価し、場所に合わせた演出のために新たな事実を作らない。
複数の完全な束縛が成立するときは、GAMEPLAY§7.3のsubject・tick・source eventによる固定tupleで一つを選び、その束縛を保存する。辞書の列挙順や追加乱数で選ばない。
イベントのない事実fallbackではfocus/primary/secondaryをnullにでき、確定入力・前後状態を直接参照する。
fallbackの`input_applied`にはInputApplicationEvidenceが必要。Outcomeで入力が選ばれただけでは成立しない。
`templateId`はcatalogの`entries[].id`を保存する。文章を指すmessageIdと別の独立IDを生成しない。
現行の`oracle-focus.v0.7`はv0.5の順位を継承し、factual候補は、scoreTuple=`cardAffinity / recentNovelty / priority`の降順、その後messageId・groupId・bindingKeyの昇順で比較する。bindingKeyは保存したpredicateBindingsから導出できるため独立recordにしない。
同じmessageIdとfocusGroupIdの完全な束縛は最小bindingKeyへ先にまとめる。全factual候補の最終順位1位のgroupを、GAMEPLAY§7.2の注目候補の第2枠へ必ず含める。第1・第3枠の固定重要度にはrecentNoveltyを混ぜず、fallbackを通常候補やgroup選抜へ入れない。主焦点1・補助最大1・input_pairの必須相手を維持する。
現行の[oracle-catalog.v0.7.json](./content/oracle-catalog.v0.7.json)では岩陰のつぼみの文に、同cellの正の追加光とLightReceivedの主焦点を要求する。既存のLightReceived・predicateBindings・focusGroupIdを使い、未変化を表す架空の自然イベントは作らない。選定版とcatalogの実体をRunStartedに固定し、v0.4のscore順やv0.5までの成立条件で保存した結果をv0.7へ読み替えない。過去の文章・焦点・根拠は当時のまま保持する。
通常rankingを使わず基本文を選ぶ場合はscoreTupleをnull、比較ID/seq列を空にし、fallbackReasonを`no_factual_candidate / card_unavailable`から記録する。
重複回避は、**同runでseqがPrepared.seqより小さい直近3件のResolved**に保存されたmessageIdだけと比較する。
3件はResolvedレコード数であり、同じmessageIdが重複していてもそのまま含める。3件未満なら存在する分だけ使う。
比較したmessage IDとResolved seqを新しい順に対応して保存する。提示の有無や後着Receiptで比較集合を変更しない。
`recentNovelty`は、この固定集合に候補messageIdがなければ1、あれば0とする。
これは既に採用した文章との比較であり、すべてを本人が読んだという意味ではない。

「前の水と今回の光で育った」と示すには、成長ルールが参照した湿り・照射の根拠を残す。
過去のどの注水に由来するかまで追跡していなければ、特定の過去 round を原因と断定しない。
文章を将来の新版で再生成しても、実際に提示した元の文章は置き換えない。
表示対象の文は `Resolved` に保存し、提示の有無は `PresentationReceipt` で別に記録する。
Receiptの完了は提示処理の報告であり、人が読んだ・理解したことを表さない。
Receiptなしでも画面に一度出た可能性があるため「未読」と断定しない。
現在形は提示対象の終状態にも成立する条件、途中の出来事は過去形を使う。
過去の文を日記で開く場合は当時の回と庭を明示し、現在の庭の描写へ読み替えない。

### 5.5 誕生時の見た目を保存事実から再構築する

外観表現を採用するrunだけ、`appearanceProjectionVersion=birth-appearance.v0.6`と[plant-appearance-profile.v0.6.json](./content/plant-appearance-profile.v0.6.json)の実体hashをRunStartedへ固定する。未採用なら版はnull、外観profileの参照は省略し、共通の中立外観を使う。これは描画projectionの版であり、自然状態・成長係数・seedの出自を変更する版ではない。新しい自然状態フィールド、外見の抽選role、外見専用journalイベントは加えない。

originKind=seedInputの個体では、seedSourceRoundIdが指す保存済みSeedPlacedをplantId・cellIdで照合し、そのsoilQAtPlacementと、同じ回のPrepared.weatherContext.weatherIdを使う。湿りは配置が実際に起きた時点の記録であり、Preparedの土中水や現在の土を再計算して代用しない。湿り0〜399 / 400〜700 / 701〜1000をivory / rose / lilac、天候clear / cloudy / drizzleをpetalWidthQ=1000 / 900 / 1100へ対応させる。値と名称は候補profileに固定し、過去個体を新版の配色へ黙って置換しない。

originKind=initialMapの初期個体は固定のivory・花弁幅1000とし、初期map由来であることを示す。架空の播種時点や天候を作らない。seedInputの必須記録が欠ける場合は中立の仮表示と詳細での不明表示を使い、初期個体の出自へ読み替えない。検証障害の扱いは既存の参照検証に従う。

描画cacheは上記の参照と版から再生成できるものとし、exportに必要な元recordとprofile実体を含める。同じ証拠と版なら端末・再起動・現在天候で配色が変わらない。花弁幅は見た目の係数で、光の露出、風の遮蔽、土の容量、種の到達範囲、stateHashの入力へ戻さない。詳しい表現上の使い方は[DIVERSITY.md](./DIVERSITY.md)を参照する。

## 6. 動的配面の意図と基準確率

`face / element` の事前に固定した意図についてのみ、次の値を計算する。

```text
matchingFaceIds = Prepared の6面のうち、固定した target に合う faceId の集合
p_t = matchingFaceIds.Count / 6
Y_t = 出目が matchingFaceIds に含まれるなら 1、それ以外は 0
```

- `p_t=0`：今回の配面では実現しない意図。履歴に残し「該当する面なし」と示す。
- `p_t=1`：すべての面が該当する意図。履歴に残し「すべての面が該当」と示す。
- `0<p_t<1`：偶然との比較対象。対応面数と確率を一緒に保存する。
- `none / gardenOutcome / freeText`：この一致率の対象外。結果に応じて自動分類しない。

現行の配面は同系統最大3面なので、面/系統目標のpは0、1/6、2/6、3/6の範囲になる。
p=1や5/6は将来の配面規則や合成投影の境界検証用であり、現行の正常な配面から生成される例ではない。
「花が咲いた」の確率を水や光の面数/6で代用しない。庭の過去状態も結果を決めるためである。
意図の自由文を AI が事後解釈して、一致を増やす経路は設けない。
構造上の不成立・必成立・対象外・欠測の件数を、比較対象の件数とは別に残す。
面番号・系統の目標を配面表示後に選び直すことは認めるが、その選択変更を記録する。
存在しない目標を自動修正せず、対象外やp=0を隠して「願い全体の成功率」を表示しない。

## 7. 設計例：読みやすくした投影

以下は**架空の説明用サンプル**であり、実測データでも読み込み可能な台帳 export でもない。
hash、byte、状態配列、完全な参照を省いた投影で、実装 fixture としてそのまま使用しない。

```json
{
  "documentKind": "illustrative-projection-not-an-export",
  "runId": "example-run",
  "roundId": "example-round-04",
  "faces": [
    { "faceId": 1, "recipeId": "water_drop", "elementId": "water" },
    { "faceId": 2, "recipeId": "wind_breeze", "elementId": "wind" },
    { "faceId": 3, "recipeId": "seed_single", "elementId": "seed" },
    { "faceId": 4, "recipeId": "water_rain", "elementId": "water" },
    { "faceId": 5, "recipeId": "light_soft", "elementId": "light" },
    { "faceId": 6, "recipeId": "wind_passage", "elementId": "wind" }
  ],
  "intention": {
    "kind": "element",
    "target": "water",
    "method": "prayer",
    "matchingFaceIds": [1, 4],
    "chanceNumerator": 2,
    "chanceDenominator": 6
  },
  "outcome": { "faceId": 4, "cardId": "circulation", "cardStatus": "drawn", "source": "local" },
  "observedMatch": 1,
  "eventKinds": ["RootZoneWetted", "SoilMoistened"],
  "text": "水が土に染み込み、草の根元が湿りました。",
  "presentationEvidence": "no-completion-receipt"
}
```

この例は提示が未完了でも一致1回・基準確率2/6として残る。
花が咲いたことや、祈りが出目を変えたことは、この記録から追加しない。
提示完了の記録がないことは、人が文章を見ていない証明ではない。

## 8. 初期グラフと集計

グラフは保存した履歴から毎回再構築できる projection とし、独自の成功カウンターを持たない。

| 表示 | 集計・表示内容 |
|---|---|
| 庭の記録 | 天候、作用場所とauto/fixedの出所、意図、6面、出目、カード、入力位置、出来事、文章を順番に見る |
| 比較できる抽選での一致 | 対象の観測数、実際の一致数`ΣY_t`、対応する確率の基準値`Σp_t` |
| 庭の変化 | 地表水、土の湿り、植物の成長、天候の帯と、ダイス入力を与えた時点 |
| 記録の状態 | 準備・意図・取得・既知出目・比較対象・p=0/1・欠測・対象変更・提示証拠・sourceの内訳 |

比較の既定集合は、出目が既知で`face / element`かつ`0<p_t<1`の回。
既知とは検証済みOutcome、または検証済みDrawEntropyのoutcome bufferから同じ版で1〜6の出目を確定できること。
`OutcomeCommitted`の追記・`Resolved`・提示が未完了という理由で、材料から確定できる出目を除外しない。
欠測を成功・不成功に補完せず、観測集合に対応する`Σp_t`を使う。欠測数を隣に示す。
この値は各回の配面から計算した基準であり、欠測が結果と無関係であることを保証しない。
方法別にも観測数と対応する期待値を示す。祈る方法の自己選択による比較は観察結果である。
庭と配面が過去の結果へ適応するので、固定確率の二項検定や単純な z 判定を追加しない。
各回の `p_t` が得られても、独立試行を仮定した Poisson-binomial 検定を自動採用しない。
天候は意図の前に分かる条件として保存し、天候別に眺める場合も同じ対象集合と各回のp_tを保つ。晴れ・雨で開花回数が異なることを、面の一致率や祈りの効果へ読み替えない。天候期は出目試行数ではなく正常解決投で区切り、欠測回に架空の40tickを追加しない。
初期版は有意性や「効果を証明」の判定を持たない。別の検証計画は新しい run/仕様で扱う。

### 8.1 同じ投影から出す件数

| 件数 | 定義 |
|---|---|
| 配面準備数／準備中断数 | PreparationStarted／PreparationInterruptedのround数 |
| 意図確定数 | kind=noneも含むIntentCommitted数。目標あり件数を別に示す |
| 出目取得開始数 | AcquisitionStarted数。配面用取得を混ぜない |
| 出目既知数 | 保存結果または保存材料から確定できる出目の数。カード未成立も含む |
| 比較対象数 | 既知出目・面/系統目標・0<p<1を満たす数 |
| p=0／p=1件数 | 固定意図と配面が構造上不成立／必成立の数 |
| 出目未成立数／未完了数 | 終端のInterruptedUnresolvedと、まだ進行中の回を別に数える |
| 確定時の対象変更数 | IntentCommitted.selectionChangedAfterPreparedがtrueの数。編集クリック数ではない |
| カード未成立数／検証不能数 | 出目の欠測や予想不一致と合算しない |

例えば目標を水に保った10回のうち9回がp=0で、残る1回だけ一致した場合、比較は1/1でも全体を100%達成と呼ばない。
履歴全体の件数と比較対象の母数を併記し、方法・source・run等のフィルターも表示・exportへ含める。

関係図では、自然モデルの入力・状態・適用ルール・結果を「計算上のつながり」として表示する。
意図・カード・出目の並びは「記録された順序／対応」として区別する。
意図から出目へ因果が証明されたような矢印は描かない。
1回の詳細は「入力→局所状態→適用規則→出来事」の固定配置を基本にする。
投影のedgeKindを`modelDependency / recordedAssociation`に分け、既存イベントの参照から作る。
例えば入力→蒸発→湿り→成長は、該当する計算依存が記録されている場合だけmodelDependencyで示す。
場所については、PreparedのcareFocusSourceRef・resolvedSiteId・anchorRefsから、その回に使われた設定→対象解決→実入力の依存を示す。autoでは保存したmapping材料による場所選定を含め、未適用の次回設定や、固定時に使わなかったdrawnSiteIdを実入力の原因にしない。場所選択はゲーム内の明示操作として示し、祈りや意図の記録からの因果矢印へ統合しない。
毎回40tickの天候による基礎光・風・降雨と、その後の蒸発・成長は、ダイスの追加作用が届かない場所でも進む。イベントのinputRefは同roundの文脈参照であり、それだけで局所作用や因果edgeの根拠にはしない。
必要に応じ「開始時の水分・成長」「40tickに適用した天候」「今回のダイスの追加作用」を別の計算入力として示す。各入力からは、対象・区間・実適用の証拠がある依存だけを結ぶ。範囲外の花が育っても、今回のダイスの風や追加光の直接効果と表示しない。
天候による注水とダイスによる注水は混ざる手前まで源別に示し、同じ土中水を二つの原因へ全量ずつ複写しない。天候の帯を過去の記録へ表示するときも、現在の天候で当時の背景を塗り替えない。
汎用ノードエディタや別のグラフ台帳は設けず、庭の歩みはround順の水量・成長推移と入力記号から表示する。
前の文章が次の意図へ影響した可能性を検討できるよう、文章と提示証拠への参照を残す。
提示証拠を読了判定に変換せず、機械が観測できなかった状態は不明のまま扱う。

## 9. 永続化 adapter の条件

展示版は、長さを持つframe単位の追記journalを、継続可能なsegmentとして保存する。
各 frame は一つの完全な `JournalEntry` を包み、途中書き込みを検出できる形にする。
詳細なbyte構造、OSごとのflush/fsync相当、プロセス間ロック、容量上限はG1で固定・検証する。
単なる JSON 追記や `OnApplicationQuit` の保存だけで durable commit を満たしたと扱わない。

hash は既存方式と同じ RFC 8785/JCS と SHA-256 の規則を新しい journal に適用する。
論理 hash object は `{createdAt, payload, prevHash, type}`、結果は小文字16進 SHA-256 とする。
`createdAt` は保存した `createdAtUtc` の文字列をそのまま使う。
識別子・schema・seq は `payload.meta` に含め、hash の対象から漏らさない。
genesis は `RunStarted`、その `prevHash` は64文字の `0` とする。
append と verify は同一の canonicalization/hash 実装を共有する。
NaN/Infinity、曖昧な数値表現、順不同のモデル更新を契約へ持ち込まない。
JSON numberとして保存する整数は絶対値`2^53-1`以内に制限する。
それを超える値を必要とする版では正規化した10進文字列などの表現を`numericEncodingVersion`で定義する。
Coreの64bit中間演算はoverflowを検出する。大きい値を丸めて検証成功にしない。

single-writer が「head読取 → コマンド・遷移・一意性検査 → canonicalize → hash → frame追記 → durable確認」を直列化する。
UI、provider、シミュレーターは保存先へ直接書き込まない。集計 cache は再生成可能とする。
`Resolved.postState` が再開時の正であり、再計算は監査用。差異があっても過去状態を置換しない。
ローカル hash chain は改竄検知可能であり、外部の証跡なしに完全改竄不能とは表現しない。
端末間同期は初期版の対象外。後から導入しても journal の競合を新しい結果として合成しない。

### 9.1 不完全末尾の継続手順

1. 排他権を取得し、対象segmentの最後の完全なframeまで長さ・JCS・hash・参照・状態遷移を検証する。
2. EOFで途中になった末尾を`incomplete_tail`と診断する。完全なframeのhash不一致は別の障害とする。
3. 元segmentをそのbyte列のまま封印する。truncate・書換え・壊れた末尾の後ろへの追記をしない。
4. `sourceSegmentId / sourceFileHash / sourceByteLength / verifiedEndOffset / lastSeq / lastHash`を回復情報とする。
5. この情報のJCS hashを安定した`recoveryId`とし、同じ元状態の再起動は同じIDへ収束させる。
6. 新segmentの最初に`RecoveryContinued`を追記し、`seq=lastSeq+1`, `prevHash=lastHash`で継続する。
7. そのrecordに回復情報・新segment ID・回復attempt番号を含め、durable確認後に通常の復元と追記を再開する。
8. 起動時はsegment群から有効な継続を探す。head checkpointだけを正にせず、同じrecoveryIdを二重適用しない。

RecoveryContinuedの保存前に再び終了した場合も、途中の新segmentを保全し、同じrecoveryIdの次attemptで継続する。
すでに完全な継続recordがあれば、その先を採用し、別の継続recordを作らない。
同じ境界から矛盾する完全な分岐が見つかった場合は、更新日時で勝者を選ばず検証障害として止める。
`RecoveryContinued`の一意キーは`runId + recoveryId`。回復attemptの未完frameは正規イベントではない。
これはrun単位の回復記録であり、roundの論理状態を変更しない。次のroundイベントは最後の確定段階から続ける。
exportはsegmentの接続・回復診断・参照した原本を含め、途切れのない単一ファイルだったように偽装しない。

途中のhash不一致・不正な完全frame・必須参照欠損は`integrity_error`として旧runの書込みを止める。
原本を保全したまま、閲覧できる検証済み範囲と障害を表示する。
継続不能なら、G1〜G3では原本と検証済み範囲を保全し、回復元・未確定範囲を示したうえで静的mapから新runを開始できる。旧runの失われた結果を埋めず、旧runの完全な復旧とも呼ばない。
検証済みsnapshotそのものを別runへ引き継ぐ機能は後続拡張とする。同runの正常な末尾回復・保存済み状態の復元とは区別する。

### 9.2 内容参照の永続性

設定・地図・weather profile・target profile・deck・文章集と、採用時だけplant appearance profileは、内容hashをキーにした保存領域へ書き、byte長・hash・durable保存を確認する。
その後にだけ、それらを参照するRunStartedを保存する。snapshotの外部参照にも同じ順序を使う。
現在のアプリに同梱された名前付きassetや、変更され得るURLだけを永続参照にしない。
runが参照中の内容は更新で置換・削除せず、exportにも実体または完全な自己完結bundleを含める。
保存状態の閲覧と、旧版モデルの再計算は別の能力である。対応する計算版がない場合、新版で代用しない。

### 9.3 新genesisの未完frameを、同じ交代先へ回復する

この節は、RunSwitchRequestedが固定したtargetRunIdについて、まだ完全なRunStartedがない境界を扱う。§9.1の通常回復は最後の完全recordがあることを前提とするため、genesis前へRecoveryContinuedを置く手段には使わない。

1. 保存adapterの排他権を取得し、固定targetRunIdの全attempt segmentを検査する。完全な先頭RunStartedがあれば、seq=1・prevHash=64文字の0・旧RunClosedと要求への参照・targetRunPlanとの一致を検証して採用する。保存応答の有無で追加のgenesisを作らない。
2. 完全なRunStartedがなく、EOFで途中の最初のframeだけが残っている場合は、それぞれをbyte列のまま封印する。旧runや別IDの新runへ書き直さず、truncate・削除もしない。
3. 各未完原本の`sourceSegmentId / sourceFileHash / sourceByteLength / diagnostic=incomplete_genesis`を保全する。これらは正規のrunイベントでもseq=0のrecordでもなく、書込みattemptの診断である。
4. 同じtargetRunIdで新しいattempt segmentを作り、固定targetRunPlanと確認済みの旧閉鎖参照からRunStartedを作る。seq=1・prevHashは64文字の0を維持し、payload.genesisRecoveryに保全した全未完genesis原本への診断参照をsourceSegmentId順で含める。新recordの保存時刻は実際のそのattemptの時刻とし、失われた時刻を推測しない。
5. このframeも途中で終了したら、それも保全して手順1へ戻る。一つの完全なgenesisができるまで、run開始成功・通常画面公開・次のPreparationStartedへ進まない。

完全なframeなのにhash/参照が不正な場合は、不完全末尾としてやり直さずintegrity_errorにする。同targetRunIdに複数の完全なgenesisまたは矛盾する継続があれば、一方を更新時刻で選ばない。排他と検証で正規のgenesisを一つにし、正常に始まった後の部分末尾は§9.1へ移る。

この回復は抽選前の静的初期化だけを扱う。新しい庭の初期状態・contentを再選定したり、新たなtargetRunIdを振り直したりしない。exportにはgenesisRecoveryが参照する不完全原本も含め、途中のattemptがなかったことにしない。初回起動など交代要求を持たない新規作成の保存手順はG1で別途照合し、ここで定義した交代回復を無条件に流用しない。

### 9.4 JSONの監査bundleと、CSVの読みやすい一覧

書出し開始時に、対象runと各runの検証済みcutoff head（seq/hash）・選択範囲を固定する。同じ排他区間で各segmentの取り込むbyteEnd（排他的）・その範囲のhash・検証済み末尾offset・状態を確定し、以後追記されてもその保存済みprefixだけを書き出す。これはcutoff時点の保存原本範囲であり、元fileをtruncateする操作ではない。prefixのhashを、後から伸びた元file全体のhashと同じものにせず、出力で範囲と区別する。RecoveryContinuedやgenesisRecoveryがsourceFileHashで全体を参照する封印済みsegmentは、そのbyte列全体と元のhashをそのまま含める。同じcutoffからJSONとCSVを作り、閉鎖中のrunや対応する新runもその時点の状態として明記する。

JSONによる書き出しは、選択したrunのプレイとグラフを再構成できる監査bundleとする。そのrunの論理record、参照するcontentの原本、元segmentのbyte列、回復診断、検証範囲と状態を含める。byte列は可逆なbase64などの版付き表現とbyte長・hashで保持し、再整形したJSONを元segmentの代わりにしない。同runの部分範囲を選んでも、検証・再構成に必要な先行recordと原本を含める。含められない場合は不完全なexportと明記する。

展示の来場者向け書出しは現在runだけに限定する。新genesisのswitchRequestRef・predecessorClosedRefをたどって、前の来場者のjournal・本文を自動的に同梱しない。範囲外runへの参照はID/seq/hashと`scope_outside_export`の診断だけを残し、「選択run内の記録・contentの検証」と「runをまたぐ交代履歴の照合」を分ける。後者を確認していないbundleを、交代履歴まで完全検証済みと表示しない。交代元も含めて照合するexportは、運営側が明示した複数runの範囲でだけ行う。新run自身の初期snapshotとcontentはそのRunStartedから復元でき、前の人の本文をゲームの再構成条件にしない。

cutoff時点でまだ作られていない次runのgenesisは未開始として示し、将来のrecordを補わない。JSON検証と、G1〜G3では対象外の取り込み・別端末でのプレイ再開機能とは区別する。

CSVは、同じ検証済み記録から作る閲覧用projectionとする。roundごとの天候・場所・意図・出目・状態量などを表へ出す。runId、範囲、元recordのseq/hash、schema/投影版、欠測・検証不能の状態を保持し、空欄を0や失敗へ置換しない。CSVだけでjournalの完全な検証・復旧・元のグラフの全情報再生成ができるとは約束しない。`export → verify → グラフ再生成`の完全性の受入対象はJSON監査bundleであり、CSVはその読みやすい要約である。

## 10. 次の実装で確認する受入条件

1. 意図・配面の参照が未確定なら、出目用乱数の取得を拒否する。
2. 準備・材料保存・解釈・解決の各境界で終了し、準備中断と出目未成立を区別して再開する。
3. 252〜255の byte 棄却と1〜6への対応を既知入力で確認する。
4. 同じ配面・出目でも意図によって庭の計算結果が変わらない。
5. 現行の`p=0,1/6,2/6,3/6`と欠測・未解釈bundle・カード未成立・提示証拠なしを検証する。p=1およびp=5/6は合成投影の境界例として別に確認する。
6. 同じ保存入力と版から計算した状態・イベントを、対象端末で監査できる。
7. 本文に参照した出来事が存在し、カード・文章・FX が結果を変更しない。
8. frame途中終了、回復segment途中終了、容量不足、改変、重複取消を検出し、未保存結果を表示しない。
9. export → verify → グラフ再生成が、ネットワークなしで一致する。
10. 旧実験の保存先・schema・統計・凍結パラメーターへ変更を加えない。
11. outcome全byte棄却とoracleだけ全byte棄却で、未成立と出目保全がそれぞれ正しく働く。
12. 意図保存・取消・AcquisitionStartedの順序を入れ替え、取消済みの画面の背後で取得を開始しない。
13. blob保存前の参照、失われた内容、二つの競合segmentを正常なrunとして扱わない。
14. Prepared後の遅延Receiptや文章選定前の中断で、比較する直近3Resolved・scoreTuple・採用文が変わらない。
15. 期0の固定晴れと期1以降の重みを確認し、全256個のUniformBelow(256)の出力が固定順の重みに対応する。初期期も4byte消費・棄却0を保つ。
16. 同一期の取消・mapping失敗・再起動で、WeatherEntropyCommittedのrefと天候値を再利用する。異なるperiodやrunの材料参照、既知材料の再取得を拒否する。
17. weather保存前、weather保存後/mapping前、Prepared後、Resolved保存前後で終了し、未知の天候・既知の天候・次期への進行を区別する。
18. 3回目のResolved保存後だけ次期を準備し、旧演出の途中には次の天候を公開しない。欠測・取消・提示だけでは天候期を進めない。
19. 天候だけの雨とダイスの雨が同時にある回で、源別注水と総収支を一致させ、混合後の土中水の由来を捏造しない。
20. v0.5から順位を継承した現行v0.7の同一カード対応候補では直近に使っていない文をpriorityより先に比較し、全候補既出なら固定順位へ戻る。全候補の勝者をgroup選抜で失わず、同じ入力順序を並べ替えても同じ結果になる。
21. 静的mapからの新runだけを固定晴れの初期meanLightで作り、同run復帰の保存値を初期化しない。G1〜G3で未定義の別run snapshot引継ぎを暗黙に行わない。
22. auto・fixedで同じmapping材料の先頭を同じUniformBelow(3)で消費し、drawnSiteIdと終了cursorが一致する。fixedの実作用先だけは固定設定に従い、その場所でneedと6面を解決する。
23. 場所の設定変更をPreparationStartedの直前／Prepared公開後のReadyへ並べ、直前だけが今回分へ入り、公開後の変更は次回分に残ることを確認する。公開済みPreparedの場所・6面・目標基準確率は書き換わらない。
24. Presentingで受け付けた場所設定→終状態表示→次準備の順、Preparing中の操作拒否、保存失敗と再送、旧run callback、同じ設定のno-opを確認する。保存完了の通知前に終了しても完全な設定recordは再開後に採用する。
25. 新runの既定auto、同run再開時の今回／次回の区別、未成立・取消後の次準備を照合する。場所の重複や種の空き不足で勝手に場所を再抽選しない。
26. 同じSeedPlaced・同じ回の天候・同じ外見profileから外見を再構築する。湿り399/400/700/701、三天候、初期個体、参照不足を確認し、外見の設定変更だけで自然状態や配面が変わらないことを照合する。


27. 交代要求の保存前後・完了応答前で終了し、未受理と完全frameありを区別する。同commandの再送・別commandの連打でもtargetRunIdと要求recordが一つに収束する。
28. 交代要求を取得開始の前後へ並べ、取得前は取消、取得後は既知材料・結果を元runへ保全する。旧callbackの内部保存を許しても、旧本文・音・意図を通常画面へ再公開しない。
29. RunSwitchRequested後、旧round終端後、RunClosed後、新RunStarted保存後の各停止点から固定targetRunIdへ収束する。旧runの再適用・新しい配面準備・閉鎖後Receipt・二つ目のgenesisが発生しない。
30. 新genesisのframe途中で繰り返し終了し、元attemptを保全したままseq=1のRunStartedを一つ作る。RecoveryContinuedをgenesisより前に置かず、完全frameのhash不正や競合genesisを末尾欠損として修復しない。
31. 保存したtargetRunPlanと異なるcontentや初期状態、旧RunClosed参照の欠損を拒否する。アプリ更新後も予定された初期状態・版を保ち、plannedByと新genesisの実行元を区別する。未対応の旧モデルを新版へ代用しない。
32. JSON監査bundleのcutoff head・segmentのbyteEnd/hashを固定した後に追記があっても、書出し内容と検証範囲が変わらない。prefixのhashと封印原本の全体hashを区別する。選択runのcontent・元segment・未完genesis原本を含む復元と、CSVの閲覧用要約を区別する。来場者exportに前runの本文を同梱せず、範囲外の交代参照を検証済みとしない。
33. 本文だけ、方法だけ、構造化目標の変更と、none/freeTextの移行を分けてselectionChangedAfterPreparedを確認する。出所refと既定の目標なしを同じ規則で復元する。
34. 外観未採用のrunでは版null・profile参照なしで基本体験と復元が成立し、採用runだけが保存した外観profileを使う。

代表的な復旧trace（実行済み結果ではなく、G1のfixture要件）:

- `PreparationStarted → kill`：準備中断を残し、出目取得数は0。
- `WeatherEntropyCommitted → kill／mappingなし`：旧準備を閉じ、次の準備で同じ期refを再利用。既知天候の再取得は0。
- `WeatherEntropyCommitted → 取消 → 同期に再準備`：材料はrunの期へ残り、同じ天候を使う。取消された準備へ遅れて届いた材料は採用しない。
- `rev2で出目未成立 → 次の準備`：期0の3投目を再び準備し、庭の時間と天候期は進めない。
- `rev2のResolved保存 → 終状態表示 → rev3の準備`：受け付け済みの場所設定を先に保存し、期1の材料を一度だけ取得して次の天候を意図の前に固定する。旧結果の提示中は次の準備を始めない。
- `MappingEntropyCommitted → kill`：固定済み場所設定と同じ材料から同じ場所抽選・6面。local取得回数は増えない。
- `CareFocusChanged → kill`：完全なrecordがあれば次回設定を復元する。既存Preparedの場所へは反映せず、同commandの再送で二重記録しない。
- `場所設定の保存失敗 → 次準備要求`：失敗を未保存のまま飛ばさず、同じコマンドの回復後に次のPreparationStartedへ進む。
- `DrawEntropyCommitted → kill`：同じ出目とカードへ復元し、Outcome未追記でも既知出目を失わない。
- `outcome受理／oracle全棄却`：同じ出目で庭を解決し、カード未成立＋事実文。カード再取得は0。
- `outcome取得成功／oracle local例外`：生存processは材料＋失敗receiptを保存し、出目と庭を保全する。
- `oracle取得中kill／bundleなし`：未保存のoutcome材料を再現したことにせず、出目未成立を残す。
- `Resolvedあり／Receiptなし`：同じ結果を復元し、読了確認やReceipt補完を次回開始の条件にしない。
- `OutcomeCommitted → Resolvedの末尾破断`：原本を残してsegment継続後、開始状態から一度だけ解決する。
- `取消commit → 遅延した取得開始要求`：状態・epoch照合で拒否し、新しい乱数は取得しない。
- `RunSwitchRequested → kill`：旧本文を公開せず、旧roundの保存段階から閉鎖を再開する。
- `RunClosed → kill`：固定targetRunIdのgenesisだけを作る。genesis保存後のkillなら既存recordへ収束する。
- `新RunStartedの途中frame → killを反復`：未完attempt原本を残し、同じtargetRunId・初期計画で一つの完全genesisへ進む。

これらは今後の検証項目であり、この設計文書の追加時点で合格済みとはしない。
