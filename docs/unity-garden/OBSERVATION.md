# 保存した一投から、庭の歩みを読む

設計版0.8 / `designCandidate` / `projectionVersion=round-observation.v0.8`。**Unityのグラフ・journal読込・実機表示は未実装。**

本書は、検証済みの記録から画面とexportに渡す値を定める。recordの成立、出目材料からの復元、保存・回復の正は[DATA_CONTRACT.md](./DATA_CONTRACT.md)。自然計算と局所の根拠は[SIMULATION.md](./SIMULATION.md)、見せ方は[EXPERIENCE.md](./EXPERIENCE.md)を正とする。新しい自然状態、成功台帳、抽選、追記recordは作らない。

## 1. 同じ範囲から、同じ投影を作る

queryには`projectionVersion / gameId / runId / cutoffHead={seq,entryHash} / recordOrdinalRange / comparisonFilters`を持たせる。cutoffは検証済みの保存位置であり、表示中に到着したrecordを途中から混ぜない。上流readerは、各roundの意図・出目・Resolved・カードをすべてこの位置までの元recordから構成し、現在の最新要約を古いcutoffへ添えない。更新するときは新しいcutoffの投影へまとめて差し替える。JSONとCSVにも使用したqueryを記す。版は表示・exportのmetadataであり、WorldSnapshotや既存runのgenesisへ後付けしない。

既定は**現在runの全範囲、比較filterなし**。展示の主画面に条件選択を増やさない。比較filterを使う場合は`method`と`outcomeSource`の完全一致とし、nullは指定なし。方法は本人が選んだラベルで、祈り・発声を検知した結果ではない。source不明を任意のsourceへ補わない。filterは意図と出目の比較に適用し、庭の状態を別の履歴に作り替えない。

検証できないrecordや不一致の参照は、正常値へ混ぜず上流の検証状態として報告する。不正なrecordを黙って除いて「全履歴の正常な投影」とはしない。本書の参照fixtureは上流で検証済みの事実を入力と仮定し、その検証器を代わりに実装するものではない。

### 1.1 横軸は「記録の順」

同runのcutoffまでにある`PreparationStarted`をseq昇順に並べ、1からの`recordOrdinal`を導出する。準備中断、取得前取消、結果不明、まだ準備済みの回も位置を持つ。表示上は「記録の順」または「記録3」と呼び、振っていない準備まで「3投目」と呼ばない。

`recordOrdinal`、正常解決で進む`worldRevision`、比較対象だけの`eligibleOrdinal`は別の値である。端末時刻・Receiptの到着順で並べ替えない。同じ時刻の記録や時計の変更でも順序は変わらない。各点はroundId・PreparationStartedの参照を持ち、対象となるResolved／Intent／既知出目の根拠へ辿れる。

範囲を選んでも横位置を1へ振り直さない。`recordOrdinalRange`は元runでの両端を含む範囲で、nullなら全体。先に全prefixの位置と庭の基準を求め、それから表示範囲を切り出す。比較の累計は**選択範囲とfilterに属する同じ集合**で計算し、範囲を変えた場合はその範囲の累計と明記する。

## 2. 庭の歩みの点と、空いた位置

### 2.1 初期点と量の定義

既定の全範囲（`recordOrdinalRange=null`）では`RunStarted.initialState`を横位置0の「庭の始まり」として置く。これは投擲結果ではない。明示した範囲は1以上の記録位置を両端に持つ。その場合も初期値をmetadataへ保持するが、位置0を範囲内の投擲として挿入しない。

| 投影の値 | 求め方 | 表示名・注意 |
|---|---|---|
| `surfaceWaterTotal` | snapshotの全セルのsurfaceWater合計 | 地表にある水。土中水を加えない |
| `soilWaterTotal` | snapshotの全セルのsoilWater合計 | 土にある水。セルごとの湿り比の平均とは区別 |
| `growthTotal` | snapshotの全個体の整数growth合計 | 植物の成長量（初期分を含む）。植物数が増えただけで平均成長が下がる指標にしない |
| `growthSinceStart` | growthTotal − RunStartedのgrowthTotal | 庭の始まりから増えた量。初期個体の成長量を新たな成果に数えない |
| `growthThisRound` | 同じResolvedのpostState合計 − 対応するbeforeState合計 | この一投で増えた量。途中範囲の最初でも0を仮の開始値にしない |
| `plantCount / flowerCount` | snapshotの個体数／stage=flowerの個体数 | 全体の状態であり、今回生まれた種数・今回咲いた数とは別 |

