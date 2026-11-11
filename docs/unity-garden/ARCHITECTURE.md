# Unity箱庭 — アーキテクチャ

状態: 設計案 v0.3 / Unity未実装
作成日: 2026-11-11
対象: 新しい箱庭ゲームのUnity版。今回の変更は設計資料のみ。

## 1. 適用範囲と正となる文書

この文書は、iOS・Android・Steam向けWindows版を一つのUnity projectから作るための責務分担を定める。既存のReact/Capacitor版は、[DESIGN.md](../../DESIGN.md)と[PROTOCOL_FREEZE.md](../../PROTOCOL_FREEZE.md)に従う別の実装として維持する。
Unity版は新しいschema・保存領域・セッション識別子を持ち、既存DBや確証解析へ箱庭の記録を混ぜない。既存台帳を取り込む場合も、別途定義する読取・検証・移行機能を通し、元データを編集しない。

状態遷移、recordの必須項目、hash、欠測・回復規則の正は[DATA_CONTRACT.md](DATA_CONTRACT.md)。
本書は同じ契約を別方式で再定義しない。実装順と段階の完了条件は[ROADMAP.md](ROADMAP.md)を参照する。
画面の進行と操作は[EXPERIENCE.md](EXPERIENCE.md)、初期庭の配置候補と参照計算は[GARDEN_LAYOUT.md](GARDEN_LAYOUT.md)にまとめる。
Unity project、C#コード、package、CI設定、ネイティブ設定は、この設計phaseでは作成しない。

## 2. 一つの計算モデルと四つの責務

| 層 | 責務 | 持ち込まない依存 |
|---|---|---|
| Pure C# Domain | 庭の状態、動的6面、格子更新、出来事判定、カードと文章の規則 | UnityEngine、端末時刻、ネット、ファイル、Steam |
| Pure C# Application | 試行進行、commit順序、single-writerへの依頼、復元、projection | MonoBehaviour、画面部品、直接のOS API |
| UnityPresentation | 3D表示、UI、入力変換、カメラ、FX、SE、表示用補間 | 台帳の直接編集、独自の抽選、自然現象の別計算 |
| PlatformAdapter | 保存先、乱数取得、共有、触覚、任意のSteam連携 | ゲームルールの変更、欠測の補完 |

依存の向きは外側から内側へ揃える。DomainはApplicationやPresentationを参照しない。Unityの起動処理でadapterを組み立て、Applicationへportとして渡す。
Applicationが返す公開用projectionをUIへ渡し、監査用データの取得は別の操作にする。
ゲームの進行と「当たったか」の判定は、アニメーション終了コールバックを真実として使わない。

### 2.1 想定するport

| port | 契約の要点 |
|---|---|
| LedgerAppendService | 全書込みを直列化し、commit済みseq/hashを返す |
| LedgerReader | 検証したrecord列と、読み取り可能な範囲を返す |
| EntropyProvider | 実際の出所と取得結果を返す。UIや庭を直接更新しない |
| Clock / IdSource | 時刻と識別子を注入し、試験で固定できる |
| ProjectionStore | 再生成可能な画面用状態。永続cacheは計測後に追加可能 |
| ExportService | 検証済み記録からJSON/CSVを生成する |
| PlatformCapabilities | 共有・触覚・Steam等の利用可否を明示する |

port名は実装時の命名候補。record名・状態名はDATA_CONTRACTに従う。出目、面の割当生成、オラクル、装飾の乱数は用途を分け、同じ可変乱数列を使い回さない。

### 2.2 初日に作る最小構成

責務の分離は、汎用frameworkを先に作る指示ではない。G1は一つのcoordinator、一つのjournal writer/reader、ローカル乱数adapter、固定できるClock、一つのSceneから始める。DI container、SQLite移行、クラウド、複数provider、永続head cacheは展示の必須条件にしない。

初期版の復元は短いjournalを検証走査して行い、最後の`Resolved`と未完了のroundを取り戻す。走査時間・容量を実測して必要な場合にのみ、破棄して再生成できるcheckpointを足す。復元手順・意図の先行確定・single-writerは省略しない。

