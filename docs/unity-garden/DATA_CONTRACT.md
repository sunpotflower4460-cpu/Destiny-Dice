# Unity Garden — 記録・復旧・観察のデータ契約

状態: **設計仕様／Unity 実装・実機検証は未実施**。対象は新しい箱庭ゲームのみ。
この文書は既存の `DESIGN.md` / `PROTOCOL_FREEZE.md` の実験・台帳を変更しない。
数値パラメーター、ファイル I/O、C# の具体的な型は、後続の実装フェーズで検証する。

## 1. 保存する目的と単位

自動記録の目的は、同じ庭を復元できることと、意図・抽選・自然の変化を後から見返せること。
プレイヤーへ各段階の保存確認を求めず、アプリケーション層が順序を強制する。

- `gameId`：このゲームの名前空間。既存の `experimentId` や Layer A/B/C を流用しない。
- `runId`：一つの庭の世代。新しい庭、ルール変更、展示の次の来場者は新しい ID にする。
- `roundId`：一回の配面から結果までの一意な ID。同じ ID の再開は新しい抽選ではない。
- `seq`：run 内で単調増加する記録順。壁時計の時刻変更で並び替えない。
- `worldRevision`：最後に確定した庭の版。`Resolved` と初期状態からだけ進める。
- `previousRunId` / `importedFrom`：庭を引き継ぐ場合の出所。過去の run を上書きしない。

ルール・計算方式・コンテンツの版を `RunStarted` で固定する。
版ラベルだけでなく、使った設定値・地図・カードと文章集の内容、または永続した内容参照とhashを保存する。
参照先は後から同名で差し替えられないものとし、保存データと一緒に復元・監査できるようにする。
版を変更する場合は新しい run を開始し、変更前後の集計を既定で混ぜない。
同じ端末の同じ人物かどうかは自動判定しない。展示の run は人物 ID を意味しない。

## 2. 一回の状態遷移

以下の各段階は、single-writer が追記を永続化してから次へ進める。
保存の成功応答は、単にメモリー上のキューへ入ったことを意味してはならない。

| 段階／イベント | 保存するもの | 保存後に許可する操作 |
|---|---|---|
| `Prepared` | 開始時の庭、6面全体、生成根拠、配面用乱数、実行時間幅 | 配面表示、意図の編集 |
| `IntentCommitted` | 意図のスナップショット、方法、対応面、基準確率 | 取得開始の記録 |
| `AcquisitionStarted` | 取得 ID、参照する意図・配面、乱数ポリシー | 初めて出目・カード用乱数を取得 |
| `OutcomeCommitted` | 出目、選択面、カード、取得の実情報 | 同じ入力で自然モデルを計算 |
| `Resolved` | 完全な更新後状態、出来事、採用文、参照情報 | 庭・出目・文章の結果演出 |
| `PresentationReceipt` | 提示を開始／完了した事実、提示対象 | 次の回へ進む／提示を再開 |

意図を持たない場合も `IntentCommitted(kind=none)` を保存して同じ経路を通る。
長押し中の下書きは変更可能だが、確定後の変更は次の round に適用する。
`Prepared` の6面は固定し、意図変更・画面回転・バックグラウンド復帰で再生成しない。
`Prepared`から`Resolved`までは開始snapshotと公開中の庭を固定し、待機時間で進めない。
`OutcomeCommitted`後に別の計算用コピーを固定tick数だけ進め、保存した`Resolved.postState`を新しい庭にする。
対象位置・方向・シミュレーション時間幅も固定し、確定後の外部入力を混ぜない。
結果待ちの光や音は使えるが、保存前に出目・カード・庭の結果を先に見せない。

## 3. 中断・再開・二重実行

| 再開時に存在する最後の段階 | 回復規則 |
|---|---|
| `Prepared` | 同じ庭と6面を復元。未確定の意図は編集可能 |
| `IntentCommitted` | 同じ意図を維持。新しい意図に置換せず、取得開始へ進める |
| `AcquisitionStarted` のみ | 真のプロセス終了後は `InterruptedUnresolved` を追記。同じ回を再取得しない |
| `OutcomeCommitted` | 保存した出目・カード・開始状態から同じ計算を実行する |
| `Resolved` | 保存した完全な更新後状態を復元する。入力をもう一度加算しない |
| 提示記録なし／提示途中 | 同じ結果の提示を再開できる。集計から結果を外さない |

