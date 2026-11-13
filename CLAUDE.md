# Claude Code の作業範囲

最初に [AGENTS.md](AGENTS.md) と [Unity並行作業の分担](docs/unity-garden/PARALLEL_WORK.md) を読む。
ユーザーの指定は **Codexが仕組み側、Claude Codeが見かけ側**。Unity Gardenの並行作業ではこの分担を守る。

- Claude Codeは `unity/DestinyGarden/Assets/DestinyGarden/Presentation/`、描画素材・シェーダー・シーン、
  `PresentationTests.cs` と見た目の確認を担当する。`GardenBootstrap.cs` のUI接続もこちらで扱う。
- Codexが担当する `Domain/`・`Application/`・`Infrastructure/`、仕組みのテストは編集しない。
  表示に必要な追加データは `docs/unity-garden/worklogs/appearance/<作業名>.md` とPR本文へ記録する。
- 最新の `origin/main` を取得し、`claude/appearance-<作業名>` ブランチと独立したcheckout/worktreeで始める。
  `feature/ecosystem-depth` は統合済み作業のブランチとして再利用しない。
- 既存の公開型・イベント名・メソッド呼び出しは維持する。表示のために庭の状態、ダイスの結果、保存内容を変更しない。
- 「基本説明より、起きたことで伝える」というユーザーの方針を維持する。
  文・光・演出は保存された出来事と同じ株・種・場所へ対応させる。
- 進捗と実際の検証結果は `docs/unity-garden/worklogs/appearance/<作業名>.md` へ記録する。
  共有の `PROGRESS.md`、分担文書、プロジェクト設定は並行して書き換えず、必要な変更をPRへ記す。
- Unityのコンパイル・描画・入力・音・性能は、実行して確認した範囲だけを報告する。

担当外の変更が必要になったら、そのファイルを直す前に、必要な項目・型・用途を引き継ぎ文書へまとめる。
担当内で進められる改善は継続する。
