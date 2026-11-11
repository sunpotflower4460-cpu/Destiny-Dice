# 庭の自然モデル

設計版0.2 / `designCandidate`。水・風・光・種の位置関係から結果を求める、小さな整数モデルの仕様である。**Unityの実装・実機試験は未実施。** 現実の流体、気象、植物生理を精密に再現したという主張ではない。

[simulation-profile.v0.2.json](./content/simulation-profile.v0.2.json)に候補値、[simulation-fixtures.v0.2.json](./content/simulation-fixtures.v0.2.json)に参照算術の入力と期待値を置く。数式・順序・意味は本文、数値はprofileを正とし、矛盾したら設定検証を失敗させる。G2で採用した値・地図・計算版のcontent hashをrunへ固定し、途中で差し替えない。

## 1. 結果を作るもの

出目から直接「池」「発芽」「開花」を発火させない。確定入力を庭へ加え、位置、岩、湿り、日陰、過去の成長量から計算する。

水は保存する物質、風は蒸発に作用する場、光はその区間の照射、種は成長可能な個体。共通の「エネルギー点数」へまとめない。風に直接の成長ボーナスはなく、蒸発で変わる湿りを通して成長へ作用する。

| 項目 | v0.2候補 | 意味 |
|---|---|---|
| 庭 | 16×16、等面積セル | 見た目は3D、計算は固定格子 |
| 近傍 | 上下左右4方向 | 斜めへ一足で水を移さない |
| セル順 | `cellId = y * width + x` | xは東向き、yは南向きに増える |
| 方向順 | N、E、S、W | 整数配分の同点を固定 |
| 時間 | 1tick = 0.1モデル秒、1投擲40tick | 描画fps・意図時間と独立 |
| 比率 | `Q = 1000` | 光・風・湿りの係数は整数 |
| 進行 | 投擲の解決中だけ | 待機中・バックグラウンドでは進めない |
| 植物 | 1種、1セル1個体 | 種→芽→葉→つぼみ→花 |

モデル4秒は現実の植物の4秒を意味しない。展示で読める成長速度は明示した係数で調整し、出目を都合よく変えない。

## 2. 状態・範囲・地図

| 区分 | 保存する状態・設定 |
|---|---|
| 固定地図 | `mapId/version`、寸法、各anchor、唯一の`drainCellId` |
| 固定セル | `height`, `solid`, `soilCapacity`, `infiltrationPerTick`, `lightExposureQ`, `lightOccluderCellId?`, `plantable` |
| 可変セル | `surfaceWater`, `soilWater`, `evaporationRemainder`, 直前40tickの`meanLight` |
| 個体 | `plantId`, `cellId`, `growth`, `growthRemainder`, `stage`, `originKind`, `seedSourceRoundId?` |
| 系 | `tickIndex`, `lastResolvedRoundId`、注入・蒸発・排出の累計 |
| 解決入力 | 固定済み`recipeId`と`resolvedParameters`、profileとmapの参照/hash |

`height`と`surfaceWater`は同じモデル水深単位。セル面積1なので水量の集計にも同じ数値を使う。メートルやリットルへ未校正の値を換算しない。

- height、surfaceWaterは0～1,000,000。soilCapacityは0～1000、soilWaterは0～その容量。
- infiltrationPerTickは0～1000、光・風・露出比は0～1000。通常土の候補は容量1000、吸水20/tick。
- 蒸発端数は0～999、成長端数は0～999,999、成長は0～2500。端数も次回へ持ち越す。
- solidは水・土・植物を持たず、流量辺と入力の対象外。plantableには`!solid && soilCapacity > 0`を要求する。
- 初期個体の段階は成長量から求める。`originKind=initialMap`とし、架空の播種roundを割り当てない。

四則演算・積・総和は明示的なchecked 64bit整数。非負の整数除算は切り捨て、符号付き差は`max(0, difference)`を先に適用する。保存整数はJSONで正確に扱える`±(2^53-1)`以内。範囲外、overflow、保存則違反は計算失敗とし、水量を黙って丸めたり、別の出目へ変更したりしない。

### 展示地図はG2で確定する

