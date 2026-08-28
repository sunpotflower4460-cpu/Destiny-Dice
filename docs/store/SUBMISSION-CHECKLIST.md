# P11 — App Store / TestFlight Submission Checklist

## 1. App identity
- Bundle ID: `com.sunpotflower4460.intentiondice`
- App name: `Intention Dice`
- Marketing version: `1.0`
- Capacitor config / Xcode Debug / Xcode Release / fastlane Appfile のbundle IDが一致していること。
- App Store Connectで同じBundle IDのApp recordを作成する。

## 2. Apple build environment
2026-04-28以降のApp Store Connect upload要件に合わせ、release workflowはXcode 26+ / iOS 26 SDK+を必須とする。`ios-smoke.yml`も同じXcode majorを検証する。

## 3. GitHub Actions secrets / signing assets
TestFlight workflowに以下を登録する。

### App Store Connect upload authentication
- `APPLE_TEAM_ID`
- `APP_STORE_CONNECT_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`
- `APP_STORE_CONNECT_KEY_CONTENT`（Team API Keyの`.p8`本文）

### iOS archive signing
- `IOS_DISTRIBUTION_CERTIFICATE_P12_BASE64`
  - `Apple Distribution` certificateとそのprivate keyを含む`.p12`をbase64化した1行文字列。
- `IOS_DISTRIBUTION_CERTIFICATE_PASSWORD`
  - 上記`.p12`のexport password。
- `IOS_PROVISIONING_PROFILE_BASE64`
  - `com.sunpotflower4460.intentiondice` 用のApp Store distribution `.mobileprovision`をbase64化した1行文字列。

GitHub-hosted macOS runnerには配布証明書/private key/provisioning profileが最初から存在しない。release workflowはこれらを一時keychainへimportし、profileのTeam IDとbundle IDを検証してからFastlaneを実行する。FastlaneはRelease configurationだけmanual signingへ切り替え、明示したApp Store profileでarchive/exportする。

秘密鍵、`.p8`、`.p12`、`.mobileprovision`、passwordをrepositoryへcommitしない。

任意のproduction endpointはGitHub Actions Variablesで管理する。
- `VITE_ANU_RNG_ENDPOINT`
- `VITE_NOTARY_ENDPOINT`

`VITE_ANU_RNG_ENDPOINT`は`length=<bytes>&type=uint8`を受け取り、`{ data: number[] }`を返すANU-compatible endpointを指定する。ANU未設定/失敗時はfrozen policyどおりfallback sourceを記録する。

## 4. Native smoke before upload
- `pnpm typecheck`
- `pnpm test`
- `pnpm simulate --days 365 --effect 0 --wish-n 120 --wish-base 0.3 --wish-effect 0`
- `pnpm release-check`
- `pnpm build`
- `npx cap sync ios`
- `xcodebuild` unsigned iOS Simulator build green
- 実機/Simulatorで以下を確認:
  - 初回登録→session→prediction→抽選→wish moment
  - 通知permissionが自動表示されず、ボタン操作時だけ要求される
  - 日次通知・願い締切通知
  - ANU未設定/失敗時にsourceがfallbackとして正直に表示される
  - sealed wish本文が締切前に願いタイムへ出ない
  - アプリ再起動で台帳を再開できる
  - Final Reportは365日終了前に表示されない
  - share/download操作

## 5. TestFlight
1. Apple Developer / App Store Connect側で、Bundle ID、`Apple Distribution` certificate、App Store provisioning profile、App recordを用意する。
2. §3の7 secretsと必要なVariablesをGitHubへ登録する。
3. GitHub Actions `iOS TestFlight release` を手動実行。
4. workflowがprofileのTeam ID / application identifierを検証し、一時keychainへcertificate/private keyをimportできることを確認。
5. `macos-26`上でXcode 26+確認、Capacitor sync、manual-sign archive/export、uploadが成功すること。
6. App Store Connectでbuild processing完了を確認。
7. internal testerへ配布し、実機smokeを再実行。
8. crash / notification / SQLite persistenceを確認。

## 6. Store metadata / review safety
- `docs/store/METADATA_JA.md` を基準に入力。
- 「超常現象を証明した」「絶対に引き寄せられる」「改竄不能」等の表現を使用しない。
- トーンは「1年間のパーソナル実験・記録アプリ」。
- Final Resultは単一個人実験の事前登録基準に対する結果として説明する。
- Layer Cは自己判定・非盲検という制約を隠さない。
- gambling / 金銭報酬 / 医療診断機能ではないことをReview Notesへ明記。

## 7. App Privacy / age rating
- `docs/store/PRIVACY.md` と実際のproduction endpoint設定を照合してApp Privacyを回答する。
- 外部anchorを本番で有効化する場合、developer-operated Workerの保持内容（hash/seq/timestamp）を実運用に合わせて申告判断する。
- 広告・tracking・課金・成人向け内容はv1にない。
- App Store Connectの最新age rating questionnaireを提出時点で回答し、古い回答テンプレートを流用しない。

## 8. Optional public anchor Worker
Cloudflareを使う場合、GitHub secretsを設定:
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_ANCHOR_KV_NAMESPACE_ID`

`Deploy public anchor Worker`を手動実行し、deploy後に:
- `GET /health` -> 200
- テスト用chain-headを`POST /anchors`
- `GET /anchors/<genesisHash>`で同じhash / seq / server `receivedAt`が閲覧できる
- POSTへ余計な`wishText`等を加えると400になる
を確認する。

このWorkerは現在public anchor専用で、ANU RNG proxy endpointではない。`VITE_ANU_RNG_ENDPOINT`は別途ANU-compatible endpointを指定する。

Worker未デプロイ・endpoint未設定でもアプリ本体は動作し、公証は自動スキップする。

## 9. Final submission
- 1024px RGB/no-alpha App IconとSplash確認。
- 必須スクリーンショットを現行App Store Connectのdevice slotに合わせて作成。
- support/privacy URLを公開ページへ設定。
- TestFlight実機smoke green後にApp Reviewへ提出。
- 審査提出時のcommit SHA / TestFlight build number / Worker URL（有効時）をrelease記録へ残す。