同一プロセス内の一時停止と、プロセス終了後の復元は区別する。
取得処理が生きている間は、その処理の完了を待つ。別の取得を並行して始めない。
`InterruptedUnresolved` は取得開始済みだが結果未保存の欠測であり、失敗した予想ではない。
欠測の run では庭を開始状態のまま保ち、新しい `roundId` で続けられる。
新しい round も履歴へ追記するため、欠測が成功例に置き換わることはない。
`AcquisitionStarted`前に明示的に終了した回は`AbandonedBeforeAcquisition`を追記する。
`IntentCommitted`があればそれも保持し、取消によって内容を編集したり出目を取得したりしない。
来場者交代でrunを終了するときも、取得開始前はこの終了記録を残す。
取得開始後は確定結果または`InterruptedUnresolved`として決着させてから、新runへ切り替える。
取得前の終了は未抽選であり、意図の一致率へ入れない。既存の記録は削除しない。

`roundId + eventType` の一意性と合法な遷移を、UI ではなく writer/application 層で検査する。
提示記録だけは `roundId + presentationAttemptId + phase` を一意にする。
連打・重複コールバック・遅れて返った取得結果は、既存の確定結果を置換できない。
終端状態になった round の遅延応答を新しい round へ流用しない。
計算や文章選定が失敗した場合も `OutcomeCommitted` を残し、乱数を引き直さない。

## 4. 乱数の役割と監査

| role | 使用箇所 | 相互依存させない条件 |
|---|---|---|
| `mapping` | 庭の条件に応じた候補から6面を生成 | 意図・将来の出目・カードを参照しない |
| `outcome` | 確定した6面から1面を選ぶ | `AcquisitionStarted` 永続化後に新しく取得 |
| `oracle` | 6枚のオラクルから1枚を引く | 出目とは別に取得し、結果に合わせて引き直さない |
| `visual` | 粒子、装飾の揺れなど | 他 role の乱数列と庭の計算状態を変更しない |

役割ごとに新しい独立の取得を行う。決定的生成器を使う場合も seed を共用しない。
将来の出目用 seed を配面生成時に作ったり、事前取得した出目を意図確定後に消費したりしない。
展示版はオフラインのローカル暗号乱数を想定し、`source=local` を正直に記録する。
外部 provider を後から追加する場合は実際の取得先・失敗・フォールバックを保存する。
テスト用 seed は `source=fixture` とし、実プレイの集計に混ぜない。

公平な D6 への変換は byte の `b >= 252` を棄却し、`1 + (b % 6)` とする。
棄却は不利な出目の引き直しではなく、剰余の偏りを除く変換処理である。
取得 byte・棄却数・変換版を記録し、同じ結果を監査時に再計算できるようにする。
配面候補の重みは「面の内容の出やすさ」であり、「完成した面の当たりやすさ」ではない。
各面が 1/6 でも、水が3面あれば水全体の確率は 3/6 になる。
オラクルは6枚から毎回復元抽出する。前回のカードも候補に戻り、連続して同じカードを引ける。
カード名や意味、カードと整数の対応は `deckVersion` で固定する。
カードの抽選結果を文に合わせて変更せず、実際の出来事に成立する文章を選ぶ。

## 5. DTO の契約

以下は C# の完成した型ではなく、保存・復旧に必要な論理フィールドである。
nullable と未取得を区別し、既知の値を便宜的な `0` や空文字で代用しない。

### 5.1 共通レコードと run

| DTO | 必須情報 |
|---|---|
| `JournalEntry` | `seq`, `type`, `createdAtUtc`, `payload`, `prevHash`, `entryHash` |
| `RecordMeta` | `schemaVersion`, `gameId`, `runId`, `roundId?`, `seq`。各 payload に含める |
| `RunStarted` | 初期状態、`rulesetVersion`, `simulationVersion`, `numericEncodingVersion`, `mappingPolicyVersion` |
| 同上・版情報 | `rngPolicyVersion`, `rngAlgorithmVersion`, `deckVersion`, `messageVersion`, `appVersion`, `buildId` |
| 同上・識別 | `platform`, `previousRunId?`, `initialStateProvenance`, `startedAtUtc` |
| `WorldSnapshot` | グリッド形状、地形・障害物・土壌容量、地表水・土中水、植物状態、`worldRevision`, `stateHash` |