初期点の`growthSinceStart`は0、`growthThisRound`はnull（投擲なし）。現行mapの初期growthTotalは4個体の1,700＋1,700＋500＋500＝4,400であり、初期点を成長0として描かない。水量と成長量はモデル単位。現実のリットル、日数、植物の高さへ換算しない。

土の見た目は引き続き各セルの容量に対する湿り比で描く。グラフの土中水合計を、全セルの見た目へ一律に割り当てない。地表水・土中水は同じ水量単位の別系列、成長は別の小グラフへ置く。成長率、幸福度、祈りの強さという新しい指標へまとめない。

### 2.2 庭の値を置けるのはResolvedのある位置

| 記録の状態 | 庭の点 | 天候帯・入力記号 |
|---|---|---|
| 検証済みResolvedあり | 同じpostStateの値。成長0は既知の0 | 実適用済みの天候とダイス入力 |
| 出目は既知、Resolvedなし | null。終状態を推測しない | 天候・入力は既知の予定条件として区別 |
| 出目の結果不明・取得中 | null | Preparedがあれば準備された天候を示せるが、適用済みの雨として描かない |
| 取得前取消・準備中断・まだPreparedだけ | null | 確認できる準備情報だけ。40tickが進んだ帯を作らない |

nullの位置へ0や直前の水量を埋めない。線はそこで切り、前後の状態を補間した線で結ばない。次に正常解決した点の値は、その回の実際のbefore/postから求める。履歴に中断があっても、同じ保存状態を引き継いだことと、欠けた一投の変化を観測したことを区別する。

すべて未解決なら、初期点と各位置の状態だけがある。庭の歩みが空でも、保存されている意図や既知出目の詳細を隠さない。カードの未成立とPresentationReceiptの有無は、庭の点の採否を変えない。

## 3. 意図と出目の二つの線

### 3.1 同じ対象集合を使う

比較点を持つ条件は、queryの範囲・filterに合い、事前確定したkindがfaceまたはelement、出目が既知、`0 < matchingFaceIds.Count < 6`であること。出目が既知かはDATA_CONTRACTの規則に従い、保存済みOutcomeに加え、検証済み材料から同じ版で確定できる出目も含む。Outcome／Resolved／Receiptが未追記という理由だけでは除外しない。

点には`recordOrdinal / eligibleOrdinal / observedMatch / chanceNumerator / chanceDenominator / cumulativeMatches / cumulativeChanceNumerator`を持たせる。`observedMatch`は出目がmatchingFaceIdsに属すなら1、それ以外は0。分母は6、基準値は**整数の累計分子÷6**として保持し、各回を小数へ丸めてから足さない。表示の小数丸めは末端だけで行う。

比較対象外の位置はnullで、両系列を同じ位置で切る。次の比較点はそれまでの**同じ範囲内の比較点**の累計を継続する。点のeligibleOrdinalは1から増えるが、横位置recordOrdinalを詰めない。未知結果を不一致0へ変えたり、比較対象外だけを基準値へ加えたりしない。

cardStatusがunavailableでも、出目が既知なら通常どおり比較する。同じ文の反復、消音、省略、Receipt欠落、開花の有無も除外条件にしない。比較表示のラベルは「実際の一致」と「この配面なら平均的に」。効果判定・順位付け・祈りの強さの採点は追加しない。

### 3.2 件数は、何を数えたかを分ける

| 件数のまとまり | 内容 |
|---|---|
| 範囲内の記録 | 準備数、取得開始数、既知出目数、Resolved数、終端の結果不明数、まだ取得後に進行中の数、取得前取消数、準備中断数 |
| 比較の分類 | 比較対象、p=0の構造化意図、p=1の構造化意図、比較目標なしの確定意図、構造化目標あり・出目不明、未確定意図、filter範囲外 |
| カード | drawn／unavailable／まだ確認できない、を各回に表示。D6の欠測と合算しない |

