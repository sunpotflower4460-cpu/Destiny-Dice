# Intention Dice（仮）

信じる人も疑う人も、同じデータの前で頷ける「1年間の意図実験装置」。

引き寄せ（実現化）を、事前登録・改竄検知可能な台帳・偶然期待値の併記のもとで、1年間かけてフラットに測るiOSアプリ。三層で測る：

- **Layer A（物理）**: 意図セッションと量子乱数のtarget-hit率の関係を、事前登録した基準で測る（★★★）
- **Layer C（人生）**: 実践は願いの実現率を変えるか。願いを実践群/封印群にランダム割付し、封印群をベースラインにする（★★）
- **Layer B（心）**: 実践は気分・エネルギーを変えるか。プラセボ込みでよい（★）

詳細仕様は [DESIGN.md](./DESIGN.md)、v2.1で固定した実装前プロトコルは [PROTOCOL_FREEZE.md](./PROTOCOL_FREEZE.md)、作業ルールは [AGENTS.md](./AGENTS.md) を参照してください。`DESIGN.md` と `PROTOCOL_FREEZE.md` が衝突する場合は、後者が優先です。

## 使うもの

Capacitor 6 + React 19 + TypeScript + Vite / SQLite（@capacitor-community/sqlite）/ pnpm

## 現在地

実装フェーズは **P0 → Gate 0 → P1 → P2 → P3 → P4a → P4 → P5 → P6 → P7 → P8 → P9 → P10 → P11** まで完了しています。プロトコル・統計・セッション・最終レポート・TestFlight署名経路はリポジトリに揃っています。残るのは [Issue #16](https://github.com/sunpotflower4460-cpu/Destiny-Dice/issues/16) の外部作業（Apple / Cloudflare 資格情報、ANU production endpoint、実TestFlight upload）です。未設定のまま TestFlight 公開済みとは扱いません。

## フェーズ（1PR = 1フェーズ）

P0 足場 → **Gate 0 Protocol Freeze** → P1 乱数 → P2 台帳 → P3 事前登録 → **P4a Stats Core** → P4 セッション(A/B) → P5 統計A/B → P6 ダッシュボードA → P7 願いレジストリ(C) → P8 統計C＋統合 → P9 Clock/通知/UX → P10 最終レポート/再検証 → P11 仕上げ・申請

各フェーズの詳細は `DESIGN.md` §6 と `PROTOCOL_FREEZE.md`、進捗は [PROGRESS.md](./PROGRESS.md) に記録されます。

## 実装上の重要原則

- ledger は追記専用で、正は常にledger。
- prediction は測定対象RNG取得より必ず先にcommitする。
- Layer A主要確証解析は `rngSource='anu'` の量子セッションのみ。fallbackは隠さず記録し、副次/探索へ回す。
- targetDir は測定対象bitstreamとは独立した登録時seedから決定的に生成する。
- ローカルhash chainは「tamper-evident（改竄検知可能）」であり、完全改竄不能とは表現しない。
- `pnpm simulate` はネット無しで完結するプロジェクトの背骨にする。
