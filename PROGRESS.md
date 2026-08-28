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

---

## P1: RNG module (2026-08-28)

### 完了したこと
- `src/rng/` を新設し、RNG provider契約と `RngService` を実装した。
- production fallback順を `ANU -> RANDOM.ORG -> local WebCrypto` として `createProductionRngService()` に固定し、成功時は実際に使われた `source` を必ず返すようにした。
- Layer A向け `getBits(nBits)` を実装し、byte-alignedなbit数をhexへ変換して `bitsHex / nBits / source` を返すようにした。
- Layer C向け `getAssignmentBit()` を実装し、均一な1byteの偶奇から0/1を作ることで128値ずつに等分される unbiased assignment を実現した。
- ANU providerを実装。endpoint / headers / timeout / minInterval / fetch / clock / sleepを注入可能にし、旧legacy endpointや70秒制限をdomainへハードコードしない構造にした。
- RANDOM.ORG providerを公式HTTP integer interfaceのplain出力に合わせて実装し、HTTP失敗・明示Errorレスポンス・不正payloadをfallback条件として扱うようにした。
- local providerを `crypto.getRandomValues` ベースで実装。65,536byte上限をchunkingで処理し、fallback時は常に `source='local'` を返す。
- provider呼び出しを直列化する設定可能な `RateGate` を実装。70秒を設定した場合の挙動もテストしつつ、サービス仕様として固定していない。
- `fetchWithTimeout()` をAbortControllerで実装し、fake timerによる決定的timeoutテストを追加した。
- test/simulate用にdeterministic seeded providerを実装。同一seed+同一取得順で同じbyte列を返す。production barrelからは意図的にexportせず、seeded値がANU/RANDOM.ORGとして誤表示される経路を作っていない。
- 全外部providerテストはmock fetchのみで、ANU / RANDOM.ORGの実サービスは一度も呼んでいない。
- CIで発見されたTypeScript 6 `erasableSyntaxOnly` / WebCrypto `ArrayBuffer`厳密型の問題をP1内で修正した。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33131746143` / job `98722607014`: **green / success**（P1コード＋timeoutテストまで含むPR merge ref）。
- `pnpm install --frozen-lockfile`: green。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**8 test files / 20 tests passed**、unhandled errorなし）。
- provider分岐テスト: ANU成功 / ANU失敗→RANDOM.ORG / ANU+RANDOM.ORG失敗→local / 全provider失敗を検証。
- provider境界テスト: ANU malformed/HTTP failure、RANDOM.ORG Error/wrong-length、local injected fill、HTTP timeout、rate guardを検証。
- seeded test mode: same seed + same request sequenceの決定性を検証。
- `pnpm build`: green（Vite 8.1.4 production build成功）。P0から既知の `jeep-sqlite` crypto externalization warningのみ継続、failureではない。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- 外部APIアクセス: **0件**（テストはfetch mockのみ）。

### 要確定・申し送り（P2へ）
- ANU production endpoint/authは、現在移行中のANUサービス仕様または将来のqrng-proxyに合わせてapplication wiring時に設定する。P1ではlegacy endpointを埋め込んでいない。
- ANU endpoint adapterのnormalization contractは `length=<bytes>&type=uint8` + `{ data: number[] }`。新サービスの形式が異なる場合はprovider/proxy adapterだけで正規化し、`RngService`のdomain契約は変えない。
- RANDOM.ORG direct HTTP利用はWeb/iOSのCORS・ネットワーク環境をintegration時に実機確認する。必要ならproxy経由へ差し替えるが、sourceは引き続き `randomorg` として記録する。
- Layer A主要確証解析への採否はP5側で `source === 'anu'` を事前固定ルールとしてfilterする。RNG側ではfallbackを隠したりANUへ偽装したりしない。
- P2 ledgerはRNG provider内部へ結合しない。RNGは値＋sourceを返すだけ、台帳化は上位application/serviceが担当する。
- seeded providerはtest-onlyのまま維持し、production public APIへ出さない。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/rng/types.ts`
- 新規: `src/rng/service.ts`
- 新規: `src/rng/factory.ts`
- 新規: `src/rng/index.ts`
- 新規: `src/rng/rateGate.ts`
- 新規: `src/rng/providers/http.ts`
- 新規: `src/rng/providers/anu.ts`
- 新規: `src/rng/providers/randomOrg.ts`
- 新規: `src/rng/providers/local.ts`
- 新規: `src/rng/testing/seeded.ts`
- 新規: `src/rng/service.test.ts`
- 新規: `src/rng/rateGate.test.ts`
- 新規: `src/rng/providers/http.test.ts`
- 新規: `src/rng/providers/anu.test.ts`
- 新規: `src/rng/providers/randomOrg.test.ts`
- 新規: `src/rng/providers/local.test.ts`
- 新規: `src/rng/testing/seeded.test.ts`
- 更新: `PROGRESS.md`

---

## P2: Ledger (2026-08-28)