前者の件数は重なる。例えば「既知出目・未Resolved」は既知出目数にも進行中数にも入るため、総数になるよう足さない。比較の分類は、まずfilter範囲外を分け、残りを表の条件で一つずつ分類する。p=0/1は固定意図と配面で決まり、出目が未取得でもその構造上の条件を示せる。比較目標なしにはnone/freeText/gardenOutcomeを含め、後から本文を解釈して分類しない。

現行の正常配面の面／系統目標はp=0、1/6、2/6、3/6。p=1は将来版または合成投影の境界例で、現行の配面生成で出せる状態として数えない。

### 3.3 記録が少ないときの表示

| 状態 | 短い案内例 |
|---|---|
| 既知出目0、取得開始0 | 「まだサイコロは振られていません。庭の始まりを見られます。」 |
| 既知出目0、取得開始あり | 「結果を比べられる一投は、まだありません。」不明と処理中の件数を添える |
| 既知出目あり、比較対象0 | 「比較できる目標つきの一投は、まだありません。庭の歩みは見られます。」目標なし・該当面なし等の内訳を添える |
| 指定範囲／filterに該当なし | 「この条件に当てはまる記録はありません。」 |
| 比較対象あり | 対象数と、同じ集合の二つの系列を表示。1件なら点で示せる |

0件の一致率を0%や100%と表示しない。意図なしで遊んだ人へ設定変更を要求せず、庭の歩みを既定の入口にする。Preparedが自動で用意されているだけで「一投を観測した」と案内しない。自由文を残したこと自体は記録に見せ、一致判定の代用品にはしない。

## 4. 一投のつながりと、局所の根拠

関係図のedgeKindはDATA_CONTRACTの`modelDependency / recordedAssociation`を使う。開始時の庭、実適用した天候、今回のダイス入力を別の入口にし、同じ対象・tickまたは区間の証拠で結ぶ。意図やカードが庭を計算で変えた矢印は作らない。

蒸発→湿り→成長を一つの局所例として示す場合は、SIMULATIONの`local-growth-witness.v0.8`に従い、同じセル・同じtickの保存済み根拠を使う。その例は最初に正の整数成長が出たtickであり、一投全体の成長量や後の開花tickを表すものではない。風のない反実仮想との差や、風が増やした成長量に読み替えない。

局所の根拠がないときは、確認できる入力・状態・出来事だけを示し、欠けた中間の矢印を埋めない。成長0、成熟した花、地表だけから蒸発した場合も、記録された状態をそのまま読める。詳細で根拠がないことは、その作用が絶対になかったという判定にはしない。

## 5. G0の照合と、実装後に残る確認

[observation-fixtures.v0.8.json](./content/observation-fixtures.v0.8.json)は、小さな合成snapshotと検証済みrecord要約を使う。実際の庭、journal export、乱数材料ではない。生byte・JCS・hash・参照先・合法な遷移の検証は上流で済んだものとして、横位置、点、null、正確な基準値、件数と空表示だけを照合する。

**各入力要約の意図・出目・Resolved・カードは、既に同じcutoff以下のrecordだけから構築済みとする。** 参照checkerが行う時点の絞込みはPreparationStarted相当のseqまでであり、それ以降に到着した各元recordの時刻やseqを読み直してcutoffを実施する処理ではない。fixtureの`cutoffSeq`は省略した入力形式で、実装のqueryに必要な`cutoffHead.entryHash`、各元recordのseq/hash、sourceRefsを持つ完全なjournalではない。この確認を、実際のjournalを途中で切り出した検証結果へ読み替えない。

```sh
python docs/unity-garden/reference/observation_v08.py --verify
```

参照checkerは標準ライブラリのみを使い、ファイルを書き換えない。自然計算、全述語、関係図のedge生成、Unity画面、OS保存の試験を兼ねない。G3では同じ入力をC# projectionでも照合する。その前段で、検証済みrun/cutoffに属するPreparationStarted・Prepared・Intent・Outcomeまたは材料・Resolvedを同じroundへ結合し、元recordのseq/hashと状態の参照を点へ残す。cutoffの前後へOutcome／Resolved／Receiptが到着する例は実際のreaderで別に検証し、その上でJSON監査bundleから得た値と画面・CSVの値が同じqueryで一致することを確認する。

人の確認はEXPERIENCEのH2（庭の差）、H4（小画面・低刺激・消音）、H5（履歴と復帰）へ組み込む。自動で確かめられる欠測・母数・丸めを毎回目視で計算し直す手順にはしない。
