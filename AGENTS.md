# AGENTS.md — このリポジトリの作業ルール

あなた（Claude Code）はこのリポジトリの実装担当です。毎セッションの最初にこのファイルと
`DESIGN.md`、`PROTOCOL_FREEZE.md` を読んでから作業を始めてください。「何を作るか」は
`DESIGN.md` v2.0 + `PROTOCOL_FREEZE.md` v2.1 が正です。両者が衝突する場合は **PROTOCOL_FREEZE.md が優先**します。
このファイルは「どう作業するか」のルールです。3文書を読んでも仕様が一意に決まらない場合は手を止めて質問すること。

## 0. 最優先の3原則（これだけは絶対）

1. 1セッション = 1フェーズだけ。指示されたフェーズ（Gate 0, P1, P2…）以外には一切手を出さない。先回り実装は禁止。
2. 自己採点をしない。「できたと思う」は完了ではない。完了は、`DESIGN.md` §6 と `PROTOCOL_FREEZE.md` の当該フェーズ完了基準に対応するコマンドの実際の出力を貼って示すことでのみ成立する。
3. 迷ったら実装せず質問する。`DESIGN.md` / `PROTOCOL_FREEZE.md` に書かれていない仕様判断（値・挙動・命名）を勝手に発明しない。不明点は箇条書きにして人間に確認を求める。憶測でコードを進めるくらいなら止まる。

## 1. 不変ルール（違反したらそのPRは無効）

1. ledger は追記のみ。削除・編集する関数・経路を存在させない（デバッグ用も不可）。
2. 事前登録後のパラメータ変更は不可。仕様を変えたくなったら勝手に変えず、人間に相談。
3. bits → hits → z の計算経路にUI都合の加工を入れない。統計は `src/stats/` に隔離し、UI非依存の純関数にしてテストを必須にする。
4. 表示する数値の隣には必ず「偶然ならこのくらい」を併記できる形にする（正直メーター）。
5. 欠測は欠測のまま。補完・後付け除外のロジックを書かない。Layer C の outcome 取扱いは Protocol Freeze §11 の事前固定ルールに従う。
6. rngSource（anu/randomorg/local）を必ず記録し、フォールバックの発生を隠さない。Layer A の主要確証解析に含めるのは `anu` セッションのみ（Protocol Freeze §2.3）。
7. 予言（prediction）は必ず測定対象の乱数取得より前に台帳へ確定する。後出し予言の経路を作らない。UIだけでなく application/domain 層で強制する。
8. 確証パートと探索パートを混ぜない。事前登録の判定ルールは登録後変更禁止。探索由来のパターンを「実証」と表示しない。
9. 願いの本文・成功基準・締切・層は登録後変更不可。割付は登録直後に自動実行し、人の選択を挟まない。クラッシュ時は Protocol Freeze §12 の回復規則に従う。
10. 封印群の願いを実践UI（願いタイム含む）に表示する経路を作らない。非表示は domain/application projection で強制する。
11. 1PR = 1フェーズ = 1目的。PROGRESS.md の更新を伴わないPRは不可。
12. targetDir は測定対象のLayer A乱数から独立させ、Protocol Freeze §3 の独立seed/決定的生成方式だけを使う。
13. ledger hash は Protocol Freeze §8 の RFC 8785/JCS 仕様だけを使う。append と verify が同一canonicalization/hash実装を共有する。
14. ledger 書き込みは single-writer append service を必ず通す。UI/provider等から直接 INSERT しない。
15. 実験日付は登録時に固定した IANA timezone + dayBoundaryHour で計算し、端末timezone変更で意味を変えない。
16. Layer B の mood pre/post は Protocol Freeze §6 の順序で ritual を挟み、抽選結果revealを挟まない。
17. ローカルhash chainは「改竄検知可能 / tamper-evident」と表現する。外部anchorなしで「完全改竄不能」とは表現しない。

## 2. フェーズの進め方

着手フェーズは人間がプロンプトで指定する。指定されたフェーズの `DESIGN.md` §6 と `PROTOCOL_FREEZE.md` の記述・完了基準だけを見て作業する。

v2.1 の実装順は以下で固定:

`P0 -> Gate 0 -> P1 -> P2 -> P3 -> P4a -> P4 -> P5 -> P6 -> P7 -> P8 -> P9 -> P10 -> P11`

トレーサーバレット原則: まず薄くても端から端まで通す。このアプリの背骨は `pnpm simulate`（合成データ→台帳→verifyChain→レポート）。各フェーズはこの背骨に沿って肉付けする。

フェーズ完了の条件は「完了基準の全項目が、実行結果で満たされている」こと。ひとつでも赤なら未完了として正直に報告し、原因と次の一手を書く。赤を緑に見せかけない。

完了基準に書かれていない“ついで改善”はしない。気づいた改善点は PROGRESS.md の「申し送り」に書くだけに留める。

## 3. Sonnet向けの作業スタイル（重要）

小さな差分で進む。1ファイル書いたら型チェック、を細かく回す。大きな塊を一気に書いて最後にまとめて直す、をしない。

決め打ちの前に確認。`DESIGN.md` と `PROTOCOL_FREEZE.md` に明記された値・型・命名だけを使う。未定義のもの（例: bundle id の本番値、未指定の閾値）は仮であることを明示し、PROGRESS.md に「要確定」として残す。

