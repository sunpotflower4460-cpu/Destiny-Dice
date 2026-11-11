# Unity Garden — 記録・復旧・観察のデータ契約

設計版: **0.3 / designCandidate／Unity 実装・実機検証は未実施**。対象は新しい箱庭ゲームのみ。自然モデルの係数・参照算術はv0.2を継続する。
この文書は既存の `DESIGN.md` / `PROTOCOL_FREEZE.md` の実験・台帳を変更しない。
数値パラメーター、ファイル I/O、C# の具体的な型は、後続の実装フェーズで検証する。

## 0. G1で実装する最小経路

展示版はlocal entropyだけを使い、配面用の材料と、出目・カード用の材料を保存してから解釈する。
「乱数の材料を保存できた」と「出目を表示した」は異なる段階である。

`PreparationStarted → MappingEntropyCommitted → Prepared → IntentCommitted → AcquisitionStarted → DrawEntropyCommitted → OutcomeCommitted → Resolved → PresentationReceipt`

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
- `previousRunId` / `importedFrom`：庭を引き継ぐ場合の出所。過去の run を上書きしない。

ルール・計算方式・コンテンツの版を `RunStarted` で固定する。
版ラベルだけでなく、使った設定値・地図・カードと文章集の内容、または永続した内容参照とhashを保存する。
参照先は後から同名で差し替えられないものとし、保存データと一緒に復元・監査できるようにする。保存順は§9に従う。
版を変更する場合は新しい run を開始し、変更前後の集計を既定で混ぜない。
同じ端末の同じ人物かどうかは自動判定しない。展示の run は人物 ID を意味しない。

run内で未終端のroundは最大一つとする。正となる庭の参照は、最後の保存済み`Resolved.postState`、まだなければ`RunStarted`の初期状態から求める。
この参照は`sourceSeq / sourceEntryHash / worldRevision / stateHash`の組であり、最新のjournal headとは区別する。後着のPresentationReceiptやRecoveryContinuedは庭の版を進めない。
`PreparationStarted`はこの組を開始状態参照として固定し、`beforeRevision / beforeStateHash`を持つ。新しい準備の保存時とResolvedの保存時に、同じ庭の参照であることをwriterが照合する。
別の未終端roundがあれば新しいround IDの開始も拒否する。準備commandは一度だけroundIdを割り当ててcommandIdと保存し、同じcommandの再送はそのroundへ収束させる。二つの配面を同じ庭から並行解決しない。

## 2. 一回の状態遷移

以下の各段階は、single-writer が追記を永続化してから次へ進める。
保存の成功応答は、単にメモリー上のキューへ入ったことを意味してはならない。

| 段階／イベント | 保存するもの | 保存後に許可する操作 |
|---|---|---|
| `PreparationStarted` | round ID、開始状態参照、配面規則、mapping取得 ID | 初めて配面用乱数を取得 |
| `MappingEntropyCommitted` | 配面用の生byte、取得元、材料形式・変換版 | この材料だけから6面を生成 |
| `Prepared` | 開始時の庭、6面全体、生成根拠、mapping材料参照、実行時間幅 | 下記の表示条件を満たしてから配面公開、意図の編集 |
| `IntentCommitted` | 意図のスナップショット、方法、対応面、基準確率 | 取得開始の記録 |
| `AcquisitionStarted` | 取得 ID、参照する意図・配面、乱数ポリシー | 初めて出目・カード用乱数を取得 |
| `DrawEntropyCommitted` | outcomeのbufferと、oracleのbufferまたは失敗receiptを一つのrecordで保存 | 初めて出目・カードとして解釈 |
| `OutcomeCommitted` | 材料参照、出目、選択面、カードまたはカード未成立理由 | 同じ入力で自然モデルを計算 |
| `Resolved` | 完全な更新後状態、出来事、採用文、参照情報 | 結果演出、次の回の非公開準備 |
| `PresentationReceipt` | アプリが報告した提示段階、提示対象 | 補助的な提示履歴。次の回の必須gateではない |