傾斜、岩と迂回路、乾いた土と湿った土、明るい場所と岩陰、芽とつぼみ、空き土を一庭へ置く。各水入力先から唯一の排出口まで非solidセルの経路があり、雨の対象が最低3セルになることを要求する。経路があるだけでは十分な排水を保証しない。

**初期16×16地図はまだ実データ化していない。** 小さな算術fixtureの合格を、展示庭の水位・見やすさ・成長速度の検証に置き換えない。

## 3. 一投擲を解決する順序

Preparedの事前状態・地図・profileと、OutcomeCommittedの選択面を入力にする。保存順序と中断再開は[DATA_CONTRACT.md](./DATA_CONTRACT.md)を正とする。Coreは新たな乱数を取得せず、同じ開始状態へ同じroundを二重適用しない。

投擲内のtickOffsetは0～39。offset0の入力は最初の更新前に加える。開始tickIndex=tなら、更新中のイベントを`tick = t + tickOffset + 1`で識別する。SeedPlaced.placedTickもこの番号で、配置はそのtickの流れ・吸水・成長より前に行う。

| 順番 | 処理 | 参照する状態 |
|---|---|---|
| 1 | 予定された水・種の注入 | 保存済みschedule、そのtickの開始状態 |
| 2 | 光・風の場 | 固定地図、保存済み範囲・方向・期間 |
| 3 | 地表水の移動 | 注入後・流れ更新前の同一snapshot |
| 4 | 排出口からの排出 | 流れを一斉反映した後の水 |
| 5 | 地表から土への吸水 | 排出後の水と空き容量 |
| 6 | 蒸発 | 吸水後の水、有効光・風 |
| 7 | 個体の成長 | 蒸発後の湿り、有効光 |
| 8 | 出来事と収支の集約 | 実適用量・閾値越え |

40tick後の全状態・入力適用証拠・イベント・文章と根拠をResolvedへ保存してからUnityで再生する。`Resolved.inputApplicationEvidence`には予定scheduleの各tick・対象・量が実際に適用された証拠を保存する。面を選んだだけではinput_appliedは成立しない。風・光の自然イベントがなくても、実際の場の適用を確認したこの証拠へ事実fallbackを束縛できる。

表示用の中間状態を補間してもCoreを再計算しない。再起動でtraceが失われたら保存済み終状態を見せ、アニメーションのために結果を作り直さない。

`meanLight = floor(sum(effectiveLightQ over 40 ticks) / 40)`を次回配面用に保存する。最初は基礎光と地図の露出比から求める。この要約を次回の光源へ再投入しない。

## 4. 8レシピと作用先の固定

| recipeId | 候補入力 | 位置・期間 |
|---|---|---|
| water_drop | 総水量1200 | waterInletの1セルへoffset0に全量 |
| water_rain | 総水量1200 | 同anchorと上下左右の有効セルへoffset0～11に各100 |
| wind_breeze | 中心強さ500 | windCenterから距離3以内、東向き、40tick |
| wind_passage | 中心強さ1000 | 同中心から距離1以内、東向き、40tick |
| light_soft | 中心追加光250 | lightCenterから距離2以内、40tick |
| light_beam | 中心追加光500 | 同中心1セル、40tick |
| seed_single | 種1個 | seedCenterから距離0～1の最寄り空き土へoffset0 |
| seed_scatter | 種2個 | 同中心から距離2～3の輪の空き土へoffset0 |

距離はマンハッタン距離。水は同総量で場所と時間が違う。種の範囲は非包含で、2粒が1粒の配置を必ず含む上位互換にしない。種は距離昇順、同距離はcellId昇順で必要数を選び、足りなければ配面生成前に候補外とする。

雨は範囲外・solidを除いた対象をcellId順へ固定する。毎tickの100を均等に割り、`floor(100 / targetCount)`ずつ、余りは小さいcellIdから1ずつ配る。3セルなら毎tick34/33/33、12tickの総量408/396/396。余りの順をランダムに変えない。雨の対象が3未満の初期地図は不適合。

profileのauthoringParametersはanchor名や選択規則。Preparedでは6面すべてを実セルへ展開し、`FaceAssignment.resolvedParameters`へ保存する。

