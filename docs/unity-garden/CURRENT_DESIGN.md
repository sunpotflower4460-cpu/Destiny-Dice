# 今作る設計と、実装へ渡す範囲

設計版 **0.8 / 2026-11-12 / designCandidate**。Unity本体・C#の検証器・実機ビルドは未実装。

**一つの庭、三つの届け先、三つの天候。公開された6面を見て振ると、同じ自然の規則で庭が変わり、実際の出来事に合う短い言葉と光・音が返る。** 意図、場所の指定、詳しい記録は任意に使い、最初の一投に設定作業を要求しない。

本書は現行の組合せと実装入口を示す索引である。詳細な数式・保存規則を別方式で定義しない。変更の経緯は[README](./README.md)、確認範囲は[VALIDATION](./VALIDATION.md)へ残す。

## 1. 新しいrunへ採用する組合せ

採用候補の原本、byte長、SHA-256、規則版を[design-manifest.v0.8.json](./content/design-manifest.v0.8.json)へ集約する。ファイル名が一番新しいものを自動で探したり、すべてを同じ版番号へ改名したりしない。

| 対象 | 現行の原本・規則 | 最初に接続する段階 | 扱い |
|---|---|---|---|
| 水・土・成長・8入力 | [simulation-profile.v0.2](./content/simulation-profile.v0.2.json) | G2 | 必須。天候・場所の明示した差分だけを適用 |
| 地形と初期の庭 | [garden-map.v0.3](./content/garden-map.v0.3.json) | G1の初期状態、G2の自然計算 | 必須。256セル・初期4個体。旧anchorは現行の作用点に使わない |
| 空と天候期 | [weather-profile.v0.4](./content/weather-profile.v0.4.json) / `weather-episodes.v0.4` | G1の保存、G2の合成 | 必須。初期晴れ、3正常解決投で一期 |
| 届け先と6面 | [target-profile.v0.6](./content/target-profile.v0.6.json) / `target-sites.v0.6` / `dynamic-faces.v0.6` | G1の確定境界、G2の生成 | 必須。入口85・岩陰135・ひらけた土154。初期はおまかせ |
| 出来事のまとめ方 | `observation-groups.v0.4` / [GAMEPLAY §6](./GAMEPLAY.md#6-神聖さを支える光間se) | G3 | 必須。主焦点1・補助最大1。全イベントは保持 |
| 6枚のカードと28文 | [oracle-catalog.v0.7](./content/oracle-catalog.v0.7.json) / `oracle-focus.v0.7` | G3 | 必須。順位はv0.5を継承し、岩陰の未変化文に同じ場所の光の根拠を追加 |
| 種から育つ花の色・幅 | [plant-appearance-profile.v0.6](./content/plant-appearance-profile.v0.6.json) / `birth-appearance.v0.6` | G3の基本動作後 | 任意。未採用なら能力・自然状態・確率に影響せず、採用済みと記録しない |

初回の作成・来場者交代・明示的な障害後の新庭は`runLifecycleVersion=run-entry.v0.8`として、DATA_CONTRACTの保存契約へ固定する。通常の来場者交代はv0.7の順序を継承し、初回と救済時には一つの不変な作成計画から同じrunへ復帰する。

成長の局所的な説明は`simulationEvidenceVersion=local-growth-witness.v0.8`として、[SIMULATION](./SIMULATION.md)の同cell・同tickの根拠を保存する。自然の式・出目・28文の条件や順位は変えない。[OBSERVATION](./OBSERVATION.md)の`round-observation.v0.8`は記録を表示・書出しするときの投影版であり、庭のstateHashや乱数の入力へ戻さない。

G1は最小の固定fixtureを通す段階であり、表の全ゲーム機能を初日に完成させる条件ではない。G1〜G3の実装順は[ROADMAP](./ROADMAP.md)を使う。

### 組合せる順序と、上書きの範囲

1. **基礎profileとmap**から地形・初期水分・初期植物・整数計算を定める。
2. **weather profile**の`explicitReplacements`だけを適用する。基礎の光・風は確定した天候の値へ置き換え、天候とダイスの場を合成してから上限・遮蔽を一度適用する。水の総量は天候とダイスを分けて記録する。
3. **target profile**の`explicitReplacements`だけを適用する。4種類のrecipe anchorを同じ確定地点へ解決し、配面材料の先頭で場所を抽選する。weather profile内の旧`dynamic-faces.v0.4`は、ここだけ`dynamic-faces.v0.6`へ置き換わる。天候の期・確率や自然係数まで置き換えない。
4. **catalog v0.7**を実イベントに結ぶ。カード・文章は庭の自然計算へ戻さない。v0.5の順位fixtureは継承する順位の確認に使い、新しい述語の正しさまで検証した結果にしない。
5. **外観を採用した場合だけ**、出生時の保存事実と固定profileから花の見た目を求める。未採用時は`appearanceProjectionVersion=null`とし、runのcontentManifestへ使わない外観profileを必須参照として加えない。

これは任意のJSONを再帰的に上書きする手順ではない。実装は許可した差分だけを扱い、未知の版・hash・組合せは検証エラーにする。旧runへ新しい地点・述語・外観を後付けしない。

## 2. 迷ったときに読む正の文書

| 判断 | 正とする文書 | 他の文書の役割 |
|---|---|---|
| 何を作るか、展示でどこまで作るか | [READMEの要件](./README.md#依頼から確定している要件) / [ROADMAP](./ROADMAP.md) | 本書は現行版への入口 |
| 保存順、取消、交代、再開、export | [DATA_CONTRACT](./DATA_CONTRACT.md) | 画面はこの契約の結果を表示する |
| グラフの横軸、初期点、件数、空表示 | [OBSERVATION](./OBSERVATION.md) | 検証済み記録を一つの投影へ変換。EXPERIENCEは見せ方を定める |
| 水・光・風・成長の数式とtick順 | [SIMULATION](./SIMULATION.md) | WEATHERとDIVERSITYは関係の説明、GARDEN_LAYOUTは配置と参照結果 |
| 候補重み、6面、出来事group、文の条件と順位 | [GAMEPLAY](./GAMEPLAY.md) | catalogはID・本文・条件の候補データ |
| 操作状態、今回と次から、見せ方・音 | [EXPERIENCE](./EXPERIENCE.md) | PresentationはCoreの結果を変更しない |
| 共通C#と端末adapterの責務 | [ARCHITECTURE](./ARCHITECTURE.md) | ストア・実機の合格は別ゲート |
| 何を計算済みで、何が未確認か | [VALIDATION](./VALIDATION.md) / 各参照report | 候補値と実装・実機の証拠を分ける |

保存契約と画面文言が食い違ったら、画面のために保存済み条件を変えず、文言と画面の境界を修正する。数式と例が食い違ったら、過去のreportを静かに書き換えず、その版と差異を記録してから新しい候補を作る。

## 3. 一投から次の一投へ守る境界

| 境界 | 守ること |
|---|---|
| 準備 | 開始状態・天候・実作用点・6面を保存してから公開する |
| 場所の変更 | Readyで変えても、いま振れる公開中の一投は変わらない。その一投が終わった後の準備から反映する |
| 意図 | 任意。確定してから出目・カードを取得し、祈りの方法で配面を調整しない |
| 庭の更新 | 一投40tick。待機・長押し・バックグラウンド時間で進めない |
| 結果 | `Resolved`の保存後に庭・出目・文を公開する。省略しても同じ終状態を使う |
| 次の準備 | 過程再生の終了・省略と終状態表示の後。受理済みの場所設定を先に保存する |
| 来場者交代 | 交代要求と次run IDを保存し、旧round終端→旧run閉鎖→固定した新runへ収束させる。途中終了で前の本文を再公開しない |
| 初めての庭 | 内容と作成計画を保存してからgenesisを作る。作成途中の終了は同じ計画・同じrun IDへ戻る |
| 記録 | JSONは対象runのプレイを再構成するbundle、CSVは閲覧用一覧。範囲外の交代参照は診断を残し、前の来場者の本文を自動同梱しない |
| 振り返り | 準備開始順の位置を保ち、未解決の庭はnull、確定した成長0は0とする。比較0件を0%の成功率へ置き換えない |

交代要求の保存前にプロセスが終了した場合は、交代が受理されたとは扱えない。保存中・回復中は引渡し完了を示さず、詳しい境界はDATA_CONTRACTに従う。通常プレイの毎投に確認操作を増やすものではない。

## 4. 展示で削らない部分と、後に回せる部分

| 優先 | 内容 | 成立の証拠 |
|---|---|---|
| 最初に通す | 1シーン・1庭・local乱数・保存・同run再開 | 展示端末の実行ファイル、同じ固定入力と終状態への復元 |
| 遊びの中心 | 三天候・三地点・8入力・動的6面・成長・短文 | C#による参照照合、初回から数投を通す画面 |
| 体験と運用 | 共通の光・鐘・自然音、既存三つの記録表示、交代と復帰 | 自動入力/trace照合、実端末の停止と復帰、少数の知覚確認 |
| 基本動作の後 | 出生時の三色×三幅、カードの全面イラスト、凝ったグラフ表現 | 採用したものだけビルド記録へ。未採用を合格数に含めない |
| 後続拡張 | 実行時AI、録音/認識、量子API、複数の庭、動物、同期 | 別フェーズの設計・実装・検証 |

花が育ち切った庭も、水・風・光・天候を眺めながら続けられる。成熟後の別ゲームや自動リセットを今回追加しない。三地点の播種範囲は静的に最大40個体だが、通常の抽選で全セルへ到達する保証や、全員の庭が永久に異なる保証にはしない。

## 5. 自動照合と、人の確認をつなぐ

この設計の参照確認は、次のコマンドから実行する。いずれもPython標準ライブラリを使い、ネットワークやUnityを必要としない。

```sh
python docs/unity-garden/reference/check_current_design.py
python docs/unity-garden/reference/observation_v08.py
python docs/unity-garden/reference/growth_witness_v08.py
python docs/unity-garden/reference/diversity_v06.py --verify
python docs/unity-garden/reference/water_stress_v07.py --verify
```

- `check_current_design.py`は採用候補の実体・hash・byte長・版の組合せ、参照資料の用途、旧catalogからの限定した差分、内部リンクを読み取り専用で照合する。Unity loader、journalや述語の実行試験ではない。
- `observation_v08.py`は検証済みと仮定した合成入力から、横軸・null・累積一致と基準・件数・空表示を照合する。実journalの検証や乱数の復元まで実施した結果にはしない。
- `growth_witness_v08.py`は小さな局所例で蒸発・成長と参照条件を照合する。庭全体のイベント生成・保存・関係図の描画は後続で確認する。
- `diversity_v06.py`は既存の場所・履歴の設計用参照計算を再現する。内部IDだけの差を物理的な多様性へ数えない。
- `water_stress_v07.py`は三地点×二つの水入力を強制小雨で各200投続ける。全6列を報告し、旧入口の長期計算を岩陰・ひらけた土の確認へ流用しない。

全参照計算を文言変更のたびに回す必要はない。変更した境界の検査と、リポジトリの必須ゲートを使う。自然の規則・profile・map・参照コードを変更した場合は、その影響範囲を再計算する。

実装後の検証は[EXPERIENCE §9](./EXPERIENCE.md#9-g3で確認する受け入れ場面)で、自動検証・実端末の運用・人の知覚へ分ける。人はH1〜H5の通し確認を使い、毎回すべての競合ケースを手で操作する計画にしない。見分けやすさ、自然な日本語、光と音の心地よさに確認時間を使う。

## 6. manifestの用途と、残る実装判断

manifestは**G0の設計原本の索引**であり、`RunStarted.contentManifest`そのものではない。hashはリポジトリの原本byte列のSHA-256で、JCS化したsnapshotのstateHashとも異なる。必要な原本をUnityへ採用・変換する際は、その変換後の実体とhashを保存し、どの候補から採用したかも追えるようにする。

実行用contentとして採用するものと、期待値・fixture・report・参照Pythonはmanifest内でも分ける。参照reportを読んで出目・成長・文章を再生する通常プレイ経路を作らない。外観の候補が索引に載っていても、それを使わないビルドのrunへ採用済みのhashを記録しない。

Unityの正確なパッチ、表示用資産、journal frameのbyte構造、C# serializer、対象端末の性能はG1以降で検証して固定する。新しい設計番号を付けただけで、未実装の保存互換性や全プラットフォーム対応を完成扱いにしない。
