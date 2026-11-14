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

---

## P7: Wish Registry (Layer C) (2026-08-28)

### 完了したこと
- `src/wish/` を新設し、wish / assignment / judgment / wishmoment の型、strict ledger projection、application service、quick registration UI、wish moment UIを実装した。
- 通常登録は既存single-writerを拡張した `appendWithFollowUp()` のwriter lockを保持したまま `wish commit -> assignment RNG 1bit -> assignment commit` を行う。通常経路ではassignmentがwishの直後のseqになり、別entryが間へ割り込めない。
- P7の割付bit mappingを **`0 = sealed`, `1 = practice`** として実装・テストで固定した。assignmentには実際の `rngSource`（anu/randomorg/local）とraw bitを保存し、fallbackでもLayer Cの割付として有効に扱う。
- assignment RNGがwish commit後に失敗した場合はwishを削除・編集せずunassignedのまま残す。`recoverUnassignedWishes()` がresume時に検出し、global writer lock内の `appendConditionally()` で最新ledgerを再確認してからだけRNGを取得・assignmentを追記する。
- 通常登録とrecoveryが同時に走る競合fixtureでもassignmentは1件のみ、RNG取得も1回のみになるよう固定した。assignment済みwishへの再割付はしない。
- `projectNormalWishRegistry()` / `projectWishMoment()` はdeadline前のsealed wish本文を戻り値へ一切含めず、sealedは件数だけ返す。Reactで隠すのではなくdomain/application projectionでsealed visibility boundaryを強制した。
- 締切到来後だけ `projectDueJudgments()` がpractice/sealed両群のwish本文・arm・sourceを判定UIへ返す。unassigned wishはpractice/sealedとして表示しない。
- judgmentは `realized / not_realized / undecidable`、withdrawalは削除ではなく `judgment(outcome='withdrawn')` の追記として実装。realizedだけpathway必須、非realizedではpathway禁止をserviceとledger projectionの両方で検証する。
- primary outcome用の固定分類をP7境界として `realized -> realized`, `not_realized / undecidable / withdrawn -> not_realized` と実装した。P8でこの規則を黙って変更しない。
- judgment / withdrawal / wishmomentも `appendConditionally()` に統一し、最新ledgerをsingle-writer lock内で再検証してから追記する。同一wishへの同時二重judgmentは1件だけ成立する。
- `recordWishMoment()` は30〜60秒のみ許可し、記録時点のdomain projectionが返すeligible practice wish IDsと完全一致する場合だけwishmomentを追記する。sealed・余分・欠落IDを含む記録は拒否する。
- `SessionFlow` の順序を `result reveal -> wish moment -> permitted miracle/resonance feedback` に拡張した。wish momentへ渡せるのはP7 projection済みpractice wishだけで、sealed本文を直接渡す型経路を作っていない。
- `WishRegistryPanel` に願い本文、2週間/1ヶ月/3ヶ月締切、起きやすさ、影響可能性、登録即時割付、practice一覧、sealed件数、締切判定、取り下げUIを実装した。
- `scripts/simulate.ts` をP7まで伸ばし、Node側ではUI/CSS barrelを通さずservice/typesを直接importして完全offlineで実行できるようにした。
- P9責務のfrozen timezone + dayBoundaryHourからcurrent experimentDateを解決するclockは先行実装せず、P7 service/UIは解決済みexperimentDate・明示timestampを注入で受け取る。

### 完了基準の結果（実行結果）
- GitHub Actions CI run `33138385936` / job `98743476775`: **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**25 test files / 113 tests passed**）。
- P7 WishRegistryService: **10 tests passed** — wish→assignment隣接、bit mapping/source保持、RNG失敗後recovery、recovery idempotency、通常登録との競合時RNG二重消費禁止、sealed本文非漏洩、deadline reveal、wishmoment exact eligibility、judgment/pathway/withdrawal、同時二重judgment拒否、semantic malformed judgment拒否、Layer C disabled guard、writer lock中のinterleaving禁止を検証。
- `pnpm simulate`: green / network access 0。entry typeは `registration -> control -> prediction -> session -> wish -> assignment -> wishmoment`、`predictionSeq=3 < sessionSeq=4`、`wishSeq=5 -> assignmentSeq=6`、`assignmentArm='practice'`、`assignmentSource='local'`、`wishMomentCount=1`、`verifyChain()`成功。
- `pnpm build`: green（Vite 8.1.4 production build成功、56 modules transformed。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- テスト/シミュレーションの外部ANU/RANDOM.ORGアクセスは0件。Layer C RNG fixtureは決定的で、actual source fieldの保存経路だけを検証した。

### UI検証上の注意
- `WishRegistryPanel` / `WishMoment` / P7拡張後`SessionFlow`はTypeScript strictとproduction buildを通過している。
- 本セッションでは実ブラウザ操作・iOSシミュレータ確認は未実施。`WishRegistryPanel`とP4/P7 `SessionFlow`の`App.tsx` runtime mountingは、P9のfrozen experimentDate resolverとproduction RNG/application wiringを入れる統合時に行う。P7内で暫定的な端末日付ロジックを発明していない。

### 手動レビューで修正した点
- 初回P7 test fixtureに存在しない `source: undefined` を期待値へ入れていたテストミスを修正した。
- 初回P7 simulateは`src/wish/index.ts`からUI/CSSまでNodeへ読み込んで`.css` extension errorになったため、トレーサーバレットは`wish/service.ts` / `wish/types.ts`を直接importするNode-safe境界へ修正した。
- `list()`がwish単独状態を一瞬観測できる並行resumeを考慮し、recoveryのassignment判定とRNG取得をglobal writer lock内のconditional appendへ移して二重assignment・余分なRNG消費を防いだ。
- judgment/withdrawal/wishmomentのread→append競合も同じconditional writerへ統一し、同時二重judgmentをテストで固定した。
- ledger projection側でもrealized pathway必須・非realized pathway禁止・judgmentはassignment後という意味検証を追加し、P8が壊れたLayer C recordを黙って集計しないようにした。
- 外部自動review botはCodex/Cursor利用上限、CodeRabbit自動review条件未達のため実質利用できず、CI + P7 E2E + offline tracer + 手動diff reviewをmerge判断の根拠とする。

### 要確定・申し送り（P8へ）
- 次は **P8 統計エンジンC＋ダッシュボード統合のみ**。P9時計/通知、P10最終レポート、P11公証/申請へ先回りしない。
- P8はP7の`wish -> assignment`順序、`0=sealed / 1=practice` mapping、sealed domain visibility boundary、withdrawn/undecidable primary classificationを変更しない。
- Layer C主要解析では締切が実験終了までに到来した割付済みwishを対象にし、`withdrawn`/`undecidable`をnot realizedへ数えるProtocol Freeze規則を使う。締切未到来は件数のみで主要分母から外す。
- Fisher両側＋2×2 BFは`src/stats/`のUI非依存純関数として実装し、DESIGN.md golden `n1=n2=2, y1=2, y2=0 -> BF10=10/3` を必ず固定する。
- P8 dashboardはpractice vs sealedの実現率を表示するとき、sealed群を「あなたの世界のベースライン」として隣接表示する。締切前sealed本文を統計画面やnormal UIへ漏らさない。
- P7 UIのApp runtime mountingとfrozen experimentDate resolverは引き続きP9統合まで保留する。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/wish/types.ts`
- 新規: `src/wish/projection.ts`
- 新規: `src/wish/service.ts`
- 新規: `src/wish/service.test.ts`
- 新規: `src/wish/id.ts`
- 新規: `src/wish/index.ts`
- 新規: `src/wish/WishRegistryPanel.tsx`
- 新規: `src/wish/WishMoment.tsx`
- 新規: `src/wish/wish.css`
- 更新: `src/ledger/service.ts`
- 更新: `src/session/SessionFlow.tsx`
- 更新: `scripts/simulate.ts`
- 更新: `PROGRESS.md`

---

## P8: Stats Engine C + Dashboard Integration (2026-08-28)

### 完了したこと
- `src/stats/layerC.ts` を追加し、Layer CのFisher両側正確検定、DESIGN.mdで凍結された2×2 Bayes factor、群別実現率・Wilson 95% CI・risk differenceをUI非依存の純関数として実装した。
- 2×2 BFは `B(1+y1,1+n1-y1) * B(1+y2,1+n2-y2) / B(1+y1+y2,1+n1+n2-y1-y2)` をそのまま実装し、golden `n1=n2=2, y1=2, y2=0 -> BF10=10/3` で固定した。
- Fisher両側は固定周辺度数のhypergeometric supportをlog-gammaで計算し、観測表以下の確率を持つ表を合計するprobability-based two-sided定義で実装した。空のarmでは比較p値を`null`にする。
- `analyzeInterimLayerC()` と `analyzeFinalLayerC()` を型から分離した。interim戻り値にはFisher p値フィールド自体を持たせず、final APIだけが `fisherTwoSidedP` と事前登録済み判定ラベルを返す。
- Layer C主要outcomeはProtocol Freezeどおり `realized=成功`, `not_realized / withdrawn / undecidable=未実現`。副次感度分析だけは`undecidable`を除外し、withdrawnは未実現のまま維持する。
- final陽性判定は Fisher両側 `p < pThresh` + `BF10 > bfPos` に加え、実践群実現率が封印群より高い方向であることを要求し、強い逆方向差を「実践で改善」と誤ラベルしない。
- final Layer C resultに evidence grade `★★` と `randomized_non_blinded_self_judgment` limitationを明示した。
- 探索として起きやすさ3層・影響可能性3層のpractice/sealed集計と、実現願いの経路（自分の行動 / 他人 / 偶然の出会い / 不明）分布を返す。UIでは既存`EXPLORATORY_WARNING`を併記する。
- `src/dashboard/layerCModel.ts` を追加し、P7のstrict wish ledger projectionから集計値だけを生成する。wish本文はpractice/sealedを問わずdashboard modelへ持ち出さず、sealed本文のvisibility boundaryを維持した。
- dashboard modelは全wish / assigned / judged / awaiting judgment / unassigned件数、assignment source別practice/sealed件数、主要比較、副次感度分析、層別、経路分布を返す。unassigned wishはpractice/sealed統計へ入れない。
- ラボ画面へLayer Cカードを統合し、明示的に **「封印群があなたのベースライン。」** と表示。practice/sealed実現率、95% CI、未実現、withdrawn、undecidable、差、BF10中立点1、割付RNG sourceを隣接表示する。
- Layer Cラボには **「Fisher p値は最終解析まで非表示」** を表示し、途中画面からconfirmatory frequentist p値を覗く経路を作っていない。
- `App.tsx` の登録済みdashboard構築にLayer C modelを追加。事前登録画面の判定ルール文言も `Layer A = Holm補正後p`, `Layer C = Fisher両側p` と区別し、genesisに既に保存される同じthresholdの意味をUIでも正しく説明するよう修正した。
- `scripts/simulate.ts` をP8まで拡張し、practice/sealed各1願いの登録・隣接assignment・wish momentでsealed除外・deadline judgment・chain verify・interim Layer C集計まで完全offlineで接続した。
- P9責務のfrozen timezone/dayBoundaryHourからcurrent experimentDateを解決するclock、通知、継続UX、P4/P7 runtime flow mountingは先行実装していない。P10最終レポート用の「実験終了時点で締切到来済みwishだけを最終分母にするcutoff projection」もP8では発明せず、final stats APIはcallerからeligible observationsを受け取る。

### 完了基準の結果（実行結果）
- P8実装head CI run `33156826119` / job `98801483931`: **green / success**（P8統計・dashboard・tracerを含む）。判定ルールUI文言補正後のrun `33157015733` も **green / success**。
- `pnpm typecheck`: green（`tsc -b --noEmit`、errorなし）。
- `pnpm test`: green（Vitest 4.1.10、**27 test files / 123 tests passed**）。
- P8 Layer C stats: **7 tests passed** — BF `10/3` golden、Fisher既知例2件、空arm、withdrawn/undecidable primary policy＋secondary sensitivity、層別/経路、interim p値非公開、final方向判定、malformed realized/pathway拒否。
- P8 Layer C dashboard projection: **3 tests passed** — source/arm/判定件数集計、wish本文非漏洩、Layer C disabled、judgment-before-assignment不正拒否。
- P8 SSR integration: green — 「封印群があなたのベースライン」、BF、Fisher非表示badge、層別、経路、fallback sourceをrenderし、practice/sealed wish本文と`fisherTwoSidedP`を途中UI/modelへ漏らさないことを確認。
- Fisher known: `(n1,y1,n2,y2)=(2,2,2,0) -> 1/3`、`(10,1,14,11) -> 0.0027594561852200836` 一致。
- `pnpm simulate`: green / network access 0。entry typeは `registration -> control -> prediction -> session -> wish -> assignment -> wish -> assignment -> wishmoment -> judgment -> judgment`。`predictionSeq=3 < sessionSeq=4`、practice `wishSeq=5 -> assignmentSeq=6`、sealed `wishSeq=7 -> assignmentSeq=8`、wishMomentCount=1、practice realization rate=1、sealed=0、interim BF10≈1.5、`verifyChain()`成功。
- `pnpm build`: green（Vite 8.1.4 production build成功、59 modules transformed。既知の`jeep-sqlite` crypto externalization warningのみ継続）。
- ledger append-only guard: green — `OK: no forbidden ledger DELETE/UPDATE paths found.`
- P8テスト/シミュレーションの外部ANU/RANDOM.ORGアクセスは0件。

### UI検証上の注意
- Layer Cラボ統合はReact `renderToStaticMarkup()`によるSSR integration testとproduction buildで検証した。途中Fisher p値非公開、baseline文言、exploratory warning、source表示、wish本文非漏洩を機械検証している。
- 本セッションでは実ブラウザ操作・iOSシミュレータ確認は未実施。P9のexperimentDate resolver + notifications + P4/P7 runtime mounting時に、ホーム→セッション→願いタイム→ラボまでの実操作をWeb/iOSでまとめて再確認する。

### 手動レビューで修正した点
- 初回P8 CIはコードではなくtest fixtureの期待エラー文言だけが不一致だった。P7 projectionが既に `judgment requires a prior assignment` と先に拒否するため、P8テストをその既存境界へ合わせた。修正後は27 files / 123 testsすべてgreen。
- 事前登録画面の既存「Holm補正後p」表記はLayer Aだけには正しいが、P8導入後はLayer CのFisherルールを説明できていなかったため、A=Holm / C=Fisherを分離表示した。
- final陽性判定にpractice > sealedの方向条件を明示し、BF/Fisherが強くても逆方向差をpractice改善として扱わないようにした。
- dashboard projectionとSSR fixtureでsealed本文だけでなくpractice本文も統計モデルへ不要に持ち出さないことを確認し、Layer C統計UIをaggregate-only境界にした。

### 要確定・申し送り（P9へ）
- 次は **P9 通知・継続UXのみ**。P10最終レポート、P11公証/申請へ先回りしない。
- P9でregistrationに固定されたIANA `timeZone` + `dayBoundaryHour`からcurrent experimentDateを解決し、端末timezone変更・旅行でも実験日境界を変えない。
- P9でP4 `SessionFlow` とP7 `WishRegistryPanel` / `WishMoment`を実App runtimeへmountする。P4のprediction-before-RNG順序、P7のsealed projection、P8のinterim no-p境界を変えない。
- 毎日リマインダー・願い締切通知・やさしいストリーク（罰なし）を実装し、03:00等の境界時刻のunit testとdeadline notification scheduling testを固定する。
- production RNG endpoint/auth/application wiringもruntime統合時に確認するが、source偽装やfallback隠蔽はしない。
- P10で最終Layer C confirmatoryを呼ぶ際は、実験終了までにdeadline到来済みの割付wishだけを主要分母へ投影し、deadline未到来件数を別表示する。P8 `analyzeFinalLayerC()` 自体の統計式は変更しない。
- `capacitor.config.ts` の本番bundle idは引き続きP11まで要確定。

### 触ったファイル
- 新規: `src/stats/layerC.ts`
- 新規: `src/stats/layerC.test.ts`
- 新規: `src/dashboard/layerCModel.ts`
- 新規: `src/dashboard/layerCModel.test.ts`
- 新規: `src/dashboard/layerCIntegration.test.tsx`
- 更新: `src/stats/index.ts`
- 更新: `src/dashboard/index.ts`
- 更新: `src/dashboard/ExperimentDashboard.tsx`
- 更新: `src/App.tsx`
- 更新: `scripts/simulate.ts`
- 更新: `PROGRESS.md`


---

## P9: Notifications / Continuity UX (2026-08-28)

### 完了したこと
- 登録時に固定した IANA `timeZone` と `dayBoundaryHour` から `experimentDate` を導出する `src/continuity/time.ts` を実装。端末timezone変更や旅行に依存せず、03:00境界の直前/直後とDSTをテストした。
- `systemContext()` でセッション時刻・曜日・探索用月相を凍結timezone基準から生成し、P4で保留していたP9の時間責務をruntimeへ接続した。
- 登録後の `App` を `ExperimentRuntime` へ切り替え、`今日 / 願い / 記録・ラボ` からP4 `SessionFlow`、P7 `WishRegistryPanel` / `WishMoment`、P6/P8 dashboardへ到達できるようにした。
- ledgerから当日の完了済み `seqInDay` を読み、最初の未完了sessionだけを再開するcontinuity projectionを実装。欠測日や過去experiment dayを後から生成・補完しない。
- daily controlは既存 `SessionFlowService.prepareSession()`、session実行は `runSession()` のみを通し、1日1controlとprediction-before-measured-RNGの不変条件をUI下で維持した。
- application RNG singletonを追加。`VITE_ANU_RNG_ENDPOINT` 設定時は `anu -> randomorg -> local`、未設定時は `randomorg -> local` とし、fallbackをquantumと表示せず実際の `rngSource` をledgerへ残す。
- `@capacitor/local-notifications@6.1.3` とiOS Pod wiringを追加した。
- 毎日リマインダーと願い締切通知のplannerを実装。凍結timezoneで絶対時刻へ変換し、pending上限を考慮して直近64件へ制限した。
- OS通知permissionは起動時に自動requestせず、ユーザーが `通知をONにする` を押した時だけrequestする。
- 通知同期はIntention Dice予約ID帯 `10000..10063` だけを置換し、他用途のpending local notificationをcancelしない。
- sealed wish本文を通知層へ渡さず、wish ID / deadline / assignment・judgment状態だけから締切通知を組み立てるprivacy boundaryを実装した。
- `deriveGentleStreak()` を追加し、記録済み日数・直近run・今日完了を表示。`penaltyApplied: false` を固定し、休んだ日があっても実験失格や罰にはしない。
- multi-session dayでは `SessionFlow` 完了後にruntimeをrefreshし、次の未完了registered sessionへ進む `onFinish` 接続を追加した。
- `LayerCRegistration.defaultDeadlineDays` を凍結UI選択肢 `14 | 28 | 90` と同じ型へ狭めた。

### 完了基準の結果
- `pnpm typecheck`: green
- `pnpm test`: green — **32 test files / 142 tests passed**
- `pnpm simulate`: green — `networkRequests: 0`、既存offline tracerとchain verificationを維持
- `pnpm build`: green
- append-only ledger guard: green
- feature integration CI: run `33163189418` / job `98822313469` green

### 不変条件セルフチェック
1. frozen timezone / experiment-day boundaryをdevice timezoneから分離 — ✅
2. 1日1controlのidempotency — ✅
3. prediction commit before measured RNG — ✅
4. fallback sourceを隠さない — ✅
5. 欠測を補完しない — ✅
6. sealed wishを通知へ漏らさない — ✅
7. ledger append-only — ✅
8. continuity UXで罰を導入しない — ✅
9. P10最終確証レポートをP9へ混入させない — ✅

### Native/UI確認の制約
- CIはUbuntuのためXcode/iOS simulator上の実通知deliveryは直接実行できない。Capacitor v6 dependency、TypeScript adapter、iOS Pod wiring、production build、通知planner/permission境界はCIで検証済み。
- iOS実機/Simulatorのpermission prompt・日次通知・締切通知deliveryはrelease環境のsmoke checkとして実施する。

### 次フェーズへの申し送り
- 凍結順序どおり次は **P10 Final Report**。P9で生成したruntime/ledgerデータを使うが、最終確証判定・最終レポート生成はP10まで開始しない。
---
## P10: Final Report (2026-08-28)

### 完了したこと
- append-only ledger exportを `verifyChain()` で検証してから最終レポートへ投影する `src/report/` pipelineを実装した。
- registration genesisのprotocol / canonicalization / RNG / analysis / stats versionと365日scheduleを検証し、壊れたexportを最終統計へ流さない。
- Layer A最終確証は既存P5 `analyzeFinalLayerA()` を再利用し、主要sampleを `rngSource='anu' && ritualValid` のみに固定。fallbackは除外理由・source情報として残す。
- Layer C最終確証は実験終了までにdeadline到来済みの割付済みwishだけを主要分母に投影し、deadline未到来・unassignedを別件数で保持する。統計式は既存P8 `analyzeFinalLayerC()` を再利用する。
- 最終Markdownを「1. 確証パート」と「2. 探索パート」に明確分離し、探索値やwarningが確証判定へ混入しない構造にした。
- P10探索としてLayer B ritual前後の気分/エネルギー差、状態依存、ミラクル日profileを純関数追加。既存P5/P8のdose-response / trend / pathway等も探索パートへ統合した。
- v1 schemaに存在しない `realizedAt` は推定せず「測定不能」と明記し、自由文predictionの意味判定も後付けせず保留した。
- `pnpm verify-export <experiment.json>` と `pnpm generate-report <experiment.json> [output.md]` を追加。CLIとアプリは同じchain verification / projection / stats / Markdown rendererを共有する。
- `FinalReportPanel` を「記録・ラボ」へ追加し、365日終了後だけ最終レポートを生成・表示・共有/保存できるようにした。終了前はcomponent自体がfinal統計を生成しないSSR gateを固定した。
- `scripts/simulate.ts` を完全offlineの365日/120願いsynthetic simulationへ拡張し、出力に `synthetic: true` と `networkRequests: 0` を明示。実ANU取得と誤認させない。
- CIにnull年golden hash、export検証→Markdown byte-identical再生成、Layer A陽性fixture、Layer C陽性fixtureを必須ゲートとして追加した。

### 完了基準の結果
- `pnpm typecheck`: green
- `pnpm test`: green — **35 test files / 151 tests passed**
- null 365日 + 120願いsimulation: green — **1456 ledger entries** / `networkRequests: 0` / `verifyChain()` success
- null report golden SHA-256: `1ec5f22034d18546ac20e2b8c64b5ea1dcab5e7faf4e903e7e7c58182f398381`
- export verification → report regeneration: green — generated Markdownがsimulation出力とbyte-identical
- Layer A positive fixture `--effect 0.01 --condition 2`: green — condition index 2のみ `positive_pre_registered_result`
- Layer C positive fixture `--wish-effect 0.25`: green — practice≈0.6271 / sealed≈0.2459 / Fisher p≈0.0000411067 / BF10≈1720.18 / `positive_pre_registered_result`
- `pnpm build`: green
- ledger append-only guard: green
- P10実装head CI run `33165163108` / job `98828707867`: success

### 不変条件セルフチェック
1. final p値は実験終了後のFinal Reportだけ — ✅
2. confirmatory / exploratoryを構造的に分離 — ✅
3. Layer A primaryはANU + ritual-validのみ — ✅
4. Layer C primaryは実験終了までにdeadline到来済みのみ — ✅
5. frozen P5/P8統計APIを再実装せず再利用 — ✅
6. exportはchain検証後だけ解析 — ✅
7. fallback/sourceを隠さない — ✅
8. 欠測・未到来・unassignedを都合よく補完しない — ✅
9. ledger append-only — ✅
10. tamper-proofとは表現せずtamper-evidentを維持 — ✅

### 次フェーズへの申し送り
- 凍結順序どおり次は **P11 公証・提出 / Release Hardeningのみ**。
- `capacitor.config.ts` の仮bundle id `com.example.intentiondice` を正式値へ確定し、iOS signing / App Store提出準備 / 実機smokeを行う。
- 外部公証を有効化する場合もローカルchainを「完全改竄不能」とは表現せず、anchor対象hash・時刻・providerを監査可能に記録する。
- P10で固定したnull report golden、A/C positive simulation、confirmatory/exploratory境界、CLI再現性をP11で壊さない。
---
## P11: Public Anchoring / Release Hardening (2026-08-28)

### 完了したこと
- production bundle identifierを `com.sunpotflower4460.intentiondice` に確定し、Capacitor / Xcode / fastlaneで統一した。
- App Store用1024x1024 RGB / no-alpha icon、splash、`ITSAppUsesNonExemptEncryption = false`、日本語metadata / privacy / submission checklistを整備した。
- Fastlane + GitHub Actionsの手動TestFlight経路、`macos-26` / Xcode 26+ unsigned iOS Simulator smoke、`pnpm release-check`を追加した。
- optional週次public anchoringを実装。外部送信は `genesisHash / headHash / headSeq / protocolVersion` のみで、wish本文、mood、prediction、raw RNG bits等は送信しない。失敗/未設定/オフラインでも実験をblockしない。
- Cloudflare Workerにstrictな `POST /anchors` / `GET /anchors/<genesisHash>`、server `receivedAt`、duplicate idempotency、extra-field rejectionと手動deploy workflowを実装した。
- chainの意味は最後まで **tamper-evident** とし、tamper-proofとは表現しない。

### 完了基準の結果
- Linux CI run `33167978274`: **success**。typecheck / tests / 365日simulation / P10 golden / build / append-only guard / release-check green。
- tests: **36 files / 157 tests passed**。
- offline 365日simulation: **1456 ledger entries / networkRequests: 0 / verifyChain success**。
- iOS native smoke run `33167978311`, job `98837863198`: **success**。Xcode 26+ validation -> Web build -> Capacitor sync -> Simulator runtime -> `App.xcworkspace` Simulator buildの全step成功。
- external readiness probe run `33167975160`, job `98837853344`: **success**。secret値は出さずpresenceのみ確認。
- PR #14: unresolved review threads **0** / submitted reviews **0**。blocking reviewなし。
- readiness helper削除後の差分はhelper 1ファイル削除のみで製品コード変更なし。bot push後の `action_required / jobs 0` はworkflow approval gateでありcompile/test failureではない。

### 外部release readiness
- Apple release credentials: **missing**
- Cloudflare release credentials: **missing**
- ANU production endpoint: **missing**
- public notary endpoint: **missing**
- よって実TestFlight uploadとWorker実deployは未実施。release/deploy経路とnative buildは検証済みで、外部資格情報/endpoint投入後の手動operationとして分離する。未実施を公開済みとは扱わない。

### 最終不変条件セルフチェック
1. ledger append-only — ✅
2. prediction commit before measured RNG — ✅
3. target scheduleとmeasured Layer A RNGの独立 — ✅
4. fallback sourceを隠さない — ✅
5. confirmatory / exploratory境界とP10 goldenを維持 — ✅
6. sealed wish本文をexternal anchorへ送らない — ✅
7. anchor failureをexperiment blockingにしない — ✅
8. tamper-evidentをtamper-proofと誇張しない — ✅
9. production bundle ID / Xcode 26 native build / store submission gate — ✅

### Release申し送り
- repository implementationは **P0 -> Gate 0 -> P1 -> P2 -> P3 -> P4a -> P4 -> P5 -> P6 -> P7 -> P8 -> P9 -> P10 -> P11** まで完了。
- 残るのはApple / Cloudflare資格情報、ANU production endpoint、public notary endpointの設定と、手動release/deploy実行のみ。
---

## P11: TestFlight Signing Hotfix (2026-08-28)

### 完了したこと
- P11 merge後のrelease再点検で、App Store Connect upload認証はある一方、GitHub-hosted macOS runnerへApple Distribution certificate/private keyとApp Store provisioning profileを投入する経路が無く、実archive signingが再現可能ではないことを検出した。
- `iOS TestFlight release`へdistribution `.p12` + password + App Store `.mobileprovision`のfail-fast gate、ephemeral keychain import、profile Team ID / production bundle ID検証を追加した。
- Fastlane `ios beta`はReleaseだけmanual signingへ切り替え、production bundle IDからsupplied App Store profileを明示mappingする。App Store Connect API keyはupload認証、P12/profileはcode signingと役割を分離した。
- workflow終了時はprovisioning profileとtemporary keychainをcleanupする。
- `pnpm release-check`へsigning secrets名、temporary keychain、profile検証、manual signing、cleanupの静的guardを追加した。
- `docs/store/SUBMISSION-CHECKLIST.md`を7 Apple secrets構成へ同期し、既存Cloudflare Workerはpublic anchor専用でANU RNG proxyではないことを明記した。
- 実配布作業はIssue #16 `Release Operations: configure production credentials and ship TestFlight`へ集約した。

### 完了基準の結果
- Linux CI run `33172253210`, job `98852003037`: **success**。
- `pnpm typecheck` / `pnpm worker:typecheck`: green。
- `pnpm test`: green — **36 test files / 157 tests passed**。
- 365日offline null simulation: green — **1456 ledger entries / networkRequests: 0**。
- P10 null report golden SHA-256 `1ec5f22034d18546ac20e2b8c64b5ea1dcab5e7faf4e903e7e7c58182f398381` を維持。
- Layer A / Layer C positive fixtures、`pnpm build`、append-only ledger guard: green。
- `pnpm release-check`: green — `testFlightSigning: explicit P12 + App Store provisioning profile`。
- iOS native smoke run `33172253191`, job `98852002975`: **success**。Xcode 26+ / Web build / Capacitor sync / `bundle exec fastlane lanes` / unsigned `App.xcworkspace` Simulator buildの全step成功。
- PR #15 review threads 0 / submitted reviews 0。Codex/Cursorは利用上限、CodeRabbitは自動review条件未達で実レビューなし。

### 不変条件セルフチェック
1. protocol / stats / registration semanticsを変更しない — ✅
2. ledger append-only、prediction-before-RNG、target independenceを変更しない — ✅
3. fallback sourceを隠さずP10 golden / confirmatory-exploratory境界を維持 — ✅
4. signing secrets/private keysをrepositoryへcommitしない — ✅
5. signing assetsをephemeral runnerからcleanupする — ✅
6. TestFlight未uploadをupload済みと表現しない — ✅

### Release申し送り
- 実TestFlight uploadは未実施。Apple側のApp record、Distribution certificate/private key、App Store profile、App Store Connect Team API Keyを用意し、Issue #16の7 GitHub secrets設定後に`iOS TestFlight release`を手動dispatchする。
- ANU production endpoint、Cloudflare credentials、public notary endpointも外部release operation。未設定時はfallback/skipを正直に記録し、ANU・公証成功と偽装しない。
- 現GitHub connectorではrepository Secrets/Variables設定や`workflow_dispatch`開始はできないため、Issue #16の人手チェックポイントとして残す。

### 触ったファイル
- `.github/workflows/ios-release.yml`
- `fastlane/Fastfile`
- `scripts/release-check.ts`
- `docs/store/SUBMISSION-CHECKLIST.md`

---

## P11: Protocol integrity audit hotfix (2026-09-01)

### 完了したこと
- 全フェーズ完了後の横断監査で、PROTOCOL_FREEZE に明記されているのに実装が緩かった箇所を閉じた。仕様の発明はしていない。
- `verifyChain()` が PROTOCOL §7 の `predictionSeq < session.seq` と「参照先が prediction」を検査するようにした。hash 検証の後に束縛検査する。
- `runSession` を single-writer queue で直列化し、同一 `(date, seqInDay)` の並行実行が予測・session を二重書きしないようにした。
- 予言commit後に RNG が失敗した場合、既存の prediction を再利用してから測定期 RNG を取り直す。後出し予言を増やさない。
- 日次 control と session append を `appendConditionally` 経由にし、ledger lock 内で重複判定する。
- 凍結済み `targetAlgorithmVersion` が現行 generator と違うときは target を再生成しない。
- セッションUIが「次へ進むと予言が確定する」と誤表示していたのを、実際の commit 点（3秒長押し）に合わせた。
- 最終レポートの探索カウント／Layer B Δ／Layer C 差に DESIGN §3.6 の「偶然ならこのくらい」を併記した。null-year Markdown が変わるため P10 golden hash を更新した。ledger head / 統計判定は変えていない。
- TestFlight ephemeral keychain の search list に login keychain を残し、WWDR/system cert 解決が壊れないようにした。
- README の「現在地」を P11 完了＋Issue #16 が外部作業であることに更新した。

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green
- `pnpm worker:typecheck`: green
- `pnpm test`: green — **36 test files / 163 tests passed**
- `pnpm build`: green
- 365日 offline null simulation: green — **1456 ledger entries / networkRequests: 0 / verifyChain success**
- null report golden SHA-256: `5c0d6bea15bf3f86faaf00f8d5ca38b76b3947c34fb148bb7d8809d9ec4792da`（正直メーター追記による Markdown 更新。entries=1456 / headHash `4035fd1e1ed3aa733d6d42d89111605cbced958a73dd5052da27ca4c0867cd08` は維持）
- export verification → report regeneration: green — byte-identical
- Layer A positive fixture `--effect 0.01 --condition 2`: green — condition index 2 のみ `positive_pre_registered_result`
- Layer C positive fixture `--wish-effect 0.25`: green — practice≈0.6271 / sealed≈0.2459 / Fisher p≈0.0000411067 / BF10≈1720.18 / `positive_pre_registered_result`
- `pnpm release-check`: green
- ledger append-only grep: green — no DELETE/UPDATE paths

### 不変条件セルフチェック
1. ledger は追記のみ — ✅ DELETE/UPDATE 経路なし
2. prediction は測定期 RNG より前 — ✅ 再利用経路でも既存 prediction を先に確定済みとして扱う
3. Layer A 主要確証 sample は anu + ritual-valid のまま — ✅ 統計式未変更
4. confirmatory / exploratory 境界を維持 — ✅ 正直メーターは表示のみ
5. targetDir は独立 seed + 凍結 algorithm version — ✅ mismatch 時は再生成しない
6. fallback source を隠さない — ✅ 未変更
7. 封印群を実践UIへ出さない — ✅ 未変更
8. tamper-evident を tamper-proof と書かない — ✅ 未変更
9. TestFlight 未upload を公開済みと書かない — ✅ Issue #16 は外部作業のまま残す

### 要確定・申し送り
- Issue #16 の Apple / Cloudflare / ANU production endpoint は今回も未設定。資格情報なしでは TestFlight upload も Worker deploy もできない。
- `origin/p11-deep-integrity-audit-hotfix` は自己適用 workflow だけで、`erasableSyntaxOnly` で CI 失敗していた。本hotfixがその監査意図のうち PROTOCOL に明記された欠陥だけを通常PRとして入れた。
- 最終レポートUIが実験最終日の翌日まで final p を出さない点は、DESIGN「365日目」の文言より PROTOCOL「終了時に1回」＋最終日データ取り込みの読みを維持した。仕様を変えるなら人間確認が必要。
- prediction payload へ `experimentId` を足すことは既存 chain hash を変えるため未実施。現行は ledger 1本=1実験。

### 触ったファイル
- `src/ledger/verify.ts`, `src/ledger/verify.test.ts`
- `src/session/service.ts`, `src/session/service.test.ts`, `src/session/SessionFlow.tsx`
- `src/registration/schedule.ts`, `src/registration/schedule.test.ts`, `src/registration/projection.ts`, `src/registration/projection.test.ts`, `src/registration/index.ts`
- `src/report/markdown.ts`, `src/report/report.test.ts`
- `scripts/simulate.ts`, `scripts/release-check.ts`
- `.github/workflows/ci.yml`, `.github/workflows/ios-release.yml`
- `README.md`, `PROGRESS.md`

---

## P11: Review follow-up on integrity hotfix (2026-09-01)

### 完了したこと
- マージ済み PR #17 への Codex レビューのうち、PROTOCOL に沿う指摘だけを追従修正した。
- `verifyChain()` が参照先 prediction の date / seqInDay / condition / targetDir 一致も見る。
- prediction の新規 commit を `appendConditionally` に載せ、複数 `SessionFlowService` が同じ identity の予言を二重書きしないようにした。
- orphan prediction 再利用時は committed confidence / prophecy / `startedAt <= committedAt` が一致しない draft を拒否する。元の mood/ritual は prediction payload に無いので復元せず、欠測として残す。
- ミラクル正直メーターを DESIGN の 1回/日概数固定から、分析 session 数 × 正規裾確率へ変更。ダッシュボードと同じ `chanceSessionCount()` を使う。
- 「P11 に他フェーズを混ぜた」指摘は、人間が事後監査とマージを指示した作業のためコードでは戻していない。

### 完了基準の結果
- `pnpm typecheck` / `pnpm worker:typecheck`: green
- `pnpm test`: green — **36 files / 167 tests**
- `pnpm build`: green
- 365日 null simulation: 1456 entries / networkRequests 0 / headHash 維持
- null report golden: `2b564cc1dfe328d1b97959d39c6662929822c9b16875e7c74af5f8144b4fd90d`

### 触ったファイル
- `src/ledger/verify.ts`, `src/ledger/verify.test.ts`
- `src/session/service.ts`, `src/session/service.test.ts`
- `src/stats/inference.ts`, `src/stats/inference.test.ts`, `src/stats/index.ts`
- `src/stats/reportExploratory.ts`, `src/stats/reportExploratory.test.ts`
- `src/report/markdown.ts`, `src/report/report.test.ts`
- `src/dashboard/ExperimentDashboard.tsx`
- `.github/workflows/ci.yml`
- `PROGRESS.md`

---

## P11: Orphan prediction abandonment (2026-09-01)

### 完了したこと
- マージ済み PR #18 の指摘どおり、予言 append 後・session append 前のクラッシュで再起動すると新しい `startedAt` が prediction より後になり、その回を完了不能になる問題へ明示的な欠測経路を足した。
- `findOrphanedPredictionSlot()` を domain projection として公開。session が無い date/seq に prediction だけがあるとき、その枠を欠測として返す。
- `ExperimentRuntime.refresh()` は次枠が orphan なら `prepareSession` / `SessionFlow` を開かない。気分・実践を後付け再開しない。
- 今日画面に欠測カードを出す。同一プロセス内で元の `startedAt` を持つ retry は従来どおり prediction 再利用で完了できる。
- prediction payload に mood / ritual / `startedAt` は足していない。同じ日の次 `seqInDay` への自動スキップもしていない。

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green（出力なし）
- `pnpm worker:typecheck`: green
- `pnpm test`: green — **36 files / 168 tests**
- `pnpm build`: green
- 365日 null simulation: green — **1456 entries / networkRequests: 0 / headHash `4035fd1e1ed3aa733d6d42d89111605cbced958a73dd5052da27ca4c0867cd08` / reportSha256 `2b564cc1dfe328d1b97959d39c6662929822c9b16875e7c74af5f8144b4fd90d`（不変）**
- `pnpm release-check`: green
- ledger append-only grep: green — no DELETE/UPDATE paths
- Web UI: 欠測カードはクラッシュ再現が必要なためブラウザ E2E は未実施。投影関数のユニットテストと、orphan 時に `plan` を作らない分岐で代替した。

### 不変条件セルフチェック
1. ledger は追記のみ — ✅ DELETE/UPDATE 経路なし。欠測用の打ち消し entry も書いていない
2. 予言は測定期 RNG より前 — ✅ 再利用経路は未変更。再起動後は後付け予言も後付け mood も作らない
3. Layer B の mood pre/post は ritual を挟み reveal を挟まない — ✅ 再起動後に順序を組み立て直さない
4. 欠測は欠測のまま — ✅ その枠を埋める経路を UI から閉じる
5. 凍結スキーマを勝手に変えない — ✅ PredictionPayload にフィールド追加なし
6. tamper-evident を tamper-proof と書かない — ✅ 未変更
7. TestFlight 未upload を公開済みと書かない — ✅ Issue #16 は残す

### 要確定・申し送り（次フェーズへ）
- 同じ日の残り `seqInDay` は、既存の「未完了の最小 seq が next」規則のまま orphan 枠で止まる。2回目以降へ自動スキップする仕様は PROTOCOL に無いので発明していない。変えるなら人間確認が必要。
- Issue #16 の Apple / Cloudflare / ANU production endpoint は未設定のまま。

### 触ったファイル
- `src/session/types.ts`, `src/session/service.ts`, `src/session/service.test.ts`, `src/session/index.ts`
- `src/runtime/ExperimentRuntime.tsx`
- `PROGRESS.md`

---

## P11: Honesty and runtime recovery polish (2026-09-01)

### 完了したこと
- プロセス内 refresh（リマインダー変更・願い画面の更新）が新しい `startedAt` を作って、完了できる orphan prediction まで欠測扱いにしていたのを止めた。真の再起動だけ欠測カードを出す。
- 「今日」以外のタブへ移っても `SessionFlow` を unmount しない。session commit 後も `onFinish` まで plan を保持し、願いタイム／フィードバックを飛ばさない。
- セッション結果の z、最終レポートの相関・BF・層別差に「偶然ならこのくらい」を併記。探索の経路ブロックにも固定警告を足した。「層別の効きめ」は「層別の探索集計」に変更。moodPre 文言を実践前に合わせた。
- `verifyChain()` が hash は通るが bitsHex と hits/z が食い違う session/control を落とす。

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green
- `pnpm worker:typecheck`: green
- `pnpm test`: green — **37 files / 176 tests**
- `pnpm build`: green
- 365日 null simulation: green — **1456 entries / networkRequests: 0 / headHash `4035fd1e1ed3aa733d6d42d89111605cbced958a73dd5052da27ca4c0867cd08`（不変） / reportSha256 `ef9462dd6dbf7e4852f66f3527d1c33c59b5ffcb8017ebac9b004284cb6cc24e`（正直メーター追記による Markdown 更新）**
- `pnpm release-check`: green
- ledger append-only grep: green
- Web UI: タブ切替・クラッシュ欠測のブラウザ E2E は未実施。recovery の純関数テストと SessionFlow のマウント維持で代替。

### 不変条件セルフチェック
1. ledger は追記のみ — ✅
2. 欠測は欠測のまま — ✅ 真の再起動の orphan は欠測。プロセス内で完了できる回を欠測に見せない
3. Layer B 順序 — ✅ moodPost は reveal 前。再起動後に後付け再構成しない
4. 確証と探索を混ぜない — ✅ 経路・層別に探索警告。共鳴／ミラクルは実証と書いていない
5. 表示数値の隣に偶然 — ✅ 不足していた z / 相関 / BF を追加
6. 凍結スキーマを変えない — ✅
7. tamper-evident — ✅ 未変更
8. Issue #16 を出荷済みと書かない — ✅

### 要確定・申し送り
- 同じ日の次 `seqInDay` への自動スキップは未定義のまま。変えるなら人間確認。
- ダッシュボードのテスト fixture は `bitsHex: '00'` のプレースホルダのまま hits を直接置く。ライブ経路の session は `runSession` が bits から計算する。dashboard projection を bits 照合にすると fixture 一式の作り直しが要る。
- Issue #16 の外部資格情報は未設定。

### 触ったファイル
- `src/runtime/sessionRecovery.ts`, `src/runtime/sessionRecovery.test.ts`, `src/runtime/ExperimentRuntime.tsx`
- `src/session/service.ts`, `src/session/service.test.ts`, `src/session/index.ts`, `src/session/SessionFlow.tsx`
- `src/ledger/verify.ts`, `src/ledger/verify.test.ts`
- `src/report/markdown.ts`, `src/report/report.test.ts`
- `src/dashboard/ExperimentDashboard.tsx`, `src/dashboard/layerCIntegration.test.tsx`
- `.github/workflows/ci.yml`
- `PROGRESS.md`

---

## P11: Analog secret-base UI (2026-09-01)

### 完了したこと
- 紙・インク・真鍮・スタンプのアナログ秘密基地トーンに画面を揃えた。Vite 初期の紫テーマと全面角丸ガラスをやめた。
- 今日のセッションを前面に置いた。通知・乱数ソースは `<details>` の後ろ。判定待ちがあるときだけ今日面に出す。
- 登録ウィザードは ID・開始・予言を前面、bit数・タイムゾーン・Layer C 細部は折りたたみ。確証判定ルールは隠していない。
- 抽選ボタンを封蝋風の長押しスタンプにした。的バッジはスタンプ、ノート罫線のセッション紙面。

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green
- `pnpm test`: green — **37 files / 176 tests**
- `pnpm build`: green
- 統計・ledger・report golden は未変更（見た目のみ）

### 不変条件セルフチェック
1. プロトコル・統計式・文言の証拠意味は変えていない
2. 封印願い本文を出していない
3. 判定ルールを折りたたみの外に残した
4. Issue #16 は残す

### 要確定・申し送り
- 紙面トーンは Web の system 明朝に依存する。
- Issue #16 の外部資格情報は未設定。

### 触ったファイル
- `src/theme.css`, `src/index.css`, `index.html`, `public/favicon.svg`
- `src/App.css`, `src/App.tsx`
- `src/runtime/ExperimentRuntime.css`, `src/runtime/ExperimentRuntime.tsx`
- `src/session/SessionFlow.css`
- `src/dashboard/dashboard.css`
- `src/wish/wish.css`
- `PROGRESS.md`

---

## P11: In-app ledger export and on-demand verify (2026-09-01)

### 完了したこと
- DESIGN §2 IN / §3.8 のアプリ内エクスポート穴を閉じた。記録タブに JSON/CSV 保存と `verifyChain()` のオンデマンド実行を置いた。
- CLI の `exportLedgerJson` / `exportLedgerCsv` をそのまま使う。生ビットは payload に含まれる。Blob 保存で巨大な data URI は使わない。
- 検証成功文は「改竄検知可能 / tamper-evident」に留め、外部anchorなしの「完全改竄不能」とは書いていない。
- 起動時の連鎖検証は従来どおり。記録タブでは毎回自動では走らせない。

### 完了基準の結果（コマンド出力の要約）
- `pnpm typecheck`: green
- `pnpm worker:typecheck`: green
- `pnpm test`: green — **41 files / 183 tests**（既存の `.tsx` ダッシュボード／レポートテストを vitest include に入れ、CIから漏れていた分を回収）
- `pnpm build`: green
- `pnpm release-check`: green
- ledger append-only grep: green
- 365日 null simulation / report golden: 未変更（export UI と vitest include のみ）

### 不変条件セルフチェック
1. ledger は追記のみ — ✅ エクスポートは list の読み取りのみ
2. tamper-evident を tamper-proof と書かない — ✅
3. 生ビットを落とさない — ✅ 既存 export 関数を使用
4. Issue #16 は残す — ✅ 資格情報・TestFlight・実機smokeは手動のまま

### 要確定・申し送り
- iOS WKWebView の download 属性は実機smoke（Issue #16 F）で確認する。Filesystem plugin は足していない。
- 同じ日の次 `seqInDay` 自動スキップは未定義のまま。
- Issue #16 の外部資格情報は未設定。

### 触ったファイル
- `src/ledger/LedgerExportPanel.tsx`, `src/ledger/LedgerExportPanel.test.ts`
- `src/runtime/ExperimentRuntime.tsx`, `src/runtime/ExperimentRuntime.css`
- `vitest.config.ts`
- `PROGRESS.md`

---

## G0: Unity箱庭ゲームの共通設計 (2026-11-11)

### 完了したこと
- ユーザーの依頼に基づく新しいUnityゲーム企画の設計を `docs/unity-garden/` に追加した。旧実験のP0〜P11とは別のG0設計フェーズとして扱う。
- 庭の状態と専用乱数による動的6面、水・風・光・種、局所格子の自然モデル、意図の先行記録、独立したオラクル、自然な短文、神聖なFX/SEを定義した。
- スマホ（iOS/Android）とSteam向けWindowsを共通C#モデルから作る責務分担、入力・品質設定・保存・中断回復・配布ゲートを文書化した。
- 可変の配面に合わせた `p_t=該当面数/6`、結果既知/欠測/未提示の区別、再抽選・二重適用防止、実イベントに基づく文章の条件を定義した。
- 約3日での一台の展示から、各プラットフォームの製品化へ進むG1〜G5の受け入れ条件を作成した。READMEから設計へ案内した。
- 独立した設計レビューで、recipe/event ID、意図変更期限、モデル停止と計算コピー、取得前の取消、入力の継続tick・対象セル、整数保存範囲、文章の成立条件を照合・修正した。

### 完了基準の結果（実際のコマンド出力）

検証環境: Node `v24.22.1` / pnpm `10.34.9`。既存CIのpnpm 10系列に合わせ、`npx --yes --package=pnpm@10.34.9 pnpm <script>`で実行した。package.json・lockfileの変更はない。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
Test Files  41 passed (41)
     Tests  183 passed (183)
exit 0

pnpm build
> tsc -b && vite build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 184ms
exit 0

文書検証
Markdown files: 7; local links: 27; JSON examples: 1; errors: 0
git diff --cached --check: exit 0
Scope: 8 documentation files; PROGRESS append-only
```

