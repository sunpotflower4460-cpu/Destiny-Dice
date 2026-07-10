# Intention Dice（仮）

信じる人も疑う人も、同じデータの前で頷ける「1年間の意図実験装置」。

引き寄せ（実現化）を、事前登録・改竄検知可能な台帳・偶然期待値の併記のもとで、1年間かけてフラットに測るiOSアプリ。三層で測る：

- **Layer A（物理）**: 意図は量子乱数を曲げるか。物理計測なので思い込みでは説明できない（★★★）
- **Layer C（人生）**: 実践は願いの実現率を変えるか。願いを実践群/封印群にランダム割付し、封印群をベースラインにする（★★）
- **Layer B（心）**: 実践は気分・エネルギーを変えるか。プラセボ込みでよい（★）

詳細仕様は [DESIGN.md](./DESIGN.md)、作業ルールは [AGENTS.md](./AGENTS.md) を参照。

## 使うもの

Capacitor 6 + React 18 + TypeScript + Vite / SQLite（@capacitor-community/sqlite）/ pnpm

## 人間側セットアップ

```sh
git init
# DESIGN.md, AGENTS.md, README.md, .gitignore を置く
git add -A && git commit -m "docs: design + agent rules"
# 以降、Claude Code に各フェーズを1つずつ投げる（KICKOFF参照）
```

## フェーズ（1PR = 1フェーズ）

P0 足場 → P1 乱数 → P2 台帳 → P3 事前登録 → P4 セッション(A/B) → P5 統計A → P6 ダッシュボードA → P7 願いレジストリ(C) → P8 統計C＋統合 → P9 通知/UX → P10 最終レポート → P11 仕上げ・申請

各フェーズの完了基準は DESIGN.md §6 に、進捗は [PROGRESS.md](./PROGRESS.md) に記録される。