意図を持たない場合も `IntentCommitted(kind=none)` を保存して同じ経路を通る。
長押し中の下書きは変更可能だが、確定後の変更は次の round に適用する。
`Prepared` の6面は固定し、意図変更・画面回転・バックグラウンド復帰で再生成しない。
`PreparationStarted`から`Resolved`まではCoreの開始snapshotを固定する。配面公開後は公開中の庭もその開始状態に保ち、待機時間で進めない。
`OutcomeCommitted`後に別の計算用コピーを固定tick数だけ進める。未終端roundの所有と開始状態参照を再確認し、`Resolved`の永続化後にだけ`postState.worldRevision = beforeRevision + 1`を新しい庭として採用する。
対象位置・方向・シミュレーション時間幅も固定し、確定後の外部入力を混ぜない。
結果待ちの光や音は使えるが、保存前に出目・カード・庭の結果を先に見せない。

次のPreparedを先に保存しても、6面公開・意図編集・投擲受付は、前回の過程再生があれば終了または省略し、画面がそのPreparedの`beforeRevision / beforeStateHash`に対応する終状態になってから許可する。初回は保存済み初期状態との対応を確認する。
途中の補間画面は開始snapshotにしない。省略操作のreleaseを次の投擲へ流用せず、公開後の新しいpointer-down/upまたはkey-down/upを要求する。処理中の連打を次回分として予約しない。
この表示条件は保存済み庭と画面の対応であり、Receiptの保存や人の読了確認を新しいgateにしない。

## 3. 中断・再開・二重実行

| 再開時に存在する最後の段階 | 回復規則 |
|---|---|
| `PreparationStarted`のみ | `PreparationInterrupted`を追記。同じroundへ新しい材料を入れず、新roundで準備できる |
| `MappingEntropyCommitted` | 保存した材料・状態・版から配面生成を再開。新しい乱数を取得しない |
| `Prepared` | 同じ庭と6面を復元。未確定の意図は編集可能 |
| `IntentCommitted` | 同じ意図を維持。新しい意図に置換せず、取得開始へ進める |
| `AcquisitionStarted`のみ | 真のプロセス終了後は`InterruptedUnresolved`を追記。同じ回を再取得しない |
| `DrawEntropyCommitted` | 保存したbufferと取得成否を解釈し、同じ出目・カードまたは未成立へ進める。未表示でも再取得しない |
| `OutcomeCommitted` | 保存した出目・カード・開始状態から同じ計算を実行する |
| `Resolved` | 保存した完全な更新後状態を復元する。入力をもう一度加算しない |
| 提示記録なし／提示途中 | 同じ結果の提示を再開できる。集計から結果を外さない |

roundの論理解決はResolvedで完了する。Receiptを失った回も、結果を復元してそのまま次へ進める。
Receiptは読了や次回開始の条件ではない。保存adapter自体の障害中は、通常どおり新規取得を止めて回復する。
同一プロセス内の一時停止と、プロセス終了後の復元は区別する。
取得処理が生きている間は、その処理の完了を待つ。別の取得を並行して始めない。
`PreparationInterrupted`は配面準備の未成立であり、出目の欠測数へ入れない。
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
提示記録だけは `roundId + presentationAttemptId + phase` を一意にする。
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

来場者交代では旧runの内容を通常画面から外して処理を閉じ、`RunClosed`保存後に新しいRunStartedへ切り替える。
旧runに処理中の回があれば、取得前取消または取得後の確定・未成立に決着させる。
背景へ移るだけの場合は新runを作らず、同じ状態を保持する。

## 4. 乱数の役割と監査

| role | 使用箇所 | 相互依存させない条件 |
|---|---|---|
| `mapping` | 庭の条件に応じた候補から6面を生成 | PreparationStarted後に取得、材料保存後に生成 |
| `outcome` | 確定した6面から1面を選ぶ | AcquisitionStarted後に新しく取得、材料保存後に解釈 |
| `oracle` | 6枚のオラクルから1枚を引く | outcomeと別bufferで取得、成功または失敗をbundleに保存 |
| `visual` | 粒子、装飾の揺れなど | 他 role の乱数列と庭の計算状態を変更しない |