- ビルド時に `jeep-sqlite` 依存の `crypto` browser externalization警告あり。ビルドは成功し、該当する実装・依存関係は今回の変更対象外。
- 初回の環境既定pnpm 11は、依存のbuild-script保護によりコマンドが停止した。許可設定を変更せず、CIと同じpnpm 10系列で上記コマンドを実行して成功した。生成されたworkspace設定はコミットに含めない。
- 説明用JSONの6面、recipe ID、意図に対応する面集合、基準確率の分子、一致判定を確認した。
- この変更は文書のみ。Unityビルド、Unityテスト、実機動作、UI確認、ストア申請は未実施であり、ゲーム完成の証拠とはしない。

### 不変条件の確認
- 既存 `DESIGN.md` / `PROTOCOL_FREEZE.md` / `AGENTS.md`、runtime、台帳、統計、依存関係、CIは変更していない。
- 新Unity企画は別のgameId/runId・保存領域を使う設計で、登録済み実験へ6面の記録を混在させない。
- 文書にも、意図確定後の乱数取得、実source記録、追記履歴、欠測の保持、tamper-evident表現を明記した。

### 要確定・申し送り
- 次の実装入口はG1。正式タイトル、展示端末、正確なUnityパッチ・パッケージ、対応する最低端末・OS、公開識別子は未確定。
- 地図・入力の量/範囲/時間、成長係数、文章集は初期案として記述し、G2/G3で実際のfixtureと体験から固定する。
- 展示ビルドの成立と、iOS/Android/Steamの一般公開は別の完了条件。3日で全ストア公開済みになるとは扱わない。
- 旧アプリのIssue #16にある外部資格情報・実配布の残件は、このUnity設計では解決していない。

### 触ったファイル
- `README.md`
- `docs/unity-garden/README.md`
- `docs/unity-garden/GAMEPLAY.md`
- `docs/unity-garden/SIMULATION.md`
- `docs/unity-garden/DATA_CONTRACT.md`
- `docs/unity-garden/ARCHITECTURE.md`
- `docs/unity-garden/ROADMAP.md`
- `PROGRESS.md`

---

## G0: Unity箱庭設計 v0.2 — 仕組みの強化 (2026-11-11)

### 完了したこと

- ユーザーの「仕組みなどの強化ブラッシュアップ」の依頼に対応し、同じG0設計の改訂として体験・自然モデル・文章・保存の契約を具体化した。Unity runtimeや既存アプリの先回り実装は行っていない。
- 動的配面の必要度を、庭全体の平均から各レシピの直接作用範囲・実効doseに変更。系統重みは候補レシピの平均とし、3系統以上・同系統3面以下・同レシピ2面以下を維持した。
- 水2レシピを同じ総量1200の集中／分散入力にし、種は中心／外側の非重複範囲へ配置する候補とした。風と光も範囲・強さの違いを定義した。
- 水深単位、流出予算、最大剰余配分、一つの排出口、吸水、蒸発の端数、乾燥／適湿／過湿の成長曲線、段階閾値をprofile JSONに整理した。
- 風と湿りの6例、水の保存・排出口・量子化、種と光の順序、蒸発要求を蓄積しないこと、遮蔽を含む16件の参照算術と期待値をJSON化した。全庭やUnityの実行結果ではない。
- オリジナル6枚に対応する24本の条件付き日本語候補（入力fallback4本を含む）を追加。事実の成立判定→表示group→カードの焦点→優先度・重複回避の順序を具体化した。
- 言葉・光・音の主焦点を共通化し、強い注目点1、副焦点1以下、鐘1回、自然音1系統、成長アクセント1回以下を初期上限とした。発見は既存イベントから自動で再生成する表示に留めた。
- 配面・出目の乱数材料を解釈前に保存する境界、カードだけ失敗した場合の出目保全、取消競合、不完全末尾から新segmentへ継続する回復手順、content実体を先に保存する順序を定義した。
- 文章の重複判定はPreparedより前の直近3Resolvedへ固定し、遅延した提示callbackで変わらないようにした。提示記録を読了判定や次回開始の必須条件にしない。
- グラフを庭の推移・意図と出目・一投の計算上のつながりへ整理。p=0、欠測、比較対象数、目標変更などの母数を同じ投影から示す。
- G1は一つのScene、coordinator、local RNG、journal writer/readerで始める。初期はjournal走査で復元し、汎用frameworkや永続cacheを先に作らない。

### 完了基準の結果（実際のコマンド出力）

検証環境: Node `v24.22.1` / pnpm `10.34.9`。既存CIのpnpm 10系列に合わせ、`npx --yes --package=pnpm@10.34.9 pnpm <script>`で実行。依存・lock・実装コードは未変更。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
Test Files  41 passed (41)
     Tests  183 passed (183)
exit 0

pnpm build
> tsc -b && vite build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 234ms
exit 0

設計の参照算術（Python整数演算、Unity実行ではない）
caseCount: 16
referenceArithmetic: all explicit assertions passed
unityTested: false

配面の分岐全列挙（Fraction、抽選標本ではない）
all weights 100: mean water faces = 3/2
water weights 300, others 100: mean water faces = 187067/81675
recipe-count states: 676 per scenario; total probability: 1

設計資料の検証
Markdown files: 8; local links: 49; JSON examples: 1; JSON design files: 3
Catalog: 24 unique templates / 4 input fallbacks / 6 cards; all predicates defined
Recipes: 8; equal water total=1200; non-overlapping seed bands; profile thresholds consistent
Scope: 12 documentation/data files; PROGRESS append-only
Errors: 0
git diff --check: exit 0
```

- 既存の`jeep-sqlite`依存による`crypto` browser externalization警告あり。ビルドは成功し、今回その依存・実装は変更していない。
- 参照算術は設計候補の小さな成分例。Unity C#、16×16展示庭、200回の最大注水、Mono/IL2CPP、実機、FX/SEの試験は未実施。
- 同じ例の数値が出たことだけで、モデルが実地形で安定する、美しく見える、一定時間で必ず開花するという合格判定はしていない。

### 不変条件の確認

- 既存`DESIGN.md` / `PROTOCOL_FREEZE.md` / `AGENTS.md`、runtime、台帳、統計、依存関係、CIを変更していない。
- Unity企画は別のgameId/runId・保存領域の設計。旧365日実験の確率・乱数変換・確証解析へ混在させていない。
- PROGRESSは既存内容を保持して末尾へ追記。設計候補と実装済み・実測済みを区別した。
- カード・文章・演出は結果を書き換えない。欠測を成功に補完せず、既知出目をカード失敗や未提示で除外しない。

### 要確定・申し送り

- 次の入口はG1。展示端末、正確なUnity Editor/パッケージ、署名・配布環境は未確定。
- profileの数値は`designCandidate`。G2で16×16の実地図を作り、200回最大注水、複数の公平な入力列、局所変化と成長の見やすさを確認して採用版を固定する。
- 経路の由来追跡は最小版に含めない。必要な証拠が未実装の文章は成立としない。通常の正の流量の集約と、特定の根までの経路検証を区別する。
- 24文の読みやすさ、実際のメッセージ比率、静かなFX/SE、グラフの可読性、来場者交代はG3の実機で確認する。
- 外部乱数provider、音声文字起こし、同期、ストア一般公開は展示の成立条件へ加えていない。旧アプリのIssue #16も別の残件のまま。

### 触ったファイル

- `README.md`
- `docs/unity-garden/README.md`
- `docs/unity-garden/GAMEPLAY.md`
- `docs/unity-garden/SIMULATION.md`
- `docs/unity-garden/DATA_CONTRACT.md`
- `docs/unity-garden/ARCHITECTURE.md`
- `docs/unity-garden/ROADMAP.md`
- `docs/unity-garden/VALIDATION.md`
- `docs/unity-garden/content/simulation-profile.v0.2.json`
- `docs/unity-garden/content/simulation-fixtures.v0.2.json`
- `docs/unity-garden/content/oracle-catalog.v0.2.json`
- `PROGRESS.md`

---

## G0: Unity箱庭設計 v0.3 — 庭の配置と一投の体験 (2026-11-11)

### 完了したこと

- 続けて設計を磨く依頼に対応し、G0の設計資料・候補データを改訂した。Unity runtime、既存アプリ、依存関係、CIの実装は追加・変更していない。
- 16×16の初期庭「静かな流域」をJSONへ具体化。浅い水入口、乾湿の違い、岩、石の流路、四つの初期植物、4作用点、一つの排出口を固定し、同じJSONからSVG配置図を作成した。
- v0.2の計算profileと16成分fixtureを変更せず、全庭の独立したPython整数参照を実行。8単発、7投の順序比較、drop/rain各200投、事前に固定したseed 0〜15各24投の全結果をreportへ残した。
- 同じ水の一投目・二投目で、入口吸水160→47、光中心の土の吸水35→39を得た。初稿から入口1セルの容量・初期水だけを調整し、その調整内容と同じseed集合を使ったことを明記した。
- 種→柔らかい光と逆順は、同じ80tick・最終土中水566で成長866／400。風の同距離2地点は、岩陰と露出で土中水950→910／950→850になった。数値の単位・基礎成長・経路証拠の限界も記載した。
- 初回の案内、横画面の構成、8つのUI状態、面番号と系統への意図、設定の持続、声の自己申告と手入力の範囲、演出省略、履歴、来場者交代をEXPERIENCEへまとめた。毎投の確認やカード開封を要求しない。
- Coreの確定終状態と途中再生を区別。画面が次のPreparedのbeforeRevision/hashに対応してから配面を公開し、新しいpressだけを受け付ける。省略・閉じる・連打を次回の投擲予約にしない。
- 同runの未終端roundを最大一つとし、異なるroundIdから同じ庭へ並行解決する経路を閉じた。開始状態参照とjournal headを区別し、後着の提示記録だけで状態不整合としない。
- 全域のSurfaceFlowObservedは一つのglobal groupに固定。文章の完全な根拠束の同点、二粒の種の必要な補助焦点、配面の列挙・乱数消費順、種のID、stateHashの対象を具体化した。
- 全投擲で進む40tick・基礎光・開始状態と、今回の局所入力を関係図で区別。入力範囲外の成長を、同roundという理由だけで今回の出目の直接効果へ結ばない。
- 文書・mapはv0.3、数式profile・成分fixture・24文の本文はv0.2を維持。catalogはselectionPolicyVersionだけをoracle-focus.v0.3へ更新した。

### 完了基準の結果（実際のコマンド出力）

検証環境: Node `v24.22.1` / pnpm `10.34.9`。既存CIのpnpm 10系列に合わせ、`npx --yes --package=pnpm@10.34.9 pnpm <script>`で実行。既存コードと依存・lockは未変更。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
Test Files  41 passed (41)
     Tests  183 passed (183)
exit 0

pnpm build
> tsc -b && vite build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 195ms
exit 0

全庭の設計用参照算術（Python整数演算。Unity実行ではない）
totalRoundResolutions: 799
totalTickUpdates: 31960
allTickWaterBalancesZero: true
allStatesInProfileBounds: true
mappingCapsCheckedForRounds: 384
oracleCardFacesDrawn: 384
fullEventAggregatorExecuted: false
oracleMessageSelectionExecuted: false

python check_design_v03.py
Markdown files: 10; local links: 79; JSON examples: 1; JSON design files: 5
Catalog: 24 unique templates / 4 input fallbacks / 6 cards; all predicates defined
Recipes: 8; equal water total=1200; non-overlapping seed bands; profile thresholds consistent
Scope: 15 documentation/data files; PROGRESS append-only
Errors: 0
v0.3 data: 256 cells / 4 initial plants / 4 anchors / 1 drain / 7 observation edges; map, expanded cells, profile and reference hashes match
Reference report: 799 resolutions / 31960 ticks; all 16 runs retained; 384 face/recipe/card conversions and mapping caps checked
Version isolation: numeric profile and 16 component fixtures unchanged; 24 catalog bodies unchanged, selector metadata only
SVG: map SHA and all cell/anchor/plant/observation-edge metadata match
v0.3 checks: passed; Unity runtime/device not tested

git diff --check: exit 0
```

- 既存の`jeep-sqlite`依存による`crypto` browser externalization警告あり。ビルドは成功し、該当依存は変更していない。
- SVGを描画して確認し、方角表示と見出しの重なり、作用点と初期植物の印の重なりを修正した。Unity画面の確認ではない。
- 参照reportのhashは診断用の整数行列hash。正式なWorldSnapshot.stateHash、content hash、journal hashとは区別している。
- 別の読み取りレビューで、候補順・乱数消費順、profileの計算順と係数、799投の内訳、種と光の結果、map/profile/scriptのhashを照合した。独立した第二シミュレータの一致試験ではない。

### 不変条件の確認

- `DESIGN.md` / `PROTOCOL_FREEZE.md` / `AGENTS.md`、既存runtime、台帳、統計、依存関係、CIを変更していない。旧実験の確証解析と新しいD6ゲームの設計を混在させていない。
- `PROGRESS.md`は直前mainの全内容を保持し末尾追記。各データをdesignCandidateとし、設計参照・実装済み・実機確認を区別した。
- 配面と出目を意図・カード・開花・演出の都合で補正していない。全16列を記録し、成長が進まない投擲も除外していない。
- 保存済み結果の再抽選、カード欠測による既知出目の除外、読了確認、保存確認を毎投要求する経路を追加していない。

### 要確定・申し送り

- 次の実装入口はG1。一つの展示対象へ起動できるUnity projectを作り、記録と復元の最小経路を通してから、G2で同じmap/profile/入力をC#で照合する。
- Pythonの31,960tickで水収支・範囲を確認したが、Unity/Core一致、C# checked Int64、保存障害、全イベント集約・文章選定、Mono/IL2CPP、スマホ・Steam実機は未検証。
- 200連続注水の初排出はdrop41投目/rain37投目、累計排出35,082/40,116。注入直後の水深とtick完了後の水深の描画、無限継続での安定性、美術上許容できる水位は別の確認事項。
- 16列の初開花2〜3投は、初期つぼみと全庭の基礎成長の寄与を含む。最終花5〜13、整数成長のない投擲は合計16回、24投では全列排出0。この集合はmap調整にも使っており、独立した検証標本・実プレイの成功率・祈りの効果の根拠ではない。
- 初期配置、横画面の寸法、再生速度、静かなFX/SEは候補。G3で三つの自然観察場面とEXPERIENCEの10場面を実機確認し、美しさ・可読性・操作の静けさを判断する。
- 外部乱数、音声文字起こし、同期、ストア一般公開は最小展示の成立条件へ追加していない。展示端末、Editor/パッケージの固定、署名・配布環境と旧Issue #16は引き続き別の残件。

### 触ったファイル

- `README.md`
- `docs/unity-garden/README.md`
- `docs/unity-garden/EXPERIENCE.md`
- `docs/unity-garden/GARDEN_LAYOUT.md`
- `docs/unity-garden/GAMEPLAY.md`
- `docs/unity-garden/SIMULATION.md`
- `docs/unity-garden/DATA_CONTRACT.md`
- `docs/unity-garden/ARCHITECTURE.md`
- `docs/unity-garden/ROADMAP.md`
- `docs/unity-garden/VALIDATION.md`
- `docs/unity-garden/content/garden-map.v0.3.json`
- `docs/unity-garden/content/garden-reference-report.v0.3.json`
- `docs/unity-garden/content/garden-layout.v0.3.svg`
- `docs/unity-garden/content/oracle-catalog.v0.2.json`
- `PROGRESS.md`

## 2026-11-11 — Gate0: 天候と庭・配面・言葉の関係を設計v0.4へ更新

### 目的・依頼

- 「さらに関係性をブラッシュアップして設計を更新」「天候もあると嬉しい」という依頼に対応。
- 晴れ・薄曇り・小雨を庭全体の条件とし、その中へサイコロの局所入力が加わる関係を具体化した。G0の設計更新であり、Unity projectやruntimeの実装を完了したものではない。
- 直前main `d9bd112b82cf0229461bb5bb1b7243db33088e6e` を基点とし、README・本記録・Unity設計文書と候補JSONだけを変更する。

### 設計と候補データの変更