グリッドや植物状態の正確な要素は自然モデルの仕様で固定する。
浮動小数の丸め、列挙順、境界処理、時間刻みも数値表現・計算版に含める。
固定時間刻みだけを根拠に、Unity PhysX の端末間完全一致を主張しない。
異なる端末での完全一致は、同じ保存入力を使った監査で確認する実装上の受入条件とする。

### 5.2 配面と意図

| DTO | 必須情報 |
|---|---|
| `Prepared` | 開始状態または永続参照、開始状態 hash、`faces[6]`, 候補重みと状態量、`mappingReceipt` |
| 同上・入力範囲 | `targetMode`, `anchorRefs`, `simulationStepCount`, `fixedDelta`, `preparedAtUtc` |
| `FaceAssignment` | `faceId=1..6`, `recipeId`, `elementId`, `resolvedParameters` |
| `resolvedParameters` | `amount`, `unitId`, `targetCells[]`, `cellWeights[]`, `radius`, `direction?`, `durationTicks`, `injectionSchedule` |
| `IntentCommitted` | `preparedSeq`, `kind`, `target?`, `freeText?`, `method`, `committedAtUtc` |
| 同上・比較 | `matchingFaceIds`, `chanceNumerator`, `chanceDenominator=6`, `eligibilityReason` |
| 同上・経過 | 実際に計測した長押し時間、前回提示文への参照、前回の意図からの継続か |

`kind` は `none / face / element / gardenOutcome / freeText` とする。
`method` は選択された `asIs / intention / prayer / spoken / unknown` とする。
方法の選択は実行の自動検出ではない。音声録音・文字起こしがない状態でも使える。
意図を前回から維持する場合も、その回の配面に対する `matchingFaceIds` を確定し直す。
`gardenOutcome` の対象は出来事として残すが、面の一致率とは別の記録にする。
6面すべての`resolvedParameters`を保存し、recipe名だけから後の版で値を推測しない。
`injectionSchedule`には総量をどのtickへ配るかを持たせ、総量と毎tick量を区別する。

### 5.3 取得と結果

| DTO | 必須情報 |
|---|---|
| `AcquisitionStarted` | `acquisitionId`, `intentSeq`, `preparedSeq`, 開始時刻、適用乱数ポリシー |
| `EntropyReceipt` | role、取得 ID、実 source、provider/adapter 版、取得開始・終了、実 byte または独立 seed |
| 同上・変換 | 使用範囲、棄却数、変換版、provider 失敗・フォールバック経路 |
| `OutcomeCommitted` | `acquisitionSeq`, `faceId`, 選択面の確定参照、`cardId`, outcome/oracle の各 receipt |
| `Resolved` | `outcomeSeq`, 完全な `postState`, `postStateHash`, `events[]`, `message`, 計算版 |
| `PresentationReceipt` | `resolvedSeq`, `presentationAttemptId`, `phase=started/completed`, 提示時刻 |
| `InterruptedUnresolved` | `acquisitionSeq`, 検出時刻、理由、`outcomeKnown=false` |

`EntropyReceipt` に認証 token、API key、リクエストの秘密ヘッダーを保存しない。
出目とカードが完全に記録される前に終了した場合は、保存済み情報のない結果を復元したことにしない。
既に保存された出目は、庭の計算・文章選定・演出に失敗しても結果集計へ残す。

### 5.4 出来事・文章・つながり

| DTO | 必須情報 |
|---|---|
| `SimulationEvent` | `eventId`, `kind`, `ruleId/version`, 発生 tick、対象 cell/植物 ID |
| 同上・根拠 | 入力参照、判定に使った状態量と変化、参照する過去状態・出来事 |
| `SelectedMessage` | `templateId`, 実際の `text`, `locale`, 文章選定版、参照した `eventIds`, `cardId` |
| 同上・選定 | 成立条件、採用理由、`fallbackUsed`。カード以外の出来事を捏造しない |

「前の水と今回の光で育った」と示すには、成長ルールが参照した湿り・照射の根拠を残す。
過去のどの注水に由来するかまで追跡していなければ、特定の過去 round を原因と断定しない。
文章を将来の新版で再生成しても、実際に提示した元の文章は置き換えない。
表示対象の文は `Resolved` に保存し、提示の有無は `PresentationReceipt` で別に記録する。

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

