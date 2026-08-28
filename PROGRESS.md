# PROGRESS.md

## P0: 足場 (2026-07-10)

### 完了したこと
- Vite + React 18 + TypeScript を pnpm で初期化（`pnpm create vite@latest . --template react-ts` 相当の構成を移植し、`tsconfig.app.json` に `strict: true` を明示追加）
- Capacitor 6.2.1（`@capacitor/core` `@capacitor/cli` `@capacitor/ios`）を導入し、`npx cap init` → `npx cap add ios` で iOS ネイティブプロジェクト（`ios/`）を生成
- `@capacitor-community/sqlite@6.0.2` を導入し、`src/db/schema.ts`（DESIGN.md §5 の `CREATE TABLE ledger` をそのまま定数化）と `src/db/sqlite.ts`（DB open → `ledger` テーブル作成まで。書き込み関数は未実装）を実装
  - Web は `jeep-sqlite`（Stencil Web Component）+ `sql.js` wasm で初期化（`initWebStore()`）
  - iOS はネイティブ実装（`SQLiteConnection.createConnection` → `open` → `execute`）を同じコードパスで実行（`Capacitor.getPlatform()` で分岐するのは Web 側の `jeep-sqlite` セットアップのみ）
- プレースホルダ画面（`src/App.tsx`）:「実験は準備中」＋「DB接続: OK/NG」表示（`initDatabase()` の結果をそのまま表示、エラー時は理由も表示）
- スクリプト整備: `pnpm typecheck`（`tsc -b --noEmit`）／`pnpm test`（vitest）／`pnpm simulate`（空実装で `"not implemented yet (P0)"` を出力）
- `.gitignore` 整備（`node_modules` `dist` に加え `ios/App/Pods` `ios/App/build` `ios/App/App/public` `xcuserdata` `android/` 等を追加）
- README.md を DESIGN.md 付属の紹介文で更新

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green（出力なし＝エラーなし）
- `pnpm test`: green（2 passed, 1 test file — `src/db/schema.test.ts` で ledger スキーマの列定義と8種の type を検証）
- `pnpm simulate`: green（`"not implemented yet (P0)"` を出力して正常終了、exit 0、ネットワーク不使用）
- `pnpm build`: green（`tsc -b && vite build` 成功、`dist/` 生成）
- `pnpm dev`（Web）: Chromium (Playwright, 検証後にアンインストール済み) で `http://localhost:5173/` を開き確認
  - 表示: `Intention Dice` / `実験は準備中` / `DB接続: OK`
  - コンソールに `getVersion new res: [{"columns":["user_version"],"values":[[0]]}]` が2回出力（jeep-sqlite 経由での DB open 成功のログ）、エラーなし
- iOS シミュレータでの確認: **未実施**（本セッションの実行環境は Linux で Xcode / CocoaPods が存在せず、`xcodebuild` も `pod` も利用不可）。`npx cap add ios` と `npx cap sync ios` は成功し、`ios/App/App.xcodeproj` が生成されていることは確認済み。Web 確認で代替した（下記「iOSビルド手順（Mac環境向け）」参照）

### 不変ルール（AGENTS.md §1 / DESIGN.md §8）自己チェック
1. ledger は追記のみ／削除・編集経路なし — ✅ `src/db/sqlite.ts` に `execute(CREATE_LEDGER_TABLE_SQL)` のみ。INSERT/UPDATE/DELETE 文は一切存在しない（grep確認済み、下記コマンド参照）
2. 事前登録後のパラメータ変更不可 — P0では登録UI自体が未実装のため該当コードなし。DESIGN.md 未変更（読み取りのみ）
3. bits→hits→z にUI都合の加工を入れない／`src/stats/` 純関数化 — P0では未着手（該当コードなし）
4. 表示数値の隣に偶然期待値を併記 — P0のプレースホルダ画面には実験数値の表示自体がないため該当なし
5. 欠測は欠測のまま — 該当ロジック未実装
6. rngSource記録／localフォールバックを隠さない — 乱数モジュール未実装（P1でやる）
7. 予言は乱数取得より前に確定 — セッションフロー未実装（P4でやる）
8. 確証/探索の分離 — 統計未実装（P5, P10でやる）
9. 願いの登録後不変・自動割付 — Layer C未実装（P7でやる）
10. 封印群を実践UIに出さない — Layer C未実装（P7でやる）
11. 1PR=1フェーズ=1目的、PROGRESS.md更新を伴う — ✅ 本コミットがそれ