- `WEATHER.md`を関係性の入口として追加。天候・開始時の庭・配面・局所入力・次の庭の計算依存図、持ち越す水分・成長・占有、カード・言葉・音の役割をまとめた。
- 天候は晴れ（光500・風0）、薄曇り（光350・東風100）、小雨（光250・風0・実注水）の3種類。天候水は全非solidへ各1をoffset0,4,…,36に加える。現在mapは253セルで2,530/投、ダイス最大1,200との合計3,730/投。
- 天候とダイス水を同tickに合算して一回の自然更新へ渡す。光・風は遮蔽前に合成・上限適用し、その後に遮蔽する。天候だけの場・合成場・ダイスの追加実効分を分ける。
- 配面の光needは今回の天候の実効基礎光から計算。前回meanLightは観察値として残し、今回の明るさへ再投入しない。風のdoseもダイス追加分を使う。水・風のneedは開始時の実水分を使い、未来の雨や開花を先読みしない。
- 一期は正常に解決・永続化した3投。初期晴れの重み256/0/0、以後96/96/64、同じ天候の連続を許可。専用32byteを同一期で一度保存し、初期も共通のUniformBelow(256)で4byte消費する。
- WeatherEntropyCommittedはrun単位、同一期で一意。取消・欠測・待機・履歴・再生では期を進めず、保存済み材料を準備の再開・やり直しで再利用する。PreparedにWeatherContextを固定し、天候と6面を同時公開してから意図を添える。
- WaterEnteredをsourceKind=dice/weatherで分ける。源別注入は記録し、混合後の吸水・成長へ水源割合を作らない。LightReceived/WindReceived/WindShelteredはダイス追加分の証拠を使い、天候だけで直接作用があったことにしない。
- 新catalogは既存24entryをそのまま保持して4文を追加した28本。雨と光・風・種・以前の種の発芽を、同じ場所と実注入tickの条件で結ぶ。雨pulseのないtickを補完しない。全イベント集約・文章選定器自体は未実装。
- 天候の「小雨」とダイス入力の「小さな雨」を区別。天候音も自然音loop最大1本に含め、主焦点1・補助最大1・鐘1・成長アクセント最大1を保つ。
- 庭の歩みに天候帯、一投の詳細に開始状態・天候・ダイスの三つの入口、植物詳細に同じplantIdの段階履歴を加える。記録は既存journalから導出し、新しい確認操作・報酬台帳を増やさない。
- 基礎profile v0.2・map v0.3へweather extension v0.4を組み合わせる。旧profile・fixture・catalog v0.2・map・report v0.3・配置SVGは変更せず、旧結果を当時の規則の比較資料として保持する。

### 設計用の自然計算

Pythonの整数参照で808投・32,320tickを実行した。Unityの実行ではない。

| 集合 | 投数 | tick数 | 確認した結果 |
|---|---:|---:|---|
| 同じ初期庭×三天候×8入力 | 24 | 960 | 晴れ8単発はv0.3の数値状態hash・主要量と一致 |
| 強制小雨＋water_drop / water_rain各200投 | 400 | 16,000 | 両方とも初排水7投目、源別入力と全tick収支を保持 |
| 固定seed0～15、各24投 | 384 | 15,360 | 全16列・384配面・128天候期を掲載 |
| 合計 | 808 | 32,320 | 全tickで水収支残差0、状態範囲内 |

- 光中心のつぼみは初期growth1,700。同じlight_beam一回で晴れ2,300・薄曇り2,210・小雨2,150。初期の同recipe候補重みは240/258/270。天候の水だけで成長が有利になるという期待値は置かなかった。
- 薄曇りの風100＋ダイス風1,000は合成1,000・追加900。遮蔽250なら合成250・天候25・追加225。前回meanLightを0／1,000へ変えても同じ現在の薄曇りで全8重みが一致した。
- 強制小雨200投の累計排出はdrop408,192／rain408,296。注入直後を含む最大水深1,265／237、tick後最大665／232。最終全庭土中水は両方54,200で全土の容量へ達した。無限継続・美術上の水位・遊び心地の合格を意味しない。
- 通常16列の初開花2〜3投、最終花4〜13、最終植物10〜21、整数成長のない投擲13回、土中水の観測範囲0〜1,000。天候128期は晴れ66・薄曇り39・小雨23で、晴れには固定初期期16期を含む。
- 別の読み取り照合で全128期の生材料と先頭wordの変換、4byte消費、3投の再利用、profile/map/script hashを確認した。先頭wordの末尾byteがbucketになること、初期も同じ経路を使うことを確認。
- 参照script `weather_reference_v04.py` のSHA-256: `7d06475f01d5ead869b384eecbeb5e45692cb7d8c29ee6573602c756f4ebafc5`。元の自然kernelを継承しており、独立した第二シミュレータとの一致試験ではない。診断用numeric hashは正式なWorldSnapshot.stateHashではない。

### 実行した確認

Node v24.22.1、pnpm 10.34.9。既存package scriptsは `npx --yes --package=pnpm@10.34.9 pnpm <script>` で実行。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
> vitest run
Test Files  41 passed (41)
Tests       183 passed (183)
Duration    2.09s
exit 0

pnpm build
> tsc -b && vite build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 239ms
exit 0

天候を含むPython参照算術
totalRoundResolutions: 808
totalTickUpdates: 32320
allTickWaterBalancesZero: true
allStatesInProfileBounds: true
mappingCapsCheckedForRounds: 384
weatherPeriodsSelected: 128
fullEventAggregatorExecuted: false
oracleMessageSelectionExecuted: false

python check_design_v04.py（保存済みデータの静的照合）
Markdown: 12 files / 108 local links / 1 JSON examples / 79 tables / 0 isolated row blocks
JSON: all 8 design files parse; all status=designCandidate
Oracle: 28 unique entries / 4 input fallbacks / 6 cards / all predicates defined / old 24 entries exactly preserved
Legacy: 6 profile/fixture/catalog/map/report/SVG files byte-identical to base
Hashes/profile: 7 content/script references match
Weather: all 128 periods checked (16 initial); raw 32 bytes / first big-endian word % 256 / 4 bytes used / 3-round reuse
Fixtures: 384 assignments / 2304 face slots / 384 D6 / 384 independent cards; caps, chosen recipes and source water totals match
Clear regression: all 8 old numeric hashes and 16 primary result fields per recipe match
Water/report: all stored source sums and aggregate balances match
Summary: all 11 fields match
Scope: 15/15 authorized documentation/data paths; no runtime changes
Errors: 0

git diff --check: exit 0
```

- buildには既存jeep-sqlite依存のcrypto browser externalization警告がある。成功しており、依存やbuild設定は変更していない。
- DATA契約・SIMULATION・GAMEPLAY・EXPERIENCE・WEATHER間で、天候の時点、同cell/tickの条件、源別証拠、同一期の再利用、自然音の上限を読み取りレビューした。DTO表の孤立行も修正した。

### 不変条件・申し送り

- DESIGN.md / PROTOCOL_FREEZE.md / AGENTS.md、既存runtime・台帳・統計・依存・CIを変更していない。PROGRESSは直前mainを全て保持した末尾追記。
- 既知の出目を欠測へ戻すこと、保存済み天候を引き直すこと、意図・カード・文章に合わせて出目や成長を補正することは認めない。参照結果も再抽選・選別していない。
- 次の実装入口はG1。天候を含む一つの準備・保存・復元を通し、G2で同じ入力をC#へ移してからG3で見た目・音・履歴へ接続する。
- Unity/C# checked Int64、天候材料の永続化・取消競合・再開、全イベント集約、文章選定、Mono/IL2CPP、スマホ・Steam実機は未検証。EXPERIENCEの14場面は実装後の受け入れ条件。
- 植物の水消費・新しい影、気温・雪・雷・季節・現実の天候API・雲の流体は最小版へ追加しない。三天候を既存の自然更新へつなげる範囲に収める。
- 初開花は初期つぼみの寄与を含む設計fixtureの結果。独立した検証標本、実プレイの成功率、祈りの効果の根拠として扱わない。

### 触ったファイル

- `README.md`
- `docs/unity-garden/README.md`
- `docs/unity-garden/WEATHER.md`
- `docs/unity-garden/GAMEPLAY.md`
- `docs/unity-garden/SIMULATION.md`
- `docs/unity-garden/DATA_CONTRACT.md`
- `docs/unity-garden/EXPERIENCE.md`
- `docs/unity-garden/GARDEN_LAYOUT.md`
- `docs/unity-garden/ARCHITECTURE.md`
- `docs/unity-garden/ROADMAP.md`
- `docs/unity-garden/VALIDATION.md`
- `docs/unity-garden/content/weather-profile.v0.4.json`
- `docs/unity-garden/content/weather-reference-report.v0.4.json`
- `docs/unity-garden/content/oracle-catalog.v0.4.json`
- `PROGRESS.md`

## Gate0: 育った庭の観察・文章の選び分け・静かな提示を設計v0.5へ更新 (2026-11-11)

### 完了したこと

- 「さらにブラッシュアップして設計更新」という依頼に対応。直前main `80983f926766593138a7a3d4a5edc935fa6ca108` を基点に、G0の設計文書と候補JSONだけを更新した。
- 反復して振ったときに同じ定常文へ偏ること、種の範囲が埋まった理由が見えないこと、土の湿りと描画量の曖昧さ、skip・中断で未再生cueをどう終えるかを具体化した。
- `oracle-focus.v0.5`の最終順位はcardAffinity / recentNovelty / priorityの降順。既存28entryの本文・条件・cardTags・priorityは全て保持し、同じカード対応なら最近使っていない成立文を先に選ぶ。唯一の事実や全候補既出では同文を許可する。
- 最大3groupの枠を、固定重要度1位／全文章候補の最終順位1位のgroup／ダイス直接入力の重要度1位とした。候補を途中で絞るために未使用文が消えることを防ぐ。主1・補助最大1、seed pairの必須相手、全支持根拠、カード未成立fallbackを維持。
- `scoreTuple`の並びと選定版を固定し、旧runの本文・焦点を新しい規則で置換しない。新規DTOフィールド、乱数、文章の自由生成は追加していない。
- 現mapの種範囲を列挙し、20土セル・初期占有3・新規最大17・全植物21、範囲外の空き土34を確認。種が今回の配面に入らなかった場合と、一粒の空き0・二粒の空き0/1を任意説明で区別する。
- 説明は公開中Preparedの開始snapshotと固定map/profileから導く投影とした。見た目の途中状態から理由を作らず、範囲外の空き土を隠す・anchorを移す・目標を変える処理を加えない。
- 土の湿りはsoilWater/soilCapacityの比、水面はsurfaceWater、葉の揺れは再生中の合成風、局所照射は追加実効光、植物形状は保存段階へ結ぶ。待機時は公開済み天候の基礎場へ戻り、前回の局所入力を持ち越さない。
- 全21個体がflower/growth2500の合成境界fixtureを文書化。追加17個体は開発用初期配置で、過去の種を引いた履歴やSeedPlacedを作らない。三天候×非種6入力の18ケースは実装後の受け入れ条件で、今回未実行。
- Presentation内だけのownerToken・cursor・cue管理を定義。skip・提示中断・run切替では未発火の音と注目の光を破棄し、保存済みの庭と本文へ静かに収束する。遅延した旧callbackを新しい天候へ採用しない。
- 低fpsでも一描画更新で強調cue最大1件。同tickの未発火cueを次frameへ残し、採用文の束縛と元eventの個体・tickを使う。group.firstTickを別個体の開花時刻の代理にしない。成長アクセントは合計最大1回。
- G1〜G3の通常新runは静的mapから作り、同run復帰はsnapshot・meanLight・revision・天候を保持する。別run snapshotの引継ぎは後続拡張へ整理し、未定義のimportを展示の必須実装にしない。
- README、天候の案内、アーキテクチャ、ロードマップ、受け入れ条件を同期。自然係数・map・三天候・8入力・カード6枚・出目の確率は変更していない。

### 完了基準の結果

既存アプリの確認は `npx --yes --package=pnpm@10.34.9 pnpm <script>` で実行した。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
> vitest run
Test Files  41 passed (41)
Tests       183 passed (183)
Duration    2.19s
exit 0

pnpm build
> tsc -b && vite build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 189ms
exit 0

Selector v0.5: 15 authored synthetic cases / 75 selection checks; all expected results match
Order invariance: group, binding and prior-record reversals preserve winner, score, focus, support and history
Catalog: all 28 v0.4 entries preserved exactly; only v0.5 metadata changed
Not executed: natural simulation, predicate evaluator, journal I/O, Unity, device/audio checks

Static map coverage: 55 plantable / 20 in seed bands / 3 initially occupied there / 17 addable / 21 total plants maximum
Outside bands: 35 plantable / 1 initial plant / 34 empty after both bands are full
Saved v0.4 seed 1: single last at round 15, scatter last at round 20; rounds 21-24 contain no seed faces
Synthetic mature fixture definition: 21 unique IDs / 18 future weather-recipe cases; physics and rendering NOT executed
Moisture display examples: 160/200 -> 800; 200/1000 -> 200
Repository files written: 0; errors: 0

python check_design_v05.py
Markdown: 12 files / 117 local links / 1 JSON examples / 89 tables / 0 isolated row blocks
JSON: all 10 design files parse; all status=designCandidate
Oracle: 28 unique entries / 4 input fallbacks / 6 cards; old 28 entries exactly preserved
Legacy: all 8 earlier JSON files and 1 SVG byte-identical to base
Selector fixtures: 15 authored cases / 75 read-only checks match every expected field; catalog/script hashes match
Selector independent properties: 11 global winners, score tuples, complete bindings and history windows match; no predicate truth evaluation
Scope: 13 authorized documentation/data paths; no runtime changes; git diff --check passed
Errors: 0
```

- buildには既存jeep-sqliteのcrypto browser externalization警告がある。成功しており、依存・build設定は変更していない。
- 選定15例は、未使用優先、全既出、唯一事実、カード優先・不一致、候補切捨て、同点、完全束縛、必須補助、不適合除外、カード欠測、適用証拠欠損、Prepared前の直近3Resolvedを含む。各例は元の順・group逆順・binding逆順・履歴逆順・全逆順の5通りで比較した。
- 期待する勝者・score・主補助・支持group・履歴集合は手で指定した。fixtureが渡す述語の真偽と束縛キーは合成入力であり、実際の雨・cell・tick・植物からそれらが正しいと判定する処理は今回実行していない。
- 参照script `oracle_selection_reference_v05.py` のSHA-256は `cdc4f3acd815cbf0c9cc09128f4f6f14d39bdf04235792f6fe01457a38f5cb42`、catalogのSHA-256は `59e377831723d6e58928d5a0d51ff1ecb7e8ef37f2960087e1b2620ad156235c`。JSONへ版・範囲・全入力・期待値・結果を残した。
- 静的な種範囲と保存済みseed=1の列を読み直した。旧v0.4の808投・32,320tickの自然計算は再実行しておらず、新しい実行数として重複計上していない。

### 不変条件と要確定・申し送り

- `DESIGN.md` / `PROTOCOL_FREEZE.md` / `AGENTS.md`、既存runtime・台帳・統計・依存・CIは未変更。PROGRESSは直前mainの全内容を保つ末尾追記。
- 旧8JSONと配置SVGをbyte単位で保持。現行は基礎profile v0.2、map v0.3、天候・配面・group生成v0.4、catalog・選定v0.5の組合せ。
- 次の実装はG1から継続。G2で自然Core、G3で実イベントからの文章候補と表示を接続する。選定部分の合成例が通ることだけで、全文章機能やゲーム体験の合格とはしない。
- 成熟庭18ケース、EXPERIENCEの18操作場面、実際の土の色・水面・植物・音、保存障害や中断、Unity/C#・スマホ・Steam実機は未検証。候補4秒の再生時間は低fps時に延長でき、美しさと読み取りやすさは実機で判断する。
- 全植物21は現在mapと固定seed帯の上限であり、通常プレイで全個体が開花する保証ではない。整数成長0だけから乾燥・過湿・失敗という理由を作らず、実際に残る状態と出来事から説明する。

### 触ったファイル

- `README.md`
- `docs/unity-garden/README.md`
- `docs/unity-garden/GAMEPLAY.md`
- `docs/unity-garden/DATA_CONTRACT.md`
- `docs/unity-garden/EXPERIENCE.md`
- `docs/unity-garden/GARDEN_LAYOUT.md`
- `docs/unity-garden/ARCHITECTURE.md`
- `docs/unity-garden/ROADMAP.md`
- `docs/unity-garden/VALIDATION.md`
- `docs/unity-garden/WEATHER.md`
- `docs/unity-garden/content/oracle-catalog.v0.5.json`
- `docs/unity-garden/content/oracle-selection-fixtures.v0.5.json`
- `PROGRESS.md`


## Gate0: 場所・履歴からそれぞれの庭が育つ設計 v0.6 (2026-11-11)

### 完了したこと

- 依頼「やる人によって無数のパターンがしっかり生まれるように」に対応し、新Unity箱庭のG0設計を更新。DIVERSITY.mdを追加し、操作・保存・自然・表現・検証・実装順を同じ規則へ統合した。既存React/Capacitorの実験、登録済み台帳、凍結プロトコル、runtimeコードは変更していない。
- 固定4anchorの収束を見直し、入口85・岩陰135・ひらけた土154の三地点を追加。その回の水・風・光・種の中心を同じ地点へ解決し、既存8recipeの総量・範囲・時間と自然係数を維持。初期地形・水・四植物は共通とした。
- 既定はおまかせ、任意の固定地点は持続。dynamic-faces.v0.6は保存mapping材料の先頭でUniformBelow(3)を必ず消費し、fixedでも同じ場所抽選の根拠を残す。残りから従来14回の配面抽選を行う。場所と配面を同じ準備roleと明記し、天候・D6・カードの独立材料は維持した。
- CareFocusChangedをrun単位で保存し、次のPreparationStartedが設定とsourceRefを固定する。公開済みの今回の場所・6面・確率は変更しない。「今回／次から」を表示し、Ready・Presenting以外では選択を受け付けない。提示終了・省略と終状態表示の後、受け付け済み場所commandを先に保存して次を準備する。非公開の先行準備による追越しを禁止した。
- 花の外観は同じ個体のSeedPlaced.soilQAtPlacementと同じ回のWeatherContextから導く表示projectionとし、三配色×三花弁幅を候補化。生態係数・新乱数・自然状態フィールド・専用journalは追加しない。初期個体は中立、証拠不足はunknownとして扱い、style原本と版をrunへ固定する。G3の任意表示として物理分岐の後に置いた。
- 三地点の種範囲は和集合40土セル、初期占有4、追加候補36、静的全個体上限40。範囲外15セルを含め正確に記載。到達保証はせず、二粒に必要な空き数と距離/cellId順による残りを認める。旧21個体の境界は当時の資料として保持した。
- 固定seed0〜15×5方針×24投の全80列を、ID・外観を除く整数状態と粗い空間状態で比較。粗い分類と候補基準を結果より先に固定し、似た結果や成長0も除外しない。28文・選定v0.5、天候・group v0.4、基礎数値v0.2を維持した。
- 自己完結したPython参照をdocs/unity-garden/reference/diversity_v06.pyへ追加。標準ライブラリと固定repo JSONのみを読み、既定/--verifyは読み取り専用、--writeは新しいdiversity reportだけを出力する。旧scratchの参照scriptは実行依存にしない。

### 完了基準の結果（実行出力）

既存アプリの必須コマンド。利用可能なpnpmをnpxの固定versionで呼び出した。

```text
$ npx --yes --package=pnpm@10.34.9 pnpm typecheck
> tsc -b --noEmit
exit 0

$ npx --yes --package=pnpm@10.34.9 pnpm test
Test Files  41 passed (41)
     Tests  183 passed (183)
Duration 2.40s
exit 0

$ npx --yes --package=pnpm@10.34.9 pnpm build
vite v8.1.4 building client environment for production...
102 modules transformed.
built in 215ms
exit 0
```

既存のjeep-sqlite由来のcryptoブラウザ外部化警告とnpmのhttp-proxy環境設定警告を確認。依存・build設定・runtimeはこの更新で変更していない。

G0の自然参照は--write成功後、/tmpから絶対pathで--verifyを実行しexit 0。保存reportと再計算内容が一致し、repo外のcwdからも実行できた。

```text
legacy anchor regression: 24 rounds, v0.4 numeric hashes/selected quantities matched
three sites x three weather x eight recipes: 72 rounds
three sites x two input orders x two rounds: 12 rounds
sixteen seeds x five policies x twenty-four rounds: 1920 rounds
exact replay seed0/auto: 24 rounds
TOTAL: 2052 rounds / 82080 ticks
all tick water balances zero; all states in profile bounds
mapping caps checked: 1944 rounds
coarse physical states at rounds 6 / 12 / 24: 80 / 80 / 80 (80 cases each)
auto at round24: 16 distinct coarse states (predeclared minimum8)
fixed-site triple all distinct: 16 of16 seeds at round12 and round24 (minimum8 each)
same weather + water/seed recipe, different sites: all36 pairs differ in coarse state
```

順序比較には差が見えない例も残した。新しい種のgrowthは岩陰1166/500、入口0/0、ひらけた土107/73。粗い状態が異なる対は岩陰だけで、ほかの2対を除外していない。全80列で重複がないことを、全プレイヤーの固有性や実機で見えた美しさの保証へ置き換えていない。

読み取り専用の統合検査でも、保存済み80列の粗い状態を実配列から再集計し、6/12/24投の各80種類、おまかせ16種類、固定三地点16/16を確認。480組の同材料pairの粗い比較と1,920配面の個数制約・選択面を照合した。これらは保存reportの独立再集計であり、追加の自然シミュレーション投数へ数えない。

```text
13 JSON: parse / designCandidate
Markdown: local links / tables / JSON example valid
Legacy: all10 JSON +1 SVG unchanged byte for byte against ade506d
Target profile: three centers / four shared anchors / pinned source hashes
Style profile: soilQ0..1000 partitions exactly once; six authored boundary rows; three weather widths
Seed geometry: 55soil /40union /4initial /36additional upper bound /40total upper bound
Report source hashes and physical recount: pass
Scope: documentation / candidate data / G0 reference only
PROGRESS: append-only; git diff --check: pass
```

主要原本のSHA-256:

- target-profile.v0.6.json: dd72a71da076a597f34085b003ee02f05660d1801410c3df17f9517c91a7cb2a
- diversity-reference-report.v0.6.json: e764c47615c2983336e022a26de1156428f8c0b62d8516d240fe0017b7d25acd (298899 bytes)
- reference/diversity_v06.py: 2e2af0ad82435698d0bb9d4be336a6b93a15071cd252f3f06ba85d2cc63d9096

レビューでUniformBelow(1)の0byte消費、元mapの展開hashとの照合、場所設定aliasとDTOの対応、旧先行準備の記述、出生projectionの過去原本参照を修正してから最終照合した。

### 要確定・申し送り（次フェーズへ）

- **今回もG0設計更新であり、Unityプロジェクト・C#実装・実機画面・SE・配布ビルドは未作成。** 既存アプリの183testsとPython参照を、Unity実装の合格とはしない。
- 参照自然kernelはv0.3/v0.4を継承。独立した第二シミュレータ、全イベント集約、述語/文章選定、journal I/O、保存競合、appearance rendererは今回の自然参照に含まれない。
- G1でCareFocusChangedの冪等保存、提示終了と準備開始の順序、失敗・再開・来場者交代を実装。G2で同入力のC#一致と三地点の局所範囲を照合。G3で今回/次からの読みやすさと実際の分岐の見分けやすさを確認する。
- mature-garden-boundary.v0.6は全40個体をflowerにした開発用の合成状態。三地点×三天候×非種6入力の54ケースは未実行の受け入れ条件で、今回の2052投へ加算しない。旧21個体・18ケースは旧版の境界条件として保持する。
- 9組の花の基準外観の発生頻度・実機での識別、消音/低刺激設定、スマホの画面・性能は未確認。外観を採用しない展示ビルドは中立表示と範囲を明記し、物理分岐と保存を優先する。
- 全ての保存値・名称・色・表示幅はdesignCandidate。新runへ版と実体hashを固定し、旧runの材料へ新しい場所抽選を後付けしない。既存実験の仕様・registered data・統計経路は変更しない。

### 触ったファイル

- README.md、PROGRESS.md（追記）
- docs/unity-garden/README.md、DIVERSITY.md（新規）、GAMEPLAY.md、DATA_CONTRACT.md
- docs/unity-garden/EXPERIENCE.md、GARDEN_LAYOUT.md、SIMULATION.md、WEATHER.md
- docs/unity-garden/ARCHITECTURE.md、ROADMAP.md、VALIDATION.md
- docs/unity-garden/content/target-profile.v0.6.json、plant-appearance-profile.v0.6.json、diversity-reference-report.v0.6.json（新規）
- docs/unity-garden/reference/diversity_v06.py（新規G0参照）


## Gate0: Unity箱庭の全体接続を設計 v0.7 へ更新 (2026-11-12)

### 完了したこと

- 依頼「さらに設計全体を見渡して改善できそうなところを探して改善」に対し、体験・自然・保存契約を横断監査した。新しいゲーム機能より、現行の組合せと実装時に食い違う境界を修正した。今回もG0の設計・候補資料・参照計算だけを扱う。
- `CURRENT_DESIGN.md`と`design-manifest.v0.7.json`を現行の入口へ追加。基礎v0.2、map v0.3、天候v0.4、場所/配面v0.6、文章v0.7の適用順を明記した。原本19件は実行時採用候補5件＋任意外観1件、検証用10件、旧catalog3件へ分類し、byte長・SHA-256を固定。設計原本のhashと、変換後のRunStarted.contentManifestを混同しない。
- 新しいrunLifecycleVersion=run-switch.v0.7を定義。明示的な庭の交代はRunSwitchRequestedで次run IDと初期計画を保存し、旧round終端→RunClosed→指定した新RunStartedへ進む。交代中の通常UI公開と旧取得の内部処理を分け、旧結果を失わず前の人の本文を再公開しない。要求未保存の終了は受理済みとは呼ばない。
- 新genesisの部分frameを保全して同じtargetRunIdへ回復する境界、閉鎖後のlate Receipt拒否、計画作成元plannedByとgenesis作成元の実値の分離を追加した。実際のI/O・プロセス終了による実装試験は将来条件である。
- JSONは対象runのプレイを再構成する監査bundle、CSVは閲覧用projectionと明記。排他下でcutoff headとsegment byte範囲/hashを固定し、書出し中の追記を混ぜない。来場者exportへ交代元の本文を自動同梱せず、範囲外の交代参照はscope_outside_exportとして未照合範囲を残す。
- 旧catalog v0.5の28本文・ID・タグ・priorityを維持し、27entryは同一。岩陰のつぼみを述べる1entryだけにlight_reached_focusと同cell LightReceivedのfocusBindingを追加。oracle-focus.v0.7はv0.5の順位・直近比較・group上限を継承する。旧runの文章は再選定しない。
- 同cellの光/追加光0/別cell/開花/岩根拠欠落/成長0の6つの合成期待例を追加。完全な述語実装や自然計算の結果ではなく、設計の最小根拠と期待値を照合する資料とした。
- 三地点×水2recipe×強制小雨を、初期mapからそれぞれ200投、合計1,200投・48,000tickで参照した。旧入口の400投を新地点の検証へ流用しない。新scriptは固定したv0.6数値kernelをimportし、既定/--verifyは読取比較、--jsonは標準出力だけで保存操作を持たない。
- Readyの場所変更は公開中の一投を保ち、その後の準備から反映する、と要約を統一。既存23受入場面を自動照合・実端末運用・人の知覚へ対応させ、人の通し確認をH1〜H5へまとめた。三地点の同じ種、同材料12投の分岐、数値差があっても同じseed段階の対照をV1〜V3として具体化した。
- 外観未採用のrunは版null・profile参照なしと明記。目標変更の集計は構造化したkind/targetだけを比較し、方法や本文だけの変更を混ぜない。
- 既存実験のAGENTS/DESIGN/PROTOCOL_FREEZE、src、依存、CI設定、過去のcontent・参照scriptを変更していない。

### 完了基準の結果（実際のコマンド出力）

既存アプリの必須gateは、pnpm 10.34.9を`npx --yes --package=pnpm@10.34.9 pnpm ...`で実行した。依存の更新はしていない。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
RUN v4.1.10
Test Files 41 passed (41)
Tests 183 passed (183)
Duration 3.12s
exit 0

pnpm build
vite v8.1.4
102 modules transformed
built in 329ms
exit 0
```

buildは既知のjeep-sqlite由来crypto browser externalization警告を出したが成功。npmのhttp-proxy設定警告もあった。既存Webの成功をUnityの成功とは扱わない。

```text
python docs/unity-garden/reference/check_current_design.py
Verified design v0.7 source index: 19 pinned artifacts; 5 required + 1 optional runtime candidates.
Catalog: 28 entries, 27 unchanged, 1 restricted; 6 synthetic evidence examples consistent.
Stress report arithmetic: 1200 saved rows checked; natural simulation is verified separately.
Documents: 13 Markdown files, 213 local links/anchors, 115 tables.
No files written. Unity, journal recovery, full predicates, rendering and device checks remain unexecuted.
exit 0
```

新しい参照scriptは`/tmp`から絶対パスで実行して再現を確認した。

```text
python /workspace/scratch/fbca1c923960/destiny-dice/docs/unity-garden/reference/water_stress_v07.py --verify
Verified water stress v0.7: 6 runs, 1200 rounds, 48000 ticks; all water balances zero; bounds valid; no files written.
exit 0
```

全6列の全tick・全round・累積で水収支残差0、状態と端数は既定範囲内。注入は各列746,000（天候506,000＋ダイス240,000）。最大セル地表水は注入直後込み1,368、tick終了時769。全6列200投後の土中水は容量合計54,200へ達した。最初の排水は入口7/7投、岩陰5/4投、ひらけた土4/4投（しずく/小さな雨の順）。長雨の過湿や、花が咲かなかった列も保持し、結果に合わせた係数変更はしていない。

参照reportの全1,200行を別の読み取り処理で再集計し、前後の水収支、源別注入和、各列合計、初排水投、最大水位を照合した。これは追加の自然計算1,200投ではなく、保存済みreportの再集計である。旧v0.6の2,052投は今回は再実行せず、原本とhashを保持した。

```text
Legacy content/reference: 15 files byte-identical against 2312460
  13 JSON + 1 SVG + reference/diversity_v06.py
G0 scope: README / PROGRESS append / docs/unity-garden only
App dependencies and frozen experiment protocol: unchanged
DATA: 34 future acceptance items; links / JSON example / tables valid
EXPERIENCE: 23 acceptance IDs covered once by evidence mapping; V1/V2/V3 agree with the v0.6 report
PROGRESS: append-only
Git diff whitespace check: pass
```

新原本のSHA-256:

- oracle-catalog.v0.7.json: c93040df8e1b46934ba352fc578ff0e08c66cf73c67b7a23cb50cd140f1d6210 (14,897 bytes)
- oracle-binding-fixtures.v0.7.json: ff2893093793f6b729bc0774f5e66c66a732f335936c3cdb120a7d9eeed20445 (8,812 bytes)
- reference/water_stress_v07.py: 4e24277234263514791a56d93a6858713289c40071d41275ee5f465c38d2e122 (13,038 bytes)
- water-stress-reference-report.v0.7.json: bfe7df8492cdc8553b05768d47c0fa5ce09b519896a642387e3ab2f919a586c4 (209,106 bytes)

### 要確定・申し送り（次フェーズへ）

- **Unity project・C#・画面・音・実機ビルドは未実装。** 次はCURRENT_DESIGNから現行組合せを確認し、ROADMAPのG1へ進む。今回の設計資料と既存183testsをUnity完成としない。
- RunSwitchRequested・genesis回復・exportのcutoffと範囲分離は設計のみ。G1で実I/O・強制終了・二重command・更新後の対応版・初回genesis（交代要求なし）も確認する。保存していない交代要求を復元できるとは保証しない。
- 自然参照は既存kernelの再利用であり、独立した第二実装との一致ではない。C# checked動作、全SimulationEvent/述語/文章選定、UI cue、永続化、無期限の安定性は未確認。
- G2で新しい6列の全状態・水収支をC#で照合する。G3では全土が満水になる長雨の水面・植物の視認性も確認する。モデル単位を現実のcmや秒の保証へ置換しない。
- 40個体の成熟54ケース、花の外観の実機識別、H1〜H5とV1〜V3は将来の受入で、今回の自然投数に加算しない。初期つぼみ・成長cap・播種範囲・自動リセットなしの方針を維持した。
- 候補manifestの原本hashと実行時に変換したcontent hashを区別する。実体を検証して採用するまで、loaderや保存互換性が完成したとは扱わない。旧runを現在の規則で再解釈しない。

### 触ったファイル

- README.md、PROGRESS.md（追記）
- docs/unity-garden/CURRENT_DESIGN.md（新規）、README.md、ARCHITECTURE.md、DATA_CONTRACT.md
- docs/unity-garden/DIVERSITY.md、EXPERIENCE.md、GAMEPLAY.md、GARDEN_LAYOUT.md
- docs/unity-garden/ROADMAP.md、SIMULATION.md、VALIDATION.md、WEATHER.md
- docs/unity-garden/content/design-manifest.v0.7.json、oracle-catalog.v0.7.json、oracle-binding-fixtures.v0.7.json、water-stress-reference-report.v0.7.json（新規）
- docs/unity-garden/reference/check_current_design.py、water_stress_v07.py（新規G0参照・静的照合）


## Gate0: Unity箱庭 — 初回作成と観察の接続 v0.8 (2026-11-12)

### 完了したこと

ユーザーの「さらに設計全体を見渡して改善できそうなところを探して改善」に対応するG0設計更新。設計の全体を、保存契約・自然の根拠・体験と観察の三方向で再点検した。Unity本体、C#、既存React実験のコード・凍結プロトコル・依存・CI設定は変更していない。

- **初回作成を保存境界へ接続した。** runLifecycleVersion=run-entry.v0.8は正常なrun-switch.v0.7の交代を継承し、最初の庭にも不変のRunCreationPlanを使う。内容保存→plan受理→固定targetRunIdのgenesisの順。planの未受理、genesis途中、完全保存後の応答消失を区別し、原本を残して一つの受理済み計画へ戻る。planの一意性再検査と保存は同じ排他区間に置く。
- **障害後の新庭を明示した経路に限定した。** recoveryFreshは、一意に特定できる現在の故障runを保全し、本人／運営者が明示した場合だけ静的mapから作る。原本診断・検証済みheadまたはnullを保持し、偽のRunClosedや失った出目・snapshotの補完を行わない。通常交代の回復待ち・曖昧な分岐を救済の名目で上書きしない。旧版にplanやentryKindを後付けしない。
- **作成planとexportの範囲を結んだ。** 現在runのplanと未完genesisは監査bundleへ含め、前の故障runの本文・原本診断は来場者exportへ自動展開しない。scope_outside_exportと現在run内の検証範囲を保ち、運営の原本回収と区別した。
- **観察の共通投影を追加した。** OBSERVATION.mdのround-observation.v0.8で、PreparationStarted順のrecordOrdinal、正常解決のworldRevision、比較対象のeligibleOrdinalを分ける。準備中断・取消・結果不明も元の位置を保持し、横軸を「記録の順」とする。cutoff・範囲・filterをmetadataへ固定し、三つの記録画面とCSVに同じ投影を使う。
- **庭の初期点・量・空欄を固定した。** 地表水と土中水、初期分を含む成長合計、初期から／今回の増分、個体数／花数を定義した。初期4個体の成長合計4,400を新しい成果にしない。未Resolvedはnullで線を切り、保存済み成長0は0。既知出目があれば庭の未解決やカード・Receipt欠落を理由に比較から落とさない。
- **目標なしの遊びと比較0件の入口を揃えた。** 庭の歩みを既定にし、未投擲・結果不明・目標なし・該当面なし・filter後0件を短い案内で区別する。0件を0%の成功率へ置き換えず、意図入力を必須にしない。基準の累積は同じ対象集合の整数分子÷6で保持する。
- **局所の自然の関係へ保存根拠を付けた。** simulationEvidenceVersion=local-growth-witness.v0.8として、既存GrowthObserved.firstPositiveGrowthEvidenceへ同cell・plant・tickの蒸発前の水／端数、実効光・風、要求／地表／土の蒸発、成長前後／端数、実適用への参照を加える。一投につき一個体に最大一つ。最初の正の整数成長tickの例を、一投全体や開花tick・風の寄与率へ読み替えない。自分自身のentryHashをpayloadに含める循環参照も避ける。
- **実蒸発0・地表だけの蒸発・成熟を説明へ反映した。** 風と成長が正でも、そのtickに土が乾くとは限らない。成長0や旧版の根拠不足では、確認できる入力・状態・出来事までを表示する。新しい自然イベント、計算式、係数、乱数、catalog本文・順位は追加／変更していない。
- **実装の入口を更新した。** manifest v0.8は24原本を索引化し、必須5＋任意外観1のruntime候補はv0.7と同一の実体を保つ。新しい合成例・参照Pythonは照合用途として分ける。GAMEPLAYの初期景観を芽2・つぼみ2・地表水0・石の流路へ揃え、WEATHERの旧catalog案内を現行v0.7へ修正した。EXPERIENCEの23受け入れ場面とH1〜H5を維持し、既存の履歴・知覚確認へ投影の確認を組み込んだ。

### 完了基準の結果（コマンド出力の要約）

pnpm 10.34.9の既存cacheを使い、`npx --offline --yes --package=pnpm@10.34.9 pnpm ...`で実行した。新規依存の取得・更新は行っていない。

```text
pnpm typecheck
> tsc -b --noEmit
exit 0

