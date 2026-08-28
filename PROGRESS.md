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
- genesisの`prev_hash`を64桁0、最初のregistration entry hashを`genesisHash`と定義した。
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

---

## P4a: Stats Core (2026-08-28)

### 完了したこと
- `src/stats/` を新設し、P4が必要とする `bitsHex -> validation/decoding -> hits -> z` をUI非依存の純関数として実装した。
- `decodeBits()` はhex長・hex文字・`nBits`整合性を厳密検証し、無効入力を補正せず例外にする。
- `countHits()` はtarget=1(HIGH)なら1の数、target=0(LOW)なら0の数を同じraw bitstreamから対称に算出する。
- `zScore()` は凍結した0.5 nullに対して `z = (hits - nBits/2) / sqrt(nBits/4)` を唯一の計算経路として実装した。
- `cumulativeDeviation()` は `hits - nBits/2` を返す最小primitiveとして実装した。
- `summarizeBitstream()` をP4向けの単一入口として用意し、`nBits / hits / z / cumulativeDeviation`を一貫した経路で生成する。
- bit sequenceの再現性のためhex→bit展開はbyte内MSB-firstで固定。hit数/zはbit順に依存しない。
- P5責務のCI・p値・Holm・Bayes factor・calibration・trend等は先行実装していない。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33133595869` / job `98728517746`: **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**18 test files / 68 tests passed**）。
- Stats Core専用: `src/stats/core.test.ts` **18 tests passed**。
- golden z examples: `(hits,nBits)=(512,1024)->0`, `(544,1024)->+2`, `(480,1024)->-2`。
- target symmetry: `f1` / 8bit で HIGH hits=5, LOW hits=3。
- malformed bitstream / invalid nBits / invalid hits / invalid target rejection: green。
- `pnpm build`: green（Vite 8.1.4 production build成功。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- `src/stats/` はDOM / React / Capacitor / ledger / network依存なしの純関数のみ。

### 要確定・申し送り（P4へ）
- P4 session recordingは `summarizeBitstream(bitsHex, nBits, targetDir)` を使い、hits/zを別実装しない。
- daily controlはtargetを持たないため、1の数を0.5と比較する。P4でcontrol用の呼び出しは`countHits(..., 1)`または同じcore primitiveに統一し、別の統計式を作らない。
- P4のapplication/domain層で `prediction` がledgerへcommit済みであることを確認してからRNG取得を許可し、`predictionSeq < session.seq`を成立させる。
- P5でCI/p/Holm/BF等を追加しても、P4aで固定したhits/z定義は`stats-plan-v1`の意味として黙って変更しない。
- `pnpm simulate` は引き続きP0 stub。P4の擬似1日E2EでMemoryLedgerStore + seeded RNG + Stats Coreをつなぐ候補とする。

### 触ったファイル
- 新規: `src/stats/core.ts`
- 新規: `src/stats/core.test.ts`
- 新規: `src/stats/index.ts`
- 更新: `PROGRESS.md`

---

## P4: Session Flow (Layer A/B) (2026-08-28)

### 完了したこと
- `src/session/` を新設し、daily control、当日session plan、prediction-before-RNG、session appendをapplication serviceとして実装した。
- registration genesisをledgerのsource of truthとして読み、P3のcurrent-day projectionからcondition/targetを毎回再導出する。`seqInDay`は1..`sessionsPerDay`として保存・照合する。
- `prepareSession()`は実験期間内・未実施slotであることを内部検証し、daily controlをcommitしてから当日planを返す。control生成はPromise queueで直列化し、同一serviceへの並列prepareでも1日最大1件に保つ。
- `ensureDailyControl()`単体でも実験期間外の日付をRNG取得前に拒否する。controlのbit統計はP4a `summarizeBitstream(bitsHex, nBits, 1)`だけを使い、1の数を0.5と比較する。
- `runSession()`はmoodPre / ritual / moodPost / confidence / contextを結果取得前に確定し、predictionをappendした後、ledgerを再読込して同じdate / seqInDay / condition / targetDirのcommit済みentryであることを照合してからだけmeasured RNGを呼ぶ。
- session payloadに実際の`rngSource`、raw `bitsHex`、P4a由来hits/z、`predictionSeq`、ritual、mood pre/post、context、started/completed timestampを保存し、`predictionSeq < session.seq`をassertする。
- 同一experiment date + seqInDayの二重sessionはmeasured RNG取得前に拒否する。
- ritual complianceをP1=60秒待機、P2=30文字以上、P3=300秒、P4=180秒、P5=30文字以上+480秒として実装し、無効な値を黙って補正しない。
- `SessionFlow` React componentを追加し、`moodPre -> ritual -> moodPost -> prediction -> 3秒長押し -> result`の順序をUIでも一致させた。結果はhits/zと同時に「偶然なら50% / 期待nBits/2 hits」、実際のRNG sourceを表示し、途中の頻度論p値は表示しない。
- P9責務のtimezone + `dayBoundaryHour`からcurrent experiment dateを求めるclockは先行実装せず、P4 service/componentは解決済み`experimentDate`を注入で受け取る。
- `scripts/simulate.ts`をP0 stubから1日分のoffline tracer bulletへ更新し、MemoryLedgerStore + deterministic seeded RNG + P3 registration + P4 flow + `verifyChain()`をネットワークなしで接続した。
- CIへ`pnpm simulate`を正式追加した。初回CIでtest-only providerのクラス名取り違えを検出し、`SeededTestRngProvider`へ修正。production barrelへtest providerを露出する回避は行っていない。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33134552935` / job `98731498431`: **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**20 test files / 79 tests passed**）。
- P4 session service: **6 tests passed** — control→prediction→session、prediction commit前のmeasured RNG禁止、prediction append失敗時RNG不使用、daily control並列idempotency、期間外control拒否、同日同seq二重session拒否。
- P4 ritual: **5 tests passed** — P1〜P5のfrozen compliance条件と不正秒数拒否。
- `pnpm simulate`: green / network access 0。entry typeは `registration -> control -> prediction -> session`、`predictionSeq=3 < sessionSeq=4`、`rngSource='local'`、`hits=511 / nBits=1024 / z=-0.0625`、`verifyChain()`成功。
- `pnpm build`: green（Vite 8.1.4 production build成功。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- P4で外部ANU/RANDOM.ORGを呼ぶテスト/シミュレーションは0件。seeded test providerはtest/simulate内部のみ。