### 完了したこと
- `src/ledger/` を新設し、`LedgerStore` port / `LedgerService` / `MemoryLedgerStore` / `verifyChain()` / JSON・CSV export/importを実装した。
- RFC 8785 / JCS canonicalizationを1か所に集約。`payload_json` はwriterが生成したcanonical JSONをそのまま保存し、verify側も同じcanonicalizerを共有する。
- hash対象をProtocol Freeze v2.1どおり `createdAt / payload / prevHash / type` のlogical objectとし、WebCrypto SHA-256 → lowercase hexで`entry_hash`を生成する。
- genesis `prev_hash`は64桁0、先頭は`registration`のみ。2件目の`registration`は禁止した。
- `LedgerService` にsingle-writer Promise queueを実装し、並列appendが同じheadを読むforkを防止。失敗したappend後もqueueは継続する。
- `verifyChain()` はseq連続性、genesis、prevHashリンク、payload parse/JCS一致、entry hash再計算を検証する。
- Protocol Freezeが要求する改竄fixture（payload/type/createdAt/prevHash/途中削除/未連結挿入/並べ替え/genesis異常/noncanonical payload）を個別テスト化した。
- JSON/CSV export/importは`StoredLedgerEntry`をlossless roundtripし、payload内のraw bitsを削らない。
- SQLite adapter `SqliteLedgerStore` はSELECT/INSERTのみ。`LedgerStore` interfaceにはupdate/delete経路を存在させていない。
- `src/db/sqlite.ts` は初期化済みDB接続を返せるよう整理し、通常のledger writeは`SqliteLedgerStore -> LedgerService`へ限定する設計を明記した。
- `SQLiteDBConnection` の `query` / `run` / `changes.lastId` 契約は公式repositoryのAPI docsでも確認した。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33132484517` / job `98724947292`: **green / success**（P2実装・tamper fixtures・export roundtripを含むPR merge ref）。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**14 test files / 42 tests passed**）。
- P2 tamper fixtures: 要求された9ケースすべてで破損検出を確認。
- 全8 ledger entry typeのappend: green。
- concurrent append single-writer test: green。
- JSON export/import roundtrip: green。CSV export/import roundtrip: green。raw bitstring保持を確認。
- `pnpm build`: green（Vite 8.1.4 production build成功。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`

### セキュリティ・意味上の注意
- このchainは **tamper-evident（改竄検知可能）**。ローカル履歴全体を攻撃者が完全に再生成するケースまでは、外部anchorなしでは排除できない。
- `verifyChain()` は完全なexperiment export（seq=1のgenesisから）を前提とする。途中区間だけを独立検証するAPIはP2では作っていない。
- payloadはJSON data modelに限定。`undefined` / `NaN` / `Infinity` / cycle / sparse array / unpaired surrogate等はwriterで拒否する。