pnpm test
RUN v4.1.10
Test Files 41 passed (41)
Tests 183 passed (183)
Duration 3.57s
exit 0

pnpm build
vite v8.1.4
102 modules transformed
built in 293ms
exit 0
```

既知のjeep-sqliteによるcrypto browser externalization警告、npmのhttp-proxy設定警告は今回も出た。build自体は成功。既存Webの確認をUnityの成功とは扱わない。

```text
python docs/unity-garden/reference/check_current_design.py
Verified design v0.8 source index: 24 pinned artifacts; 5 required + 1 optional runtime candidates unchanged.
Catalog: 28 entries, 27 unchanged, 1 restricted; 6 synthetic evidence examples consistent.
Stress report arithmetic: 1200 saved rows checked; natural simulation is verified separately.
Documents: 14 Markdown files, 238 local links/anchors, 124 tables.
Run entry and local evidence policies indexed; projection and scalar fixtures have separate read-only checks.
No files written. Unity, journal recovery, full predicates, rendering and device checks remain unexecuted.
exit 0

python docs/unity-garden/reference/observation_v08.py --verify
Verified observation v0.8: 16 synthetic cases; exact numerator/6, stable positions, null/zero and counts agree.
Inputs assume upstream verified facts. No journal, entropy, nature, graph edges or Unity executed. No files written.
exit 0

python docs/unity-garden/reference/growth_witness_v08.py --verify
Verified growth witness v0.8: 12 scalar goldens; 6 accepted and 13 rejected synthetic bindings.
No files written. Full simulation, first-positive selection, graph projection, journal, C# and Unity remain unexecuted.
exit 0
```

投影の16例は手で指定した期待値と照合し、入力collectionの逆順でも同じ結果だった。`python -O`では成功表示前に停止することも確認した（想定どおりexit 1）。合成要約の各fieldは既にcutoff時点へ絞られたものと仮定する。referenceはPreparationStarted以外の各recordの時刻切出し、raw entropy復元、JCS/hash検証を実装していない。

局所算術12例は正の整数成長6例・成長0の6例。根拠19例は受理6・拒否13で、cell・plant・tick・round・源・実効量・算術不一致を区別した。場は合成資料であり、地図・遮蔽・scheduleから実イベントを生成した証拠ではない。親event.firstTickとの一致と、40tickを通じて本当に最初だったことの検証も区別する。

```text
Preserved 20 baseline content/reference artifacts byte-for-byte against d27c676
  17 JSON + 1 SVG + reference/diversity_v06.py + reference/water_stress_v07.py
G0 scope: README / PROGRESS append / docs/unity-garden only
App dependencies and frozen experiment protocol: unchanged
DATA: 41 future acceptance items; creation/switch/recovery and export scopes specified
EXPERIENCE: 23 acceptance scenes retained; H1-H5 retained
Git diff whitespace check: pass
```

旧自然参照の2,052投・強制注水1,200投は再計算していない。数式・入力原本・旧scriptが無変更であることをbyte比較し、現行checkerで既存注水reportの全1,200行の保存収支を再集計した。新しい単セル例や静的照合を、全庭シミュレーションの追加投数へ加算していない。ledger追記・source保持・意図の先行確定・欠測を補わない既存の不変ルールに反するruntimeコードは追加していない。

### 要確定・申し送り（次フェーズへ）

- **Unity project・C#・画面・音・実機ビルドは未実装。** G1はCURRENT_DESIGNの採用範囲を読み、対象端末の薄い保存・復帰ループから開始する。
- RunCreationPlan／genesis／recoveryFreshのframe byte形式、flush、実際のプロセス間排他と停止点はG1で固定・故障試験する。未受理の初回操作が必ず同じIDに戻るとは保証しない。正常交代の保存境界は維持する。
- G2は実際の40tickから局所witnessを生成し、同じ自然終状態・最初の正のtick・場の出所が一致することをC#で照合する。G0の単セル例を全イベント集約やC# checkedの合格にしない。
- G3は検証済みの実journalを同じcutoffへreduceし、JSON監査bundleから画面・CSVへ同じ投影を再構成する。今回のcheckerは上流で検証済みと仮定した合成要約を使う。
- 実装後の41受け入れ、EXPERIENCEの23場面、成熟庭54ケース、出生時外観、H1〜H5、V1〜V3は未実施。数値の正しさと、美しさ・日本語・音・実機性能を別の証拠で確認する。
- 最初に花を咲かせるための再抽選、意図から成功への後付け分類、常設池や別種の木は追加しない。既存の公平な抽選・蓄積する庭・任意の意図という基本操作を保持する。

### 触ったファイル

- README.md、PROGRESS.md（追記）
- docs/unity-garden/README.md、CURRENT_DESIGN.md、ARCHITECTURE.md、DATA_CONTRACT.md、EXPERIENCE.md、GAMEPLAY.md、ROADMAP.md、SIMULATION.md、VALIDATION.md、WEATHER.md
- docs/unity-garden/OBSERVATION.md（新規）
- docs/unity-garden/content/design-manifest.v0.8.json、observation-fixtures.v0.8.json、growth-witness-fixtures.v0.8.json（新規）
- docs/unity-garden/reference/check_current_design.py（更新）、observation_v08.py、growth_witness_v08.py（新規G0参照）

## G1+G2: Unity足場・保存ループ・庭と動的サイコロ (2026-11-12)
### 完了したこと
- 専用ブランチ `feature/unity-garden-g1-g2` で `unity/DestinyGarden/` に Unity 6000.3.23f1 (URP) プロジェクトを作成。横画面固定、asmdef分離 (Domain/Application は Unity 非依存、Infrastructure、Presentation、Tests)。
- G1: 純C# Core、保存してから解釈する乱数材料 (weather 32byte/期、mapping 256byte、outcome/oracle 各32byte)、framed append-only journal (単一writer、JCS正規化+SHA-256 hash chain、不完全末尾は検出のみで切り捨てない)、journal走査による同run復元。
- G2: 16x16整数シミュレーション (水の同時流動・排出口・吸水・蒸発・光/風場・成長段階、1投40tick)、三天候 (初期晴れ固定・3正常解決投で一期)、三地点 (入口85/岩陰135/ひらけた土154)、局所need重みによる動的6面 (15回 UniformBelow、系統3以上・同系統3面以下・同recipe2面以下)。
- 最小Presentation: 保存済み状態から描く庭グリッド、湿り/水面/段階の色分け、天候連動の光、公開6面とワンボタンの投擲。演出の作り込みと文章・音は G3。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green (8 passed) — UniformBelow/D6棄却、初期晴れ、配面制約、水収支残差0、決定性、4投ループ→復元で stateHash 一致、不完全末尾検出
- macOS player build: Succeeded。実バイナリを起動し例外なし・journal 書込みを確認 (初回は Shader.Find null を検出し、fallback + Always Included Shaders で修正後に再確認)
- iOS: Xcode プロジェクト生成 Succeeded、`xcodebuild` (署名なし generic iOS) exit 0。実機実行・署名は未実施
- 既存アプリ: `tsc -b --noEmit` green、`vitest run` 183 passed、`vite build` green (`pnpm` ラッパは既存環境の approve-builds 未承認で停止するため node_modules/.bin で同一コマンドを実行)
- 設計整合: `check_current_design.py` exit 0 (作業前確認)
### 要確定・申し送り（次フェーズへ）
- bundle identifier は開発用仮値 `com.example.destinygarden.dev` (要確定)。正式タイトル未定のまま "Destiny Garden" を使用。
- iOS実機での起動・中断復帰、Android/Windows モジュール導入とビルド、Steam 配布は未検証 (G4/G5)。本機で確認できたのは macOS 実行と iOS コンパイルまで。
- G3: オラクル28文と焦点、光・鐘・自然音、記録画面、来場者交代UI、見た目の作り込み (参照画像と実画面の比較による調整を含む)。
- 参照report (全庭2,052投・注水1,200投) のC#再計算照合、local-growth-witness の実証拠生成は未実施。現在のテストは決定的fixtureの範囲。
- journal のプロセス間排他・容量上限、RunCreationPlan の専用frameは未実装 (現行は RunStarted を起点とする単一journal)。
### 触ったファイル
- unity/DestinyGarden/ (新規Unityプロジェクト一式: Assets/DestinyGarden 配下のC#・Content・Tests・Editor、Packages、ProjectSettings、Assets/Scenes/GardenScene.unity)
- .gitignore (Unity生成物の除外を追加)
- PROGRESS.md (本追記)

## G3: 表現・文章・記録画面と全プラットフォームビルド (2026-11-12)
### 完了したこと
- オラクル: catalog v0.7 の6枚・28文を生成C#データとして採用し、RoundEvidence（吸水累計・流れ総量・水たまり120越え・光/風の追加実効・成長の十分性・播種時水分ほか）から述語を判定。ObservationGroup（4近傍連結、SurfaceFlowObservedは全域1件）を構築し、oracle-focus.v0.7 の順位（カード対応→直近3件の未使用→priority→固定順）で主焦点1・補助最大1を選定。verified_path_to_root は経路証拠を記録しない版として常に不成立（推測で成立させない）。カード未成立時は入力fallback文へ分岐し焦点なし。
- 表現: visual-plan/PRD と参照コンセプト画2点を置き、暗い青緑の霧に浮かぶ庭盤、湿りで変わる土色、水面の発光、5段階の植物、岩、光る目の石サイコロ、焦点の金の輪、天候連動の光と雨粒子、花粉粒子、Bloomを実装。音は全てコード合成（鐘・水・風・光・種・成長・環境音、外部素材なし）。
- UI: 公開6面チップ（押すとその目を願う）、系統の意図、次からの届け先、記録一覧、水量/成長の歩みグラフ（意図の一致数には偶然の平均を併記）、書き出し（CSV/JSONL）、来場者交代（RunClosed→新run）、設定（音量・消音・動きを抑える）。
- QA用に起動引数 `-dgAutoRolls N` / `-dgCapture PATH`（明示時のみ、通常プレイ不変）を追加し、実プレイヤーで撮影・目視確認した。撮影で見つけた Shader.Find の null と SSAO 資源不足による描画例外は修正し、例外0で再確認。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green (13 passed) — 既存8件 + オラクル5件（fallback分岐、焦点付き事実文、長期強制注水180投で水収支0・範囲内）
- macOS player: Succeeded。fresh journal で8自動投→撮影、例外0、画面目視確認（visual-plan/refs/build-final.png）
- iOS: Xcodeプロジェクト生成 Succeeded、xcodebuild (署名なし) exit 0
- Android: DestinyGarden.apk (約34MB) Succeeded
- Windows (Steam想定): DestinyGarden.exe Succeeded（このMacでは実行未検証）
- 既存アプリ: tsc green、vitest 183 passed、vite build green（pnpm ラッパは既存環境の approve-builds 未承認のため .bin で同一実行）
### 要確定・申し送り（次フェーズへ）
- **人間の判断が必要**: 正式タイトル/ロゴ、bundle id 等の本番識別子（現在は開発用仮値）、Apple/Google/Steam のアカウント・署名・審査、ストア素材、実機（iPhone/Android/Windows PC）での操作・音・性能の最終確認、最低OS/機種、収益化の有無。
- 参照report（全庭・注水1,200投）のC#完全再計算照合、local-growth-witness の実証拠搭載、RunCreationPlan専用frame、カード全面イラスト、出生時外観（三色×三幅）は未実装。
- 音・植物・岩はコード生成の簡易表現。素材を差し替える場合は出所とライセンスを記録すること。
### 触ったファイル
- unity/DestinyGarden/Assets/DestinyGarden/Domain: Oracle.cs、OracleCatalogData.cs、Simulation.cs、GardenTypes.cs、CanonicalJson.cs、GardenMapData.cs
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: GardenBootstrap.cs、GardenView.cs、GardenUi.cs、GardenAudio.cs、DestinyGarden.Presentation.asmdef
- Editor: BuildScripts.cs（Android/Windows追加、SSAO無効化）
- Tests: OracleTests.cs、ProjectSettings一式、visual-plan/（PRD・参照画像・確認画像）
- PROGRESS.md（本追記）

## G3 visual quality: 商品レベルへの見た目の引き上げ (2026-11-12)
### 完了したこと
- G1+G2+G3 を main へマージ後、専用ブランチ `feature/unity-garden-visual-quality` で視覚品質を全面刷新した。
- アートディレクション: 参照コンセプト画2点と visual-plan/PRD に基づき、独立したアートディレクター役と批評家2名（可読性/商品性）で3ラウンドの撮影→批評→修正を実施。批評のblocker（サイコロ目とUI出目の不一致、文字の可読性、水と乾湿の読みにくさ、平坦な照明）を解消した: サイコロ上面は出目数に連動する発光ピップ、願い=青緑/結果=金で色分け、全テキストに半透明の背面パネル、水量で広がる水面、土の3段湿りランプ+濡れ光沢。
- 表現: コード生成テクスチャ（土・石・苔・木目）、変位させた岩メッシュ、土の盛りドーム+苔の畝、木枠、霧シートと光条、遠景の岩影、花粉/雨粒子、曲がった茎と葉メッシュの植物、二層の花、角丸の石サイコロ、ACES+Bloom+SMAA、角丸スプライトのUI、金地に濃色文字でコントラスト確保、UIを隠す撮影モード（設定内、4秒）。
- 撮影で見つけた不具合（透明マテリアルのバリアント剥奪による霧板の不透明化、木枠の回転誤り）を修正し、最終撮影で例外0を確認した。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green (13 passed)
- macOS player: Succeeded。fresh journal で自動3投→RenderTexture撮影、例外0、出目・面チップ・サイコロ上面ピップの一致を目視確認（visual-plan/refs/build-visual-final.png）
- iOS: Xcodeプロジェクト生成 Succeeded、xcodebuild（署名なし）exit 0
- Android: APK Succeeded / Windows: exe Succeeded（実行はこのMacでは未検証）
- 既存アプリ: tsc green、vitest 183 passed、vite build green
- 批評家スコア（修正前スナップショット採点）: Readability 3 / Cohesion 4 / UI clarity 3、Mood 3 / Material 3 / Composition 4 / Store readiness 2 — blocker/major は本ラウンドで対応し最終撮影で再確認。再採点は未実施のためスコア更新は主張しない
### 要確定・申し送り（次フェーズへ）
- このブランチの main へのマージと push は未実施（人間の判断待ち）。
- 正式タイトル、本番識別子、署名・審査、実機確認、最低機種、収益化は前回欄の判断事項を引き継ぐ。
- 植物・岩・質感はコード生成の範囲での作り込み。さらなる品質向上には実素材（モデル/テクスチャ/フォント）の導入判断が要る。
### 触ったファイル
- Presentation: ProceduralArt.cs（新規）、GardenView.cs、GardenUi.cs、GardenBootstrap.cs
- Editor: BuildScripts.cs（影設定）、Assets/Settings（RPAsset影）、ProjectSettings
- visual-plan: art-plan.md、refs/build-visual-final.png、PROGRESS.md（本追記）

## Jam: Unity Game Jam 2026 提出用 WebGL 対応 (2026-11-12)
### 完了したこと
- Unity 6000.3.23f1 に Web Build Support モジュールを導入（本人承認済み）。
- `BuildScripts.BuildWebGL` を追加: 1280×720、gzip + decompression fallback（itch.io は Content-Encoding を付けないため）、独自テンプレート。
- 独自 WebGL テンプレート `Assets/WebGLTemplates/DestinyGarden`: どの枠でも 16:9 レターボックス、`autoSyncPersistentDataPath: true`（これが無いと Unity 6 は journal を IndexedDB に保存しない）、DPR 上限 1.5。
- 日本語フォント: WebGL は OS フォントを使えず豆腐になるため Noto Serif JP Regular（SIL OFL 1.1、本人承認済み）を `Resources/Fonts` に同梱。WebGL のときだけ使用し、他プラットフォームは従来の OS フォントのまま。
- 書き出し: WebGL ではブラウザ内のファイルに手が届かないため、押すと「Web版は書き出し非対応」と表示するだけにした。
- `link.xml` で PhysicsModule を保持: CreatePrimitive のコライダークラスがエンジンコード削除で消え、WebGL でエラーが出ていた。
- 提出素材 `docs/jam-submission/`（SUBMISSION.md、カバー1枚、スクリーンショット5枚）。ゲーム内容・設計は変更なし。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green (13 passed)
- 既存アプリ（変更なし）: tsc green、vitest 183 passed (41 files)、vite build green
- WebGL build: Succeeded（22,565,021 bytes）、zip 21,996,735 bytes、index.html が zip ルート
- ヘッドレス Chromium での通しプレイ: 願い→振る×3→オラクル→記録→庭の歩み→書き出し→設定、コンソールエラー 0。再読み込み後も 3投目まで記録・同じ配面が残ることを確認。960×760 / 1400×600 の枠でも 16:9 で欠けない。
- 音: "Audio context resumed" をログで確認。実際に耳では未確認。
### 要確定・申し送り（次フェーズへ）
- **本人の作業**: 耳で音を確認、itch.io へのアップロードと提出、SUBMISSION.md の Team 欄と生成AI開示の [ ] を事実で埋める（コンセプト画を作った画像生成ツール名がリポジトリから判別できない）。
- Safari / Firefox / スマホ / 実際の itch.io ページでは未確認。
- 以前からの表示の小問題（未修正）: 2行のオラクル文がヘッダーに触れる、「庭の歩み」グラフの説明文が棒と重なる。
- `PlayerSettings` の WebGL 値（画面サイズ・圧縮）は ProjectSettings に保存される。runInBackground はデスクトップと共有のため変更しない。
### 触ったファイル
- unity/DestinyGarden/Assets/DestinyGarden/Editor/BuildScripts.cs、Presentation/GardenUi.cs、Presentation/GardenBootstrap.cs
- unity/DestinyGarden/Assets/DestinyGarden/link.xml、Resources/Fonts/（NotoSerifJP-Regular.otf、NotoSerifJP-OFL.txt）
- unity/DestinyGarden/Assets/WebGLTemplates/DestinyGarden/index.html、ProjectSettings/ProjectSettings.asset
- docs/jam-submission/、.claude/launch.json、PROGRESS.md（本追記）

## Jam v2: 幸運の七・神聖な見た目・庭の小さな命（ecology.v1）(2026-11-12)
### 完了したこと
- 本人の方向付け（ラッキーナンバー7、虹の八と天使、蝶、海、神聖な庭、祈り、生き物が庭の育ち方を変える、表現は繊細に）を受けて実装。退避版は `feature/jam-webgl` の 924e1de と `Builds/DestinyGarden-WebGL-v1-safe.zip`。
- 幸運の七（lucky-seven.v1, Domain/LuckySeven.cs）: 願いが叶った次の準備でだけ7面目を提示。加えると `LuckySevenAdded` を意図確定より前に記録し、保存済みの outcome bytes から公正な D7（rejection sampling）で振る。7面目は届け先で最も必要な恵みを2倍で。正直メーターの「偶然なら」は各回の面数で割るよう修正。
- 生態（ecology.v1, docs/unity-garden/ECOLOGY.md, Domain/Ecology.cs）: 自然シミュレーションの後に独立して走る一歩。花蜂・蝶・小鳥・綿毛が種を運び（1投最大2粒）、精霊（願いが叶った時）・蛍・きのこ・ミミズが成長を少し助ける。偶然は `DrawEntropyCommitted.ecologyBase64` だけから引き、結果は `Resolved.postState` と `Resolved.ecology` に保存。水は動かさない。既存の `Simulation.Resolve` は不変（段階イベント関数の可視性のみ internal に）。
- 見た目: 夕暮れの海と島の岸辺の光、海上を巡る七つの灯、立ちのぼる光の粒、強めのブルーム。蜂・蝶・トンボ・小鳥・光る苔・蛍・きのこ・綿毛・花びら・露・魚影・精霊・虹の八と天使をコード生成で描画（Presentation/SacredAtmosphere.cs, GardenWonders.cs, GardenLife.cs）。
- UI: 初回の題名カード（日英、遊び方ボタンで再表示）、恵みごとの色分け、7面目チップ、「庭の気配」（いまいる生き物）、状況の一行は説明でなく気配だけ（例「……かすかな光が、花のほうへ降りていった。」）。メッセージ札とヘッダーの重なり、グラフの棒がキャプションを覆う不具合、記録のはみ出しを修正。
- QA専用の起動引数 `-dgWonders`（`-dgCapture` 併用時のみ、台帳に書かない）で虹の八等のスクリーンショットを撮れるようにした。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（25 passed: 既存13 + 幸運の七・祈りの精霊・計測 9 + 生態 3）
- 計測（720投・常に多い系統を願い七を取る方針）: 願いの一致 45%、七の提示 45%、七が出た 50回、虹の八 9回（約80投に1回）、蝶 13%。住人の出現率: 花蜂98 / ミミズ83 / 光る苔85 / 小鳥70 / トンボ47 / 蛍46 / 精霊45 / 綿毛25 / 露24 / 魚11 / きのこ6 %。120投後の平均: 植物55株・花42.7。
- WebGL: ヘッドレス Chromium で20投の通しプレイ、コンソールエラー0、七の提示6回を取得、再読み込み後も状態保持。
- macOS player: 旧（生態以前）の台帳を読み込んで2投 → 例外0（後方互換を確認）。手元の台帳はバックアップから復元済み（SHA一致）。
### 要確定・申し送り（次フェーズへ）
- **本人の判断**: 虹の八は約80投に1回。ジャムの審査で見られる可能性は低い。条件を緩めるかは本人判断（緩めるなら版を上げて記録）。
- 生態の数値は初期値。120投で庭が満ちる（植物55株）。長期の遊び心地は未検証。
- ボトルガーデン（瓶の中の閉じた世界）としての見せ方、種を蒔くだけの操作への再設計はジャム後の設計テーマ。
- Safari/Firefox/スマホ/実際の itch.io ページ、音の実聴は未確認。
### 触ったファイル
- Domain: LuckySeven.cs（新規: 幸運の七・NatureBalance・PrayerSpirits）、Ecology.cs（新規）、Simulation.cs（EmitStageEvents を internal に）
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: GardenBootstrap.cs、GardenUi.cs、GardenView.cs、ProceduralArt.cs、SacredAtmosphere.cs・GardenWonders.cs・GardenLife.cs（新規）
- Tests: LuckySevenTests.cs、EcologyTests.cs（新規）
- docs/unity-garden/ECOLOGY.md（新規）、docs/jam-submission/、PROGRESS.md（本追記）

## Jam v3: 見た目の磨き込み — 丸い聖なる島・川・種の多様さ・静かなUI (2026-11-12)
### 完了したこと
- 本人の講評（世俗的な虹→神秘的に、暗い、四角い盤面、花が一種類で配置が一定、瓶の中の世界・凹凸・川、透明感、左下の表示は裏へ）を受けて見た目を作り直した。ゲームの規則・保存・シミュレーションは変更なし。
- 島: 16×16 の盤面とセルの立方体・木枠をやめ、丸い一枚の地形（Presentation/IslandTerrain.cs）に。庭の範囲では各セルのモデル高さをそのまま通るので、植物・水たまり・演出の位置はシミュレーションと一致。外側は丘と砂浜、海へ。
- 川: 庭の後ろの泉から西の海へ流れる景観としての川（水のモデルには含めない）。泉の光、流れるテクスチャ。
- 苗床: 土の盛り上がりメッシュをやめ、島の表面テクスチャに土を描き込む方式に（各セルの湿りを滑らかに補間、毎投更新）。継ぎ目・格子感なし。
- 花: 6種（サイコロ由来・初期はコスモス／デイジー／ホタルブクロ、生き物が運んだ種はキンポウゲ／ラベンダー／野ばら）。株ごとの高さ・傾き・位置の揺らぎ。
- 島の木と低木、牧草の群生、小さな野花、小石。明るく透明感のある色調（霞・露出・海の浅瀬・彩度控えめ）。
- 虹の八: 線で描くのをやめ、淡いプリズムの光として空気から凝結し、呼吸し、上へ溶ける演出に。天使は小さく淡く。
- UI: 面は中央の細い列、「願いをかける」で引き出し（願う恵み・届け先）を開閉。ガラス調の半透明パネル、ふちの光、文字の影。画面の縁に瓶のガラス越しのような光。
- 種まきの瞬間: サイコロ由来の種はサイコロから光の弧で土へ降り、着地で小さな音。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（25 passed）
- WebGL: Succeeded、zip 22,119,849 bytes。ヘッドレス Chromium で16投の通しプレイ・七の取得・再読み込み、コンソールエラー0。
- macOS player（QA撮影用）: Succeeded。手元の台帳は毎回バックアップから復元（SHA一致）。
### 要確定・申し送り（次フェーズへ）
- 本人: 音を耳で確認、itch.io へのアップロードと提出、SUBMISSION.md の Team と生成AI開示の [ ]。
- 虹の八は約80投に1回（変更なし）。
- QA撮影は macOS player の `-batchmode` で行う（画面ロック中は通常起動だと描画が止まる）。
### 触ったファイル
- Presentation: IslandTerrain.cs（新規）、GardenView.cs、GardenUi.cs、GardenWonders.cs、GardenLife.cs、SacredAtmosphere.cs、ProceduralArt.cs、GardenBootstrap.cs
- docs/jam-submission/（SUBMISSION.md、cover.png、screenshot-1〜5）、PROGRESS.md（本追記）

## Jam v5: 生きている瓶の島 — 調和・菌根・植物の条件と関係・遷移（2026-11-12 夜）
### 完了したこと
- 計画（~/.claude/plans/8-5-8-wiggly-dream.md）と本人の追加講評（菌根は見えなくてよい、瓶は遠くから見える程度、植物の条件と関係、草はもじゃもじゃより「生きるために生えている」、瓶と土と水から始まり自分だけの庭へ、虹は相当ラッキーだがジャム向けにやや出やすく）を反映。
- 規則（docs/unity-garden/ECOLOGY.md §6・§7）: 調和 harmony.v2（陰陽・4つの恵み・命・住人の多様さ・咲く種の多様さ、0〜8、芒星 5/7/8）、菌根の分け合い（ecology.v2）、植物15種の条件と働き（flora.v1/ecology.v3: 土の硬さ・水捌け・湿り・光、蝶と蜂の好み、土をやわらげる／水捌けを変える、親の種を受け継ぐ芽吹き、合う場所で育つ）。種は `PlantState.Species` として保存（未設定なら書き出さないので旧状態のハッシュは不変）。虹の八 rainbow.v2（七の目＋同じ一投で蕾か開花、または七芒星以上）。
- 見せ方: カスタムシェーダー3種（Foliage/Glow/Glass）。島は裸の土・泉・川から始まり、保存状態の生命度（投数・花・調和）で水辺から緑が広がり、苔→草→野の花→木→木陰の植物→最後に泉のそばの小さな家。植生は条件の合う場所にだけ、まばらに。花15種の形（花びらメッシュ）。格子を消す歪み場。出目は島全体を渡って届く（水は泉から川を下る等）。菌根は見えない（分けられた株にだけ淡い光）。瓶は遠景で見える。開始時は瓶全体を見せてから庭へ寄る。虹の八は空中で大きく、カメラが引いて瓶の肩が見える。
- UI: 庭の気配に ☯調和、庭の歩みに「いま咲いている花」、関係を一行で（例「やわらいだ土に、ユリが根づいた」「小鳥の落とした種から、野ばらの芽」— その一投で実際に起きた時だけ）。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（36 passed）
- WebGL: Succeeded、zip 22,155,414 bytes。ヘッドレス Chromium で20投の通しプレイ、コンソールエラー0、60fps。
- 計測: 虹の八 約36投に1回、調和の平均 約5、120投で11種が出現。
### 要確定・申し送り（次フェーズへ）
- 本人: 音の実聴、itch.io へのアップロードと提出、SUBMISSION.md の Team と画像生成ツール名。main へのマージは本人確認後。
- 遅いGPU・Safari・スマホでの描画速度は未確認（植生の密度は IslandFlora の step で調整可能）。
### 触ったファイル
- Domain: Harmony.cs・Flora.cs（新規）、Ecology.cs、LuckySeven.cs、GardenTypes.cs、CanonicalJson.cs
- Application: GardenCoordinator.cs（前段で）、HistoryProjector.cs
- Presentation: IslandFlora.cs・FoliageMesh.cs・PlantForms.cs・IslandHarmony.cs（新規）、GardenView.cs、IslandTerrain.cs、SacredAtmosphere.cs、GardenWonders.cs、GardenUi.cs、GardenBootstrap.cs
- Shaders: DGFoliage/DGGlow/DGGlass（新規）、Editor/BuildScripts.cs
- Tests: HarmonyTests.cs・FloraTests.cs（新規）、EcologyTests.cs、LuckySevenTests.cs
- docs/unity-garden/ECOLOGY.md、docs/jam-submission/（SUBMISSION.md・cover・screenshot-1〜5）、PROGRESS.md

## Jam v6: 磨き込み — 木・祭壇のサイコロ・図鑑・弱る植物（2026-11-13 未明）
### 完了したこと
- 木を作り直した（Presentation/TreeForms.cs）: 広葉樹・花の木（花びらが舞う）・川辺の枝垂れ柳・針葉樹・低木。幹と枝のチューブ、葉の塊、Foliage シェーダーで揺れる。島の生命度で順に育つ（低木→木→花の木）。
- 立石と岸の石に苔と地衣。サイコロは石の祭壇の上に浮かび、ゆっくり回り、祭壇が淡く光る。
- 図鑑（右メニュー）: この庭に現れた花と、その条件（土の硬さ・水・光）と働き（土をやわらげる・水捌け・蝶と蜂・実・綿毛）。種のデータそのものから生成するので規則と食い違わない。未知の種は ？？？。
- 植物の条件に合わない場所に立つ株は、淡く疲れた色に（`Flora.Fits` に基づく見せ方）。
- 陰陽の光の色の偏りを半分に、背の高い草を少なく・遅く・緑に、地衣を控えめに。
- 規則は変更なし（ECOLOGY.md §7 のまま）。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（36 passed）
- WebGL: Succeeded、zip 22,156,972 bytes。ヘッドレス Chromium で20投の通しプレイ、コンソールエラー0、60fps。図鑑パネルの表示を確認（10投で 10/15）。
### 要確定・申し送り（次フェーズへ）
- 本人: 音の実聴、itch.io への提出、SUBMISSION.md の Team と画像生成ツール名。
### 触ったファイル
- Presentation: TreeForms.cs（新規）、FoliageMesh.cs、GardenView.cs、GardenUi.cs、GardenBootstrap.cs、IslandFlora.cs
- Application: HistoryProjector.cs
- docs/jam-submission/（SUBMISSION.md・cover・screenshot-1〜5）、PROGRESS.md

## Jam v7: 時の流れ・音・地形・開花の瞬間・品質ガード（2026-11-13 朝）
### 完了したこと
- 時の流れ（Presentation/SkyClock.cs）: 投数だけで決まる朝→昼→金色の午後→夕→月夜（1日=16投）。太陽・補助光・環境光・霞・海の色・ブルーム・七つの灯・光の粒が連動。空に太陽・月・星（瓶を引いた視点・始まりの視点で見える）。夜は泉の家の灯りが強まる。保存された投数から決まるので同じ庭は同じ時刻に見える。
- 音（Presentation/AudioSynth.cs・GardenAudio.cs）: すべてコード合成。海・川・葉ずれ・小鳥・夜の虫が島の生命度に応じて育ち、調和が上がるごとに和音（パッド）の声が増える。D長調ペンタトニックで統一。花が咲くたびに種ごとの音が鳴る（種15種×3オクターブ）、芽吹き・幸運の七・星の段階・虹に専用の音。小さなホール残響つき。
- 音の実測（claude-music-ear、24ファイル）: クリップ0、DCなし、ピーク −4〜−10 dBFS、背景 −33〜−39 LUFS／出来事 −18〜−26 LUFS。海の高域が強すぎ（−11〜−14 dB）、弦にDC（−49.8 dB）があったので修正し、再測定で全24ファイル警告なし。**耳での確認は未実施。**
- 地形: 奥の丘・縁・川の谷を深く。泉に光る池と小さな滝・霧。
- 花が開く瞬間: 開いた花は種の色で光と花びらを散らし、その種の音を鳴らす（1投4つまで）。芽吹きは小さな光。
- 品質ガード（QualityMonitor）: 平均30fps未満が3秒続くと段階的に軽くする（被写界深度とAA→影→ブルームと描画70%）。判定ロジックはテスト済み。この環境は60fpsで作動せず、軽い側の見た目は実機未確認。
- 最初のロールボタンがゆっくり呼吸して始め方を示す。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（40 passed）
- WebGL: Succeeded、zip 22,180,011 bytes。ヘッドレス Chromium で20投の通しプレイ、コンソールエラー0、60fps。
### 要確定・申し送り
- 本人: 音の実聴（特に音量バランス）、itch.io への提出、Team と画像生成ツール名。
- 遅いGPU・Safari・スマホは未確認。
### 触ったファイル
- Presentation: SkyClock.cs・AudioSynth.cs（新規）、GardenAudio.cs、GardenView.cs、SacredAtmosphere.cs、IslandTerrain.cs、GardenLife.cs、GardenBootstrap.cs、GardenUi.cs、GardenWonders.cs、PlantForms.cs、ProceduralArt.cs
- Tests: PresentationTests.cs（新規）、asmdef
- docs/jam-submission/、PROGRESS.md

## Jam v8: 庭ごとに違う島・回して見られるカメラ・風に梳かれた草原（2026-11-13 朝）
### 完了したこと
- 島の種（Presentation/IslandSeed.cs）: 保存された run id だけから決まる。高地の向き・丘と窪み・縁の高さ・川の泉と曲がり方・立石・木（種類と配置）・家の位置・島が好む植物（信号種2つ、乏しい種1つ）・草原の色・始まりの時刻（朝〜昼）・風向き。庭の16×16は変えない。同じ庭は同じ島、新しい来場者は別の島。「次の来場者」はシーンを再開して新しい島を作る。
- 草: ぱらぱらした集合をやめ、島の風に沿って倒れる扇形の房にした（群落は大きなまとまり、色は連続的に変化、高い草は群落の中心ほど高い、地面にも風向きの梳き目、苔は盛り上がり、タイムは緑の座布団に）。
- カメラ（Presentation/CameraRig.cs）: ドラッグで島を見まわし、ホイール／ピンチで寄り引き、「視点を戻す」。ボタン上から始めたドラッグは無視。開始演出と虹の引きの間はロック。
- 泉の滝は十字の面で、どの方向から見ても見える。
- 開始カードの説明に操作を追記。
### 完了基準の結果（コマンド出力の要約）
- Unity EditMode tests: green（41 passed、島の種のテスト含む: 決定的、庭ごとに違う、川は庭に入らない、正面に木を置かない）
- WebGL: Succeeded、zip 22,198,079 bytes。ヘッドレス Chromium で20投の通しプレイ、60fps、エラー0。別スクリプトでドラッグ・ホイール・視点を戻す・次の来場者（新しい島でシーン再開）を確認、エラー0。
- ブラウザの試験で見つけた不具合を修正: タッチ画面デバイスがある環境でドラッグが効かなかった。
- 撮影: 種を変えた4つの島を並べて差を確認。島を東・北・西から見た視点でも成立。
### 要確定・申し送り
- 本人: 音の実聴、実機タッチ（ピンチ・1本指ドラッグ）の確認、itch.io への提出、Team と画像生成ツール名。
- 実機のタッチ操作は未確認（マウス入力のみ試験）。
### 触ったファイル
- Presentation: IslandSeed.cs・CameraRig.cs（新規）、IslandTerrain.cs、IslandFlora.cs、GardenView.cs、GardenBootstrap.cs、GardenUi.cs、GardenWonders.cs、SkyClock.cs
- Tests: PresentationTests.cs
- docs/jam-submission/、PROGRESS.md

## Ecology v4: 生態系の循環と多様性を深める（2026-11-12）
### 完了したこと
- 本人の依頼「ひとつひとつの生態系や自然の関係性の深みや種類を可能な限りブラッシュアップ」を、生態系v4の一つの改修として実装。仕様は `docs/unity-garden/ECOSYSTEM_V4.md`。
- 花を15→24種に。シロツメクサ・カラスノエンドウ・ノコギリソウ・タイム・ハッカ・サクラソウ・スミレ・ミソハギ・アキノキリンソウを追加。硬さ・排水・湿り・光・蜜源の好みと、根粒菌・地表の覆い・背の高い株の陰・結実の得意な時期を共通データから扱う。
- 岩陰・草木の陰・水辺・乾いた草地・花の草地。湿りと葉の覆いを履歴として保存し、成熟した住み場所を一投の水だけで作らない。高い株が育つと新しい陰が生まれ、株が退場すると居場所も変わる。
- 蜂・蝶が直接種を作る旧v3を、新規庭では受粉→数歩後の結実→有限の種の散布に変更。小鳥は実、風は風散布の種、他は自然落下。親と厳密に同種、親種が育つ空き地にだけ散布。各投2粒を上限に経路を巡回する。
- 落ち葉→きのこ・ミミズ・トビムシ・カタツムリ→養分→成長の循環。分解は落ち葉から同量を引き、菌根は豊かな根から乏しい根へ同量を移す。根粒菌と雨の養分は外部供給として分ける。水収支と元の `Simulation.Resolve` は変更しない。
- アブラムシの局所的な群れと、テントウムシ・ハナアブ・クモ・カエル・小鳥の捕食。餌・葉の隠れ場所・花・持続した湿りを組み合わせ、存在する獲物だけを減らす。新しい住人7種。
- 株ごとの負担と回復、花後の休息と再成長、寿命と土への還元。混雑・養分不足・小虫・不適合な居場所が負担になり、解消すれば回復。96投で巡る四つの時期が種の実りに作用する。
- 関係を `Resolved.ecology.relations` に保存し、現在の一行と履歴を同じ投影で表示。図鑑には今の環境、土の循環、出会った住人と植物の役割を表示。図鑑と全投の履歴をスクロール可能に。隠れたパネル更新でもScrollRectを取得する。
- 新種の形・色、ハナアブの飛行、地表の住人と落ち葉を追加。地表の住人は1メッシュで描画し、所有メッシュ・マテリアルを解放。衰弱は色に、実際の菌根の養分移動は根元の光に反映。
- `RunStarted` でv4を記録し、新しい庭だけ適用。古い庭はv3で続け、初期状態・24投後の旧ハッシュを改修前 `ac12b67` の実行から採取して照合する。
- 実際のUnity C# Domain / Application / Infrastructureと既存テストを直接動かす .NET 8 検証プロジェクト、CIを追加。表示・ゲームコードの写経はしない。
### 完了基準の結果（実行出力）
```text
dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release
Passed! - Failed: 0, Passed: 50, Skipped: 0, Total: 50
```
- 内訳: 既存のエンジン非依存テスト36＋生態v4の14。`PresentationTests.cs` の5テストはUnityが必要なので、この実行には含めない。
- 4庭×240投（計960投）: 24/24種、追加した7住人を含む全ての自然住人と11種の関係イベントが発生。240投後の株数36・36・42・37、最初の120投の散布25・26・28・16粒。水の不変、2粒上限、個体・資源・履歴の範囲、保存復帰を検証。
- 旧初期状態 `c27986226e09f89eca6906985d1f65739ee8e9a369dec7cc3df958a2658513b1`、旧24投後 `b7448dd6b371601884ca452f99b9956fd115ab8ae75f374c3a500ff856999d60` と一致。
```text
pnpm typecheck
$ tsc -b --noEmit                 (exit 0)
pnpm test
Test Files 41 passed (41)
Tests      183 passed (183)
pnpm build
vite v8.1.4 building client environment for production...
✓ 102 modules transformed.
✓ built in 278ms
```
- 既存アプリ365日・願い120件の `pnpm simulate` は `ok: true, networkRequests: 0`。レポートの既存golden `ef9462dd6dbf7e4852f66f3527d1c33c59b5ffcb8017ebac9b004284cb6cc24e` と一致。`verify-export` 成功、再生成レポートを `cmp` で一致確認。
- C#9構文検査: Unity内の50ファイル、構文エラー0（Unityの型解決・ビルドを意味しない）。`git diff --check` 成功。
- 既存実験の `src/`、凍結プロトコル、台帳の計算・書き込み経路は変更していない。生態の偶然は保存済みbytesのみ。過去の状態の再解釈・ジャーナルの削除・編集は追加していない。
### 未確認・申し送り
- **この環境にはUnityエディタ／実行可能なWebGLビルドがないため、Unityコンパイル・全EditModeテスト・WebGL書き出し・3D表示・操作・音・fpsは未検証。** 上記50件は.NETでの実C#ドメイン検証であり、Unityテスト実行済みとは扱わない。
- Unity 6000.3.23f1で全EditMode実行後、WebGLで新しい来場者→複数投→図鑑と全履歴のホイール/タッチスクロール→再読込を確認する。旧ジャーナルを開いた際はv3のまま、次の来場者ではv4になることも確認する。地表の住人のサイズ、遅いGPUの負荷、花の再開花、長文の折返しは画面確認が必要。
- 実ストア・itch.io提出用ビルドや画像は更新していない。新しい規則の実プレイでの心地よさは、上記の960投による規則検証に加えてUnityで確認が必要。
### 触ったファイル
- Domain: EcosystemState.cs、EcosystemSerializer.cs、LivingEcosystem.cs、EcologyObservations.cs（新規）、Ecology.cs、Flora.cs、GardenTypes.cs、CanonicalJson.cs
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: EcosystemResidents.cs（新規）、GardenLife.cs、GardenUi.cs、GardenBootstrap.cs、GardenView.cs、PlantForms.cs、IslandHarmony.cs
- Tests: LivingEcosystemTests.cs（新規）、HarmonyTests.cs（旧版のテストを旧状態から起動）
- tools/ecology-check/、.github/workflows/garden-ecology.yml、.gitignore、docs/unity-garden/ECOLOGY.md・ECOSYSTEM_V4.md、PROGRESS.md

## Ecology v5: 土の記憶・花蜜・庭を観察（2026-11-13）
### 完了したこと
- 本人の継続依頼「さらにブラッシュアップ」に対し、前回のPR #30の生態系を一つの目的で深化。24種の数は維持し、過去の植物・居場所・花を訪れる命の関係を深めた。仕様は `docs/unity-garden/ECOSYSTEM_V5.md`。
- 休眠する種を保存。散布は即座に株を作らず、土へ同種の種を残す。親のID・場所・投・運び手を保ち、2歩以上と空き地・水/光/土・養分2を待って芽吹く。親の退場後も子が生まれる。
- 土の種は全体96粒、セルごと3粒、種を届けるのは一投2粒まで。古い種は土へ還り、期限や個体メモリは有限。休息の頃を経験してから芽吹くサクラソウ・スミレ・アネモネなど、種ごとの戦略を導入。
- 花蜜（0〜6）と訪花の記憶を保存。晴れ2/薄曇り1/小雨0で補い、訪花で1を消費。受粉が必要・長く訪れていない・好みの花を巡り、毎回最初の3セルだけが訪問される偏りを解消。蜂が食べた後、蝶は残る蜜から経路を選び直す。ハナアブは開いた花に限定。
- 分解者・捕食者は実在する資源が豊かな場所へ。得点が同じなら投数で巡り、地図の前方だけが常に作用する偏りを抑えた。
- 右メニューに「庭を観察」。全体と5つの居場所で、今の株・花・土の資源・眠る種を読む。株ごとに由来、花蜜、乾湿・光・硬さ・排水・養分・小虫・混雑を根拠つきで表示。種が今何を待つか、条件が戻ってもまだ回復に時間が必要な株も区別する。
- 種の着地と後の芽吹きを、関係イベント・履歴の集計・演出で区別。履歴は保存済みの原因、観察は現在の状態を共通のドメイン投影から読む。
- 新規庭だけ `ecology.v5`。保存済みv3/v4は規則を変えず復帰。`succession` を持たないv4のペイロードでは新しいキーを省略し、初期状態・48投後の既知ハッシュとの一致を検証。
### 完了基準の結果（実行出力）
```text
dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release
Test Run Successful.
Total tests: 65
     Passed: 65