- amount/unitId: 水は総量/modelWater、風は中心強さ/windStrengthQ、光は中心追加量/additionalLightQ、種は個数/seedCount。
- targetCellsは確定ID列。cellWeightsは水なら均等重み、風・光なら後述のfalloffQ、種なら各1。
- radius、direction?、startTickOffset、durationTicks、水・種の各tick・各セル・量へ展開したinjectionSchedule。
- 風・光はoffset0からduration未満まで有効でinjectionSchedule=[]。dropのdurationは1、rainは12、種は1。

seedの必要度を測る支持範囲全体と、実配置セルは別にする。表示後に空き土や作用先を選び直さず、後のprofileから値を推測しない。地図・対象・scheduleの不整合はPrepared前に拒否する。将来のタップによる位置選択も配面固定より前に行う。

## 5. 水を移し、吸わせ、排出する

### 5.1 同時更新とセル全体の流出予算

注入後のsnapshotでW_i=surfaceWater_i、H_i=height_i+W_iとする。solidまたはW_i=0からの希望流量はすべて0。通水可能な隣接jについて:

```text
request(i,j) = floor(max(0, H_i - H_j) * conductance / 1000)
R_i = sum_j(request(i,j))
B_i = min(W_i, R_i)
```

conductance=125、許容範囲0～125。R_i=0なら流出0。そうでなければ各方向へ:

```text
base(i,j) = floor(request(i,j) * B_i / R_i)
remainder(i,j) = (request(i,j) * B_i) % R_i
left = B_i - sum_j(base(i,j))
```

剰余の大きい方向からleft個へ1ずつ追加し、同剰余はN/E/S/W順。希望量0の辺は候補外。`0 <= flow <= request`、`sum(outgoing) = B_i <= W_i`を検証する。辺ごとの手元量capだけでは4方向の合計を制限できない。

全セルの配分後に一斉更新する。

```text
W_next(i) = W_i - sum_j(flow(i,j)) + sum_j(flow(j,i))
```

受け取った水を同tick中に再び流さず、solidと庭の外への辺は0。逐次走査で隣の水を書き換えない。

**量子化の限界:** 平らな2セルの水面高差1～7は、125の切り捨てで流量0。v0.2は辺ごとの分数を追加せず、微小な残りを許容する。完全平衡化を保証しない。

### 5.2 唯一の排出口

非solidのdrainCellIdを一つ外部sinkへ接続し、他の外周は閉境界。堰高は`spillLevel = height_drain + 150`、容量100/tick。

```text
outflow = min(W_drain, max(0, height_drain + W_drain - spillLevel), 100)
W_drain -= outflow
```

流れ更新後・吸水前に一度適用し、実排出を収支へ加える。外から戻る水はない。描画も排水先を見せ、庭の途中から水だけが消えたようにしない。

最大注水1200に対し、一投擲の排出容量上限は40×100=4000。`1200 < 4000`は、この出口だけで一投擲分を排出できる容量を持たせる設計上の必要条件であり、到達や非水没を保証しない。蒸発を含む実収支、滞留、排水勾配は別途確認する。

G2ではwater_dropを200回、water_rainを200回、各々専用の開発runで連続適用する。各tickの収支・上限・入口と根元の水位・排出量を記録する。地図と数値がこの試験を通るまで長時間展示に耐えると判定しない。通常の公平な抽選へ強制列を混ぜない。

### 5.3 吸水と収支

```text
absorbed = min(surfaceWater, soilCapacity - soilWater, infiltrationPerTick)
surfaceWater -= absorbed
soilWater += absorbed
```

流れ・排出の後に行う。容量不足の水は地表に残し、土を容量へ丸めて超過分を消さない。通常土は容量1000、吸水20/tick。単独算術fixtureでは吸水0を明示して他の処理を切り離せる。

各tickと投擲全体で次の残差が整数で0であることを要求する。

```text
post(地表水 + 土中水) - pre(地表水 + 土中水)
    = 実注入量 - 実地表蒸発 - 実土中蒸発 - 実排出量
```

吸水・セル間移動は内部移動なので右辺へ足さない。累積吸水・累積輸送は現在の水の総量と別の値である。