grep確認コマンドと結果:
```
$ grep -rniE "DELETE FROM ledger|UPDATE ledger|ledger.*\.delete\(|ledger.*\.remove\(" src/ scripts/
OK: no matches
$ grep -rniE "INSERT INTO ledger|appendLedger|ledger.*\.execute\(" src/ scripts/
OK: no matches
```
（`execute(CREATE_LEDGER_TABLE_SQL)` のみで、`ledger.execute(` のような書き込みパターンには一致しない）

### 要確定・申し送り（次フェーズへ）
- **要確定**: `capacitor.config.ts` の `appId` は仮値 `com.example.intentiondice`。本番申請前（P11想定）に正式なbundle idへ変更が必要
- **既知の技術的ハマりどころ（記録として重要）**: `jeep-sqlite@2.8.0` は内部に `sql.js@1.11.0` 向けのグルーコードを同梱しており、`public/assets/sql-wasm.wasm` に最新の `sql.js`（1.14.1等）の wasm を置くと `WebAssembly.instantiate(): LinkError` で Web版のDB初期化が失敗する。`package.json` で `sql.js` を `1.11.0` に固定し、そのバージョンの `sql-wasm.wasm` を `public/assets/` にコピーして解決した。**今後 `jeep-sqlite` や `sql.js` をバージョンアップする際は、この組み合わせを必ず再検証すること**（`src/db/sqlite.ts` にもコメントを残した）
- iOS実機/シミュレータでの動作確認は次フェーズ以降、macOS環境（Xcode + CocoaPods利用可能な環境）で実施する必要がある。下記「iOSビルド手順」を参照
- `pnpm build` 時に `Module "crypto" has been externalized for browser compatibility` という警告が出る（jeep-sqlite が内部で Node の `crypto` を参照している箇所をViteがブラウザ向けに外部化している旨の警告）。動作上は問題なし（DB接続OKを確認済み）だが、P6以降で気になる場合は要調査
- `.oxlintrc.json`（Vite scaffold付属のlinter設定）はそのまま残した。AGENTS.mdはESLintを指定していないため今回は変更していない。必要なら次フェーズでlintルールを見直す
- README.md の「人間側セットアップ」節はDESIGN.md/AGENTS.md/README.mdの配置手順を含むが、これらは本コミットで既にリポジトリに追加済み

### iOSビルド手順（Mac環境向け・本セッションでは実行不可のため手順のみ記載）
```sh
pnpm install
pnpm build
npx cap sync ios
npx cap open ios   # Xcodeが開く
# Xcode上で Signing & Capabilities にチームを設定後、シミュレータ or 実機でRun
```
CocoaPods未インストールの場合は `sudo gem install cocoapods` の上で `npx cap sync ios` を再実行すること。

### 触ったファイル
- 新規: `AGENTS.md`, `DESIGN.md`, `README.md`（更新）, `PROGRESS.md`, `.gitignore`, `.oxlintrc.json`
- 新規: `package.json`, `pnpm-lock.yaml`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `vite.config.ts`, `vitest.config.ts`, `capacitor.config.ts`, `index.html`
- 新規: `src/main.tsx`, `src/App.tsx`, `src/App.css`, `src/index.css`
- 新規: `src/db/schema.ts`, `src/db/sqlite.ts`, `src/db/schema.test.ts`
- 新規: `scripts/simulate.ts`
- 新規: `public/favicon.svg`, `public/icons.svg`, `public/assets/sql-wasm.wasm`
- 新規: `ios/`（`npx cap add ios` で生成されたネイティブプロジェクト一式。`Pods` と `App/public` は `.gitignore` で除外）

---

## Gate 0: Protocol Freeze v2.1 (2026-08-28)