```
- 前回の50件＋今回15件。実際のDomain/Application/Infrastructureを.NET 8でコンパイル・実行。Unity依存のPresentationTests 5件は含めない。
- v5の384投×2庭を、継続と96投後の復帰それぞれで走らせ、計1536投の全状態ハッシュを照合。途中復帰による分岐なし。水の不変、種の供給2粒と芽吹き2株、貯蔵96粒・セル3粒、蜜の範囲、現在の個体メモリの掃除、セル重複なしを確認。
- v5の2庭で23/24種。384投後の株数36・42、土へ残った種379・390粒、そこからの芽吹き104・111株、土の種の最大96粒。受粉・花蜜・休眠種の着地/目覚め/還元を含む14種類の関係イベントが発生。
- v3の初期/24投の既知ハッシュと、v4（改修前 `578f711`）の初期 `e0a4eabc212de46929090ca4cdbcef2d8b51936ddc7647a4f599b34b310b5ace` /48投 `bc0bb990d677452a3baf62eb910b15908c8e1f4f6e41de888ba58c5cd61170f2` を照合。
```text
npx --yes pnpm@10 typecheck
> tsc -b --noEmit                         (exit 0)
npx --yes pnpm@10 test
Test Files 41 passed (41)
Tests      183 passed (183)
npx --yes pnpm@10 build
✓ 102 modules transformed.
✓ built in 355ms
```
- C#9構文検査はUnity内55ファイル、エラー0。`git diff --check` 成功。既存実験の `src/`・凍結プロトコル・水のシミュレーションには触れていない。乱数の取得・保存順序を変えず、過去のジャーナルを書き換える経路は追加していない。
- 前回PR #30のGitHub側 `CI` と `Garden ecology` は本着手前にsuccessを確認。本追記時点で今回の変更のリモートCIは未実行。
### 未確認・申し送り
- この環境にUnityエディタと実行可能なWebGLプレイヤーがないため、Unityでの型解決・全EditMode実行・3D表示・観察パネルの選択/スクロール・音・fpsは未検証。.NETとC#構文検査をUnityの実行確認とは扱わない。
- Unity 6000.3.23f1で、新規の来場者→複数投→観察の6フィルタ→種が残る投と後の芽吹き→再読み込み→旧v3/v4の復帰を確認する。特に観察の長文、狭いWebGL枠での右メニュー、タッチスクロールと住人の実際の飛行を確認する。
- 今回も実配布ビルド・提出画像・ストア公開は更新しない。ルールと新UIを含めてPR #30を更新し、Unity確認前のドラフトを維持する。
### 触ったファイル
- Domain: SuccessionState.cs、SuccessionSerializer.cs、SuccessionRules.cs、EcologyInspection.cs（新規とmeta）、EcosystemState.cs、EcosystemSerializer.cs、LivingEcosystem.cs、Ecology.cs、EcologyObservations.cs
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: GardenUi.cs、GardenBootstrap.cs、GardenLife.cs
- Tests: SuccessionTests.cs（新規とmeta）、LivingEcosystemTests.cs（v4の長期試験をv4で固定）
- docs/unity-garden/ECOSYSTEM_V5.md（新規）、ECOLOGY.md、tools/ecology-check/README.md、PROGRESS.md

## Ecology v6: 定着・子育て・水辺の食物網（2026-11-13）
### 完了したこと
- 本人の継続依頼「さらにブラッシュアップ」に対し、住人が庭で暮らし、次の世代につながる一つの改修を実装。仕様は `docs/unity-garden/ECOSYSTEM_V6.md`。
- 蝶・テントウムシ・ハナアブ・トンボ・カエルが、食草/葉の覆い/持続する水辺と食べものの条件が3投続いた場所へ定着。種類ごと2組、各組の成体3・幼い命2、候補地6まで。成体の活動は住み場所からの範囲と実在する数で制限する。
- 卵→幼生→（対象種のさなぎ）→成体を保存。蝶の幼虫は食草、テントウムシとハナアブの幼虫はアブラムシ、オタマジャクシは藻、ヤゴは水生の餌を実際に消費。幼生は食べ足りない時には成長しない。卵とさなぎの蓄えを使う期間も区別する。
- 蝶の幼虫は食草の根と葉の基礎を残して食べる。ハナアブは成体の花蜜と幼虫の捕食を分離。テントウムシの成体は開いた花の実在する蜜でも暮らせる。成体が食べられても幼生の食べものがない場所で産卵を続けない。
- 成体の食事は保存される実活動の消費量から割り当て、同じ量を2組へ二重計上しない。水辺には養分→藻、落ち葉/藻→小さな餌を追加し、消費量と上限を守る。乾いた時の還元も記録する。水収支は変えない。
- 一時的な餌不足/住み場所の喪失には猶予。子育て中でない成体は近くの合う場所へ移り、幼生や卵は池の間を瞬間移動しない。成体への成長、外への旅立ち、移動、還元を別の原因イベントとして保存する。
- 観察に住人の成体数、卵/幼虫/さなぎ/オタマジャクシ/ヤゴの段階、餌不足と居場所の変化、水辺の食物網を追加。新しい段階は既存の地表メッシュにまとめて描く。育った場所や定着/移動の場所に小さな光を出す。
- 調和の多様性で同じ住人の卵・幼虫・さなぎを別種として数えない。旧規則の調和は変更しない。
- 新規庭だけ `ecology.v6`。v3/v4/v5の保存済み庭は旧規則で復帰。新しいメモリを推定・追加しない。住人の順序・植物の選択順序を固定し、実JSONを経た復帰でも同じ結果にする。
### 完了基準の結果（実行出力）
```text
dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release
Test Run Successful.
Total tests: 85
     Passed: 85
```
- 前回の65件＋今回20件。実際のDomain/Application/Infrastructureを.NET 8で実行。Unity依存のPresentationTests 5件は含めない。
- v6の384投×2庭×継続/JSON定期復帰＝1536投で、48投ごとにコーディネーターが生成した全記録のペイロードをJSONへ往復させた後も、全状態ハッシュが一致。
- v6の2庭で5種類すべての住人が定着。定着69/99件、成体への成長イベント10/37件、組の最大数9/10、384投後の植物31/40株。食事・移動・乾燥・還元などを含む28種類の関係イベントを確認。
- 水の不変、各種の組/個体数、候補地、藻/小さな餌の上限、二重に食事を使わないこと、食草の根を残すこと、餌不足時に育たないこと、卵や幼生の移動制約を確認。
- v3初期/24投、v4初期/48投の既知ハッシュを維持。v5の改修前 `0171418` で採取した初期 `16ccb702f7f652d45a96f64b40f5ecda03687a306d8e4755db7e408b83c4c967` /96投 `9d88938fac694561e4850779ba321d1a8737498c7d31a87a8f28603e9b5def47` と一致。
```text
npx --yes pnpm@10 typecheck
> tsc -b --noEmit                         (exit 0)
npx --yes pnpm@10 test
Test Files 41 passed (41)
Tests      183 passed (183)
npx --yes pnpm@10 build
✓ 102 modules transformed.
✓ built in 345ms
```
- Unity内の60ファイルをC#9構文検査し、エラー0。`git diff --check` 成功。
- 既存実験の `src/` と凍結プロトコル、自然シミュレーション、台帳の書き込み経路は変更していない。新しい乱数取得・時刻依存・過去のジャーナルの編集を加えていない。
### 未確認・申し送り
- この環境にはUnityエディタ/実行可能なWebGLプレイヤーがなく、Unityの型解決・全EditMode・新しい姿の描画・操作・音・fpsは未検証。85件は実C#の.NET実行であり、Unity検証済みとは扱わない。
- Unity 6000.3.23f1で、新しい来場者→湿った場所と食草/蜜源を育てる→観察→卵/幼生/さなぎ/羽化→乾いた場所の移動を確認する。小さい幼生の見え方、観察の長文、WebGLの狭い枠とタッチ操作、描画負荷の確認が必要。
- 旧v3/v4/v5ジャーナルを開く際に旧規則を維持し、次の来場者からv6になることをUnity側でも確認する。配布ビルド・提出画像・ストア公開は更新せず、PR #30をUnity確認前のドラフトとして更新する。
### 触ったファイル
- Domain: CommunityState.cs、CommunitySerializer.cs、CommunityRules.cs、CommunityInspection.cs（新規とmeta）、SuccessionState.cs、SuccessionSerializer.cs、EcosystemState.cs、LivingEcosystem.cs、Ecology.cs、EcologyObservations.cs、EcologyInspection.cs
- Application: GardenCoordinator.cs
- Presentation: EcosystemResidents.cs、GardenLife.cs、GardenUi.cs
- Tests: CommunityTests.cs（新規とmeta）、SuccessionTests.cs（v5で固定）
- docs/unity-garden/ECOLOGY.md・ECOSYSTEM_V6.md、tools/ecology-check/README.md、PROGRESS.md

## Ecology v7: 土の育ち・根の広がり・生態系の境目（2026-11-13）
### 完了したこと
- 「さらにブラッシュアップ」の依頼を受け、先に生きた株の働きが次の植物の居場所へ続く改修を、同じPR #30へ追加した。
- 落ち葉を即時の養分と持続する腐植へ分配する。両方の保存上限を見て実在する落ち葉だけを分解し、二重に資源を作らない。腐植は硬さを下げ、適度な湿りで4投に1回、養分へ戻る。植物が去った後にも土の働きを残す。
- 種類ごとの生育限界は維持し、その内側の湿り・水捌け・硬さ・光との相性を段階化した。生態による生長と花蜜の補充、サイコロ由来の種の種類選びへ反映する。
- 野イチゴ・タイムのつる、ハッカ・スミレの地下茎を追加。同種の子株だけを作り、親の蓄え6・養分2・成長100を使う。合う隣の空き地がない時は待機し、根から1株まで、種の目覚めと合計して生態の出生2までを共有する。
- 腐植、根の蓄え、各区画の居場所と継続時間を保存。6投以上続いた居場所が変わった時や、子株が実際の境目を越えた時は、その時の両側の環境を記録する。後の庭の状態から昔の環境を推測しない。
- 「庭を観察」に腐植、環境が続いた区画、隣り合う異なる居場所の接点、株と土地の相性、根が広がるために待つ条件を追加。由来と図鑑では種・つる・地下茎を区別する。土の色へ腐植を反映し、根からの子株には親から隣への低い光を追加した。
- 新規庭を `ecology.v7 / flora.v2` とした。保存済みv3〜v6には `habitat` を追加せず、各旧規則で続く。「次の来場者」から新版になる。
### 検証結果（実際の出力）
- `.NET SDK 8.0.428` でUnity内の実際のDomain / Application / InfrastructureとEditModeテストを直接実行した。Unity依存のPresentationTests 5件を除く既存85件＋新規16件。

```text
$ dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release --no-restore --nologo --logger 'console;verbosity=normal'
Test Run Successful.
Total tests: 101
     Passed: 101
 Total time: 43.7298 Seconds
```

- 分解の収支、保存上限、腐植の養分化、先駆けの株がいなくなった後の硬さ、相性と既存限界の整合、親の負担、待機、出生上限の共有、深い複製、観察の非破壊性、実際の草地/陰の境目を越えた履歴を確認した。
- v7は384投×2庭×継続/64投ごとのJSON復帰の1536投。coordinatorが生成したメモリジャーナルのpayloadを正規JSONへ往復して復帰し、全投の状態ハッシュを照合した。実ファイルの384投復帰テストとは区別する（ファイルジャーナル自体のテストは既存テストに含まれる）。水量、腐植・根の蓄え・継続時間、出生数、同一セルの重複が上限内であることも確認。

```text
garden 0, restore=False: plants=36, clones=3, habitat-changes=218, humified=1254, retained-humus=663
garden 0, restore=True:  plants=36, clones=3, habitat-changes=218, humified=1254, retained-humus=663
garden 1, restore=False: plants=38, clones=4, habitat-changes=261, humified=1355, retained-humus=694
garden 1, restore=True:  plants=38, clones=4, habitat-changes=261, humified=1355, retained-humus=694
```

- `clones` は実際の根からの出生数、`habitat-changes` は6投以上続いた居場所の変化数、`humified` は累計の腐植生成量、`retained-humus` は最終時点の量。同じ庭の継続/復帰は全ハッシュが一致し、異なる庭の最終ハッシュは異なる。
- v6の1536投（定着69/99、成長10/37、最大9/10組、最後31/40株）、v5の1536投、v4の960投も従来通り通過。v3初期/24投、v4初期/48投、v5初期/96投の既知ハッシュを維持した。
- v6改修前 `0523e33` で採取した初期/96投のハッシュを今回から固定し、v7で変わらないことを確認した。

```text
v6 initial: 9fa9c77ef7aa922143e84873086c10ca0a05304773950b3fed2289b9c6529aaf
v6 96 rolls (v6-golden): 2963c9a052d431e2a321286ef846fa2e57fd742407a2a6fcde0da25c390c5f00
C# 9 syntax: 65 files, 0 errors (syntax only; Unity type checking requires the editor).
```

- リポジトリ指定のpnpm 11はこの環境で依存ビルドの許可規則により起動できないため、前回同様pnpm 10で同じスクリプトを実行した。

```text
$ npx --yes pnpm@10 typecheck
> tsc -b --noEmit
$ npx --yes pnpm@10 test
 Test Files  41 passed (41)
      Tests  183 passed (183)
$ npx --yes pnpm@10 build
> tsc -b && vite build
✓ 102 modules transformed.
✓ built in 463ms
```

- Webビルドの `jeep-sqlite` による既存の `crypto` browser externalization警告は残る。VSTestは `VSTEST_ARTIFACTS_DIRECTORY` 未設定の案内を出したが、101件は正常終了した。
- `git diff --check`: 成功。既存実験の `src/`、凍結プロトコル、`Simulation`、台帳の書き込み経路は変更していない。外部乱数API呼び出し、時刻依存、保存済みジャーナルの編集経路は追加していない。
### 未確認・申し送り
- Unityエディタ/実行可能なWebGLプレイヤーがこの環境にない。Unityの型解決・全EditMode・土の色と子株の光の描画・フィルタ/スクロール/タッチ入力・音・fpsは未検証。101件は実C#の.NET実行であり、Unity検証済みとは扱わない。
- Unity 6000.3.23f1で、新しい来場者→庭の成長→腐植・相性・根の待機理由→子株の由来と境目を越えた履歴を確認する。幼生を含む既存の姿、狭いWebGL枠での観察の長文と操作、描画負荷も確認が必要。
- 旧v3〜v6のジャーナル復帰で同じ規則が続くことをUnity側でも確認する。値は投数で進むゲームの調整値であり現実の土壌/生物の推定値ではない。配布ビルド・提出画像・ストア公開は更新せず、PR #30はUnity確認前のドラフトで継続する。
### 触ったファイル
- Domain: HabitatState.cs、HabitatSerializer.cs、HabitatRules.cs、HabitatInspection.cs（新規とmeta）、EcosystemState.cs、EcosystemSerializer.cs、Flora.cs、GardenTypes.cs、LivingEcosystem.cs、SuccessionRules.cs、Ecology.cs、EcologyInspection.cs、EcologyObservations.cs
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: IslandTerrain.cs、GardenLife.cs、GardenUi.cs
- Tests: HabitatTests.cs（新規とmeta）、CommunityTests.cs（v6で固定）
- docs/unity-garden/ECOLOGY.md・ECOSYSTEM_V7.md、tools/ecology-check/README.md、PROGRESS.md

## Ecology v8: ダイスと各環境の働きかけをつなぐ（2026-11-13）
### 完了したこと
- 各環境・生態系をダイスなどのユーザーの働きかけに対応させる依頼に対し、同じ生態系深化のPR #30へ追加した。
- 「庭を観察」で水辺・岩陰・草木の陰・乾いた草地・花の草地を次の届け先に選べるようにした。株の負担、眠る種、住人と餌不足、落ち葉と養分を見てその環境内の区画を決める。環境がまだない時は元の場所抽選へ明示的にフォールバックする。
- 実際の中心を準備時に保存し、面を公開した後の届け先変更で動かさない。七の目も保存済みの中心を使う。復帰で確定済みの意図も引き継ぐようにし、以前の `LastIntentKind/Value` が初期値のままになる経路を修正した。
- 追加の光・風を自然シミュレーションと同じ上限・距離減衰・地形遮蔽から求める。風の面だけで庭全体の綿毛の親を対象にせず、実効風が届く親と、その強さによる東への距離で散布する。
- 追加の光が届いた適合する元気な花は、実在する養分1を蜜1へ使う。湿りの履歴・養分・藻の空きがある水辺では、追加の光が藻の生産へつながる。蜜はその後の訪花、藻は既存の水辺の食物網で実際に消費される。
- 面の候補の必要度にも、範囲内の株の湿りの好み・水辺の住人・実り・蜜や藻の空き・眠る種を反映した。6面の構成と重みの下限は維持し、意図で出目を調整する経路は追加していない。願いが的中した時の既存の成長の贈り物も、今回の実際の到達範囲内の株に限定した。
- 同じ開始状態・天候でダイスの寄与がなかった40tickも読み取り用に計算し、水量と既存株の自然成長の差を保存する。水が下流へ届く場合、強い風で乾いて生長が減る場合、天候だけでダイスの接触がない場合も区別する。
- 結果と履歴に、開始時点の環境・届いた区画・水差・生長差・実際の播種や追加の蜜/藻/綿毛の種を表示する。後の庭から過去の環境を推測し直さない。
- 観察に環境ごとの4系統の働き、今回の中心、各面が直接届く土の数と環境を追加し、庭へ中心の四角い輪郭を表示した。種まきは距離2〜3の周囲に届くため、中心と別の環境が播種先になる場合もその通り表示する。七の目の範囲も増幅済みの入力で表示する。
- 新規庭だけ `ecology.v8 / habitat-focus.v8 / ecological-faces.v8`。`habitat.linkedCare` を保存し、旧v3〜v7の庭は従来の配面・生態を維持する。自然シミュレーションの式は変更していない。
### 検証結果（実際の出力）
- `.NET SDK 8.0.428` でUnity内の実際のDomain / Application / InfrastructureとEditModeテストを直接実行した。Unity依存のPresentationTests 5件を除き、既存101件＋新規36ケース。

```text
$ dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release --no-restore --nologo --logger 'console;verbosity=normal'
Test Run Successful.
Total tests: 137
     Passed: 137
 Total time: 1.1896 Minutes