既存の型定義は勝手に変えない。ただし `PROTOCOL_FREEZE.md` §10 が明示する RegistrationPayload v2.1 の追加フィールドは設計済み変更として扱う。それ以外のフィールド追加・改名が必要なら、実装前に理由を添えて人間に提案する。

ライブラリのAPIは推測せず、インストール済みバージョンの実物を確認してから使う（node_modules のREADME / 型定義 / 公式ドキュメント）。特にCapacitor系はバージョン差が大きい。

長い作業の前に「これから何をどの順でやるか」を3〜6行で宣言してから着手する。

## 4. コーディング規約

- TypeScript strict（strict: true）。any は原則禁止、使うなら理由をコメント。
- `src/stats/` は純関数のみ。副作用・DOM・Capacitor依存を持ち込まない。全関数にゴールデンテスト（既知入力→既知出力）を付ける。
- 乱数・時刻・IDなど非決定的なものは、テストできるよう注入可能にする（シードモード／Clock／時刻引数等）。
- ハッシュ計算・RFC 8785/JCS canonicalization は1か所に集約し、append と `verifyChain()` が同じ関数を使う。
- RNG provider と fallback orchestration を分離し、provider固有のendpoint/auth/quota/timeoutをdomainへ漏らさない。
- SQLiteはadapter。domain/application logicから直接Capacitor SQLite APIを呼ばない。
- コミットは小さく、メッセージは `Pn: 要点` または `Gate0: 要点` の形。

## 5. テスト & コストの鉄則

- テストで外部APIを絶対に叩かない。ANU/RANDOM.ORG へのfetchは必ずモック。乱数はシードモードで決定的に。（コスト暴走・quota消費・フレーキー化を防ぐ）
- `pnpm simulate` はネット無しで完結すること。
- テストは決定的であること（時刻・乱数を固定）。フレーキーなテストを残さない。
- CIに含める簡易ガード: ledger に削除/更新を行うコードが無いことの grep チェック。
- Layer Aのテストfixtureでは `anu/randomorg/local` が混在しても主要確証sampleが `anu` のみに絞られることを必ず検証する（P5以降）。

## 6. 自己検証プロトコル（毎フェーズの終わりに必須）

以下を実際に実行し、出力を貼って報告する。文章での「できました」は不可。

1. `pnpm typecheck` → 出力を貼る（緑であること）
2. `pnpm test` → 出力を貼る（緑であること）
3. `pnpm build` → 出力を貼る（緑であること）
4. そのフェーズ固有の完了基準に対応するコマンド（例: `pnpm simulate …`）→ 出力を貼る
5. UI確認が要るフェーズは `pnpm dev`（Web）で確認した旨とスクショ的説明。iOSは実機/シミュレータが使えない環境なら「ビルド手順を記述＋Web確認で代替」と正直に書く
6. 不変ルール（§1）に反するコードを追加していないか自己チェックし、宣言する
7. PROGRESS.md を更新する

Gate 0 は文書フェーズだが、既存P0のコードを壊していないことを確認するため 1〜3 を実行する。

## 7. PROGRESS.md のフォーマット

各フェーズ完了時に追記（上書きせず追記）。テンプレート:

```
## Pn: <フェーズ名> (YYYY-MM-DD)
### 完了したこと
- …
### 完了基準の結果（コマンド出力の要約）
- pnpm typecheck: green
- pnpm test: green (N passed)
- pnpm build: green
- <phase固有>: <結果>
### 要確定・申し送り（次フェーズへ）
- …（仮置きした値、未解決の判断、気づいた改善点）
### 触ったファイル
- …
```

## 8. 既知の技術的注意点（ハマりどころ）

- SQLite（@capacitor-community/sqlite）は Web と iOS で初期化が違う。Webは jeep-sqlite Web Component ＋ wasm（initWebStore）が要る。iOSはネイティブ。`pnpm dev` での動作確認を成立させるため、Web用の初期化も配線する。正確なAPIはインストール済みバージョンのREADMEで確認すること。
- `jeep-sqlite@2.8.0` と `sql.js@1.11.0` の互換性注意は PROGRESS.md のP0申し送りを維持する。
- ANU legacy QRNG API は縮小・新しいANU Quantum Numbers系へ移行中。P1ではendpointや古い70秒制限をdomainにハードコードせずprovider adapterとして扱う。テストは実APIを呼ばない。
- qrng-proxy Worker は未デプロイでも全機能が動くこと（localフォールバック）。週次公証も未デプロイ時は自動スキップ。ただし Layer A quantum-primary sample は fallback を主要解析へ混ぜない。
- Capacitorのネイティブビルドは環境依存。動かせない環境では手順を文書化し、Webで代替検証する。

## 9. Definition of Done（PRを閉じてよい条件）

- 指定フェーズのスコープ内だけを変更した（先回りなし）
- `DESIGN.md` / `PROTOCOL_FREEZE.md` の当該フェーズ完了基準が全て、実行出力で満たされている
- 不変ルール（§1）に違反していない
- テストが決定的で、外部APIを叩いていない
- PROGRESS.md を更新した
- 赤・未解決・仮置きを隠さず報告した

この6つが揃って初めて「完了」。ひとつでも欠けたら未完了として報告する。