### 完了したこと
- `PROTOCOL_FREEZE.md` を新規作成し、P1着手前に曖昧だった科学仕様・暗号仕様・時間仕様をv2.1として固定した。
- Layer A主要確証解析は `rngSource='anu'` の量子セッションのみとし、`randomorg` / `local` fallbackはledgerから消さずQC・感度分析・探索に残す方針を固定した。
- Layer Cのrandomizationは `anu -> randomorg -> local` fallbackでもRCTとして有効とし、実際のsourceを必ず記録・表示する方針を固定した。
- targetDirを測定対象bitstreamから独立させ、登録時の独立seed + SHA-256 counter streamで決定的に生成する方式を固定した。
- condition schedule / target scheduleの決定性とクロスプラットフォーム再現性のため、`Math.random()` を禁止し、SHA-256 counter expansion + rejection sampling / Fisher-Yatesを採用する設計を固定した。
- 実験timezoneをIANA timezoneとして登録時に固定し、03:00等のdayBoundaryHourを端末timezone変更や旅行で変化させないルールを固定した。
- daily controlを「1 experiment-dayにつき最大1件」のidempotent処理として固定した。
- Layer Bのpre/postを抽選結果revealの前に完了させ、ritualそのものの短期変化と抽選結果への感情反応を混ぜない順序へ修正した。
- prediction commitをUIではなくapplication/domain層でRNG取得前に強制するルールを固定した。
- ledger hashを RFC 8785 / JCS canonicalization + SHA-256 で定義し、`prevHash/type/payload/createdAt` をcanonical objectとしてhashする方式を固定した。
- genesisの`prev_hash`を64桁の0、最初のregistration entry hashを`genesisHash`と定義した。
- ledger appendをsingle-writer serialization queue経由に限定し、直接INSERTを禁止する設計を固定した。
- ローカルhash chainは「tamper-evident（改竄検知可能）」であり、外部anchorなしに「完全改竄不能」とは表現しないことを固定した。
- RegistrationPayload v2.1に protocol/canonicalization/schedule/target/rng/stats/app version と frozen timezone / target seed を含めることを固定した。
- Layer Cの`withdrawn` / `undecidable`を主要解析ではnot realizedとして扱い、`undecidable`除外版は副次感度分析に限定するルールを固定した。
- wish→assignment間クラッシュの回復とassignment idempotency、sealed wishのdomain-level visibility boundaryを固定した。
- P4が必要とするbits→hits→zの依存逆転を解消するため、P3とP4の間に小さな純関数フェーズ `P4a Stats Core` を追加した。
- Reactの実装基準を、実際に導入済みのReact 19系に合わせることを明文化した（Capacitorはv6据え置き）。
- `AGENTS.md` をv2.1へ更新し、DESIGN.mdとProtocol Freezeが衝突した場合の優先順位、不変ルール、フェーズ順を実装担当に強制した。
- `README.md` をv2.1の現在地・実装順へ更新した。
- `.github/workflows/ci.yml` を追加し、`pnpm install --frozen-lockfile` → typecheck → test → build → ledger mutation grep guard をPR品質ゲートとして自動化した。

### 完了基準の結果（実行結果）
- ローカルcloneによる検証: 作業環境の外部DNS制限により `Could not resolve host: github.com` でclone前に停止。これはリポジトリコードの失敗ではないため、同一PR merge refをGitHub Actionsで検証した。
- GitHub Actions CI run `33131030852` / job `98720340523`: **green / success**。
- `pnpm install --frozen-lockfile`: green。lockfileはup-to-date、174 packages導入。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、1 test file / 2 tests passed）。
- `pnpm build`: green（`tsc -b && vite build`、Vite 8.1.4、production build成功）。既知の `jeep-sqlite` → Node `crypto` browser externalization warningはP0時と同様に残るがbuild failureではない。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- Runtime source (`src/`, `scripts/`, package/lockfile) の変更: **なし**。Gate 0はprotocol/docs/CIのみ。

### 外部サービス確認
- ANU公式legacy QRNG documentationは、旧サービスを縮小しANU Quantum Numbers/AWS側へ移行する旨を案内している。P1では旧endpoint/70秒前提をdomainへハードコードしない。
- RANDOM.ORG HTTP interfaceはIP単位のbit quotaを持つため、P1ではquota exhaustionを通常のfallback条件として扱い、テストで実サービスを叩かない。

### 要確定・申し送り（P1へ）
- P1は **RNG moduleのみ**。ledger appendやUIには着手しない。
- ANU providerはlegacy endpointに固定せず、endpoint/auth/timeout/retry/quotaをadapter/configとして注入可能にする。
- `RandomResult` は少なくとも `bitsHex`, `nBits`, `source` を返し、sourceを絶対に偽装しない。
- production fallback順は `anu -> randomorg -> local`。unit testはfetch mockのみで3 provider分岐、invalid payload、timeout、network failure、fallbackを決定的に検証する。
- test/simulateは外部APIを一切呼ばない。
- ANU無料枠などの運用上のquotaは変化し得るため、科学プロトコルではなくprovider operational configとして扱う。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `PROTOCOL_FREEZE.md`
- 新規: `.github/workflows/ci.yml`
- 更新: `AGENTS.md`
- 更新: `README.md`
- 更新: `PROGRESS.md`
- **未変更**: `DESIGN.md`（v2.0本体は履歴として保持し、v2.1 normative addendumが上書きする方式）
- **未変更**: runtime implementation (`src/`, `scripts/`, `package.json`, `pnpm-lock.yaml`)