## 6. 光と風の届き方

### 範囲と光

中心からのマンハッタン距離d、半径rに対し、範囲内は`K = floor((r + 1 - d) * 1000 / (r + 1))`、範囲外・solidは0。Preparedへ保存したKを使い、描画メッシュで適用セルを再判定しない。

基礎光500に、そのrecipeの期間だけ追加光を与える。地図の露出比X=lightExposureQを使う。

```text
localExtra = floor(extraLightQ * K / 1000)  # 光入力がなければ0
unoccludedLightQ = min(1000, 500 + localExtra)
unoccludedAdditionalLightQ = unoccludedLightQ - 500
L = effectiveLightQ = floor(unoccludedLightQ * X / 1000)
baseEffectiveLightQ = floor(500 * X / 1000)
additionalEffectiveLightQ = L - baseEffectiveLightQ
```

和を1000へ制限してから露出比を掛ける。X=0なら追加してもL=0。基礎光も影で弱まる。入力終了後は基礎光へ戻り、追加光を永久に蓄えない。

影は静的map値。GPUの影画像をCoreの正にしない。岩による影と述べるセルにはlightOccluderCellIdで実在solidの根拠を持たせ、nullなら岩に帰属させない。成長した植物の動的な影は含めない。

### 風上のray

風向はN=(0,-1)、E=(1,0)、S=(0,1)、W=(-1,0)。対象cからc-direction、c-2*directionと1セルずつ**風上**へ進み、最初のsolidで止める。対象自身はrayに含めず、境界までsolidがなければ非遮蔽。

```text
shelterQ = firstSolidFound ? 250 : 1000
unoccludedWindQ = floor(strengthQ * K / 1000)
V = effectiveWindQ = floor(unoccludedWindQ * shelterQ / 1000)
```

複数の岩の係数を重ね掛けしない。地形高、岩の高さ、渦、圧力はこの風モデルへ入力しない。基礎風は0、入力のない区間はV=0。

蒸発も葉の揺れも同じVを参照する。岩陰の見た目だけを変え、モデルを一律の風量にしない。種・水の運搬は行わず、運搬したという文章も生成しない。

## 7. 蒸発はセル全体の一つの要求量

吸水後の水、有効光L、風Vを使う。

```text
numerator = evaporationRemainder + L + 4 * V
demand = floor(numerator / 1000)
nextRemainder = numerator % 1000
actual = min(demand, surfaceWater + soilWater)
surfaceEvaporated = min(actual, surfaceWater)
soilEvaporated = actual - surfaceEvaporated
```

地表から先に、残りを土から引く。同じdemandを地表と土のそれぞれから引かない。L=500・無風なら平均0.5/tick、L=500・風1000なら平均4.5/tickの要求となる。

引ききれなかった整数の要求量は捨てる。処理前から水0なら計算を省略して端数0、処理後に水0になった場合も端数0。乾いた40tickの要求を次の雨へ請求しない。水がある間の0～999の分数だけは次回へ保持する。

成長による水消費は追加しない。後の版で吸収・蒸散を導入するときは別sinkとして収支へ加える。

## 8. 湿りと光による成長

種は成長0・端数0で配置する。配置時の湿りと、そのtickの有効光をSeedPlacedへ記録する。配置と発芽を分け、配置したtickも通常の成長計算を適用する。

蒸発後の`m = floor(1000 * soilWater / soilCapacity)`を使う。容量0には植物を置けない。

| mの範囲 | 湿り係数F |
|---|---|
| m <= 100 または m >= 950 | 0 |
| 100 < m < 400 | floor((m - 100) * 1000 / 300) |
| 400 <= m <= 700 | 1000 |
| 700 < m < 950 | floor((950 - m) * 1000 / 250) |

光係数はLそのもの、成長速度は25。

```text
n = growthRemainder + 25 * F * L
delta = floor(n / 1000000)
growthNext = min(2500, growth + delta)
growthRemainderNext = n % 1000000
```

2500に達したら端数0、その後成長を増やさない。F=0またはL=0の間は成長せず、以前の分数は保持する。乾燥・過湿で遅くなる／止まるが、枯死、害虫、栄養競争は追加しない。