`docs/unity-garden/content/`のJSONは実装時の照合用の設計資料。現時点ではUnityのloaderも検証器もない。採用時にはschemaと値を検証し、runへ固定するcontentに変換する。
v0.3の庭配置はv0.2の計算profileを使う。文書版・地図版・計算版を一つの番号だと扱わず、contentManifestにそれぞれの実体とhashを残す。参照計算reportは照合用であり、通常プレイの出目列や成長結果を供給するassetにはしない。

## 3. Unity projectの予定配置

予定するproject rootは `unity/DestinyGarden/`。以下は作成予定であり、現時点の実在構成ではない。

| project内の予定パス | 内容 |
|---|---|
| Assets/DestinyGarden/Domain/ | Unity非依存の型と計算。独立asmdef |
| Assets/DestinyGarden/Application/ | 進行・commit・復元。独立asmdef |
| Assets/DestinyGarden/Presentation/ | Scene接続、View、Input、Audio、FX |
| Assets/DestinyGarden/Infrastructure/ | journal、hash、export、乱数adapter |
| Assets/DestinyGarden/Platform/ | Mobile / Desktop / 任意のSteam adapter |
| Assets/DestinyGarden/Content/ | 庭の配置・素材・モデル係数のauthoringデータ |
| Assets/DestinyGarden/Tests/ | EditMode / PlayModeとfixture |
| Assets/Scenes/ | 展示用の一つの入口Scene |
| Packages/ | manifestとlock |
| ProjectSettings/ | Editor・描画・入力・build設定 |

ScriptableObject等のauthoring形式は、起動時に検証して純C#の不変設定へ変換する。
Contentの編集は既存セッションの意味を変えないよう、モデル版・配置版と結び付ける。保存済みの庭を、新しいContentの値で無言に再解釈しない。
旧Web版とのC#・TypeScript相互呼出しや、WebViewへの本体埋込みを最小版の前提にしない。

## 4. 格子シミュレーションと時間

初期設計は16×16、1tick=0.1モデル秒、1投擲=40tick。数値表現とモデルの正は[SIMULATION.md](SIMULATION.md)。変更はモデル版として記録し、既存セッション内で変更しない。
Domainは明示的なstep数と入力から次の状態を求め、描画フレームの回数を入力にしない。
地形・障害物・植生位置は、計算と3D表示が同じ配置データを参照する。
視覚上の岩だけ存在して水の計算では通過する、といった二重定義を作らない。

水の移動、吸水、光、風などの更新順と係数はモデル仕様に従う。
セルの走査順は固定し、一つ前の状態から更新量を計算してから次の状態を確定する。
表示は確定状態の間を補間できるが、補間値を次の計算へ戻さない。
草や空の装飾的な揺れは継続できるが、実践中のCore状態は確定条件に従って停止する。
バックグラウンド中の経過時間を、復帰時に無記録の大量stepとして追加しない。

### 4.1 FPSによらない記録

30fpsと60fpsの比較では、同じ入力列・固定Clock・乱数fixture・step数を使う。この条件でOutcomeCommittedとResolvedの内容、庭の状態、出来事列が一致することを確認する。
実際の人の操作時刻や描画回数まで同一になる、という主張ではない。
表示側が遅れた場合も、Coreのstepを黙って捨てたり、出目を再取得したりしない。
同一端末での再実行と、異なる端末・scripting backend間の再実行は別に確認する。

### 4.2 サイコロ表示との関係

出目はDATA_CONTRACTの抽選経路で確定し、3Dサイコロはその確定結果を表示する。
物理風の軌道や演出用Rigidbodyを使っても、接触結果で確定出目を書き換えない。
庭の変化は格子計算の結果から表示し、出目別の固定イベント動画で代替しない。
自由物理のサイコロを追加する場合は、別mode・別記録として設計する。
PhysXは固定刻みだけで端末間の完全な決定性が保証されるものではない。[Unityの説明][U1]

### 4.3 確定した庭と、演出中の庭を混同しない

Coreの最新状態は保存済みResolvedのpostState。画面は、その同じ計算から得た一投分の経過を一時的に再生できる。表示中の補間値を次の配面の入力にしない。

表示用traceはメモリー上の一投分に限定できる。過去全roundの全tickをjournalへ追加することは最小版の要件にしない。traceが失われた復帰時は保存済みの終状態と文を示し、推測した過程で埋めない。