### UI検証上の注意
- P4の`SessionFlow` component自体はtypecheck/production buildを通過しているが、`App.tsx`への実日付runtime wiringは行っていない。理由はProtocol Freeze/P3申し送りどおり、frozen timezone + `dayBoundaryHour`からcurrent experiment dateを算出する責務がP9に固定されているため。
- したがってP4ではprotocol順序をcomponent + service + pseudo-E2Eで固定し、端末時計からの実日付接続とWeb/iOSでの実画面動作確認はP9 integration時に行う。P4内で暫定的な端末timezoneロジックを発明していない。

### 要確定・申し送り（P5へ）
- 次は **P5 統計エンジンAのみ**。P4 session UIやP7願い、P9時計へ先回りしない。
- P5はP4aで固定した`bits -> hits -> z`定義を変更せず、CI / 二項p / Holm / one-sample BF / cumulative deviation series / control QC / prediction calibration / dose-response / quarterly trendを純関数として追加する。
- Layer A主要確証sampleはProtocol Freezeどおり`rngSource === 'anu'`のvalid sessionだけに絞る。`randomorg`/`local`はledgerから消さずsource counts/QC/sensitivity/exploratoryへ残す。
- interim APIにはconfirmatory frequentist p-valueを出さず、最終confirmatory APIと構造的に分離する。
- production ANU endpoint/auth/application wiringは依然として運用設定が必要。P4はRNG service注入に留め、テスト/CIでは外部APIを使っていない。
- prediction append後〜RNG/session append前のプロセスクラッシュではpredictionだけが残り得る。Protocol FreezeはP4のorphan prediction recovery規則を定義していないため、P4では勝手な再利用/削除を実装していない。ledger上は監査可能なまま保持される。
- `SessionFlow`のApp runtime mountingはP9のexperimentDate resolverと統合する際、P4のservice順序を変えずに行う。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/session/types.ts`
- 新規: `src/session/ritual.ts`
- 新規: `src/session/service.ts`
- 新規: `src/session/ritual.test.ts`
- 新規: `src/session/service.test.ts`
- 新規: `src/session/SessionFlow.tsx`
- 新規: `src/session/SessionFlow.css`
- 新規: `src/session/index.ts`
- 更新: `scripts/simulate.ts`
- 更新: `.github/workflows/ci.yml`
- 更新: `PROGRESS.md`

---

## P5: Stats Engine A (2026-08-28)

### 完了したこと
- P4aで固定した `bitsHex -> hits -> z` の定義は変更せず、その上にLayer Aの統計エンジンを純関数として追加した。
- `wilsonInterval95()` を95% Wilson score intervalとして実装。DESIGN.mdがCI方式を指定していないため、`stats-plan-v1`の実装詳細として関数名・method名・golden testで固定し、既存experiment中に黙って変更しない。
- `twoSidedBinomialNormalApproxP()` はP4aのzを使った両側正規近似、continuity correctionなしとして固定した。
- `oneSampleBayesFactor10()` はBeta(1,1)対立仮説 vs p=0.5点帰無の事前登録式を実装し、DESIGN.mdのBF既知値をgolden test化した。
- `holmAdjust()` はstep-down Holm補正を実装。Layer Aの事前登録familyは常に5条件で固定し、欠測条件は出力では`null`のままでもfamily sizeから落とさない（Holm順序上のp=1相当）ようにした。
- `analyzeInterimLayerA()` と `analyzeFinalLayerA()` を型・戻り値から分離。interim APIにはraw p / Holm pのフィールド自体が存在せず、途中覗きでconfirmatory頻度論p値を出す経路を作っていない。
- 主要確証sampleは `rngSource === 'anu' && ritualValid` のsessionだけ。`randomorg` / `local` fallbackやritual invalidは主要sampleへ代入せず、source counts / exclusions / QC / exploratoryへ残す。
- final confirmatoryはregistrationのdecisionRuleAを受け取り、5条件について `positive_pre_registered_result` / `negative_evidence` / `inconclusive` の凍結ラベルだけを返す。
- control QCは全sourceを隠さず集計し、overallに加えて `anu` / `randomorg` / `local` ごとのsummaryも返す。
- 累積偏差系列 `D_k = cumulativeHits - cumulativeBits/2` と95% chance envelope `±0.98√k` を実装した。
- 予言校正はconfidence×zのPearson相関と、confidence 0–20 / 21–40 / 41–60 / 61–80 / 81–100 の5帯でhit-rate summaryを返す。
- 強度勾配は設計記載どおり P1=0（なし） / P2・P3・P4=1（single practice） / P5=2（full combo）を固定し、dose×z相関と条件内ritualSeconds×z相関を返す。
- 四半期トレンドは登録開始日から365日を4分割し、四半期summaryと条件別session ordinalに対するz slope/correlationを返す。
- exploratory関数は全て固定の日本語warningを返し、「多重比較を含む探索であり、実証ではなく次の事前登録実験の仮説」と明示する。
- `src/stats/` はDOM / React / Capacitor / ledger / network依存を持たない純関数のまま維持した。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33135691797` / job `98735013918`: **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**23 test files / 96 tests passed**）。
- P5 inference: **7 tests passed** — BF golden、Holm既知例、欠測を含む5-hypothesis family、z→両側p、Wilson CI、invalid input拒否。
- P5 Layer A boundary: **5 tests passed** — interim p値非公開、ANU+validのみ、fallback非代替、5-test Holm family、累積偏差、source別control QC。
- P5 exploratory: **5 tests passed** — calibration golden、fallback exploratory inclusion、dose ordering、quarterly/session trend、実験期間外date拒否。
- BF golden: `n=8,y=8 -> 28.444444...` / `n=8,y=4 -> 0.406349...` 一致。
- Holm golden: `[0.01, 0.04, 0.03, 0.002] -> [0.03, 0.06, 0.06, 0.008]` 一致。欠測fixture `[0.01,null,0.04] -> [0.03,null,0.08]` でfamily sizeを3のまま維持することを確認。
- Wilson golden: `512/1024 -> [0.469432844260..., 0.530567155739...]` 一致。
- calibration golden: confidence `[10,30,50,70,90]` と hit rates `[0.5,0.625,0.75,0.875,1]`、confidence×z correlation=1を確認。
- `pnpm simulate`: green / network access 0。P4 tracer bulletは `registration -> control -> prediction -> session`、`predictionSeq=3 < sessionSeq=4`、`hits=511 / nBits=1024 / z=-0.0625`、`verifyChain()`成功のまま維持。
- `pnpm build`: green（Vite 8.1.4 production build成功。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`

### 手動レビューで修正した点
- 初版のHolm実装は`null`条件をfamily sizeから除外していたため、欠測条件があると補正が事前登録の5検定より緩くなる問題を手動差分レビューで発見した。P5内で修正し、欠測は結果`null`のままでも事前登録した5仮説familyを維持するよう固定した。
- control QCもoverallだけではsource混在を見落とし得るため、source countsに加えてsource別summaryを返すようP5内で強化した。
- PRの外部自動レビューはCodex/Cursor利用上限、CodeRabbitの自動review条件未達により実行されなかったため、merge判断はCI + golden tests + 手動diff reviewで行う。

### 要確定・申し送り（P6へ）
- 次は **P6 Dashboard Aのみ**。P7願い、P8 Layer C統計、P9時計、P10最終レポートへ先回りしない。
- P6の途中経過カードは必ず `analyzeInterimLayerA()` を使い、実験途中に `analyzeFinalLayerA()` を呼ばない。final confirmatory APIはP10最終レポート用として扱う。
- 条件カード・グラフではhit rateだけでなく、必ずchance expectation 0.5 / expected hitsを隣接表示する。
- 主要カードはANU+validのみを基準にしつつ、source countsとfallback件数を隠さない。
- exploratory表示を入れる場合は `EXPLORATORY_WARNING` を同じ画面に明示し、探索パターンを「実証」ラベルへ昇格させない。
- P6でledgerから`LayerASessionObservation`へprojectionする際はledgerをsource of truthとし、stats側へledger/SQLite依存を持ち込まない。confidenceはprediction entryとの`predictionSeq` joinで取得する。
- P4 `SessionFlow` のApp runtime mountingは引き続きP9のexperimentDate resolver統合時。P6で暫定clockを発明しない。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/stats/math.ts`
- 新規: `src/stats/inference.ts`
- 新規: `src/stats/layerA.ts`
- 新規: `src/stats/exploratory.ts`
- 新規: `src/stats/inference.test.ts`
- 新規: `src/stats/layerA.test.ts`
- 新規: `src/stats/exploratory.test.ts`
- 更新: `src/stats/index.ts`
- 更新: `PROGRESS.md`