「花が咲いた」の確率を水や光の面数/6で代用しない。庭の過去状態も結果を決めるためである。
意図の自由文を AI が事後解釈して、一致を増やす経路は設けない。
構造上の不成立・必成立・対象外・欠測の件数を、比較対象の件数とは別に残す。

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
  "outcome": { "faceId": 4, "cardId": "circulation", "source": "local" },
  "observedMatch": 1,
  "eventKinds": ["RootZoneWetted", "SoilMoistened"],
  "text": "水が土に染み込み、草の根元が湿りました。",
  "presentationCompleted": false
}
```

この例は提示が未完了でも一致1回・基準確率2/6として残る。
花が咲いたことや、祈りが出目を変えたことは、この記録から追加しない。

## 8. 初期グラフと集計

グラフは保存した履歴から毎回再構築できる projection とし、独自の成功カウンターを持たない。

| 表示 | 集計・表示内容 |
|---|---|
| 庭の記録 | 意図、6面、出目、カード、入力位置、出来事、文章を順番に見る |
| 意図と出目 | 対象の観測数、実際の一致数 `ΣY_t`、対応する期待一致数 `Σp_t` |
| 庭の変化 | 地表水、土の湿り、植物の成長と、入力を与えた時点 |
| 記録の状態 | 取得開始数、結果保存数、欠測、提示未完了、乱数 source の内訳 |

意図と出目の既定集合は、結果が保存され `face / element` かつ `0<p_t<1` の回。
`Resolved` や `PresentationReceipt` が未完了という理由で、既知の出目を除外しない。
欠測を成功・不成功に補完せず、観測集合に対応する `Σp_t` を使う。欠測数を隣に示す。
方法別にも観測数と対応する期待値を示す。祈る方法の自己選択による比較は観察結果である。
庭と配面が過去の結果へ適応するので、固定確率の二項検定や単純な z 判定を追加しない。
各回の `p_t` が得られても、独立試行を仮定した Poisson-binomial 検定を自動採用しない。
初期版は有意性や「効果を証明」の判定を持たない。別の検証計画は新しい run/仕様で扱う。

関係図では、自然モデルの入力・状態・適用ルール・結果を「計算上のつながり」として表示する。
意図・カード・出目の並びは「記録された順序／対応」として区別する。
意図から出目へ因果が証明されたような矢印は描かない。
前の文章が次の意図へ影響した可能性を検討できるよう、提示記録への参照を残す。

## 9. 永続化 adapter の条件

展示版の保存候補は、長さを持つ frame 単位の追記 journal とする。
各 frame は一つの完全な `JournalEntry` を包み、途中書き込みを検出できる形にする。
詳細な byte 構造、OS ごとの flush/fsync 相当、ロック、容量上限は実装時に固定・検証する。
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

single-writer が「head読取 → 遷移・一意性検査 → canonicalize → hash → frame追記 → durable確認」を直列化する。
UI、provider、シミュレーターは保存先へ直接書き込まない。集計 cache は再生成可能とする。
不完全な末尾 frame は確定記録として読まず、途中の破損や hash 不一致は正常履歴と混ぜない。
破損した原本を上書きせず、最後に検証できた境界と回復診断を保存し、安全な回復経路を用意する。
`Resolved.postState` が再開時の正であり、再計算は監査用。差異があっても過去状態を置換しない。
ローカル hash chain は改竄検知可能であり、外部の証跡なしに完全改竄不能とは表現しない。
端末間同期は初期版の対象外。後から導入しても journal の競合を新しい結果として合成しない。

## 10. 次の実装で確認する受入条件

1. 意図・配面の参照が未確定なら、出目用乱数の取得を拒否する。
2. 保存段階ごとの強制終了と再開で、再抽選・二重注水・結果の消失が起こらない。
3. 252〜255の byte 棄却と1〜6への対応を既知入力で確認する。
4. 同じ配面・出目でも意図によって庭の計算結果が変わらない。
5. `p=0,1,1/6,2/6,5/6`、欠測、提示未完了を混ぜた集計が契約どおりになる。
6. 同じ保存入力と版から計算した状態・イベントを、対象端末で監査できる。
7. 本文に参照した出来事が存在し、カード・文章・FX が結果を変更しない。
8. frame途中終了、容量不足、改変、重複操作を検出し、未保存結果を表示しない。
9. export → verify → グラフ再生成が、ネットワークなしで一致する。
10. 旧実験の保存先・schema・統計・凍結パラメーターへ変更を加えない。

これらは今後の検証項目であり、この設計文書の追加時点で合格済みとはしない。