次の配面を公開するときは、表示している庭がそのPreparedのbeforeRevision/beforeStateHashに対応する終状態になっていることを確認する。前回の流れを再生中の庭へ次の作用範囲を重ねず、演出を省略する場合も終状態への反映を先に行う。

これはPresentation側の公開条件であり、PresentationReceiptの保存を読了・次回開始の必須gateにするものではない。前のcallbackや離した指を、新しいroundのRollへ転用しない。具体的な入力状態はEXPERIENCEに従う。

## 5. 動的6面の生成と試行進行

面の候補は、固定した庭の事前状態・配置・候補規則・専用乱数から生成する。
各レシピの作用セル・遮蔽・予定doseから局所的な必要度を求め、候補重みに使う。実際に作用しない遠方の平均値や、ユーザーの願いを必要度へ代入しない。
6面が確定した後に、祈りの内容や欲しい結果へ合わせて面を差し替えない。
提示する6面、対象地点、対応する要素と強度はPreparedへ記録する。
面の重複等による確率はDomainで求め、UI独自の「当たりやすさ」を作らない。
再起動時は記録済みの6面を読む。現在の庭から再生成しない。
同じrunの未終端roundは最大一つとし、新しいroundIdだけを作って並行取得する経路も拒否する。準備元のworldRevision/hash、commit時の最新状態をDATA_CONTRACTの条件で照合する。

通常の順序は、`PreparationStarted → MappingEntropyCommitted → Prepared → IntentCommitted → AcquisitionStarted → DrawEntropyCommitted → OutcomeCommitted → Resolved → PresentationReceipt`。
詳細な許可遷移・必須項目・例外はDATA_CONTRACTを正とする。

| record | この層が守ること |
|---|---|
| PreparationStarted | 配面の取得前にroundと事前状態を固定 |
| MappingEntropyCommitted | 同じ保存済み乱数材料から配面計算を再開できる |
| Prepared | 事前状態、6面、対象地点など、その試行の条件を固定 |
| IntentCommitted | 実践・意図の確定を受けてから測定用乱数へ進む |
| AcquisitionStarted | 取得開始を記録し、重複する開始要求を拒否 |
| DrawEntropyCommitted | 出目・カードの用途別材料を、解釈と表示の前に保存 |
| OutcomeCommitted | 実際に確定した出目・出所を再利用可能にする |
| Resolved | 計算後の全状態を含め、同じ試行の二重適用を防ぐ |
| PresentationReceipt | アプリの提示処理の段階を記録。人が読んだ・理解したことの証拠とはしない |

OutcomeCommittedは計算を許可する境界。出目・カード・庭・文章の結果表示と、新しい庭の公開はResolvedの保存成功後に行う。
表示を閉じる、演出を飛ばす、端末を回すといった操作は再抽選・再適用を呼ばない。
短い反応の経過を保持する場合も、同じCore計算から生成した表示データとして扱う。
保存形式に存在しない独自の「適用済みフラグ」を別ファイルへ足して整合性を分散させない。

Roll・Back・来場者交代は`commandId / runEpoch / expectedRoundState`を持つ同じApplicationのコマンド列へ入れる。取消が永続化される前にUIだけで「取消済み」にしない。取得開始が先に確定した場合は、画面を閉じても既存roundを処理し、既知の出目を消さない。細かな競合の勝敗はDATA_CONTRACTに従う。

## 6. single-writerと保存adapter

記録の正は追記journal。画面状態・集計・head checkpointは置換可能なprojection。UI、乱数provider、プラットフォームcallbackはjournalへ直接書き込まず、Applicationへ通知する。
一つのwriter queueがseq・prevHashの決定からrecordの保存完了までを直列化する。
同じroundIdの重複要求は、契約に従って既存の確定結果へ収束させる。

最小版の保存adapterはframed JSON append journalとする。論理commitの単位は一つのrecord。フレーム途中の物理書込みを成功recordとして扱わない。
JCSによる正規化とSHA-256をappend/verify/exportで共有する。
独自のJSON並べ替え、浮動小数点の丸め、別のhash式を各画面へ実装しない。
payloadの数値制約と正規化の詳細はDATA_CONTRACTに集約する。