役割ごとに新しい独立の取得を行い、bufferを共用しない。装飾用streamも分ける。
別の取得で内容が偶然同じになっても再取得しない。値の違いを独立性の判定に使わない。
G1では有限の生byteをそのまま保存する。seedから展開する方式は採用時に別の材料形式・生成版を定義する。
将来の出目用 seed を配面生成時に作ったり、事前取得した出目を意図確定後に消費したりしない。
展示版はオフラインのローカル暗号乱数を想定し、`source=local` を正直に記録する。
テスト用 seed は `source=fixture` とし、実プレイの集計に混ぜない。

### 4.1 G1の有限buffer

初期案はmapping用256byte、outcome用32byte、oracle用32byte。長さは乱数ポリシーに保存する。
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
候補順・抽選順・shuffle方向は[GAMEPLAY.md](./GAMEPLAY.md)§5.1の`dynamic-faces.v0.3`に固定する。列挙コンテナや辞書の順序を乱数の解釈へ混ぜない。

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
| 同上・版情報 | `rngPolicyVersion`, `rngAlgorithmVersion`, `deckVersion`, `messageVersion`, `groupingVersion`, `selectionPolicyVersion`, `appVersion`, `buildId` |
| 同上・識別 | `platform`, `previousRunId?`, `initialStateProvenance`, `startedAtUtc` |
| 同上・内容 | `contentManifest`：設定・地図・deck・文章集の永続参照、内容hash、byte長 |
| `RunClosed` | 終端head、終了理由、最後のworldRevision。進行中の回を残して通常終了しない |
| `WorldSnapshot` | profile/mapの内容hash、グリッド形状、地形・障害物・土壌容量、地表水・土中水・端数、植物状態、tickと累計、`worldRevision`, `stateHash` |

グリッドや植物状態の正確な要素は自然モデルの仕様で固定する。
浮動小数の丸め、列挙順、境界処理、時間刻みも数値表現・計算版に含める。
固定時間刻みだけを根拠に、Unity PhysX の端末間完全一致を主張しない。
異なる端末での完全一致は、同じ保存入力を使った監査で確認する実装上の受入条件とする。

`stateHash = SHA-256(JCS(WorldSnapshotからstateHashだけを除いたobject))`の小文字16進値とする。snapshotはSIMULATIONの状態項目を全て含み、セル配列はcellId昇順、個体配列はcellId・plantIdの順とする。整数は§9の範囲、IDはordinal比較を使う。
`Resolved.postStateHash`は`postState.stateHash`と同じ値。外側のrecord、hash自身、文章、演出trace、UI状態をsnapshot hashへ入れない。内容原本を識別するcontent hash、journal record全体のentryHashとは別の対象をhashする。

### 5.2 配面と意図

| DTO | 必須情報 |
|---|---|
| `PreparationStarted` | 開始状態の`sourceSeq / sourceEntryHash / beforeRevision / beforeStateHash`、`commandId`, `preparationId`, 配面ポリシー、予定buffer長 |
| `MappingEntropyCommitted` | `preparationSeq`, `mappingReceipt`, 材料形式、変換版 |
| `Prepared` | `mappingEntropySeq`, PreparationStartedと同じ開始状態参照/hash、`faces[6]`, 候補重みと状態量、材料消費記録、`selectionAtPrepared` |
| 同上・入力範囲 | `targetMode`, `anchorRefs`, `simulationStepCount`, `fixedDelta`, `preparedAtUtc` |
| `FaceAssignment` | `faceId=1..6`, `recipeId`, `elementId`, `resolvedParameters` |
| `resolvedParameters` | `amount`, `unitId`, `targetCells[]`, `cellWeights[]`, `radius`, `direction?`, `startTickOffset`, `durationTicks`, `injectionSchedule` |
| `IntentCommitted` | `preparedSeq`, `kind`, `target?`, `freeText?`, `method`, `committedAtUtc` |
| 同上・比較 | `matchingFaceIds?`, `chanceNumerator?`, `chanceDenominator?`, `eligibilityReason` |
| 同上・経過 | 実際に計測した長押し時間、前回提示文への参照、前回の意図からの継続か |
| 同上・選択 | `selectionChangedAfterPrepared`, `selectionMode=carried/selected/none`。開始時の目標はPreparedのmetadataを参照 |