```

- 5環境の届け先と全ての適格な入力を通し、種の実際の播種先、光と風の遮蔽と上限、蜜の消費、水辺の必要条件、下流の水への作用、乾燥時の生長減少、意図による出目不変、天候だけの接触0を確認した。
- v8は192投×2庭×継続/24投ごとの準備・意図確定後のJSON復帰の768投。coordinatorが生成したメモリジャーナルのpayloadを正規JSONへ往復し、全投の状態ハッシュと一投の反応記録が一致した。ファイルに書いた768投の復帰試験とは区別する。実ファイルの検証は既存のジャーナルテストに含まれる。

```text
care garden 0, restore=False: plants=36, recipes=8, fallback=4, nectar=22, algae=4
care garden 0, restore=True:  plants=36, recipes=8, fallback=4, nectar=22, algae=4
care garden 1, restore=False: plants=29, recipes=8, fallback=16, nectar=9, algae=1
care garden 1, restore=True:  plants=29, recipes=8, fallback=16, nectar=9, algae=1
```

- `recipes` は実際に出た入力の種類数、`fallback` は選んだ環境が不在だった準備数。`nectar/algae` は追加の光による実際の生成量。状態と一投の反応を通じ、水量・養分・腐植・土の種・住人の上限と、開始時点の環境への対応を確認した。
- v7の1536投（子株3/4、環境変化218/261、最終腐植663/694）、v6/v5それぞれの1536投、v4の960投も従来通り通過。v3〜v6の既知ハッシュを維持し、v7改修前 `c84fa6f` で採取した初期/96投のハッシュも今回から固定した。

```text
v7 initial: fb35c4d8310fb9f7fd49b6f2db0bbf10bfff142a8cdd5f841d385e4e863e11fc
v7 96 rolls (v7-golden): 4c849755daac0320dd5c2da45f9419a5fb2c8b182326b817572f60eed60b07e6
C# 9 syntax: 69 files, 0 errors (syntax only; Unity type checking requires the editor).
```

- 前回同様、この環境のpnpm 11の依存ビルド許可規則を避け、pnpm 10で同じ既存スクリプトを実行した。

```text
$ npx --yes pnpm@10 typecheck
> tsc -b --noEmit
$ npx --yes pnpm@10 test
 Test Files  41 passed (41)
      Tests  183 passed (183)
$ npx --yes pnpm@10 build
> tsc -b && vite build
✓ 102 modules transformed.
✓ built in 298ms
```

- `jeep-sqlite` の既存の `crypto` browser externalization警告と、VSTestの `VSTEST_ARTIFACTS_DIRECTORY` 未設定の案内は残る。各コマンドは正常終了。
- `git diff --check`: 成功。追加C#のUnityメタデータを作成し、GUIDの重複と欠落を確認した。既存実験の `src/`、凍結プロトコル、`Simulation`、台帳の書き込み経路は変更していない。外部乱数API、時刻依存、過去のジャーナルの編集経路は追加していない。
### 未確認・申し送り
- この環境にはUnityエディタ/実行可能なWebGLプレイヤーがなく、Unityの型解決・全EditMode・描画・入力・音・fpsは未検証。137件は実C#の.NET実行であり、Unity検証済みとは扱わない。
- Unity 6000.3.23f1で各環境を選ぶ→現在の一投は維持→次の準備で中心が切り替わること、不在時の説明、中心の輪郭と区画、各面/七の目の範囲、実際の光と風の弱まり、水や生長の差、保存復帰を確認する。観察の長文、結果の2行表示、狭い画面とタッチ操作も確認対象。
- 比較計算は自然計算部分の差であり、全ての分解・定着・芽吹きの因果がそのダイスに帰属すると扱わない。値はゲームの調整値。旧v3〜v7の復帰と新規庭だけの採用もUnity側で確認する。
- 配布ビルド・提出画像・ストア公開は更新せず、PR #30はUnity確認前のドラフトで継続する。
### 触ったファイル
- Domain: CareRules.cs、CareTargets.cs、CareReport.cs（新規とmeta）、HabitatState.cs、HabitatSerializer.cs、EcosystemState.cs、FaceGenerator.cs、LuckySeven.cs、LivingEcosystem.cs、CommunityRules.cs、Ecology.cs、EcologyObservations.cs
- Application: GardenCoordinator.cs、HistoryProjector.cs
- Presentation: GardenBootstrap.cs、GardenUi.cs、GardenView.cs
- Tests: CareTests.cs（新規とmeta）、HabitatTests.cs（v7で固定）
- docs/unity-garden/ECOLOGY.md・ECOSYSTEM_V8.md、tools/ecology-check/README.md、PROGRESS.md

## Care guidance: お世話の見通しと、届いた土のその後（2026-11-13）
### 完了したこと
- 継続して磨く依頼を受け、ダイスと環境の対応を読む表示を同じPR #30へ追加した。生態・配面・自然シミュレーション・保存形式はv8のまま維持し、新しい庭の規則版は作らない。
- 保存済みの株・眠る種・水辺の家族から、補いたい条件と待つ条件を読む。元気な株にも保ちたい湿りを示す。各環境3件・最大15件へ絞り、画面では選んだ環境の上位3件を表示する。
- 公開中の各面が出た場合を、保存された中心・天候と実際の自然シミュレーションで計算する。天候だけの場合と分けて、湿りの範囲へ近づく面と外れを生む/広げる面を表示する。重複面は別の枠として数え、七の目は実際の増幅済み入力を使う。
- 土の薄い入口で、乾きから過湿へ振れるケースを確認した。反対側へ飛び越す場合を改善とは表示しない。休眠・空き地・養分不足などは、湿りの変化だけで解決したと扱わない。
- 花蜜や綿毛の種には、必要な追加の光や風が届く面を示す。到達と芽吹き・回復・繁殖の成立を区別する。見通し用の種には一時IDだけを使い、外部乱数・ID発行・台帳の書き込みを呼ばない。
- 「恵みが届いた土、その後」を観察と記録へ追加。同じ区画へ届いた最も新しい前の投と、12投以内の実際の芽吹き・回復・負担・結実・定着・子育て・喪失・乾燥・還元・環境の変化を結ぶ。隣の区画、同じ投、未解決の投、接触記録のない庭へ補わない。
- 当時の居場所で振り返りを絞り、後の庭から過去を作り直さない。同じ投や関係の重複を除き、最近24件を保持する。同じ場所の後の出来事として表示し、他の投や天候も関わることを明記する。
- 見通しの自然計算は観察画面を開いた時に行い、開始状態・公開面・天候が同じ間は再利用する。フィルタや願いの選択だけで再計算しない。画面を閉じている時は見通し用の自然計算を行わず、準備や七の目が変われば更新する。
### 検証結果（実際の出力）
- `.NET SDK 8.0.428` でUnity内の実際のDomain / Application / InfrastructureとEditModeテストを直接実行した。Unity依存のPresentationTests 5件を除く、既存137件＋新規28ケース。

```text
$ dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release --no-restore --nologo --logger 'console;verbosity=normal'
Test Run Successful.
Total tests: 165
     Passed: 165
 Total time: 53.3119 Seconds
```

- 公開された各面と実際の自然計算の一致、天候だけでの回復、正常な湿りからの悪化、乾きから過湿への行き過ぎ、七の目・重複面、休眠・空き地・養分不足、水辺の幼生の優先、到達の機会、件数と列挙順の安定性を確認した。
- 4投で育てた実際の庭を準備済みJSONから復帰し、観察対象を含む同じ見通しが再構築されることを確認した。その後の観察はJSONの実記録形式から、同じセル・前の投・12/13投の境界・重複・負の出来事・run分離・当時の分類を検証した。
- 見通しと振り返りを読む/読まない2通りで、実coordinatorの96投を実行。全投の状態ハッシュと反応記録が一致し、観察で台帳の件数や準備状態が変わらないことを確認した。

```text
guidance inspect=False: rolls=96, final-plants=26
guidance inspect=True: rolls=96, final-plants=26
v8 initial: 668ca78cd1c4d4153e0b7ad093ac0475dd8a800e2d3f1d6e263209442996e8ae
v8 96 rolls (v8-golden): 4e19b9158377f916864048584c6eb45b08700076a0842a61be4c90d7cb3ca297
```

- v8の既知ハッシュは改修前 `94b3cc7` の実行から採取した。v8の768投、v7/v6/v5それぞれ1536投、v4の960投、v3〜v7の既知ハッシュのテストも維持して通過した。
- 前回と同じ環境でpnpm 10を使い、既存のスクリプトを実行した。

```text
$ npx --yes pnpm@10 typecheck
> tsc -b --noEmit
$ npx --yes pnpm@10 test
 Test Files  41 passed (41)
      Tests  183 passed (183)
$ npx --yes pnpm@10 build
> tsc -b && vite build
✓ 102 modules transformed.
✓ built in 446ms
C# 9 syntax: 72 files, 0 errors (syntax only; Unity type checking requires the editor).
```

- `jeep-sqlite` の既存の `crypto` browser externalization警告、VSTestの `VSTEST_ARTIFACTS_DIRECTORY` 未設定の案内は残るが、各コマンドは正常終了した。`git diff --check`、Unityメタデータの存在とGUIDの重複チェックも成功した。
- この追記でDomain、既存実験の `src/`、凍結プロトコル、台帳の書き込み経路は変更していない。表示用の投影から状態・配面・抽選・過去のジャーナルを書き換える経路を追加していない。
### 未確認・申し送り
- Unityエディタ/実行可能なWebGLプレイヤーがないため、Unityの型解決・全EditMode・描画・操作・音・fpsは未検証。165件は実C#の.NET実行であり、Unity検証済みとは扱わない。
- Unity 6000.3.23f1で、観察を開く/閉じる、フィルタ、通常面と七の目、次の準備、準備済み復帰、当時の分類での振り返りを確認する。初回表示の応答時間、長文と小画面・タッチ/ホイール、閉じたままの連続プレイも確認が必要。
- 湿りの見通しは自然計算部分であり、生態全体の結果や成功確率ではない。その後の記録は同じ場所の観察で、特定の一投だけが原因だとは扱わない。配布ビルド・提出画像・ストア公開は更新せず、PR #30はUnity確認前のドラフトで継続する。
### 触ったファイル
- Application: CareOutlook.cs、CareAftercare.cs（新規とmeta）
- Presentation: GardenUi.cs
- Tests: CareGuidanceTests.cs（新規とmeta）
- docs/unity-garden/CARE_GUIDANCE.md・ECOLOGY.md、tools/ecology-check/README.md、PROGRESS.md

## Event expression: 起きたことで伝わる庭へ（2026-11-13）
### 完了したこと
- 「基本説明よりは起きたことで説明・表現する」という指定に合わせ、普段の結果・観察・記録・図鑑を出来事が先に出る形へ変更した。同じPR #30の生態系深化の範囲として実装し、生態・配面・保存形式はv8のまま維持する。
- `GardenMoments` を追加。解決済みの関係・成長イベントと、その投の終了状態から短い場面を作る。同じセル・種類の蜜の補充と訪花、藻の生長と食事、小虫の捕食と株の回復など、記録がそろった組み合わせだけを結ぶ。
- 「蜜を補ったデイジーに、花蜂が訪れた」「ミミズがほどいた落ち葉の一部が、腐植になった」など、関わった命と実際の変化を伝える。居場所の境目を越えた根、土の種の目覚め、結実、成長や定着も扱う。
- いるだけの住人数から誕生を作らず、0量の資源から食事や散布の成功を作らない。喪失・弱り・乾燥・還元も残し、花を閉じた株や入れ替わった株に咲いている花として焦点を当てない。
- 一投の大きな文と光の輪郭を、同じ出来事のセルへ合わせた。観察から出来事を選ぶ場合も、文を大きく表示してパネルを閉じ、実際の場所を示す。投擲再生中はこの操作を無効にし、過去の植物を現在の庭へ作り直さない。
- 観察の初期表示は最近の出来事とその後の記録にした。見通し・比較値・条件・仕組みは任意の詳細へ移し、その詳細を開くまで見通し用の自然計算を行わない。記録の通常表示も出来事を中心にし、元のカードの言葉と比較値は詳細表示に残す。
- 図鑑の冒頭にあった長い一般説明を、その庭の最近の出来事へ置き換えた。種類ごとの条件と役割は参照として残す。保存復帰後の大きな文も最後の解決済みの場面から戻す。
- ライブと履歴は同じ投影を使用。履歴の復元は最近12投に限定し、当時の終了状態・種類・居場所を使う。現在の庭から過去を推測せず、未解決の投・他run・記録にない訪問や感情を補わない。
### 検証結果（実際の出力）
- `.NET SDK 8.0.428` でUnity内の実際のDomain / Application / InfrastructureとEditModeテストを直接実行。Unity依存のPresentationTests 5件を除く既存165件＋新規30ケース。

```text
$ dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release --no-restore --nologo --logger 'console;verbosity=normal'
Test Run Successful.
Total tests: 195
     Passed: 195
 Total time: 54.3952 Seconds
actual scenes: 32 live rounds; latest 3 rounds match JSON history
```

- 同じセル・種類・株IDの組み合わせ、離れた出来事や0量を結ばないこと、存在と誕生の区別、喪失、5つの居場所、閉じた花・入れ替わった株、種まきと芽の由来、表示順の安定性を検証した。
- 実coordinatorで32投を解決し、ライブの文と場所を保存。JSON復帰後の最新3投について、文・詳細・場所・種類・順序が一致した。投影の前後で状態ハッシュ・台帳の件数・元のカードの焦点が変わらないことも確認した。
- v8の768投、v7/v6/v5それぞれ1536投、v4の960投、v3〜v8の既知ハッシュ、見通しと振り返りの有無を比較する96投も維持して通過した。
- 前回と同じ環境でpnpm 10を用い、既存スクリプトを実行した。

```text
$ npx --yes pnpm@10 typecheck
> tsc -b --noEmit
$ npx --yes pnpm@10 test
 Test Files  41 passed (41)
      Tests  183 passed (183)
$ npx --yes pnpm@10 build
> tsc -b && vite build
✓ 102 modules transformed.
✓ built in 455ms
C# 9 syntax: 74 files, 0 errors (syntax only; Unity type checking requires the editor).
```

- `jeep-sqlite` の既存の `crypto` browser externalization警告、VSTestの `VSTEST_ARTIFACTS_DIRECTORY` 未設定の案内は残るが、各コマンドは正常終了した。`git diff --check`、追加C#のメタデータとGUID重複チェックも成功した。
- Domain、既存実験の `src/`、凍結プロトコル、台帳の書き込み経路は変更していない。元の `selectedMessage`、抽選、保存済みの状態や台帳の編集経路を追加していない。光の焦点は表示用の位置指定で、保存したカードの焦点へ書き戻さない。
### 未確認・申し送り
- Unityエディタ/実行可能なWebGLプレイヤーがない。Unityの型解決・全EditMode・描画・操作・音・fpsは未検証。195件は実C#の.NET実行であり、Unity検証済みとは扱わない。
- Unity 6000.3.23f1で、出来事の文と輪郭の一致、観察の初期表示、場所を見る時のパネル閉鎖、詳細との往復、低刺激表示、図鑑、過去の場所、保存復帰後の最後の文を確認する。文字の収まり・小画面のスクロールとタッチ・初回表示の応答時間も要確認。
- 文はゲーム内の実記録の抜粋であり、特定の蜜一単位を誰が飲んだかや、その一投だけが原因かまでは断定しない。普段は出来事を先に伝える方針を今後も維持する。
- 配布ビルド・提出画像・ストア公開は更新せず、PR #30はUnity確認前のドラフトで継続する。
### 触ったファイル
- Application: GardenMoments.cs（新規とmeta）
- Presentation: GardenBootstrap.cs、GardenUi.cs、GardenView.cs
- Tests: GardenMomentsTests.cs（新規とmeta）
- docs/unity-garden/EVENT_EXPRESSION.md（新規）・EXPERIENCE.md・CARE_GUIDANCE.md・ECOLOGY.md、tools/ecology-check/README.md、PROGRESS.md

## Garden memory: 一株と一粒の続きから、世代を読む（2026-11-13）
### 完了したこと
- 継続の依頼に対し、同じPR #30の生態系を深める範囲で、出来事から株・種の歩みへ移る入口を追加した。「基本説明よりは起きたことで説明・表現する」方針を維持し、生態・配面・保存形式はv8のままにする。
- 観察で最近12投の出来事を一件ずつ送り/戻しでき、選んだ文と場所の対象を切り替えられる。居場所で絞り、株・種が特定できた場面だけ「この株の歩み」「この種の歩み」を出す。過去の出来事へ現在の振り返りを混ぜない。
- 読み取り用の `GardenMemories` を追加。ダイスの種、初期の株、土の種からの芽、つる・地下茎の子株を区別し、実際の由来と直近8件の節目を読む。成長だけでなく、光を受けた蜜の補充、花粉の到着、結実、回復、花を閉じたこと、土への還元も含める。
- 保存された株ID・種ID・親株IDで、親株 → 鳥/風が運んだ種 → 子株、または親株 → つる/地下茎の子株をたどる。親が土へ還った後も続きに移れる。記録に存在する対象だけを結び、続きは現在も残るものを優先して最大8件を抜粋。種・子株の総数はIDで重複を除いて併記する。
- 同じ場所・種類だけから親子を作らない。株IDを持たない関係は、その投の終了状態にいる同種の株へ結び、同じセルの還元/入れ替わりの投では省く。種が落ちた入口には、同じ投・出生投・種類・運び手・出発/到着セルで一粒だけ特定できることを要求する。
- ない記録から誕生・枯死・種の失効を作らず、最初/最後に確認できた投を示す。各節目の面と居場所は当時の記録から読み、生まれた投のスナップショットがなければ現在で補わない。過去の命を今の庭へ作り直さない。
- 歩みを開いた時だけ同じrunの保存記録を走査し、表示する節目に必要な状態だけ復元する。自然の再計算は行わない。観察を閉じた連続投擲では歩みを再走査せず、次に表示する時に更新する。返す表示データにジャーナルの参照を残さない。
- 追加した操作列の下に本文が重ならないよう、アンカーとpivotから余白を確認して本文領域を調整した。上端基準の操作列はy=178〜212、本文はy=-168〜170で8pxの間隔を取り、下の操作列との間隔も8pxを保つ。実描画は引き続きUnity確認対象とする。
### 検証結果（実際の出力）
- `.NET SDK 8.0.428` で実際のUnity内Domain / Application / InfrastructureとEditModeテストを直接実行。既存195件に18ケースを追加した。Unity依存のPresentationTests 5件は除外している。

```text
$ dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release --no-restore --nologo --logger 'console;verbosity=normal'
Test Run Successful.
Total tests: 213
     Passed: 213
 Total time: 55.5369 Seconds
life histories: 96 real rounds; 27 exact identities and 45 lineage links match JSON history without mutation
actual scenes: 32 live rounds; latest 3 rounds match JSON history
```

- 親の死後の種/子株、鳥・風・つる・地下茎、同じセルの別株、種IDや場所が食い違う候補、休息、光・受粉・結実、0量、欠けた記録、居場所の変化、抜粋と総数、run分離、読み取り前後の不変性を検証した。
- 実coordinatorの96投から、最近12投の入口27個体と45本の由来/続きリンクをJSON復帰後と照合した。どのリンクも実在する記録へ移れること、文と親の一致、状態ハッシュと台帳内容が変わらないことを確認した。32投の既存照合も、文・場所・順序に加えて歩みの入口IDを含む。
- 旧v3〜v8の既知ハッシュ、長期継続と復帰、各生態の資源収支・出生上限、ダイスの作用と意図による出目不変のテストも維持して通過した。

```text
$ npx --yes pnpm@10 typecheck
> tsc -b --noEmit
$ npx --yes pnpm@10 test
 Test Files  41 passed (41)
      Tests  183 passed (183)
$ npx --yes pnpm@10 build
> tsc -b && vite build
✓ 102 modules transformed.
✓ built in 253ms
C# 9 syntax: 76 files, 0 errors (syntax only; Unity type checking requires the editor).
Unity metadata: 76 C# files have meta; 103 unique GUIDs
```

- 前回と同様にpnpm 10で既存スクリプトを実行した。既存の `jeep-sqlite` による `crypto` browser externalization警告とVSTestの環境変数案内は残るが、各コマンドは正常終了した。`git diff --check` も成功した。
- Domain、既存実験の `src/`、凍結プロトコル、台帳の書き込み経路は変更していない。表示用のIDを保存形式へ追加せず、元のカード・状態・抽選へ書き戻す経路を追加していない。
### 未確認・申し送り
- Unityエディタ/実行可能なWebGLプレイヤーがないため、Unityの型解決・全EditMode・ビルド・描画・入力・音・fpsは未確認。213件は実C#の.NET実行であり、Unity検証済みとは扱わない。PR #30はドラフトで継続し、配布ビルドや公開は更新しない。
- Unity 6000.3.23f1で、出来事の送り/戻しと居場所切替、親株/種/子株/出来事への往復、複数の続き、見通し詳細との往復、過去の場所の輪郭、閉じたままの連続投擲後の再表示、保存復帰を確認する。追加行の文字と操作、小画面・タッチ/ホイールの収まり、長い履歴で歩みを初めて開く時の応答時間も確認が必要。
- 歩みは全保存記録からの抜粋である。IDのない住人個体の系譜や、特定の一投だけが原因だったという断定は追加しない。普段は実際の出来事を先に伝える方針を維持する。
### 触ったファイル
- Application: GardenMemories.cs（新規とmeta）、GardenMoments.cs
- Presentation: GardenUi.cs、GardenBootstrap.cs
- Tests: GardenMemoriesTests.cs（新規とmeta）、GardenMomentsTests.cs
- docs/unity-garden/GARDEN_MEMORY.md（新規）・EVENT_EXPRESSION.md・EXPERIENCE.md・ECOLOGY.md、tools/ecology-check/README.md、PROGRESS.md

## Parallel work: 最新の統合と、仕組み/見かけの分担（2026-11-13）
### 完了したこと
- ユーザーから「一度最新までマージ」「Codexは仕組み側、Claude Codeは見かけ側で並行してぶつからないように」と明示された。PR #30のゲーム変更と分担の準備を今回の統合対象にする。
- `AGENTS.md` とClaude Codeが読む `CLAUDE.md` に分担の入口を追加した。具体的な担当・接続・Git運用は `docs/unity-garden/PARALLEL_WORK.md` へ記録した。
- CodexはDomain / Application / Infrastructureと仕組みのテスト、Claude CodeはPresentation全体・GardenBootstrapの表示接続・素材・シーン・PresentationTestsとUnity確認を担当する。既存の公開APIを保ち、担当外に必要な変更は具体的な引き継ぎとして記録する。
- ブランチとcheckout/worktreeを分離する。進捗は担当別の作業ファイルへ分け、共有のPROGRESS.mdやプロジェクト設定を同時編集しない。過去の進捗や相手の変更を上書きしない。
### 検証結果
- 統合対象のゲームcommit `ccd7a970916f4d4d795c7294649ab8839602c5e6` について、GitHub Actionsの `CI`（run 448）と `Garden ecology`（run 9）が両方 `success` であることを再確認した。C#213件・Web183件の実行結果は直前の項目に記載済み。
- 今回追加するのは分担用Markdownのみ。ゲーム実装、テスト、保存形式、既存実験、凍結プロトコルを変更しない。既存の同名CLAUDE.mdや担当指示がないことを確認して追加した。
### 未確認・申し送り
- Unityのコンパイル・描画・入力・音・性能は引き続き未確認。ユーザーの明示的な指示によりmainへ統合するが、検証済みや配布済みという意味へ変えない。
- Claude Codeは最新mainを取得し、CLAUDE.mdと分担文書を読んで見かけ用ブランチを開始する。担当境界を越える修正は必要な接続を記録して順番に扱う。
### 触ったファイル
- AGENTS.md、CLAUDE.md、docs/unity-garden/PARALLEL_WORK.md、PROGRESS.md

## Jam 祈りの星 — 出目で変わる祭壇の星・ラッキーナンバー・八の虹・ドレミの音（2026-11-13）

### 完了したこと

- 祭壇のサイコロの下に「祈りの星」を置いた（Domain/PrayerStar.cs、prayer-star.v1、ecology.v4）。一投ごとに保存済みの事実だけで星が決まる。
  五芒星（命の星）／六芒星（前の一投と恵みの陰陽が入れ替わった、または願いが叶った）／七芒星「ラッキーナンバー」（七の目、または前の目＋今の目＝7）／
  八芒星「八の虹」（ラッキーナンバー＋陰陽の結びか願い＋調和7以上）。偶然は使わない。
- 星は生態系の育ち方を変える: 花でない若い株へ 五=+5×5株、六=+16×6株、七=+77×7株、八=花でない全株に+88。水は動かさない。記録は `Resolved.ecology.prayerStar` と effects kind=`star`。
- 虹の八（rainbow.v3）: 八芒星でも虹がかかる（従来の幸運の七＋庭の応答も継続）。
- 見せ方（Presentation/AltarSigil.cs、新規）: 祭壇を囲む光の星をゆっくり回し、一投後に古い星が回りながら縮んで消え、新しい星が現れて落ち着く。
  八芒星は虹の八色。七・八では「ラッキーナンバー ７」「八芒星・八の虹」と、前の目・今の目・偶然の確率（1/6 または 1/7）を表示。恵みを受けた株に光が降りる。動きを抑える設定では回転しない。
- 音（AudioSynth / GardenAudio）: 出目 n の音（D長調 1=ド … 7=シ）と、星の和音（五=ド・ソ、六=ド・ミ・ソ、七=ド・ミ・ソ・シ、八=ドからドへ音階を昇って和音）。
- 設計: docs/unity-garden/ECOLOGY.md §8。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="50" passed="50" failed="0"`（HEAD の 41 + 新規 9）。macOS ビルド `Succeeded`。
- 星の出現率（720投の計測）: 五 25.8%、六 54.6%、七 16.7%、八 2.9%。虹の八は約18投に1回（rainbow.v2 だけなら約26投に1回）。星の恵みは1投平均 159.4。
  初案（八芒星の調和しきい値6）では八が 5.3%・虹が13投に1回と多すぎたため、しきい値を7にした。
- 音の実測（claude-music-ear / pyloudnorm、再生音量込み）: 出目の音 約 −24.7 LUFS、星の和音 −24〜−28 LUFS。既存の鐘 −19.5、開花 −19.9、星 −20.9、幸運の七 −19.2 より 5〜7 LU 小さい。
  出目の音の基本周波数は D長調から 0.4 Hz 以内（ド 587.5 … シ 1108.7 Hz）。クリップ 0。
- macOS 隔離プレビュー（別の会社名・製品名・バンドルID の一時ビルド）で、10投後の五芒星、七芒星とラベル、虹色の八芒星を撮影して確認した。
- `pnpm typecheck` 終了コード 0、`pnpm test` `183 passed (183)`、`pnpm build` `✓ built`（依存確認は無効化して既存の依存関係で実行）。

### 要確定・申し送り

- 星が切り替わる回転アニメーションと、恵みの株に光が降りる演出は、静止画の撮影でしか確認していない（実際の一投での動きは未確認）。本人が実機で一投して確かめること。
- 音は実測のみで、耳での確認は本人に依頼する。既存の鐘（`AudioSynth.Bell()` の既定 523.25 Hz = C5）は D長調の外の音。今回は変えていない。
- QA の注意: macOS ビルドの CFBundleIdentifier を後から書き換えても保存先は分かれず、本来の庭（`Destiny Garden/Destiny Garden`）に書き込まれる。
  今回一度これで自動10投が本来の台帳に追記されたため、追記前の先頭 24,490 バイトに戻して SHA-256 一致（d605e4eb…）を確認した。隔離には会社名・製品名・バンドルIDを変えてビルドすること。
- Codex と並行作業のため、このブランチ（`claude/prayer-star`）は df92f63 から分岐。GardenBootstrap.cs は数行の追加だけに留めた（Codex 側の未コミット変更と同じファイル）。

### 触ったファイル

- Domain: PrayerStar.cs（新規）、Ecology.cs。Application: GardenCoordinator.cs、HistoryProjector.cs。
- Presentation: AltarSigil.cs（新規）、AudioSynth.cs、GardenAudio.cs、GardenBootstrap.cs（数行）。
- Tests: PrayerStarTests.cs、SacredTonesTests.cs（新規）。docs/unity-garden/ECOLOGY.md §8、PROGRESS.md。

## Jam 祈りの星の磨き込み — 七への灯・陰陽の六芒星・D長調の鐘・動きの確認（2026-11-13）

### 完了したこと

- 七への灯: 直前の目 n のとき、祭壇の星の外側を 7−n 個の小さな灯がゆっくり巡る（次にその目が出ればラッキーナンバー）。言葉では説明しない手がかり。
  再開時も記録の最後の目から灯を戻す（`AltarSigil.ShowSaved(history)`、`PrayerStar.FaceCompletingSeven`）。
- 六芒星を陰（水色）と陽（金色）の二つの三角で描くようにした。
- 一投ごとの鐘を C5 から D5（上のド）にして、出目の音・星の和音と同じ D長調にそろえた。
- QA専用の起動引数 `-dgRevealFrames DIR`（`-dgStarReveal N -dgFace F -dgPrevFace P`）: 祭壇の近くから星の切り替わりを0.2秒ごとに保存する。振らず、台帳にも書かない。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="52" passed="52" failed="0"`（七への灯と最後の目の読み取り、鐘の基本周波数の2件を追加）。
- 隔離プレビュー（別の会社名・製品名・バンドルID）で七・六・八の切り替わりを各22コマ撮影: 古い星が回りながら縮み、新しい星が大きめに現れて落ち着く、
  金の光の粒が昇り、恵みの株に光が降りる、六芒星の二色、八芒星の虹色と虹色の粒、七への灯を確認した。本来の庭の台帳の SHA-256 は撮影前後で d605e4eb… のまま。
- 鐘の実測: −19.5 → −19.1 LUFS（音量はほぼ同じ）、基本周波数 D5。

### 要確定・申し送り

- 七への灯は控えめな表示にした。気づきにくければ数や明るさを上げる。
- 撮影用の近いカメラでは、ラベルがサイコロの後ろに一部隠れる（撮影専用の表示方法のため。通常のプレイでは画面の最前面に出る）。
- 実機での一投と耳での確認は本人に依頼する。

### 触ったファイル

- Domain/PrayerStar.cs、Presentation/AltarSigil.cs・AudioSynth.cs、Presentation/GardenBootstrap.cs（前回足した1行の差し替えだけ）、Tests/EditMode/SacredTonesTests.cs、docs/unity-garden/ECOLOGY.md §8.3、PROGRESS.md。

## Jam 祈りの星を島の下の海へ・サイコロと連動（2026-11-13）

### 完了したこと

- 本人の指定（「サイコロと下の芒星はリンクするように」「芒星はそこじゃなくて島の下のやつ」）を受け、祈りの星を祭壇から**島の下の海**
  （七つの灯の輪、半径 13.4）へ移した。以前そこに描いていた調和の芒星は表示しない（`SacredAtmosphere.SetStar(0)`）。調和の芒星が増えたときの音と一行も外した
  （祈りの星の音・表示と重ならないように）。
- サイコロとの連動: 星はサイコロのゆっくりした回転に歯車のように合わせて回り（×0.35）、サイコロが転がる間は一緒に勢いよく回る。
  答えが出るとサイコロから海の星へ光の糸が降り、そこで星が切り替わる。七への灯も海の輪の上を巡る。
- 名前を `AltarSigil` から `PrayerStarSigil` に変えた。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="52" passed="52" failed="0"`。隔離プレビューの macOS ビルド `Succeeded`。
- 隔離プレビューで、サイコロを転がしてから答えを出す流れを0.2秒ごとに30コマ撮影した（`-dgRevealFrames`、振らず台帳にも書かない）。
  転がる間に星の先が輪に沿って動くこと、サイコロから海へ降りる光の糸、八芒星の虹色、その後もサイコロの回転に合わせて星が回り続けることを確認した。
  通常の視点（6投後）でも、島の外側に星の先が見えることを確認した。本来の庭の台帳の SHA-256 は d605e4eb… のまま。

### 要確定・申し送り

- 海の星は島に隠れて先だけが見える（以前の調和の芒星と同じ見え方）。もっと見せたい場合は線の太さ・明るさ、または島の外へ広げる。
- GardenBootstrap.cs の変更: 調和の芒星の表示を 0 にし、その音と一行の3行を削除した（Codex 側はこの行を変えていない）。

### 触ったファイル

- Presentation/PrayerStarSigil.cs（AltarSigil.cs から改名）、Presentation/GardenBootstrap.cs、Tests/EditMode/SacredTonesTests.cs、docs/unity-garden/ECOLOGY.md §8.3、PROGRESS.md。

## Jam 星の頂点に灯・出目で育ち方が変わる庭（prayer-star.v2・flora.v2）（2026-11-13）

### 完了したこと

- 本人の指定「芒星の頂点に点が来るように（動いている時はよい）」: 星の各頂点の水面に光の点を置いた。海の七つの灯籠（SacredAtmosphere の SevenLights）は、
  星が落ち着くと頂点の真上へ寄り、動いている間は遅れてついていく。五・六芒星では余った灯籠が小さくなって消え、八芒星では八つめの灯籠を加える。
  灯籠は横位置だけを動かす（SacredAtmosphere は上下の揺れだけを書き換えるため衝突しない。Codex 版の水面の映り込みにも対応）。