書込み完了は、adapterが定めるflush・検証条件を満たした場合に返す。
全OSでファイル追記が無条件に原子的であるとは主張せず、途中終了の試験を行う。
末尾の不完全フレームを検出したら、元ファイルと不完全部分を保全する。
自動truncateや削除で「検証成功」に見せず、DATA_CONTRACTの回復手順へ進む。
回復は検証済みheadを引き継ぐ新しいsegmentへ追記する。同じ回復IDで重複した継続先を作らず、途中のhash不一致を末尾の欠損として扱わない。
復元は検証済みのrecordから行い、head checkpointだけで確定結果を判断しない。

地図・ルール・文章集をcontent blobとして保存する場合は、実体の保存・hash検証・durable確認を完了してから、その参照を`RunStarted`へ書く。hashだけを保存し、実体を後から用意する順序にしない。

Resolvedのfull post-stateが庭の回復点になる。projectionはそこから再生成できる。
checksum/hashエラーを空の新規庭として無言で置き換えない。
保存失敗は新しい投擲を止め、結果未保存・未適用の状態をそのまま保つ。
将来SQLite adapterへ移す場合も、Application/Domainの契約と元journalの証拠を維持する。
旧Capacitor DBのテーブルを、新Unity schemaの書込み先として共有しない。

保存先はUnity側が取得した `Application.persistentDataPath` をadapterへ渡す。[保存先の仕様][U2]
JSON/CSV出力は保存先の直接公開ではなく、検証結果と完全性情報を添えたexport操作とする。
端末間の取り込み・Steam Cloud同期は、異なる履歴の上書きや自動合成をしない別phaseで扱う。

## 7. モバイル中断・復帰とオフライン

重要なrecordは操作中に保存する。終了時にまとめて保存する方式にはしない。OnApplicationPause/OnApplicationFocusは進行停止と追加保存の機会として使う。
iOSの中断やモバイルOSの終了ではOnApplicationQuitを受けられない場合がある。[Unityの注意][U3]
Androidも、ユーザーに見えないプロセスが終了され得る前提で復元する。[Androidの説明][A1]

`PreparationStarted`だけで配面材料を回収できない場合は`PreparationInterrupted`。出目の欠測とは別に数える。
`MappingEntropyCommitted`があれば同じ材料からPreparedまでを再開する。
`AcquisitionStarted`だけで出目材料を回収できない場合は`InterruptedUnresolved`として保持し、同じ回を再取得しない。
`DrawEntropyCommitted`があれば保存済み材料を解釈してOutcomeCommittedへ進める。まだ表示されていないという理由で欠測へ変えない。
Prepared / IntentCommittedまでの復元は、その6面・意図を維持してDATA_CONTRACTの規則に従う。
OutcomeCommittedがあれば、保存済みの結果から続ける。新たな乱数要求を出さない。
Resolvedがあれば、そのfull post-stateを復帰させる。水や風をもう一度適用しない。
PresentationReceiptがなくても、結果を未確定に戻さない。
保存途中の回復順、InterruptedUnresolvedの記録方法はDATA_CONTRACTに従う。

pauseとprocess killは区別する。同じprocessが続いている場合も、重複callbackはwriterで直列化する。
再開後に届いた古い非同期応答はroundId・取得ID・現在状態を照合する。
外部providerの冪等性がない場合、API呼出し回数の厳密な一回性を約束しない。
外部取得とUI表示の間の終了でも、保存済み結果の再抽選禁止と適用の一回性を守る。

基本の遊びと保存はオフラインで完結する。ローカル乱数を量子乱数と表示しない。
外部乱数、音声認識、クラウド保存、Steam APIを起動条件にしない。
声を出す実践の自己申告と、マイク録音・音声理解は別の機能として扱う。
外部接続や録音を後から加える場合も、現在の試行条件を途中で変更しない。

## 8. 入力と画面

初回から次の一投、履歴、来場者交代までの画面状態と日本語文言は[EXPERIENCE.md](EXPERIENCE.md)を正とする。UIに独自の投擲queueや自動連投を追加しない。