| 段階 | 閾値 | 初回の閾値越えイベント |
|---|---:|---|
| 種 | 0 | 配置時のSeedPlaced |
| 芽 | 250 | Germinated |
| 葉 | 750 | LeafOpened |
| つぼみ | 1500 | BudFormed |
| 花 | 2500 | Bloomed |

`beforeGrowth < threshold <= postGrowth`のときだけ発行。複数を越えれば閾値昇順ですべて記録し、表示の焦点と区別する。

**順番も結果になる。** 種がない区間の光を後から成長へ加算しない。初期土中水600、各40tickの例で、種→光は成長1500、光→種は500。双方とも計80tick、終土中水540で、前者だけ時間を余分に進めた比較ではない。

## 9. 計算した出来事と、伝える言葉

### tickの事実をround単位へ集約する

小さな変化を毎tick別の発見として保存・発音しない。基本キーはroundId+kind+cellId/plantId、流れ全体だけはroundごとに1件。共通にeventId、kind、ruleId/version、対象、firstTick/lastTickまたはtick、入力参照、数値根拠を持つ。

| イベント | 成立条件・保存する証拠 |
|---|---|
| WaterEntered | 正の実注入。セルごとの累計実量・区間 |
| SurfaceFlowObserved | 正の適用辺流量が1つ以上。first/lastTick、昇順involvedCellIds、positiveEdgeTransferCount、cumulativeTransport |
| WaterDrained | 正の実排出。排出口・累計量・区間 |
| SoilMoistened | 正の吸水が1tick以上。cumulativeAbsorbedと事前/終状態 |
| RootZoneWetted | 吸水時点で発芽済みの同セルの個体。その対象tickの正の吸水だけを累計 |
| PuddleFormed | tick開始の地表水が120未満、更新終了時120以上。round最初の越え、越え回数、最大水深 |
| LightReceived | 今回の追加実効光が正。区間・積算追加光・無遮蔽追加光・実効光・露出比・影の根拠 |
| WindReceived | 今回のVが正。区間・cumulativeEffectiveWindQ・無遮蔽風・実効風・遮蔽比・blocker |
| WindSheltered | 無遮蔽風が正で、実在solidによりVが小さい。同じ風場の値を根拠にする |
| SeedPlaced | 保存済み空きセルへの実配置。plantId/cellId/placedTick、soilQAtPlacement、actualLightQAtPlacement |
| GrowthObserved | 整数成長が増加。roundのbefore/postGrowth、正の成長tick数・区間、最初の同tick証拠 |
| 段階越え4種 | 閾値越え。個体、tick、threshold、直前/直後growth |

positiveEdgeTransferCountは正の流れを持つ「辺×tick」の件数、cumulativeTransportはその量の総和。同じ水の反復移動を含み、ユニークな水量・注入量・下流到達量ではない。この集約だけでは特定の根までの時間順経路を確認できない。

吸水はcumulativeAbsorbedと、round開始preSoilWater、終了postSoilWater、差netSoilWaterDeltaを分ける。吸水後に蒸発しても「染み込んだ」は成立し得る。根元の吸水は、そのtickの成長で初めて発芽した個体へ遡及しない。

光のcumulativeAdditionalEffectiveLightはadditionalEffectiveLightQをtickで加えた値。追加照射の遮蔽はunoccludedAdditionalLightQとadditionalEffectiveLightQを比較し、基礎光込みのLとは比較しない。風の積算も物理単位のエネルギーではない。静的map・区間内一定の場なので、無遮蔽値・実効値はその区間の値を記録する。将来変化させる版では単一値で変動を隠さず区間を分ける。

GrowthObserved.firstPositiveGrowthEvidenceはtick、soilWater、soilCapacity、moistureFactorQ、effectiveLightQ、growthDeltaを持つ。ここでgrowthDeltaは上限適用後のpostGrowth-beforeGrowth。別tickの湿りと光を結び付けず、同じ個体・同じ成長tickの根拠にする。

eventIdはround・種類・対象から安定して作り、追加乱数を使わない。配列順もfirstTick、kindのordinal順、対象cellId/plantId順で固定する。閾値の一度だけの記録とUnityによる再提示を区別する。