### 申し送り（P3へ）
- P3 registrationは `LedgerService.append('registration', payload, createdAt)` でgenesisを書き、返された`entryHash`を`genesisHash`として扱う。
- `RegistrationPayload` v2.1の追加provenance（`protocolVersion` / `canonicalizationVersion` / `scheduleAlgorithmVersion` / `targetAlgorithmVersion` / `targetSeed` / `timeZone` / `rngPolicyVersion` / `analysisPlanVersion` / `statsVersion` / `appVersion` / `buildId?`）を必ずpayloadへ含める。
- schedule/target生成はP3の責務。P2 canonicalizer/hashを再実装せず、必ずledger public APIを使う。
- P3の日付・時刻はfrozen IANA timezone設計に従うが、P2は`createdAt`のexact stringを受け取ってhashするだけで時計仕様を発明しない。
- `pnpm simulate` tracer bulletのledger接続は後続phaseで`MemoryLedgerStore`を利用できる。P2ではsimulateのP0 stubを先行変更していない。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/ledger/types.ts`
- 新規: `src/ledger/canonicalize.ts`
- 新規: `src/ledger/hash.ts`
- 新規: `src/ledger/memoryStore.ts`
- 新規: `src/ledger/service.ts`
- 新規: `src/ledger/verify.ts`
- 新規: `src/ledger/export.ts`
- 新規: `src/ledger/index.ts`
- 新規: `src/ledger/canonicalize.test.ts`
- 新規: `src/ledger/hash.test.ts`
- 新規: `src/ledger/service.test.ts`
- 新規: `src/ledger/verify.test.ts`
- 新規: `src/ledger/export.test.ts`
- 新規: `src/db/sqliteLedgerStore.ts`
- 新規: `src/db/sqliteLedgerStore.test.ts`
- 更新: `src/db/sqlite.ts`
- 更新: `PROGRESS.md`

---

## P3: Preregistration (2026-08-28)

### 完了したこと
- `src/registration/` を新設し、Protocol Freeze v2.1の`RegistrationPayload`、固定version識別子、入力validation、登録serviceを実装した。
- 365日condition scheduleを`condition-schedule-v1` domain separator + SHA-256 counter stream + rejection sampling付きFisher-Yatesで生成。365日では各条件73回ずつになる。
- target scheduleは別の`target-schedule-v1` domain separator + 独立`targetSeed`から生成し、Layer Aで測定するRNG bitstreamとは分離した。`sha256-counter-target-v1`ではSHA-256 blockをMSB-firstで消費する仕様を実装・golden vectorで固定した。
- registration時の`scheduleSeed` / `targetSeed`はWebCrypto `getRandomValues`由来の32-byte seedとして生成し、生成関数はテスト可能なfill注入式にした。
- `RegistrationService.register()`が365日scheduleを生成し、固定判定ルールA/C、Layer C設定、5条件のprediction、timezone、全provenanceをまとめて`LedgerService.append('registration', ...)`へ渡す。返されたentry hashをgenesis hashとして扱う。
- `getApplicationLedgerService()`を追加し、アプリ実行中のSQLite ledger writerを1つの`LedgerService`へ集約した。
- `projectCurrentSchedule()`を実装。normal UIへ返すのは指定されたcurrent experiment dateのconditionと当日session分targetのみで、full future condition/target scheduleをprojectionに含めない。
- React placeholderをP3 preregistration wizardへ置換。experiment ID、開始日、固定IANA timezone、bits/draw、sessions/day、day boundary、affirmation、5条件prediction、Layer C設定を入力し、判定ルールを確認してgenesisへロックできる。
- 起動時は既存ledgerを`verifyChain()`してから、未登録ならwizard、登録済みなら編集不能のlocked summary + genesis hashを表示する。
- 入力validationエラーはwizard内に表示し、台帳/DBの起動エラーと分離した。
- `predictionByCondition` / `decisionRuleA` / `layerC`のいずれかを変更するとgenesis hashが変わることを明示テストした。
- schedule/targetのクロスプラットフォーム再現性を守るため、既知seedのcondition先頭15件・target先頭24bitをgolden vectorとして固定した。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33133186524` / job `98727111383`: **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**17 test files / 50 tests passed**）。
- 365日condition balance: `[73, 73, 73, 73, 73]` を検証。
- same seed -> same condition schedule / same target schedule: green。
- cross-platform golden vectors: condition 15件 / target 24bit一致。
- current-day-only projection: full schedule / future target scheduleを返さないことを検証。
- genesis provenance: protocol/canonicalization/schedule/target/rng/analysis/stats/app version、timezone、targetSeed、scheduleをpayloadに含むことを検証。
- genesis hash inclusion: prediction / decisionRuleA / Layer C変更でhashが変わることを検証。
- second registration rejection: green。
- `verifyChain()` after registration genesis: green。
- `pnpm build`: green（Vite 8.1.4 production build成功。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- ローカルWeb実行確認: **未実施**。作業コンテナから`github.com`をDNS解決できず、`git clone`が`Could not resolve host: github.com`でclone前に停止したため。コード失敗ではない。PR merge refはGitHub Actionsで上記すべてgreen。

### 固定した実装識別子
- `analysisPlanVersion = 'analysis-plan-v2.1'`
- `statsVersion = 'stats-plan-v1'`
- `appVersion = '0.0.0'`（現在のpackage versionと一致）
- これらはP3でgenesisに入るprovenance識別子。既存experimentでは後から変更しない。

### 要確定・申し送り（P4aへ）
- 次フェーズはProtocol Freezeで追加された **P4a Stats Core** のみ。P4 session UIには先回りしない。
- P4aは`bitsHex -> bit validation/decoding -> hits -> z`をUI非依存の純関数として実装し、P4の`SessionPayload.hits/z`依存を解消する。
- target algorithm `sha256-counter-target-v1` のbit順はP3実装で **MSB-first** としてgolden vector固定済み。既存experimentの途中で変更しない。
- P9まではtimezone + dayBoundaryHourから「現在のexperimentDate」を求めるclock実装を先行しない。P3 projectionは既に算出済みのexperiment date文字列を受け取る。
- UIの実ブラウザ/iOS動作確認は、GitHubへアクセスできるWeb/macOS実行環境で後続統合時に再確認する。CI production buildはgreen。
- `analysis-plan-v2.1` / `stats-plan-v1` はv1実験の固定provenance識別子として扱う。統計コード追加時に同じ意味のまま維持し、意味を変える必要が生じた場合は新experiment/versionとして扱う。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/registration/types.ts`
- 新規: `src/registration/counterStream.ts`
- 新規: `src/registration/schedule.ts`
- 新規: `src/registration/seed.ts`
- 新規: `src/registration/validation.ts`
- 新規: `src/registration/service.ts`
- 新規: `src/registration/projection.ts`
- 新規: `src/registration/index.ts`
- 新規: `src/registration/schedule.test.ts`
- 新規: `src/registration/service.test.ts`
- 新規: `src/registration/projection.test.ts`
- 新規: `src/ledger/appService.ts`
- 更新: `src/App.tsx`
- 更新: `src/App.css`
- 更新: `PROGRESS.md`