Unity Input SystemのActionsで、タッチ・マウスを同じ意味の操作へ変換する。[タッチ入力][U4]
候補ActionはPoint / Select / Roll / Confirm / Back。正確なassetはG1で作る。
ポインター速度や端末のタッチ圧を、測定用乱数の値へ混ぜない。
UIに触れた指を庭への地点選択としても解釈する二重入力を防ぐ。
選択地点と6面を確定した後は、ドラッグや画面回転で記録条件を変えない。
ゲームパッドは追加可能な入力adapterとし、最小版で対応済みとは表示しない。

スマホはsafe areaと可読文字サイズを考慮し、庭の構図と主操作を一画面に保つ。
縦横比の違いはカメラ範囲・UI配置で吸収し、計算格子や出目確率を変更しない。
デスクトップではクリック操作、ウィンドウサイズ変更、フォーカス移動を確認する。
exportの共有先選択は各OSのadapterへ渡し、Domainにファイルダイアログを持たせない。

### 8.1 最初のグラフは固定した三つの見方にする

| 見方 | 最小の表示 | 元になる記録 |
|---|---|---|
| 庭の歩み | round順の水量・植物の成長の推移と、各回の入力記号 | RunStarted、Resolvedの状態・収支 |
| 意図と出目 | 比較できる抽選の累積一致数と基準値、対象数・p=0/1・欠測 | IntentCommitted、確定した出目 |
| 一投のつながり | 入力→局所状態→適用規則→出来事の固定配置 | その回のresolvedParameters、イベント根拠、ruleId |

最初から汎用ノードエディタや自動配置の物理演算を作らず、見たい回を選ぶと同じ配置へ具体的な量と場所を入れる。例えば「風→蒸発180→土中水950から770→成長184」は、その回に実際に保存された値の場合だけ表示する。別の場所の平均を一つの因果連鎖へつなげない。

自然モデルの計算依存を表すedgeと、意図・カード・文章が同じroundに記録された対応を表すedgeは意味を分ける。カードから開花への計算依存edgeを作らない。詳細にはruleIdと根拠イベントへ辿れる参照を持たせ、別の成功台帳を追加しない。履歴にある過去の庭と、現在操作中の庭は表示上も区別する。
全投擲で庭全体が40tick進むため、初期の水分・成長と基礎光も計算への入力に含める。今回の追加作用が届かない植物の成長を、同roundという理由だけで出目の直接効果へ結ばない。詳細の根拠条件はDATA_CONTRACT§8に従う。

## 9. URPと表示品質

描画はURPを候補とし、採用するUnity Editorとの互換組合せをG1で固定する。[URP資料][U5]
モバイル用とデスクトップ用のQuality設定を分けても、庭の計算と記録は共通にする。
影の解像度、描画倍率、粒子数、ポスト処理を品質調整の対象にする。
水量、光の遮蔽、風の作用範囲、植物の反応条件を低品質設定で省略しない。

格子の値を小さなmesh・texture・少数の植物Viewへ反映し、セルごとにRigidbodyを置かない。
日陰や岩の影響はCoreの配置・遮蔽情報と整合させ、shaderだけで因果関係を作らない。
神聖なFX/SEは入力や実際の出来事へ反応させ、当たりを発生させる処理を持たない。
Coreの集約された事実からObservationGroupを作り、カードと文章が選んだ主焦点を光・音でも使う。主焦点1、副焦点1以下、鐘1回、入力の自然音1系統、成長アクセント1回以下を初期上限にする。個々のセルイベント数だけ同じ音を重ねない。
吸水の累計、終状態の湿り差、開花などの段階変化を区別して表示へ渡す。一時的な変化と終状態を同じ文章条件にしない。履歴から過去の文章を開いたときは、当時の庭・時点を明示する。
演出用の揺らぎを使う場合は専用streamとし、Domainへ逆流させない。
発熱・電池・メモリ・保存待ち時間は実機計測で確認し、未測定のfpsを保証しない。

## 10. プラットフォームとbuildの境界

| 対象 | 本体と入力 | 動作確認のゲート | 公開時に追加するもの |
|---|---|---|---|
| iOS | 共通Core、タッチ、モバイルQuality | iOS module、対応macOS/Xcode、署名、実機 | 配布設定、TestFlight/App Store審査 |
| Android | 共通Core、タッチ、モバイルQuality | Android module、対応SDK/NDK/JDK、実機 | 署名鍵、AAB、Play公開条件 |
| Windows / Steam | 共通Core、マウス、Desktop Quality | Windows build、対象PCで起動・保存 | Steamworks登録、depot、審査 |
| macOS | 展示端末候補。製品対応は将来対象 | Mac向けbuildと実機検証を別途行う | 配布形態に応じた署名等 |