`kind` は `none / face / element / gardenOutcome / freeText` とする。
`method` は選択された `asIs / intention / prayer / spoken / unknown` とする。
方法の選択は実行の自動検出ではない。音声録音・文字起こしがない状態でも使える。
意図を前回から維持する場合も、その回の配面に対する `matchingFaceIds` を確定し直す。
`gardenOutcome` の対象は出来事として残すが、面の一致率とは別の記録にする。
比較値を持つのはface/elementだけ。該当面なしは空配列・0/6、それ以外のkindは比較値をnullとする。
`selectionAtPrepared`は配面生成に使わない記録用metadata。確定した目標との差をselectionChangedAfterPreparedにする。
このboolは最終目標が変わったかを示し、全編集操作や自由文の入力履歴を記録するものではない。
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
| `Resolved` | `outcomeSeq`, Preparedと同じ開始状態参照、完全な `postState`, `postStateHash`, `inputApplicationEvidence`, `events[]`, `message`, `presentationPlan`, 計算版 |
| `PresentationReceipt` | `resolvedSeq`, `presentationAttemptId`, `phase=presenter_started/presenter_completed`, アプリ報告時刻 |
| `InterruptedUnresolved` | `acquisitionSeq`, 検出時刻、理由、`outcomeKnown=false` |
| `PreparationInterrupted` | `preparationSeq`, `mappingEntropySeq?`, 理由。出目の欠測とは別 |
| `AbandonedBeforeAcquisition` | 準備・配面・意図の存在する参照、取消command ID、終了理由 |

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
| 同上・根拠 | 入力参照、判定に使った状態量と変化、参照する過去状態・出来事 |
| `InputApplicationEvidence` | 確定recipe・scheduleへの参照、実際に適用したtick/対象/量、適用完了。予定値と実適用を区別 |
| `ObservationGroup` | `groupId`, `kind`, 対象ID群、`firstTick`, `lastTick`, 元の`eventIds`, `aggregateEvidence`, `groupingVersion` |
| `SelectedMessage` | `templateId`, 実際の `text`, `locale`, 文章選定版、参照した `eventIds`, `cardId?`, `cardStatus` |
| 同上・選定 | `focusGroupId?`, `supportingGroupIds`, `predicateBindings`, `selectionPolicyVersion`, `scoreTuple?`, `recentMessageIdsCompared`, `recentResolvedSeqsCompared`, `fallbackUsed`, `fallbackReason?` |
| `PredicateBinding` | predicate名、具体的なsourceRefs、対象plant/cell、tickまたは区間、観測値、版付き閾値への参照 |
| `PresentationPlan` | `primaryGroupId?`, `secondaryGroupId?`, 参照した`observationGroups[]`, `groupingVersion` |

ObservationGroupは、実イベントを意味と場所でまとめた表示用の投影であり、新しい自然イベントや独立台帳ではない。
`observation-groups.v0.3`では同kindの4近傍成分を基本とし、全域集約のSurfaceFlowObservedだけは一つのglobal groupにする。分離した流れへ集計値を複写せず、局所到達の根拠にしない。
Resolvedには全SimulationEventと、文章・表示で参照するgroupだけを保存する。全候補groupは同じgroupingVersionから再生成できる。
`primaryGroupId=focusGroupId`とし、secondaryは最大一つ。言葉とFX/SEは同じ焦点を参照する。
input_pairの二つ目のgroupは必要な補助焦点として優先する。supportingGroupIdsは焦点以外の全ての述語根拠groupの参照であり、表示する一つのsecondaryとは区別する。
`predicateBindings`は文の各成立条件を具体的なイベント・入力・状態へ結び付け、group名だけを真偽の根拠にしない。
同じ対象を述べる複数条件はplant/cellを合わせ、各述語で定義した時点・区間の関係を満たすことを要求する。異なる場所の事実を組み合わせて成立させない。
複数の完全な束縛が成立するときは、GAMEPLAY§7.3のsubject・tick・source eventによる固定tupleで一つを選び、その束縛を保存する。辞書の列挙順や追加乱数で選ばない。
イベントのない事実fallbackではfocus/primary/secondaryをnullにでき、確定入力・前後状態を直接参照する。
fallbackの`input_applied`にはInputApplicationEvidenceが必要。Outcomeで入力が選ばれただけでは成立しない。
`templateId`はcatalogの`entries[].id`を保存する。文章を指すmessageIdと別の独立IDを生成しない。
`oracle-focus.v0.3`の候補は、scoreTuple=`cardAffinity / priority / recentNovelty`の降順、その後messageId・groupId・bindingKeyの昇順で比較する。bindingKeyは保存したpredicateBindingsから導出できるため独立recordにしない。
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
| 庭の記録 | 意図、6面、出目、カード、入力位置、出来事、文章を順番に見る |
| 比較できる抽選での一致 | 対象の観測数、実際の一致数`ΣY_t`、対応する確率の基準値`Σp_t` |
| 庭の変化 | 地表水、土の湿り、植物の成長と、入力を与えた時点 |
| 記録の状態 | 準備・意図・取得・既知出目・比較対象・p=0/1・欠測・対象変更・提示証拠・sourceの内訳 |