- 七への灯を海の輪からサイコロのまわりへ移した（海の輪の上には頂点以外の点を置かない）。
- 出目で庭の育ち方が変わる仕組みを二つ足した（ECOLOGY.md §7.3a・§8.1a）:
  - prayer-star.v2: 星の恵みが先に届く株を、その一投の恵みで変える（水=乾いた土、光=日向の日向好き、風=蜂と蝶に好かれる種、種=若い株）。
  - flora.v2: 芽吹きの種の重みに、直近8投の恵みの傾きを足す（水=湿り好き、光=日向好き、風=風散布・虫に好かれる種、種=実のなる種）。
- 計測テスト `GardenPathsTests.Measure_GardensFollowTheirBlessings` を追加（水・光・風・種のどれかが半分出る4通りの祈り方で、8つの庭×48投）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="55" passed="55" failed="0"`。隔離プレビューの macOS ビルド `Succeeded`。本来の庭の台帳の SHA-256 は d605e4eb… のまま。
- 祈り方ごとの庭（48投後、8庭の平均。前は prayer-star.v2 のみ、後は flora.v2 も適用）:
  - 水: 湿り好き 10% → 15%、上位 タンポポ19% コスモス12% アイリス9% キンポウゲ9%、土の湿り 121‰
  - 光: 日向好き 27% → 39%、上位 タンポポ23% コスモス14% ポピー12% ヒマワリ9%
  - 風: 上位 タンポポ30% コスモス12% 野ばら11% デイジー10%（花 21.4 と最少）
  - 種: 株 53.6・花 32.3 と最多、上位 タンポポ18% 野ばら15% コスモス11% 野イチゴ11%
- 星の出現率（720投）: 五 24.3%、六 55.1%、七 18.2%、八 2.4%。虹の八 約18投に1回。
- 隔離プレビューの撮影: 七・五芒星で、頂点の水面の点と、その真上に寄った灯籠を確認した。動いている間の遅れも確認した。

### 要確定・申し送り

- タンポポがどの祈り方でも最多。土が全体に乾いていて（67〜121‰）、多くの種の湿りの下限（250〜300‰以上）に届かず、合う種がない場所は先駆けのタンポポになるため。
  日陰好き（ホタルブクロ・ジギタリス）はほぼ0%。水の量を変えるのは自然シミュレーション（水収支の検証つき）の変更になるので、今回は触っていない。
- サイコロのまわりの七への灯は、明るい草の上では淡い（少し大きくしたが、大きくした後の見え方は未撮影）。
- 灯籠は SacredAtmosphere の内部の名前（SevenLights / Light* / Lantern reflection）で探している。Codex 側で名前が変わると灯籠は動かない（星は表示される）。

### 触ったファイル

- Domain: PrayerStar.cs、Flora.cs、Ecology.cs。Presentation: PrayerStarSigil.cs。
- Tests: PrayerStarTests.cs、FloraTests.cs、GardenPathsTests.cs（新規）。docs/unity-garden/ECOLOGY.md §7.3a・§8、PROGRESS.md。

## Jam 土の乾きとタンポポの調査・芽吹きの多様さ（flora.v2 の続き）（2026-11-13）

### 調べたこと（`SoilDiagnosisTests.Measure_WhyNothingFits`、6庭×60投）

- 合う種がない場所での芽吹きは 0%。タンポポが最多なのは「乾いて何も合わないから」ではなかった。
- タンポポは土の硬さ・湿り・水捌けのすべてに合うので、どこでも候補に入る。芽吹きの候補は平均4.3種で、同じ重み2なので約1/4がタンポポになり、
  ほかの種は場所ごとに分かれるため、庭全体ではタンポポが最多になっていた（芽吹きの24%、サイコロの種の32%）。
- 土は実際に乾いている: 植えられる55セルの35%が湿り100‰未満、平均317‰。1投あたり蒸発 約1179、入る水は サイコロ 約373 ＋ 天気 約633。
  種が合わない理由で最も多いのは乾き。前回の「土の湿り 67〜121‰」は岩や土のないセルも含めた全256セルの平均で、植えられる土の値より低く出ていた。
- 岩陰の土は硬さ +150 で 500 以上になるが、日陰の種の上限は 500・480 だったため、日陰の種はほぼ根づけなかった。

### 完了したこと

- flora.v2 に追加: 先駆けの遠慮（合う種が4種以上の場所ではタンポポの基本の重み 2→1）、混み合い（距離2以内の同じ種 n 株で重み ×3/(3+n)）、
  日陰の種の硬さの上限 650（ホタルブクロ・ジギタリス）。
- 水の量（自然シミュレーション）は ECOLOGY.md §1 の不変の決まりなので変えていない。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="57" passed="57" failed="0"`。
- 芽吹きがタンポポになる割合 24% → 15%、サイコロの種では 32% → 12%。日陰の種 0% → 2〜3%。
- 祈り方ごとの庭（8庭×48投、前 → 後）: 多様さ（Simpson）水 0.90→0.91、光 0.88→0.90、風 0.85→0.87、種 0.89→0.90。
  タンポポ 水 19→17%、光 23→15%、風 30→28%、種 18→17%。水の庭は15種すべてが現れる。風の庭は風散布のタンポポの原っぱのまま（意図どおり）。
- 星の出現率（720投）: 五 23.6%、六 57.1%、七 17.2%、八 2.1%。虹の八 約20投に1回。

### 要確定・申し送り

- 土をもっと潤すには、蒸発・雨・サイコロの水の量（自然シミュレーション）を変える必要があり、これは凍結された決まりの変更になる。本人の判断が要る。

### 触ったファイル

- Domain/Flora.cs、Tests/EditMode/FloraTests.cs・GardenPathsTests.cs・SoilDiagnosisTests.cs（新規）、docs/unity-garden/ECOLOGY.md §7.3a、PROGRESS.md。

## Jam めぐり — 実って土に還る庭（lifecycle.v1）・虹は八芒星と一緒に（2026-11-13）

### 完了したこと

- 本人の判断「より仕組みとしてうまくいっている方で」: 水の量（自然シミュレーション）は変えず、長く遊ぶと庭が止まる問題を直した。
- 調査（`LongPlayTests`、6庭×200投）: 60投で全セルが埋まり、その後は芽吹き 0、サイコロから種の面が消え、200投で全株が花になって止まっていた。
- めぐり（Domain/Lifecycle.cs、ECOLOGY.md §9）: 花は咲いている投数を数え、種ごとの寿命（タンポポ 12、一年草 20、ほか 30）で土に還ってセルを空ける。
  一投で1株まで。となりの株に +40。`PlantState.BloomRolls` は 0 のとき保存しない（以前の状態のハッシュは不変）。土に還った場所に光の粒。
- 虹の八は八芒星のときだけにした（rainbow.v3）。八芒星の調和の条件を 7 → 8 に上げた（めぐりで調和が上がったため）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="61" passed="61" failed="0"`（めぐりの決まり・保存・長期プレイの3件を追加）。隔離プレビューの macOS ビルド `Succeeded`。
- 長期プレイ（6庭×200投、前 → 後）: 100投以降の芽吹き 0 → 約0.6/投、種の面 0 → 0.2〜0.4/投、200投の花 55（全株）→ 約21、種の数 12.5 で固定 → 11〜13 で入れ替わる。
- 星（120投×6）: 五 26.3%、六 53.2%、七 17.4%、八 3.2%。虹の八 約31投に1回。調和の平均 5.7。
- 隔離プレビューで80投後の庭を撮影: 芽・葉・蕾・花が混ざり、サイコロに種の面が戻っていることを確認。例外 0。本来の庭の台帳の SHA-256 は d605e4eb… のまま。

### 要確定・申し送り

- 寿命・1投1株・+40 は初期値。花が少なく感じれば寿命を延ばす（そのぶん入れ替わりは遅くなる）。
- サイコロのまわりの七への灯（大きくした後）はまだ撮影で確認できていない（撮影用の視点が島の中心向きのため）。

### 触ったファイル

- Domain: Lifecycle.cs（新規）、GardenTypes.cs（BloomRolls）、CanonicalJson.cs（保存）、Ecology.cs、PrayerStar.cs。Presentation: PrayerStarSigil.cs。
- Tests: LifecycleTests.cs・LongPlayTests.cs（新規）、EcologyTests.cs、PrayerStarTests.cs、LuckySevenTests.cs（計測の虹を現行の決まりに）。docs/unity-garden/ECOLOGY.md §8・§9、PROGRESS.md。

## Jam 因果の強化 — 受粉と実り（lifecycle.v2）・一投ごとの因果の一行（2026-11-13）

### 完了したこと

- 本人の方向付け「因果関係をより強化」。それまで蜂と蝶の訪れは次の代に関係していなかったため、つないだ（ECOLOGY.md §9.4）:
  - 受粉: 蜂・蝶が訪れた花に印（`PlantState.Pollinated`、true のときだけ保存）。同じくらい好まれる花は投ごとに順番が回る。
  - 実り: 受粉した花が土に還るとき、その場所に種を残す（親の種 +10、祈りの傾き、混み合い）。訪れのない花の跡は空き、サイコロや生き物の種の場所になる。
  - 蝶は恵みが4種そろったときだけ来るので、出目のそろい方がどの花の系統が続くかを決める。
- 一投ごとに因果を一行で表示（PrayerStarSigil）: 星の恵みがどの株へ（出た恵みしだい）、花が土に還った・実りを落とした。記録された effects だけから作る。文字に縁取りを付けた。
- 八芒星の調和の条件は 7 に戻した（8 では八芒星 1.5%・虹 約65投に1回と少なすぎた）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="64" passed="64" failed="0"`（受粉の保存、受粉した花だけがその場所に実りを残す、因果の一行の3件を追加。既存の種の置き場所のテストは「実りは親の跡」に合わせた）。
- 実り: 土に還った花 110 のうち 66 が実りを残し、25 が親と同じ種（200投）。長期プレイで実り 0.3〜0.5/投、受粉した花 5〜6 輪、種の数 12 前後。
- 星（120投×6）: 五 25.1%、六 56.5%、七 13.2%、八 5.1%。虹の八 約19投に1回。120投後 株 53・花 20.5。
- 隔離プレビューで因果の一行の表示位置を確認（撮影用のカメラ描画ではぼけの効果が文字にもかかるため、読みやすさは通常のプレイでの確認が要る）。本来の庭の台帳の SHA-256 は d605e4eb… のまま。

### 要確定・申し送り

- 因果の一行の読みやすさ（縁取り後）は実機で未確認。
- 実りは親の跡だけに残す決まりにした（近くの空き地に落とすと、サイコロの種の場所を奪い、種の面と調和が下がったため）。

### 触ったファイル

- Domain: GardenTypes.cs・CanonicalJson.cs（Pollinated）、Ecology.cs（受粉・実り・訪れの順番）、Lifecycle.cs、PrayerStar.cs。Presentation: PrayerStarSigil.cs。
- Tests: LifecycleTests.cs、EcologyTests.cs、PrayerStarTests.cs、SacredTonesTests.cs、LongPlayTests.cs。docs/unity-garden/ECOLOGY.md §9.4・§9.5、PROGRESS.md。

## Jam: 水辺と島の構図の磨き込み（2026-11-13）

### 完了したこと

- 島ごとの水際に沿って浅瀬の色と細かな波を描き、沖の深い青緑へつないだ。川の面は地形の細かな起伏に沿わせ、河口は海に溶け込むようにした。
- タイトルから庭へ入るカメラと通常の視点を引き、瓶の輪郭、島の岸、手前の川を画面に収めた。岩の根元を地面に接地させ、サイコロに丸い面取りと発光する象嵌を加えた。
- 地面の土と濡れた砂の色、霧と発光の強さ、操作ボタンと背景の明暗差を調整した。結果欄は表示する内容があるときに出す。

### 完了基準の結果

- Unity 6000.3.23f1 の macOS と WebGL ビルドは成功し、ビルドエラーは 0 件。EditMode は 41 件すべて成功。
- macOS で初期の庭と 12 投後の庭を同じ島と保存記録から比較した。タイトルからの開始、一投と結果表示、ドラッグによる回転、視点リセットを確認した。WebGL は Chrome で起動と一投を確認し、ゲームのコンソールエラーは 0 件だった。
- 既存アプリの型検査、テスト 183 件、Vite ビルドは成功。pnpm の依存確認は警告モードで実行し、インストール済みの依存関係を使用した。
- ゲームの抽選・庭の計算・台帳の実装に差分はなく、検証前後の既存の庭の保存ファイルは SHA-256 が一致した。

### 確認範囲

- macOS とデスクトップ版 Chrome で確認。スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の Presentation、ラグーンのシェーダーとそのビルド設定、本進捗記録。

## Jam: 草地と岸辺の植生の磨き込み（2026-11-13）

### 完了したこと

- 草の配置と地面の色に共通の模様を使い、細い葉が同じ風に沿って揺れる草地にした。土の見える範囲と岸辺の緑は、境界をゆるやかにつないだ。
- 岸の花と川辺の葦を、環境に合った場所にまとまって生えるように配置した。庭の種を小さな細長い形にし、開花した花の形と色が俯瞰でも見えるようにした。
- 岩の苔を岩肌の質感に重ねた。地面で光る苔は、近くの生息地の光を重ねて濃くせず、地形に沿うひとつの面にした。木の葉の輪郭と俯瞰時のガラスの反射もやわらげた。

### 完了基準の結果

- Unity 6000.3.23f1 の macOS・WebGL ビルドはエラー 0 件で成功。EditMode は 41 件すべて成功。
- 同じ島と保存記録で、12 投後と 22 投後の変更前後を比較した。初期の庭と別の島も描画し、俯瞰、通常、近距離を確認した。macOS とデスクトップ Chrome の WebGL で、一投後の結果表示を確認した。
- 既存アプリの型検査、テスト 183 件、Vite ビルドが成功。pnpm は依存確認を警告モードで実行した。
- 抽選・庭の計算・保存処理に差分はなく、検証前後で既存の庭と前回プレビューの保存ファイルの SHA-256 は一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の植生、地面、岩、発光の描画と、植生・ガラスのシェーダー、本進捗記録。

## Jam 海岸線と水際の磨き込み（2026-11-13）

### 完了したこと

- 岸の形と標高を島ごとに作り、張り出した岬、岩棚、河口の入り江、傾斜の緩い砂浜を配置した。岩棚には地層と苔を重ね、岬の先に一部が水面から出る岩を置いた。
- 浜と岩場の水深に応じて浅瀬の色と白波の幅を変えた。川の幅と岸の濡れ方を調整し、海に向かって水面がなじむようにした。
- 初回の庭にも岸辺と川沿いに草と苔を生やし、中央には植え付け用の土を残した。七つの灯を結ぶ線を細くした。

### 完了基準の結果

- Unity 6000.3.23f1 の macOS と WebGL ビルドは `Succeeded (0 errors)`。EditMode は `total="44" passed="44" failed="0"`。3 種の島で海岸線の起伏、川の始点と終点、栽培区画の高さを確認した。
- macOS で 22 投後の同じ庭を斜めと俯瞰から比較し、初回の庭、低い視点、別の島も確認した。macOS とデスクトップ版 Chrome の WebGL で、一投後の結果表示と保存を確認した。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built`。pnpm は依存確認を警告モードで実行した。
- 抽選・庭の計算・保存処理に差分はなく、既存の庭と前回までのプレビューの保存ファイルの SHA-256 は検証前後で一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の地形、川、植生、岩、周辺演出の描画とラグーンのシェーダー、EditMode テスト、本進捗記録。

## Jam 木の枝ぶりと葉の磨き込み（2026-11-13）

### 完了したこと

- 広葉樹、花木、柳、針葉樹、低木の5種類を作り直した。幹は根元を広げて枝を曲げ、広葉樹は枝ごとに葉を重ねた。柳には弧を描く細枝と細長い葉、針葉樹には先端へ向かって短くなる枝を配置した。
- 花木には枝先に五弁の花を咲かせ、花びらが回りながら落ちるようにした。葉を透かす光と木漏れ日の影を追加し、風で動く葉と影が一緒に動くようにした。動きを抑える設定では、揺れと落花を止めた。
- ダイスの台座に近い木の位置を調整した。macOS のプレビューは、育った木をすぐに見られるよう専用の保存先に22投後のサンプルを入れた。

### 完了基準の結果

- Unity 6000.3.23f1 の macOS と WebGL ビルドは `Succeeded (0 errors)`。EditMode は `total="46" passed="46" failed="0"`。各樹種の面の向きと花びらの透過を確認した。
- 22投後の庭を複数の島で、斜め、低い視点、俯瞰から確認した。macOS と Chrome の WebGL で一投と保存を確認し、Chrome では22投後の庭も再読込して表示した。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built`。pnpm は依存確認を警告モードで実行した。
- 抽選・庭の計算・保存処理に差分はなく、既存の庭の保存ファイルは検証前後で SHA-256 が一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の樹木メッシュと配置、植生シェーダー、花びらの描画、EditMode テスト、本進捗記録。

## Jam 庭の草花と成長段階の磨き込み（2026-11-13）

### 完了したこと