Steamは配布先であり、Windows/macOS/Linux全てへの対応を意味しない。
Steamworks API統合はSteam出荷の必須条件ではない。[Steam公式][S1]
Steam連携はdesktop限定assemblyとし、未接続時も基本の遊び・保存を動かす。
実績・Cloud・overlay等は能力確認後に呼び、初期化失敗でCoreを停止させない。

iOSはUnityがXcode projectを生成し、ローカル完成buildにはmacOSとXcodeが必要。[iOS build][U6]
使用するXcodeとmacOSの対応を確認する。[Apple対応表][P1]
個人端末の開発testと、TestFlight/App Storeへの配布資格は区別する。[Apple会員区分][P2]
AndroidのSDK/NDK/JDKはUnityの対応表と合わせる。[Android環境][U7] Google Playへの新規公開はAABを使う。[App Bundle][A2]
展示端末で動くことと、各ストアへ公開できることを別の完了条件にする。

## 11. Asset管理・CI・IL2CPP

Unityで作ったassetと対応する.metaを一緒に管理し、GUIDを手作業で再生成しない。[Asset metadata][U8]
Assets / Packagesのlock / ProjectSettingsを管理対象とし、Library / Temp等の生成物は除外する。
素材の出所・使用条件・生成記録を残す。署名鍵・認証情報を素材や設定へ埋め込まない。
CIは将来の実装phaseで追加し、既存Web CIを置き換えない。
EditModeではCore・JCS・journal回復・6面固定・二重適用防止をfixtureで確認する。
PlayModeでは入力、表示、pause、Scene再読み込みを確認する。外部乱数APIは呼ばない。
IL2CPP/AOTではserializer・暗号処理・必要型のstripping・adapterの取り込みを実buildで確認する。[scripting backend][U9]
反射や動的code生成に依存するpackageを、Editorで動いたという理由だけで採用しない。
CI用Unity利用条件・module・署名環境が未整備なら、実機buildの代替とは記載しない。

## 12. G1で固定する環境情報

| 項目 | 現在の扱い |
|---|---|
| Unity Editor / exact patch | 対応LTS候補からG1の実機build後にpin |
| URP / Input System / Test package | Editor互換版を検証しmanifest/lockへpin |
| C# API compatibility / scripting backend | 対象platformとIL2CPP試験後に記録 |
| iOS最小OS / Xcode / SDK | 展示・製品対象機とApple要件から確定 |
| Android最小API / targetAPI / ABI | 対象機と公開時の要件から確定 |
| Windows architecture / build host | 最初の配布PCで検証して確定 |
| 製品名 / bundle ID / App ID / 署名 | 仮値と本番値を明示し、G1以降の担当を決める |

16×16・dt=0.1のモデル基準と、未確定のtoolchain版を混同しない。
3日展示の成否はROADMAPの実機ゲートで判断し、全platformのストア公開を前提にしない。

## 13. 公式参照

確認日: 2026-11-11。toolchainと配布要件は採用・公開時にも再確認する。

[U1]: https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/physics-optimization/cpu/manual-simulation
[U2]: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/persistentdatapath
[U3]: https://docs.unity3d.com/jp/current/ScriptReference/MonoBehaviour.OnApplicationQuit.html
[U4]: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Touch.html
[U5]: https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/requirements
[U6]: https://docs.unity3d.com/ja/current/Manual/iphone-BuildProcess.html
[U7]: https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/sdksetup
[A1]: https://developer.android.com/guide/components/activities/process-lifecycle
[P1]: https://developer.apple.com/xcode/system-requirements
[P2]: https://developer.apple.com/help/account/membership/programs-overview/
[S1]: https://partner.steamgames.com/doc/sdk/api
[U8]: https://docs.unity.com/en-us/engine/6000.3/manual/assets-and-media/import-assets/asset-metadata
[U9]: https://docs.unity3d.com/jp/current/Manual/scripting-backends-intro.html
[A2]: https://developer.android.com/guide/app-bundle