### 述語の正はGAMEPLAY

[GAMEPLAY.md](./GAMEPLAY.md)の述語と対象束縛を文章選定の正とする。SimulationEventを表示用ObservationGroupへまとめても、新しい自然現象を作らない。

- surface_flow_observedは正の辺流量の集約で成立する。「水が動いた」は示せる。
- verified_path_to_rootは経路追跡を実装・保存しないv0.2ではfalse。「岩を回った水が根へ届いた」の根拠にしない。
- 現在の湿りと途中の吸水、今回の照射と過去の成長、種の配置と発芽を区別する。イベントがないだけで「届かなかった」と推測しない。
- 「前の雨がこの花を咲かせた」と特定入力の由来を断言しない。モデルは入力・局所状態・規則・結果を持ち、水粒子の系譜や意図→乱数の因果は持たない。

## 10. 参照算術と、残る受け入れ

fixture JSONの16件は、明示的な期待値をPython整数算術で照合した。小さな領域で他の処理を無効化した例を含み、Unityや展示庭の試験結果ではない。初期状態、tick数、無効化した処理、終状態、端数、収支をJSONに残す。

以下6件は単セル、地表水0、容量1000、光500、種の成長0・端数0から40tick。吸水・流れ・排出を無効化し、蒸発→成長だけを確認する。

| 初期土中水 | 風V | 終土中水 | 蒸発量 | 成長 | 成長端数 |
|---:|---:|---:|---:|---:|---:|
| 950 | 0 | 930 | 20 | 20 | 0 |
| 950 | 1000 | 770 | 180 | 184 | 0 |
| 950 | 250 | 890 | 60 | 61 | 0 |
| 250 | 0 | 230 | 20 | 233 | 162500 |
| 250 | 1000 | 70 | 180 | 101 | 275000 |
| 250 | 250 | 190 | 60 | 199 | 0 |

同じ風が過湿では成長条件を改善し、乾き気味の土では悪化させる。候補モデルの挙動であり、現実の植物への助言ではない。

残る例は排出口の300→200→150、水十字の同時流入と1単位配分、乾いた高地の流出0、平地差7の流量0、種と光の順序2例、乾燥期間の蒸発要求を持ち越さない例、完全な光遮蔽、風上rayの向きと最初の岩。

実装後は次を確認する。

- C#が同じprofile・map・開始状態・resolvedParametersから同じ終状態と端数を返す。
- 16×16で処理を組み合わせ、各tickの水収支残差0と全セルの範囲を満たす。
- 乾湿による下流水量、影、岩陰、入力順の違いを実際の庭で追える。
- 最大注水200回を各水recipeで行い、滞留・上限・排出・表示を確認する。容量の不等式だけで合格にしない。
- 30/60fps、演出省略、中断再開で結果が同じ。Mono/IL2CPP・各端末の一致は実行するまで未確認。
- 入力適用証拠、集約イベント、述語の対象・時間条件が一致する。細かなイベント数で音と焦点を増やさない。

最小の受け入れは、最初の水が土へ入り、追加した水の一部が先へ動き、その湿りと光で種が育つこと。開発fixtureで確認してから公平な抽選へつなぐ。見た目や数投での開花を算術だけで保証しない。

## 11. 実装上の根拠と境界

純C#格子モデルとUnityの表示・物理サイコロは分離する。物理の固定時間刻みは更新間隔の制御に有用だが、PhysXや描画を異なる端末間の整数結果一致の根拠にしない。[Unity: Manual simulation](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/physics-optimization/cpu/manual-simulation)

C#整数はuncheckedではwrapし得るため、算術を実際に行う各関数内でcheckedを有効にする。呼び出し元だけをcheckedで囲んでも、呼ばれた関数内へ自動的には伝わらない。[Microsoft: checked and unchecked](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked)

過去の正は保存済みResolved。更新後の計算版で過去の庭や文章を上書きしない。全体設計は[ARCHITECTURE.md](./ARCHITECTURE.md)、確認順序は[ROADMAP.md](./ROADMAP.md)、照合索引は[VALIDATION.md](./VALIDATION.md)を参照する。