- 庭で育つ草花15種の立体形状を作り直した。デイジーには細い花びら、アイリスには立ち上がる花びらと垂れる花びら、野イチゴには三つに分かれた葉と小さな実を作った。コスモスの細かく分かれた葉、ホタルブクロの下向きの筒、ジギタリスの片側に並ぶ花は、NC State Extension と RHS の資料を参考にした。[コスモス](https://plants.ces.ncsu.edu/plants/cosmos-bipinnatus/)、[ホタルブクロ](https://plants.ces.ncsu.edu/plants/campanula-punctata/)、[ジギタリス](https://www.rhs.org.uk/plants/5862/digitalis-purpurea/details/)
- 種、双葉、本葉、つぼみ、開花の形を分け、種類に応じて茎の曲がり方や葉のつき方を変えた。花と葉に透過光と影を付け、開花の光と蝶が寄る位置を花の高さに合わせた。
- macOS のプレビュー専用の保存先に22投後の庭を用意した。同じ庭の斜めと俯瞰の比較画像、15種の開花一覧を作成した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の macOS と WebGL ビルドは `Succeeded (0 errors)`。EditMode は `total="47" passed="47" failed="0"`。15種の5段階すべてで面積のある三角形と表示範囲を確認した。
- macOS で初回と22投後の庭を描画し、別の新しい庭で8投して種から開花まで確認した。デスクトップ版 Chrome では起動、一投後の保存、22投後の庭の再読込を確認し、ブラウザとシェーダーのエラーは0件だった。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built`。pnpm は依存確認を警告モードにして既存の依存関係で実行した。
- 差分は Unity の表示と検証に収まり、既存の庭と以前のプレビューの保存ファイルは検証前後で SHA-256 が一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の草花メッシュと描画、蝶と成長の演出、EditMode テスト、本進捗記録。

## Jam 庭の生き物と妖精・天使の磨き込み（2026-11-13）

### 完了したこと

- 花蜂の縞と花粉、蝶の翅の模様と翅脈、トンボの細長い胴と四枚の翅、蛍の淡く点滅する腹部を作った。花蜂と蝶は訪れた花の高さに寄り、トンボは水たまりの上、蛍は光る苔の近くを飛ぶ。虫の形と発光の位置は博物館や公園局の資料を参考にした。[NPS の花蜂の形](https://www.nps.gov/articles/000/upload/Bees-of-Alaska.pdf)、[Smithsonian のトンボの飛翔](https://howthingsfly.si.edu/ask-an-explainer/how-do-dragonflies-fly)、[AMNH の蛍](https://www.amnh.org/explore/ology/ology-cards/345-common-eastern-firefly)
- 小鳥は岩に足を置いて羽をたたみ、種を運んだときは羽ばたいてくちばしから種を落とす。ミミズは湿った土の表面で伸び縮みし、魚影は水が流れ出たときに岸沿いを尾でかいて進む。
- 願いの精霊を、花びらの衣と透ける翅を持つ妖精として描き、恵みが届いた植物に舞い降りるようにした。虹の場面では、羽根を重ねた翼と光輪を持つ三体の天使がゆっくり昇る。虹の間は遠方のぼけを抑え、演出後に元のカメラと設定へ戻す。
- macOS のプレビュー専用の保存先に22投後の庭を用意した。同じ庭と虹の変更前後、妖精・蛍・小鳥・魚影の拡大画像を作成した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の macOS と WebGL ビルドは `Succeeded (0 errors)`。EditMode は `total="48" passed="48" failed="0"`。保存状態に対応する住人の表示と、いなくなったときの非表示を確認した。
- macOS で22投後の庭、虹、最初の一投と保存を確認した。隔離した Unity 検証で、固定の乱数を使った一投の結果から、種を運ぶ小鳥の離陸と帰着、植物に降りる妖精、虹の天使の退場とカメラ復帰を確認した。水が539単位流れ出るテストで魚影を表示し、夜の蛍と動きを抑える設定での蝶・妖精の停止も確認した。
- デスクトップ版 Chrome の WebGL では、起動、一投後の保存、22投後の庭の再読込を確認した。ブラウザとシェーダーのエラーは0件だった。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built`。pnpm は依存確認を警告モードにして既存の依存関係で実行した。
- 差分は Unity の表示と検証に収まり、既存の庭と以前のプレビューの保存ファイル5件で、検証前後の SHA-256 が一致した。

### 確認範囲

- 妖精、小鳥の種運び、魚影と虹の演出は macOS で確認した。スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の生き物と精霊の模型、翅のテクスチャ、庭と虹の演出、EditMode テスト、本進捗記録。

## Jam 島の光と空気感の磨き込み（2026-11-13）

### 完了したこと

- 前回までのローカル差分を別の Git worktree とブランチ `codex/garden-atmosphere` に引き継いだ。Unity のキャッシュ、ビルド出力、プレビューの保存先は専用の場所を使った。
- 保存済みの天気と投数から、雲の量、水平線の色、太陽と月の見え方を変えた。空と海が同じ空の色を参照し、遠くの海面は水平線へ溶け込み、近くでは夕日や月の光が波に沿って揺れる。描画座標の変換には URP の関数を用いた。[Unity の URP シェーダー資料](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-transformations.html)
- 夜には小さな星と七つの灯の水面への映り込みを加えた。瓶の反射と島を照らす光を和らげた。動きを抑える設定では、雲、水面、七つの灯の動きがその場で止まる。
- macOS のプレビュー専用の保存先に22投後の庭を用意した。同じ庭の昼・夕暮れ・夜の変更前後と、晴れ・薄曇り・小雨の画像を作成した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の macOS と WebGL のビルドは `Succeeded (0 errors)`。EditMode は `total="49" passed="49" failed="0"`。天気と雲量の対応、時刻が一周する際の空と月のつながりをテストした。
- macOS で22投後の庭を斜め・俯瞰・反対側から描画した。虹と天使の表示、動きを抑える設定での雲と水面の停止・再開、23投目の保存ファイルへの追記を確認した。
- デスクトップ版 Chrome で WebGL の起動、一投後の保存、22投後の庭の再読込を確認した。ブラウザとシェーダーのエラーは0件だった。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built`。pnpm は依存確認を警告モードにして既存の依存関係で実行した。
- 元の作業フォルダの差分と従来の保存ファイル2件を照合し、作業開始時点と一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の空と海の描画、照明、シェーダーのビルド設定、EditMode テスト、本進捗記録。

## Jam 瓶と庭の外観の磨き込み（2026-11-13）

### 完了したこと

- 滑らかな肩、厚みのある口、丸い底を持つガラス瓶を作った。空の色、太陽と月を映す反射と、水際に広がる淡い光を加えた。ガラスの手前と奥の面には別々のカリングを使った。[Unity の ShaderLab 資料](https://docs.unity3d.com/6000.0/Documentation/Manual/SL-Cull.html)
- ホイールで瓶全体まで引けるようにした。近景と俯瞰では反射が薄くなり、花と生き物が見やすくなる。引いた画角でも庭にピントと木陰が残るよう調整した。昼の直射光を和らげて、葉や砂浜の明暗を残した。
- 起動画面で瓶の全体を見せ、クリックまたは Enter で庭へ入れる構図にした。従来の説明は「遊び方」から読める。
- 作業は専用 worktree の `codex/garden-atmosphere` で実施した。Claude Code の `claude/prayer-star` と共通する `GardenBootstrap.cs` は一時コピーで三方向比較を行い、競合マーカーが生じないことを確認した。macOS プレビューは専用の保存先に、撮影に使った22投後の庭を用意した。元の worktree の差分と既存の保存ファイル3件を作業前後で照合した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の EditMode は `total="49" passed="49" failed="0"`。macOS と WebGL のビルドはそれぞれ `Succeeded (0 errors)`。
- macOS で16:9と4:3の起動画面、晴れ・夕暮れ・夜・小雨の遠景、近景、俯瞰、虹と天使を描画した。22投後の保存を読み込み、23投目が追記され、元のバイト列と全フレームのハッシュが有効であることを確認した。
- Chrome の WebGL プレイヤーで起動、一投目の保存、22投後の庭の再読込、「遊び方」の開閉、視点の回転・ズーム・復帰を確認した。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 287ms`。pnpm は既存の依存関係で実行した。
- 変更は Jam の表示と撮影範囲。台帳と実験の不変ルールに反する経路を追加していない。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。

### 触ったファイル

- Unity の瓶メッシュ、ガラスと海のシェーダー、照明と影、視点、起動画面、本進捗記録。

## Jam 庭と一緒に育つ家（2026-11-13）

### 完了したこと

- 庭を始めた時点から、玄関灯と飛び石のある小さな家を置いた。島ごとに川、岸、花壇、祭壇との距離を調べて敷地を取り、土台と階段を傾斜に合わせた。
- 4投目でベンチと屋根のある縁側が加わる。10投目には、それまでの成立した投で最も多かった恵みから、水辺のテラス、風を聴くロフト、光の温室、花の工房のいずれかが建つ。同数なら先に出た恵みを使う。18投目で花箱や風鈴などが加わる。
- 右側の「家を見る」で近づき、「視点を戻す」で庭に戻れる。暗くなると窓と玄関灯が暖かく光る。同じ材質の部材は結合して描画する。[Unity の Mesh.CombineMeshes 資料](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.CombineMeshes.html)
- 家の段階と増築は既存の投の記録から復元する。表示側で完結し、抽選・生態・台帳の不変ルールに反する経路を追加していない。専用 worktree とプレビュー用の保存先を使用した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の EditMode は `total="62" passed="62" failed="0"`。段階の境目、4種類の分岐、同数の場合、後続の出目による建て替わりの防止、保存復元をテストした。
- macOS と WebGL のビルドは `Succeeded (0 errors)`。macOS では0投目、4投目、4種類の完成例を複数の島で撮影した。22投済みの庭では実際の保存から温室を復元し、昼夜を描画した。23投目の追記後にフレームのハッシュと既存のバイト列を照合した。
- デスクトップ版 Chrome で「家を見る」、新しい庭から4投、22投後の庭の再読込、視点復帰、来場者の切り替えを確認した。ブラウザとシェーダーのエラーは0件だった。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 309ms`。pnpm は既存の依存関係で実行した。

### 確認範囲

- 家と増築部分の外観、縁側、温室内の鉢植えを描画した。屋内への移動とスマートフォン実機での操作は確認範囲外。

### 触ったファイル

- Unity の家の進行、敷地、建物メッシュ、材質、カメラ、表示、植生配置、EditMode テスト、本進捗記録。

## Jam 家と玄関先の磨き込み（2026-11-13）

### 完了したこと

- 木の扉に板の継ぎ目と真鍮の取っ手、窓に鎧戸、縁側の屋根に瓦の重なりを加えた。玄関先の飛び石に沿って地面を砂利色になじませ、道の脇に鉢植えと草花を置いた。敷地に合わせて岸、川、花壇の近くでは鉢と石の配置を控えた。
- 水辺のテラスに石の水鉢と銅の水栓、風を聴くロフトに風見と風鈴、光の温室に植え替え用の作業台、花の工房に縞の布の日よけを作った。雨で瓦に艶が出て、夜は玄関と温室が灯る。動きを抑える設定では水鉢の水面模様、風見、風鈴が止まる。
- 家の外観は投の記録から復元される。差分は Unity の表示に収まり、台帳と実験の不変ルールに反する経路を追加していない。専用 worktree の `codex/garden-atmosphere` とプレビュー用の保存先を使用した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の EditMode は `total="62" passed="62" failed="0"`。macOS と WebGL のビルドは `Succeeded (0 errors)`。
- macOS で最初の家、実際に4投した縁側、4種類の増築を撮影した。増築一覧の撮影用の出目はメモリに設定した。別に既存の22投後の保存を復元し、温室を昼・夕暮れ・小雨・夜に描画した。水鉢、風見、風鈴を動かす時間が、動きを抑える設定で止まることを確認した。macOS のローカルプレビューで23投目を追記し、フレームのチェックサムと元の保存のバイト列を照合した。
- デスクトップ版 Chrome の WebGL で「家を見る」、新しい庭から4投して縁側を表示、22投後の庭の復元、視点復帰、来場者の切り替えを確認した。ブラウザとシェーダーのエラーは0件だった。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 363ms`。pnpm は依存確認を警告モードにして既存の依存関係で実行した。
- 元の worktree の差分と従来の保存ファイル3件を作業前後で照合し、一致を確認した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。
- `claude/prayer-star` と統合する際は、双方が末尾に追記した本進捗記録の調整が必要。

### 触ったファイル

- Unity の家のメッシュ、敷地、材質、草の配置、地面の色、本進捗記録。

## Jam 24投目から育つ4種類の家（2026-11-13）

### 完了したこと

- 4投目の縁側、10投目の増築、18投目の飾り付けに続き、24投目に母屋の屋根が変わる段階を加えた。19〜24投目の成立した投で最も多かった恵みを採用し、同数ならこの区間で先に出た恵みを使う。10投目に決まった増築と組み合わさる。
- 水は丸い銅屋根と鎖樋の「雨音の家」、風は布張りの羽根が回る「風車の家」、光は藍色のドームと望遠鏡を持つ「星見の家」、種は草花と小窓のある「草屋根の家」になる。「家を見る」で家の名前と姿を確認できる。
- 家は保存済みの投の記録から描画する。変更は Unity の表示に収まり、台帳と実験の不変ルールに反する経路を追加していない。作業は `codex/garden-atmosphere` の worktree と専用のプレビュー保存先で行った。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の EditMode は `total="69" passed="69" failed="0"`。4種類の分岐、23投目と24投目の境目、同数時の判定、後続の投による変化の防止、保存復元を確認した。macOS と WebGL のビルドは `Succeeded (0 errors)`。
- 固定乱数を使い通常の投の処理を24回通した庭から4種類を復元し、macOS で撮影した。別に、23投後の庭で「サイコロを振る」を押し、縁側と増築が残ったまま風車が建つこと、動きを抑える設定で羽根が止まることを確認した。星見の家を入れた macOS プレビューで25投目を追記し、既存の保存のバイト列と各フレームのチェックサムを照合した。
- デスクトップ版 Chrome の WebGL では、新しい庭の起動、23投後の庭の復元、24投目の保存、再読込後に同じ家を表示することを確認した。ブラウザとシェーダーのエラーは0件。
- `pnpm typecheck` は終了コード 0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 267ms`。pnpm は依存確認を警告モードにして既存の依存関係で実行した。
- 元の worktree の差分と従来の保存ファイル3件が作業前後で一致した。

### 確認範囲

- スマートフォン実機での表示とタッチ操作は未確認。Claude Code 側との統合時には、双方が末尾に追記した本進捗記録の調整が必要。

### 触ったファイル

- Unity の家の成長判定、母屋の形状と演出、家の EditMode テスト、本進捗記録。

## Jam 祈りの星・生態系・家の統合（2026-11-13）

### 完了したこと

- `origin/main` の生態系・お世話・庭の歩みを土台に、Claude Code の祈りの星と実り（`a960489`）、Codex の瓶・島・生き物・家（`41a2894`）をマージした。双方のコミットと進捗記録を引き継いだ。
- 新規の庭では `ecology.v8` の受粉・結実・種の来歴を使う。その投の調和が決まった後に `prayer-star.v2` の恵みが届き、`flora.v3` はその場所との相性と恵みの履歴を種の候補に反映する。画面の星は実際に恵みが届いたセルを照らす。
- 既存の庭は記録された規則で再開する。星や家の分岐が参照する履歴は解決済みの投から復元する。既存実験の `src/`、設計、凍結プロトコルの差分は0件。台帳と実験の不変ルールに反する経路を追加していない。
- 統合は専用の worktree で行い、元の3作業先の差分と保存ファイル20件が作業前後で一致した。

### 完了基準の結果（コマンド出力の要約）

- Unity 6000.3.23f1 の EditMode は `total=270 passed=270 failed=0`。.NET 8 の生態・保存テストは `230/230` 件成功。
- Unity の macOS と WebGL のビルドは `Succeeded (0 errors)`。macOS で Claude Code の80投後と Codex の24投後の庭をそれぞれ復元し、次の一投を追記した。元のバイト列と全フレームのチェックサムを照合した。
- デスクトップ版 Chrome で新しい庭の一投と、24投後の庭で「家を見る」「庭を観察」「見通し・しくみを読む」を確認した。25投目を保存して再読込し、家と投の履歴が戻ることを確認した。ブラウザとシェーダーのエラーは0件。
- `pnpm typecheck` は終了コード0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 620ms`。

### 確認範囲

- スマートフォン実機の表示・タッチ操作と、統合後の音の聞こえ方は未確認。

### 触ったファイル

- Unity の生態と保存復帰、観察と星の表示、瓶・島・植生・住人・家の描画、テスト、生態の仕様、本進捗記録。

## Jam 仕組みの総点検 — 独立レビュー・再開の正しさ・止まる株・見える星の恵み・届け先・願いの重み（2026-11-14）

### 完了したこと

- 本人の方向付け「仕組みを最大限ブラッシュアップ」。全仕組みの寄与を計測する `SystemHealthTests`（成長の内訳・新しい株の出どころ・住人の頻度・3つの祈り方の比較）を作り、
  決まりのコードを独立したレビュー（読むだけ）にかけ、8件の指摘をコードで確かめてから直した（ECOLOGY.md §10）。
- 正しさ:
  - 再開を DATA_CONTRACT §3 どおりにした。`IntentCommitted` 後の再開で願いが消えていた。`DrawEntropyCommitted`・`OutcomeCommitted` 後の再開で乱数を引き直していた。
    出目の並びが二重に数えられ得た。以前からあった不具合で、`ResumeTests` 5件で確認した。
  - 七つめの面がある回のラッキーナンバーの確率表示を 1/7 にした（以前は常に 1/6）。
- 止まる株（lifecycle.v3）: 合う場所で自然の成長がない株は根を張って +30、合わなくなった場所で8投止まった株は枯れる（一投1株）。
  菌根の「網が育ったか」の判定を直し、止まった株へ先に分ける。虫の訪れは、同じ好みならまだ受粉していない花へ先に行く。
- 星の恵みを目に見える形にした（五 +60×1株、六 一段×1株、七 半段×7株、八 一段×全株）。届け先の近く（距離3以内）が先。届け先の近くでは祈りの傾きが2倍。
- 願いの重み: 叶った願いの精霊の数を min(7, 面の数÷合う面の数) にした（面の願い 6、3面の系統 2）。叶った一投の芽吹きは、その恵みに合う種へ +10。
  恵みに「合う種」の定めを広げた（水=湿り好き 300‰〜、種=自分で広がる種）。八芒星の一投では咲いている花がみな受粉する。
- 寿命 1.3 倍（16・26・40）。成り立たない綿毛の天気の条件を外した（振る舞いは不変）。§8.1 の表と実装の食い違い（調和 8 と 7）を直した。
- 一投ごとの因果の一行に、届け先・段階の恵み・枯れたことを加えた。枯れた場所に鈍い色の粒を出す。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="75" passed="75" failed="0"`。隔離プレビューの macOS ビルド `Succeeded`。
- 計測（6庭×60投）: 成長の内訳 自然 47〜51%・星 27〜31%・根 11〜13%。60投後の花 25〜28（直前の版 11〜17）。
  叶った一投の芽吹きが恵みに合う割合 51〜66%（叶わない一投 41〜46%）。虹の八 約15〜51投に1回（祈り方による）。
- 長期プレイ（6庭×200投）: 空く場所 約0.35/投、種の面 0.15〜0.25/投、芽吹き 約1/投、花 21〜31、種の数 11〜12。
- 祈り方の庭（8庭×48投）: 花 18〜29・咲いている種 10〜11（直前 11〜14・7〜9）。水=キンポウゲ・野イチゴ、光=コスモス・ポピー、種=野ばら、風=タンポポの原っぱ。
- 隔離プレビューで40投後の庭（花と蝶、調和 8/8）と、保存からの再開を確認。例外 0。本来の庭の台帳の SHA-256 は d605e4eb… のまま。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- 星の恵みが成長の約3割。自然の恵みより目立たせたくなければ、七の株数か八の段数を下げる。
- 実際の一投での演出（因果の一行の読みやすさ、枯れた粒、七への灯）は実機で未確認。

### 触ったファイル

- Application/GardenCoordinator.cs。Domain: Ecology.cs、PrayerStar.cs、Lifecycle.cs、Flora.cs、GardenTypes.cs、CanonicalJson.cs。Presentation: PrayerStarSigil.cs。
- Tests: ResumeTests.cs・SystemHealthTests.cs（新規）、LifecycleTests.cs、PrayerStarTests.cs、FloraTests.cs、HarmonyTests.cs、EcologyTests.cs、SacredTonesTests.cs。docs/unity-garden/ECOLOGY.md §3・§8・§9・§10、PROGRESS.md。

## Jam 二度目の独立レビュー・実プレイ確認・めぐりの音（2026-11-14）

### 完了したこと

- WebGL ビルドをアプリ内ブラウザ（localhost の別ポート。台帳はそのブラウザの中だけ）で実際に振って確認した。
  - 因果の一行が七・八芒星以外の回で表示されていなかった（ラッキーナンバーの文字と同じキャンバスで透明度 0 のままだった）。別のキャンバスに分け、
    文字を 17 → 20 にし、文字の幅に合わせた半透明の暗い下地を付けた。
  - サイコロのまわりの七への灯は、通常の視点ではボタンの陰で見えなかった。因果の一行の下に「次に n の目が出ればラッキーナンバー（どの目も同じ確率）」を添えた。
  - 前のビルドで保存した庭がそのまま読み込めることも確認した。
- 二度目の独立レビュー（コードのみ読む）の9件をすべてコードで確かめて直した（ECOLOGY.md §11.2）。
  新しい来場者に前の願いが残る（前回の再開修正による）、再開用の保存材料の消し忘れと交代時の未解決、枯れる株への恵み、精霊の上限の順序、
  七芒星の「半段」表示、灯籠の柱の置き去り、実りの「やわらいだ土」の記録、テストの弱さ。
- めぐりの音: 還る（ソ→ド）、実り（ソ→ド→上のド）、枯れる（低いド・ソ）。一投に一つまで。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="78" passed="78" failed="0"`（来場者交代の再開2件、めぐりの音の選び方1件を追加。枯れることを実際に確かめるようにした）。
- WebGL ビルド `Succeeded (22806210 bytes)`。アプリ内ブラウザで振って、因果の一行と次のラッキーナンバーの添え書きが読めることを確認。ブラウザのコンソールに例外 0。
- 音の実測（再生音量込み）: 還る −28.4 LUFS、実り −29.3、枯れる −30.4（初案の単音は −35.7 で聞こえにくかったため空五度にした）。音程は D長調どおり。クリップ 0。
- 計測（6庭×60投）: 成長の内訳 自然 47〜51%・星 26〜31%・根 10〜13%。長期プレイの空き・種の面・芽吹きは前回と同程度。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- 音は実測のみ。耳での確認は本人に依頼する。
- 七への灯（サイコロのまわりの光）は通常の視点では見えにくいまま残している（言葉の添え書きが主）。不要なら消せる。

### 触ったファイル

- Application/GardenCoordinator.cs。Domain: Ecology.cs、LuckySeven.cs。Presentation: PrayerStarSigil.cs、AudioSynth.cs、GardenAudio.cs、GardenBootstrap.cs（自分で足した1行）。
- Tests: ResumeTests.cs、LifecycleTests.cs、SacredTonesTests.cs。docs/unity-garden/ECOLOGY.md §11、PROGRESS.md。

## Jam 似たゲームを参考にした仕組み — 群落と季節の願い（community.v1・season.v1）（2026-11-14）

### 完了したこと

- 本人の方向付け「似たゲームを探し、仕組みとして参考にして修正改善」。Web 検索とページ取得のツールがモデル設定のエラーで使えなかったため、
  アプリ内ブラウザで Wikipedia の Terra Nil・Cascadia・Wingspan・Photosynthesis の記述を読んで確認した（Spirit Island の元素の閾値は
  参照先がボット確認を求めたため使っていない）。ECOLOGY.md §12 に対応表。
- 群落（Domain/Community.cs）: 同じ恵みの核の種の花が隣り合って3株以上で群落。恵みが出ると応えて周りが育つ（Wingspan のエンジン）、
  近くの芽吹きがその種類へ寄って広がる（Cascadia の回廊）、4株以上で蝶・トンボ・小鳥を呼ぶ（Terra Nil の生き物の帰還）。
- 季節の願い（Domain/Season.cs、GardenCoordinator）: 12投ごとに庭に足りないものを一つ願い、叶えると次の回に七つめの面（Wingspan のラウンド目標・Terra Nil の段階目標）。
  記録と再開に対応。
- 表示: 一投ごとの因果の一行に群落の応え、その下に次のラッキーナンバーの目、季節の願い（3行まで）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="84" passed="84" failed="0"`（群落3件・季節3件を追加。幸運の七のテストは「願いが外れても季節の願いが叶った回だけは七つめが出る」に合わせた）。
- 群落: 4庭×80投で群落のある回 75%、群落が応えた回 約0.5/投。生き物の頻度（6庭×60投）: 蝶 32〜38%、トンボ 70〜73%、小鳥 81〜86%（初案は 84〜88%・87〜89%・90% で多すぎたため絞った）。
- 季節: 120投で10季節中4つが叶った。
- WebGL ビルド `Succeeded (22814064 bytes)`。アプリ内ブラウザで振り、因果の一行・次のラッキーナンバー・季節の願い（「咲いている花を5種に（あと 6 投）」）の3行が読めることを確認。
  季節のない旧版で保存した庭も、途中の季節から正しく始まった。コンソールに例外 0。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- 季節の願いの表示は一投ごとの因果の一行の中だけ（常に見える場所には出していない。常設するなら GardenUi 側の領域が要る）。
- 群落の範囲を地面に描く表示はない（応えたときの光の粒と一行だけ）。

### 触ったファイル

- Domain: Community.cs・Season.cs（新規）、Ecology.cs、Flora.cs。Application/GardenCoordinator.cs。Presentation/PrayerStarSigil.cs。
- Tests: CommunityTests.cs・SeasonTests.cs（新規）、LuckySevenTests.cs。docs/unity-garden/ECOLOGY.md §12・§13、PROGRESS.md。

## Jam ラッキーナンバーを主役に — 七つの幸運・ダブル・灯籠と音階・金色の言葉（lucky.v1）（2026-11-14）

### 完了したこと

- 本人の方向付け「ラッキーナンバーというキーワードが輝くように全体的なブラッシュアップ」（ECOLOGY.md §14）。
- 決まり: 庭のラッキーナンバーを数え、7回目ごとに八芒星（七つの幸運・八の虹）、2回続けばダブル ラッキーナンバー（七芒星の恵みが一段）。
  保存された出目の並びから数えるので再開しても同じ。確率は変えない。
- 見せ方: ラッキーナンバーで星の頂点の灯籠が一つずつ灯り、ドレミファソラシ（ド）が上がる。大きな「7」「8」と題・確率・k/7 が少し大きく現れて落ち着く（小さな行には暗い下地）。
  一投ごとの一行で「ラッキーナンバー」と次の目を金色に。
- 均衡: 七つの幸運で虹が増え、実りが場所を埋めたため、受粉した花の実りを 2回に1回にした（lifecycle.v4）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="87" passed="87" failed="0"`（数え方・七つの幸運・ダブル、ラッキーナンバーの文字と金色の語の3件を追加）。
- 計測: 七つの幸運 約42投に1回、ダブル 約23投に1回、虹の八 約19投に1回。長期プレイの種の面 0.08〜0.30/投（直前 0.01〜0.06 に落ちていたのを戻した）。
- 音の実測: ラッキーナンバーの音階 −25.5 LUFS、8音とも D長調どおり、クリップ 0。
- 隔離プレビュー（別の会社名・製品名・バンドルID）で、灯籠が順に灯る様子、「8」と「七つの幸運　・　八の虹」、「7」「ラッキーナンバー」と確率・3/7 の表示を撮影して確認。例外 0。
  本来の庭の台帳の SHA-256 は d605e4eb… のまま。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- 音は実測のみ。耳での確認は本人に依頼する。
- ラッキーナンバーは確率 1/6 の偶然のため、ブラウザでの実プレイでは今回は狙って出していない（隔離プレビューの撮影で確認）。

### 触ったファイル

- Domain: PrayerStar.cs、Ecology.cs、Lifecycle.cs。Application/GardenCoordinator.cs。Presentation: PrayerStarSigil.cs、AudioSynth.cs、GardenAudio.cs。
- Tests: PrayerStarTests.cs、SacredTonesTests.cs。docs/unity-garden/ECOLOGY.md §14、PROGRESS.md。

## Jam 商品化に向けた総点検 — 記録の回復・再開の正しさ・保存失敗・表示の食い違い（lifecycle.v5）（2026-11-14）

### 完了したこと

- 本人の方向付け「商品化として完成する前提で、仕組みを中心に可能な限り全て見て、デバッグも含めて」（ECOLOGY.md §15）。
  遊びの決まりは変えず、記録を失わない・二度引かない・見えたことと保存が食い違わないことを直した。
- 記録（FileJournal）: 書きかけの最後の枠を `.torn-日時` へ、壊れた鎖の記録は全体を `.corrupt-日時` へ残してから、検証できたところまでで続ける
  （以前は、その後に足した枠がすべて読めなくなった）。書き込み失敗時は自分の枠だけ取り下げる。数の読み取りを言語設定に依存させない。読めない枠で例外を出さない。
- 再開: 準備の途中で終わった回を、保存済みの面の材料から同じ面で続けるか `PreparationInterrupted` で閉じる。
  引いた後の保存失敗で庭・季節・数え上げを動かさず、もう一度振ると保存済みの材料から同じ結果（二度引かない）。
  株の名前を回の名前から決め、再開しても同じ。振らずに交代した回は `AbandonedBeforeAcquisition`、閉じた run は再開しない。
  七つめの面がない回の「7の目」の願いは叶う面のない願いとして数える。再開で例外が出ても起動できなくならない。
- 決まりの食い違い（lifecycle.v5）: 同じ一投の生態の一歩で養われた株は「止まっていた」と数えない（光が降りた芽がその場で枯れない）。
- 報いと季節の願い（lucky-seven.v2・season.v2）: 七つめの面の権利を2つまで貯める（重なると失われていた）。願いが七つめの面を呼ぶのは望みの薄い願い（1/3以下）だけ。
  季節の願いを本来の調和の値と、始まりかけた群落から選び、進み具合を出す。枯れた理由を保存して一行で伝える。
- 表示の追加: 画面左上の「季節の願い 2/3（あと 5 投）」「ラッキーナンバー 3/7・次は 4 の目」、願いの結果と確率、一年ごとのふりかえり、初めてのことへの一度だけのヒント。
- 表示: ラッキーナンバーの「偶然なら」を、その回に当たりになる面の数で（七つめの面がある回は 2/7）。前の見せ方が残っていれば止めてから次を始める。
  八芒星の虹色の線が薄くなるとき白線に置き換わるのを直した。星の回転を角速度で判定（低フレームレートで止まっていた）。動きを減らす設定で去る星の回転・拡大もしない。
  星の言葉の画面を本体の UI の下に（タイトルや図鑑の上に出ない）。「UI を隠す」と撮影を本体の画面に正しく向けた（以前は最初に見つかった画面で、星の画面を選ぶことがあった）。
  因果の一行の折り返し、前の投の状態の一行を残さない、受粉した花がないときの虹の一行を出さない、恵みの光の粒を全体の数で配る、毎フレームの割り当てを減らした。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="104" passed="104" failed="0"`（記録の回復4件・再開と保存失敗8件などを追加）。
- 記録の負担（300投・1回の run）: 起動時の全検証 949 ms、プレイ中の1投ごと 読み取り 2.6 ms ＋ 履歴 13.9 ms。記録 5.6 MiB。
- 連続1200投の耐久: 未解決 0・生態の偶然切れ 0・再開比較 49 回一致・1投 平均 6.2 ms（最悪 80 ms）。
- 星（720投）: 五 25.7%、六 58.3%、七 11.0%、八 5.0%、虹の八 約20投に1回。長期プレイの種の面 0.05〜0.25/投。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- 本来の庭の台帳の SHA-256 は d605e4eb… のまま。
- WebGL ビルドをブラウザで実プレイ（14投・再読み込み1回）: ラッキーナンバー（5→2）の「7」と「偶然なら 1/6（2 の目）」「1/7」、
  左上の2行、季節の願いが叶った回の七つめの面、再読み込み後の同じ庭と数え上げ、「UI を隠す」が星の言葉も隠すこと、
  次の投で前の一行が消えることを確認。コンソールのエラー 0。

### 要確定・申し送り

- **main との統合が必要**: main（PR #31）に入っているこのブランチの作業は lifecycle.v2 の回まで。その後の5回（本回を含む）は
  このブランチだけにあり、main の生態系の拡張（ecology.v5〜v8、約3,300行）と GardenCoordinator・Ecology・Flora・PrayerStarSigil・
  GardenBootstrap・CommunityTests・ECOLOGY.md・PROGRESS.md で衝突する。統合は担当を決めてから行う。
- 記録は1投あたり約 18 KiB（保存状態をまるごと持つ）。起動時の全検証は 300投で約 1 秒。run ごとの分割か差分化を、記録の形式の版を上げて検討する。
- 縦長の画面での文字の大きさ（本体の UI と同じ幅基準の縮尺）。
- 履歴の一覧（GardenUi.RefreshHistory、Codex 側）は、成立しなかった回を「0投目」と表示する（まれ: 振っている途中で終了した回）。表示の担当側で扱いを決める。
- 生き物の一歩の「養われたら止まっていない」には専用のテストがない（耐久1200投・長期プレイのテストは通過）。

### 触ったファイル

- Domain: Ecology.cs、Lifecycle.cs、LuckySeven.cs、Season.cs、Community.cs、Flora.cs、PrayerStar.cs、SimpleJson.cs。Application: GardenCoordinator.cs、HistoryProjector.cs。Infrastructure/FileJournal.cs。
- Presentation: PrayerStarSigil.cs、GardenBootstrap.cs（起動時の回復の報告・再開の例外・振るときの保存失敗・UI を隠す/撮影の対象・星の言葉の接続のみ）、GardenUi.cs（履歴の虹の印1行のみ）。
- Tests: JournalRecoveryTests.cs・SoakTests.cs（新規）、ResumeTests.cs、SacredTonesTests.cs、SeasonTests.cs、LuckySevenTests.cs。docs/unity-garden/ECOLOGY.md §15、PROGRESS.md。

## Jam main への統合 — 祈りの星の5回分と生態系（ecology.v5〜v8）（2026-11-14）

### 完了したこと

- `claude/prayer-star` の5回分（総点検・二度目のレビュー・群落と季節の願い・ラッキーナンバー・商品化に向けた総点検）を main（PR #31 以降）へ統合した。
- 衝突の解き方: main の設計（新しい庭は生態系 `LivingEcosystem`、記録した版で古い庭の決まりを固定）をそのまま採り、
  祈りの星 v3（届け先・段階の恵み・ラッキーナンバーの数え上げ・七つの幸運・ダブル）は main の `PrayerStar.ApplyGift` を通して新しい庭にも届くようにした。
  めぐり・実り・止まる株・群落の応え（§17〜§20）は従来の祈りの星の庭（生態系なし）で働く。
- 古い庭を変えない: 株の名前の新方式は `plantIdPolicyVersion = plant-ids.v1` で始めた run だけ。精霊の数・群落の生き物・菌根の新しい分け方は祈りの星の庭だけ。
  main の各版（v4〜v8・従来）の96投の固定ハッシュはすべて一致。
- 祈りの星の版が上がっても、祈りの星で始めた庭は星を持ち続ける（以前は版の文字列が一致しないと星が消える作りだった）。
- 季節の願いの群落の判定は、どちらの庭でも `Communities.Find` で行う。テストの同名クラスは `PrayerCommunityTests` に改名。
- ECOLOGY.md の節番号を main に合わせ（§18〜§23）、統合の対応表を §23.6 に置いた。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="310" passed="310" failed="0"`（main の固定ハッシュのテストを含む）。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- WebGL を統合後にビルドしてブラウザで確認: 統合前の保存（祈りの星の庭・13投）がそのまま続き星と因果の一行が出る。次の来場者の新しい庭（生態系）でも振れて、左上の2行が進む。コンソールのエラー 0。

### 要確定・申し送り

- 生態系の庭では記録が1投あたり約 40 KiB（300投で約 12 MiB）、起動時の全検証は約 2 秒、1投の処理は平均 11.6 ms（最悪 199 ms）。記録の分割・差分化は優先度を上げて検討する。
- 生態系の庭では調和が高く出やすく、八芒星（八の虹）が約12投に1回（従来の庭の調整値は約19投に1回）。虹の特別さを保つなら八芒星の条件を生態系の庭向けに見直す。
- §23.5 の申し送り（縦長画面の文字、履歴の「0投目」）は継続。

## Jam Codex の積み重ねた PR（#32〜#35）を main へ統合（2026-11-14）

### 完了したこと

- `codex/system-star-moments`（#32）→ `system-garden-navigation`（#33）→ `system-garden-style`（#34）→ `system-garden-opening`（#35）の積み重ねを、
  別の統合ブランチ `claude/merge-codex-stack` で main（PR #36 後）にまとめた。Codex のブランチには書き込んでいない。
- 衝突は GardenBootstrap.cs の1か所だけ: 七つめの面の案内は Codex の文（「七つめの面を加える」で、幸運の七を使えます。）を採り、前の投の状態の一行を消す処理を残した。
- 統合で見つけた食い違い: 新しいタイトル（#35）が画面全体を覆わなくなったため、祈りの星の左上の2行がタイトルの上に見えていた。
  タイトルが出ている間は2行を隠し、庭に入ると現れるようにした（GardenUi に `TitleCardOpen` を読み取り専用で追加）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="324" passed="324" failed="0"`。`pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- WebGL をブラウザで確認: タイトルに余計な行が出ない／「庭に入る」から振ると因果の一行と左上の2行が出る／コンソールのエラー 0。

## Jam 文字の仕上げ — 見出しと説明とボタンの書体（2026-11-14）

### 完了したこと

- 本人の方向付け「タイトルや説明・ボタンのフォントやデザインに完成版の感覚が足りない」。Codex の #37（絵本の庭・手描きのタイトル文字）の上に重ねた。
- 書体の役割を `GardenType` にまとめた: 本文は Noto Serif JP Regular、見出し・ボタン・選ばれた面は **Noto Serif JP SemiBold（本物の太さ）**、
  英字の大きな文字（タイトルの予備・祈りの星の「7」「8」）は Cormorant Garamond SemiBold。どれも SIL OFL 1.1 で同梱（本人承認済みの取得）。
- それまでの太字は Unity の合成の太字で、日本語の線がにじんでいた。ボタン・見出し・出来事の見出し・選ばれた面・タイトルの一文を本物の SemiBold にした。
- アプリ版（macOS など）は端末のゴシック体に置き換わっていたのを、ブラウザ版と同じ同梱の明朝体にそろえた。
- タイトルは Codex の手描きの「Destiny Garden」（StorybookLettering）を採用。文字のタイトルは画像がないときの予備。
- 祈りの星の文字（左上の季節の願いとラッキーナンバー、一投ごとの一行、ラッキーナンバーの小さな2行）を本体と同じ札（GardenUiArt.Plaque）と同じ縮尺にそろえた。
  左上の札は出来事の札と重なっていたので、3行の細い札にした。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="324" passed="324" failed="0"`。`pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- WebGL をブラウザ（1280×720）で確認: 手描きのタイトル、SemiBold の一文とボタン、左上の札が出来事の札と重ならない、一行の札。コンソールのエラー 0。

### 要確定・申し送り

- WebGL の読み込みが約 7 MB 増えた（SemiBold 6.3 MB・Cormorant 1.0 MB）。重さが気になる場合は SemiBold を使う文字だけに絞る（サブセット化）ことができる。
- 文字は Unity の従来の Text のまま（字間の調整はできない）。さらに仕上げるなら TextMeshPro への移行を検討する。

## Jam ラッキーナンバーと祈りで、自分だけの庭へ（prayer-star.v4）（2026-11-14）

### 完了したこと

- 本人の方向付け「ラッキーナンバーをテーマに、祈りや神聖さも介入して、生態系が毎回独自に育って自分だけの庭ができるゲームの完成系」（ECOLOGY.md §24）。
- 八の虹を「幸運に祈りが重なったとき」に（ラッキーナンバー・願いが叶った・調和8、または7回目ごとのラッキーナンバー）。陰陽だけでは八にならない。
- 幸運の株: ラッキーナンバーの恵みが最初に届いた株に印が残り、金色の光をまとう。一行と左上の札で伝える。
- 庭の名前（いちばん多い花・生き物・運の形容）を左上の札に、12投ごとの季節の終わりに証の一行。
- 願いをかけた瞬間の祈りの音。

### 完了基準の結果（コマンド出力の要約）

- 計測（新しい庭）: 変更前 八 8.5%・虹 12〜15投に1回 → 変更後 願わない 51投に1回／祈る 約26投に1回、七は 14.7〜17.2%。
- 幸運の株: 160投でラッキーナンバー27回すべてが印を残し、保存・再開で同じ。
- 祈りの音: −27.8 LUFS（星の和音と同程度、出目の音より小さい）、クリップ 0、D長調のレとラ。
- Unity EditMode: `total="327" passed="327" failed="0"`（幸運の株・八の決まり・庭の名前の3件を追加）。`pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- WebGL をブラウザで実プレイ（10投・再読み込み1回）: 庭の名前が育ちにつれて変わる（〈ノコギリソウと光る苔の庭〉→〈ノコギリソウとトンボの庭〉）、
  ラッキーナンバー（4→3）で「ノコギリソウにラッキーナンバーの光が宿った（幸運の株）」と「幸運の株 1」、面の願いが叶って季節の願いも叶う、
  再読み込み後も同じ。コンソールのエラー 0。幸運の株の金色の光は画面写真の解像度では小さな点としてしか確かめられていない。

### 要確定・申し送り

- 祈りの音は実測のみ。耳での確認は本人に依頼する。
- 一投ごとの一行は、ラッキーナンバーや季節の節目では4〜5行になり庭の中央を覆う時間がある。行数を絞るかは遊んだ感触で決める。
- 幸運の株の光の見え方（近くで見たときの大きさ）は実機で確認してほしい。

### 触ったファイル

- Domain: PrayerStar.cs（v4・幸運の株）、GardenTypes.cs・CanonicalJson.cs（LuckyMarks）。
- Presentation: PrayerStarSigil.cs、GardenKeepsake.cs（新規）、AudioSynth.cs・GardenAudio.cs（祈りの音）、GardenBootstrap.cs（願いの瞬間の音の1か所）。
- Tests: LuckyPlantTests.cs（新規）、PrayerStarTests.cs、SacredTonesTests.cs。docs/unity-garden/ECOLOGY.md §24、PROGRESS.md。

## Jam 説明を減らし、出来事で伝える（2026-11-14）

### 完了したこと

- 一投ごとの因果の数行・確率・案内・ヒントを出さず、言葉は「季節の願いが叶った」と季節の終わりの〈庭の名前〉だけにした。
- ラッキーナンバーは「7／ラッキーナンバー／3 ＋ 4」。数え上げは左上の七つの点（星が出た瞬間に灯る）。
- 左上の札を3行（名前・季節の願い・七つの点）に。ラッキーナンバーや虹の状態行をなくした。
- 祭壇まわりに置く光の粒も試したが、いつもの視点では小さく草に隠れて見えないため、札の点にした。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="328" passed="328" failed="0"`。`pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- WebGL をブラウザで確認: ラッキーナンバー（3→4）で「7／ラッキーナンバー／3 ＋ 4」だけが出て、左上の点が1つから2つへ灯る。説明の段落は出ない。コンソールのエラー 0。

### 要確定・申し送り

- 出来事の札・下の一行（「しずくの一投。」など）・庭の気配の欄は Codex の画面。さらに減らすなら Codex 側で扱う。
- 使わなくなった文の関数（CauseLine・WishLine・HintLine など）はテストとともに残している。観察の画面で使うか、消すかは次で決める。

## Garden: PR #42 の統合 (2026-11-14)

- ラッキーナンバーの点と庭名の表示テストを Unity 専用の PresentationTests へ移動。幸運の株の記録・復帰と八の成立条件は .NET の生態テストで引き続き検証する。
- ローカルで .NET の生態・台帳テスト 280 件、Unity EditMode の該当テスト 4 件、TypeScript の型チェック・テスト 183 件・ビルドが成功。

## Jam 本番に向けた仕組みの磨き込み — 起動の速さ・つぼみが咲く幸運・叶う季節の願い（2026-11-14）

### 完了したこと

- 起動の読み込みと検証を約2倍速く（記録の形式は同じ）: 保存バイトでの記録の検証、JSON 読み取りの高速化、再開は最後の状態だけを組み立てる。
- ラッキーナンバーの恵みはつぼみへ先に届き、咲かせる（幸運の株も花に）。
- 季節の願い v3: 群落はつぼみも数え二マス以内に三つ、恵みの系統で判定。始まりかけていない群落は願わない。「種をこぼす草むら」に改名。
- 計測テストを追加（ProductionMeasureTests: 記録の内訳と長期の庭、起動の各段階、季節の願いの叶い方）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="331" passed="331" failed="0"`。`pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。
- 起動（300投・12 MiB）: 読み込みと検証 2283 → 約1000〜1200 ms、再開 181 → 7〜10 ms。耐久1200投 未解決0・再開比較48回一致。
- 季節の願い（6庭×300投）: 群落の願い 0〜27% → 27〜73%。長期の庭の最後の花 4.2 → 5.8〜7.8 輪。
- WebGL をブラウザで確認: 統合前からの保存が続き、季節の終わり（12投目）に〈庭の名前〉だけが出る。コンソールのエラー 0。

### 要確定・申し送り

- 記録の大きさ（1投 約40 KiB、ブラウザでは毎回ファイル全体を保存）は残る。分割は形式の版上げとして別に扱う。
- 蜜の花畑・種をこぼす草むらは 27〜29% と難しい願いのまま（花の少ない生態系の庭の性質）。

## Jam 記録の圧縮と分割 — framed-json.v2（2026-11-14）

### 完了したこと

- 課題「記録が1投 約40 KiB、ブラウザでは毎回ファイル全体を保存し直す」を解決。記録の中身とハッシュ（庭の stateHash を含む）は変えず、書き方だけを版上げ。
- 圧縮（`FrameCompression`: 依存なしの LZ77、WebGL でも同じ・決定的・壊れた入力は FormatException）。v1 の非圧縮フレームも読める。
- 分割（1 MiB ごとのセグメント、鎖はまたいでつながる）。保存で書き直すのは最後のセグメントだけ。
- 回復をセグメント対応に（切れた末尾は torn、壊れた鎖はセグメントごと corrupt、以後のセグメントも横へ移す。捨てない）。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="336" passed="336" failed="0"`（圧縮の往復と破損、セグメントでも同じ鎖、v1 からの継続、中間セグメントの破損、新セグメント先頭の切れた書き込み）。
- 300投: 記録 12 MiB → 約3.4 MiB・4セグメント、起動の読み込みと検証 約0.6〜0.7 秒。耐久1200投 未解決0・再開比較48回一致。
- WebGL: 以前の形式の保存（13投）を読み、2投を新しい形式で足し、再読み込みで15投・同じ点と願いが戻る。コンソールのエラー 0。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- v2 で書いた記録は前の版のゲームでは読めない（前の版は v2 フレームを書きかけと見なし、末尾として横へ移す）。古い版のビルドで同じブラウザの保存を開かないこと。
- DATA_CONTRACT の「frame byte 構造は G1 で固定」は framed-json.v2 に更新した（FileJournal の説明に記載）。

## Jam 幸運のしるし — ゾロ目・三つぞろい・出目の合計77・願いの目（prayer-star.v5）（2026-11-14）

### 完了したこと

- 本人の方向付け「特定の結果を出せた場合などにラッキー感」（ECOLOGY.md §26）。確率は変えず、出目の並びだけから読む。
- ゾロ目: つぼみが咲く。三つぞろい: つぼみがすべて咲き、七つめの面。出目の合計が77の倍数を越える: 七つめの面。願いの目: 見せ方と音。
- 見せ方は数字と短い名前（「33／ゾロ目」「333／三つぞろい」「77／出目の合計」「4／願いの目」）と音と光。ラッキーナンバーと重なると順に出す。

### 完了基準の結果（コマンド出力の要約）

- Unity EditMode: `total="340" passed="340" failed="0"`（しるしの判定、つぼみ、七つめの面と再開、表示の4件を追加）。
- 300投: ゾロ目 49・三つぞろい 11・合計77 13。七つめの面の提示 約14% → 約22%。耐久1200投 未解決0。
- WebGL: ゾロ目（3→3）で「33／ゾロ目」とヒマワリの開花を確認。コンソールのエラー 0。
- `pnpm typecheck` 0、`pnpm test` 183 passed、`pnpm build` built。

### 要確定・申し送り

- 三つぞろいと合計77は実プレイでは今回出ていない（テストで確認）。七つめの面の提示が約22%に増えたので、多すぎると感じたら合計77の報いを外す。
