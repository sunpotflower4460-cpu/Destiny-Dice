# Unity Garden の並行ブラッシュアップ

2026-11-13のユーザー指定: Codexは仕組み側、Claude Codeは見かけ側を担当する。
この分担と独立したGit作業領域で同じファイルの同時編集を減らす。共通部分の変更は順番に統合する。
ユーザーの新たな指示がある場合は、その指示を優先する。

## 担当する場所

パスは `unity/DestinyGarden/Assets/DestinyGarden/` を基準にする。

| 担当 | 主なファイル | 進める内容 |
| --- | --- | --- |
| Codex: 仕組み | `Domain/` | 生態・土・水・種・住人・ダイスの作用、決定性、保存形式と旧庭の互換性 |
| Codex: 仕組み | `Application/` | coordinator、入力の確定、出来事・歩み・履歴など読み取り用データと事実に基づく意味 |
| Codex: 仕組み | `Infrastructure/` | 乱数・保存・読み取りなどの実装 |
| Codex: 仕組み | `Tests/EditMode/` の `PresentationTests.cs` 以外、`tools/ecology-check/`、生態のCI | 仕組み・保存・復帰・収支・由来の検証 |
| Claude Code: 見かけ | `Presentation/` 全体 | 画面構成、植物と住人の描画、カメラ、光・音・動き、操作感、表示性能 |
| Claude Code: 見かけ | `Presentation/GardenBootstrap.cs` | 現在の公開APIを使う画面・演出の接続。仕組み側から同時に変更しない |
| Claude Code: 見かけ | `Shaders/`、`Resources/`、描画用素材・prefab | シェーダー、フォント、色、造形、素材、視覚表現 |
| Claude Code: 見かけ | `unity/DestinyGarden/Assets/Scenes/`、`Editor/BuildScripts.cs`、`PresentationTests.cs` | シーン・表示確認・Unityでの描画/操作/ビルドの確認 |

各ファイルの `.meta` は元のファイルと同じ担当。移動・改名時は既存GUIDを維持する。
仕組みの仕様文書はCodex、体験・画面・素材の文書はClaude Codeが担当し、
他担当も使う文書の変更は対象をPRへ明記して順番に統合する。
`AGENTS.md`・`CLAUDE.md`・この文書・共通README・`PROGRESS.md`、asmdef・`link.xml`・
`Packages/`・`ProjectSettings/` の変更は共有の統合事項として扱い、並行して書き換えない。
既存実験の `src/`・凍結プロトコルはこのブラッシュアップの対象にしない。

## 仕組みと見かけの接続

- 仕組み側は既存の公開フィールド、イベント名、メソッド名、必須引数を維持する。
  拡張は追加の型・省略可能な項目など、既存のPresentationが使える形を優先する。
  互換性を壊す必要がある場合は、両方の変更内容を一つの統合作業として先に整理する。
- `WorldState` / `RollOutcome` / `EcologyResult` は仕組み側の事実。
  `GardenMoments` / `GardenMemories` はその読み取り用の表現データ。現在の
  `GardenMoment.MemoryKey`、`GardenUi.OnReadMemory`、出来事の `CellId` による接続を維持する。
- 見かけ側は既存APIから読む。UIイベントは既存のcoordinatorの操作へ渡し、表示都合で状態・乱数・投数・台帳を変えない。
  アニメーションの長さやカメラ操作で生態の結果を変えない。
- 表示の追加が必要な仕組み変更は、Codexがデータ側を作り、必要な表示の項目・型・利用例・既定値を
  自分の引き継ぎ文書とPR本文へ書く。Claude Codeが必要なPresentationの接続を行う。
  見かけ側から追加データが必要な場合も、同様に具体的な項目と用途を記す。
- 基本説明より、実際に起きた出来事を先に伝える。親株・種・子株、食事や受粉、場所の由来を守り、
  見ていない誕生・感情・因果を演出で補わない。過去の株を現在へ復活させない。

## Gitと進捗の分離

1. 両者とも最新の `origin/main` から開始する。今回の統合対象はPR #30のゲーム変更と、この分担文書。
2. Codexは `codex/system-<作業名>`、Claude Codeは `claude/appearance-<作業名>` を使う。
   同じディレクトリでブランチを切り替え合わず、別checkoutか `git worktree` を使う。
3. 進捗・実行した検証・他担当へ必要な変更は、作業ごとに独立したファイルへ記録する。
   Codexは `docs/unity-garden/worklogs/system/<作業名>.md`、Claude Codeは
   `docs/unity-garden/worklogs/appearance/<作業名>.md`。Unity並行作業ではこれを進捗更新として扱う。
4. 担当ごとにPRを作り、触った範囲と公開APIの互換性を示す。統合時に共有の `PROGRESS.md` へ追記する。
   mainへ統合済みの内容を取り込んでから次の作業を続ける。mainや他担当へforce pushしない。
5. 競合が出たら、相手のファイルを自分の版で丸ごと上書きしない。変更の担当を確認し、
   担当外は最新mainの変更を保ちながら接続を合わせる。意味が衝突する変更は一つずつ統合する。

例（独立した作業先を用意する場合）:

```sh
git fetch origin
git worktree add -b codex/system-ecology ../Destiny-Dice-system origin/main
git worktree add -b claude/appearance-garden ../Destiny-Dice-appearance origin/main
```

既に使われているブランチや作業先があれば別名を選び、既存の変更を削除しない。
この分担は共同作業の運用であり、GitHubのアクセス制限やファイルロックを新設するものではない。

## 確認の分担

仕組み側は実際のC#テストと状態・由来・保存復帰の一致を確認する。
見かけ側はUnityのコンパイル、画面、低刺激表示、入力、文字の収まり、音、性能を確認する。
統合時は両者の接続を確認し、未確認のものを確認済みと扱わない。
PR #30のゲーム変更ではC#213件、Web183件と両CIが成功しているが、Unity実行は未確認のまま。
今回のユーザーのマージ指示はmainへの統合であり、Unity検証済みや配布公開を意味しない。
