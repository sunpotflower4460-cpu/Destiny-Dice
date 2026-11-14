# 1980年代の児童向けファンタジーを思わせる庭（2026-11-14）

## 変更

- ガッシュ画風の空を描き起こし、Unity のスカイドームに貼った。空の桃色とすみれ色を海の反射と瓶の縁にも入れた。投数による時刻と保存された天気を使い、雨では彩度を落とし、夜には雲を藍色に沈める。
- 海岸の砂と岩、川辺の苔を同じ色調で描き分けた。家の窓と海の灯りは琥珀色にした。夜空の星と漂う光の密度を抑えた。
- タイトルに金の飾り罫とコルク付きの瓶の紋章を置き、札やボタンに濃い藍紫、金、くすんだバラ色を使った。

## 作業範囲

- PR #35 のコルクとタイトルに重ねた専用 worktree で実装した。外観の公開 API、時刻の進行、天気の選択、操作、保存形式に変更はない。
- Claude Code 側では PR #36 を取り込んだうえで #32〜35 の統合を進めていた。その時点のステージと #35 を今回の13パスで照合し、追加の変更が重なるパスはなかった。
- macOS プレビューは専用の保存先を使う。

## 空の素材

- Codex の組み込み ImageGen で生成し、1024×512 の [StorybookSky.jpg](../../../../unity/DestinyGarden/Assets/DestinyGarden/Resources/StorybookSky.jpg) として格納した。Unity は sRGB テクスチャとして読み込む。
- 生成に使ったプロンプト

```text
Create a landscape environment texture for the distant sky in a real-time Unity game.
Hand-painted gouache and airbrushed clouds from an original 1980s children's fantasy film. Cloudscape only, filled edge to edge. Indigo and faded plum upper sky, wide lavender cloud banks with dusty rose, warm apricot and cream-painted edges toward the lower quarter. The shapes are broad, layered and irregular, with soft brush texture and restrained saturation. Keep the lowest 10 percent a soft continuous peach-violet atmospheric glow. Horizontal composition, about 2:1, with cloud banks stretching beyond both sides, similar hues and values at left and right. Keep the visual center calm for a miniature garden in a glass bottle that will be rendered separately by the engine. Sky only. Exclude land, trees, sea, sun, moon, stars, objects, borders and lettering. Matte painted backdrop, no glossy effects.
```

## 検証

- Unity 6000.3.23f1 の macOS と WebGL ビルドは両方 `Succeeded (0 errors)`。macOS で瓶のコルク、海の反射、家の灯りと庭の描画を確認した。
- Chrome で初回の一投、願いと届け先の操作を行った。24投の保存を読み込み、動きを抑える設定で25投目を保存した。夕暮れ・夜・雨の保存も読み込み、元のジャーナルのバイト列が維持されたことを確認した。
- 16:9、4:3、844×390 の横長タッチ再現で、タイトルの文と瓶が収まることを確認した。横長タッチ再現でメニューを閉じたあと、一投だけが記録された。スマートフォン実機は未確認。
- macOS の既存の保存21件の SHA-256 は作業前後で一致した。
- Unity EditMode は `total=284 passed=284 failed=0`。時刻の循環、天気による雲量、夜空の明暗のテストを含む。
- `dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release` は `241/241` 成功。`pnpm typecheck` は終了コード0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 744ms`。