---

## P6: Dashboard A (2026-08-28)

### 完了したこと
- `src/dashboard/` を新設し、append-only ledgerをsource of truthとしてP5 `analyzeInterimLayerA()`へ投影する `buildLayerADashboardModel()` を実装した。Dashboard側からSQLiteやP5 final confirmatory APIを直接呼ぶ経路は作っていない。
- 登録済み画面を「ホーム / ラボ」の2タブへ置換。ホームは統計を追わせない禅的な画面にし、実験ID・期間・固定timezone/day boundaryと折りたたみ監査情報だけを表示する。数字はユーザーがラボを開いた時だけ表示する。
- ラボに5条件カードを実装。各カードはANU + ritual-valid primary sampleのみを使い、hit率の隣に偶然50%、hitsの隣に期待hits=nBits/2、zの隣に偶然中心0、Wilson 95% CIの隣に50%基準、BF10の隣に中立点1を表示する。
- interim境界を構造的に維持し、Dashboard model/cardには `rawP` / `holmAdjustedP` を持たせない。画面にも「p値は最終日まで非表示」と明記した。
- source stripでANU主要セッション数、fallback記録数、ritual無効数を分離表示。fallback sessionは主要カードへ代入しない一方、履歴・QC・ミラクルログから隠さない。
- P5 `cumulativeDeviationSeries()`を使ったCanvasグラフを実装し、ANU+valid sessionの累積偏差と凍結済み95%偶然包絡線 `±0.98√bits`、偶然中心0を描画する。
- machine control QCをoverall + `anu` / `randomorg` / `local` source別に表示し、それぞれ偶然50%を併記する。
- ritual-valid sessionの `z >= +3` をミラクルログとして表示し、実際のRNG source、fallback/主要sample区分、hitsと偶然期待hitsを併記する。ミラクル件数の正直メーターは同じritual-valid session数 × 片側z>=3帰無確率を分母として表示する。
- 365日カレンダーを実装。登録scheduleから未来条件を埋めず、実際にsessionが記録された日のcondition/source/共鳴/ミラクルだけを表示する。空欄は「未記録または未来」とし、未来scheduleの漏洩を防いだ。
- Dashboard projectionは表示前にsession/controlのrecorded zがP4a `zScore()`と一致すること、日付が登録期間内であること、session conditionが登録scheduleと一致すること、`seqInDay`が登録範囲内であること、session slot/control dateが重複していないことを検証する。
- `App.tsx` は起動時に従来どおり `verifyChain()` を通した後だけdashboardを構築する。登録直後もledgerを再読込して同じprojection経路を使う。
- P9責務のfrozen timezone + dayBoundaryHourからcurrent experimentDateを解決するclock、P4 SessionFlowのruntime mountingは先行実装していない。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33136947408` / job `98738916123`: **green / success**（表示母数修正・semantic guard追加後のP6 implementation head）。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**24 test files / 103 tests passed**）。
- P6 dashboard projection: **7 tests passed** — interim-only境界、fallback miracle visibility、future schedule非漏洩、source別control QC、recorded z整合、期間外session拒否、登録schedule不一致拒否。
- 365日render snapshot/signature: green — 365 calendar cells、5 condition cards、Canvasあり、chance expectation表示あり、`rawP`/`holmAdjustedP`漏洩なし、mixed ANU/local source counts、365日末日までSSR成功。
- `pnpm simulate`: green / network access 0。既存P4 tracer bulletは `registration -> control -> prediction -> session`、`predictionSeq=3 < sessionSeq=4`、`hits=511 / nBits=1024 / z=-0.0625`、`verifyChain()`成功を維持。
- `pnpm build`: green（Vite 8.1.4 production build成功、56 modules transformed。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- P6で外部RNG/APIアクセスは追加していない。テスト/SSR fixtureは完全決定的。

### UI検証上の注意
- 365日fixtureをReact `renderToStaticMarkup()`でLab全体へ流し、DOM構造・365セル・5カード・Canvas・chance表示・no-peek境界が崩れないことをsnapshot signatureで確認した。production buildもgreen。
- 本セッションでは実ブラウザのクリック/Canvas描画ピクセル確認およびiOSシミュレータ確認は未実施。P6のUI構造はSSR + buildで検証し、P9のexperimentDate resolver + SessionFlow runtime統合時にWeb/iOSの実操作をまとめて再確認する。

### 手動レビューで修正した点
- 初版ではsource stripの「ANU主要セッション」がritual-invalid ANUも含む総ANU件数だった。`primarySessions = ANU + ritual valid`をmodelへ明示し、ラベルと母数を一致させた。
- 初版のミラクル偶然期待件数は全sessionを分母にしていた一方、ログはritual-validのみだった。期待値も同じvalid session分母へ揃えた。
- 表示前projectionを強化し、期間外session/control、登録scheduleと異なるcondition、重複session slot/control date、recorded z不一致を黙って表示しないようにした。
- 外部自動review botはCodex/Cursor利用上限、CodeRabbit自動review条件未達のため実質利用できず、CI + 365日render snapshot + 手動diff reviewをmerge判断の根拠とした。

### 要確定・申し送り（P7へ）
- 次は **P7 願いレジストリ（Layer C）のみ**。P8のFisher/BF/Layer C dashboard統計、P9時計・通知、P10最終レポートへ先回りしない。
- P7は `wish -> assignment` を必ず連続順序で追記し、wish登録後に人間が割付を選ぶ余地を作らない。assignment sourceは実際の`anu/randomorg/local`を保存する。
- wish→assignment間のクラッシュ回復をProtocol Freeze §12どおりidempotentに実装する。assignment済みwishへの二重割付は禁止する。
- sealed armのwish本文はdeadline前のnormal UI projectionへ返さない。Reactで隠すだけではなくdomain/application projectionで境界を強制する。
- withdrawnは削除ではなくjudgment/追記として扱い、P8 primary outcomeではnot realizedに数える規則を壊さない。
- P6 Dashboard Aのinterim-only統計境界、未来condition schedule非漏洩、ANU primary/fallback表示はP7で変更しない。
- P4 SessionFlowのApp runtime mountingとfrozen experimentDate resolverは引き続きP9まで保留する。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/dashboard/model.ts`
- 新規: `src/dashboard/model.test.ts`
- 新規: `src/dashboard/ExperimentDashboard.tsx`
- 新規: `src/dashboard/ExperimentDashboard.snapshot.test.tsx`
- 新規: `src/dashboard/dashboard.css`
- 新規: `src/dashboard/index.ts`
- 更新: `src/App.tsx`
- 更新: `PROGRESS.md`