比較の既定集合は、出目が既知で`face / element`かつ`0<p_t<1`の回。
既知とは検証済みOutcome、または検証済みDrawEntropyのoutcome bufferから同じ版で1〜6の出目を確定できること。
`OutcomeCommitted`の追記・`Resolved`・提示が未完了という理由で、材料から確定できる出目を除外しない。
欠測を成功・不成功に補完せず、観測集合に対応する`Σp_t`を使う。欠測数を隣に示す。
この値は各回の配面から計算した基準であり、欠測が結果と無関係であることを保証しない。
方法別にも観測数と対応する期待値を示す。祈る方法の自己選択による比較は観察結果である。
庭と配面が過去の結果へ適応するので、固定確率の二項検定や単純な z 判定を追加しない。
各回の `p_t` が得られても、独立試行を仮定した Poisson-binomial 検定を自動採用しない。
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
毎回40tickの基礎光・蒸発・成長は、今回の追加作用が届かない場所でも進む。イベントのinputRefは同roundの文脈参照であり、それだけで局所作用や因果edgeの根拠にはしない。
必要に応じ「開始時の水分・成長」「40tickの経過・基礎光」「今回の追加作用」を別の計算入力として示す。今回の入力からは、対象・区間・実適用の証拠がある依存だけを結ぶ。範囲外の花が育っても、今回の風や追加光の直接効果と表示しない。
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
継続不能なら、検証済みsnapshotを明示的な出所とする新runを開始できる。旧runの失われた結果を埋めない。
この新runは旧runの完全な復旧とは呼ばず、回復元と未確定範囲を記録する。

### 9.2 内容参照の永続性

設定・地図・deck・文章集は、内容hashをキーにした保存領域へ書き、byte長・hash・durable保存を確認する。
その後にだけ、それらを参照するRunStartedを保存する。snapshotの外部参照にも同じ順序を使う。
現在のアプリに同梱された名前付きassetや、変更され得るURLだけを永続参照にしない。
runが参照中の内容は更新で置換・削除せず、exportにも実体または完全な自己完結bundleを含める。
保存状態の閲覧と、旧版モデルの再計算は別の能力である。対応する計算版がない場合、新版で代用しない。

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

代表的な復旧trace（実行済み結果ではなく、G1のfixture要件）:

- `PreparationStarted → kill`：準備中断を残し、出目取得数は0。
- `MappingEntropyCommitted → kill`：同じ材料から同じ6面。local取得回数は増えない。
- `DrawEntropyCommitted → kill`：同じ出目とカードへ復元し、Outcome未追記でも既知出目を失わない。
- `outcome受理／oracle全棄却`：同じ出目で庭を解決し、カード未成立＋事実文。カード再取得は0。
- `outcome取得成功／oracle local例外`：生存processは材料＋失敗receiptを保存し、出目と庭を保全する。
- `oracle取得中kill／bundleなし`：未保存のoutcome材料を再現したことにせず、出目未成立を残す。
- `Resolvedあり／Receiptなし`：同じ結果を復元し、読了確認やReceipt補完を次回開始の条件にしない。
- `OutcomeCommitted → Resolvedの末尾破断`：原本を残してsegment継続後、開始状態から一度だけ解決する。
- `取消commit → 遅延した取得開始要求`：状態・epoch照合で拒否し、新しい乱数は取得しない。

これらは今後の検証項目であり、この設計文書の追加時点で合格済みとはしない。
