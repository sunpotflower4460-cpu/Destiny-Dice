# 雲と波とガラスの描画（2026-11-14）

- 桃色とすみれ色の雲に、明るい縁と筆致のある雲底を描いた。Unity で奥の背景に重ねる薄雲を時間と天気に合わせ、日と月の光を弱める。空と遠い水面の色を揃えて水平線をなじませた。
- 海は岸辺の青緑と沖の藍を描き分けた。雲の色を波面で崩し、日と月の光が波の向きに沿って伸びるようにした。遠くでは細かな波を弱め、岸辺に短い白波と瓶の接水部に細い反射を入れた。
- 瓶の口と底の断面を丸くし、ガラスに雲を薄く映した。コルクの面取り、金縁の帯、星の封印に陰影を加えた。
- PR #37 の上に外観の差分を置いた。Claude Code の書体変更がある PR #39 との試験マージで競合が出ないことを確認した。

## 背景素材

- 組み込み ImageGen で生成し、1536×768 の [StorybookCloudbanks.jpg](../../../../unity/DestinyGarden/Assets/DestinyGarden/Resources/StorybookCloudbanks.jpg) として取り込んだ。
- 生成に使ったプロンプト

```text
Paint a single wide environment texture for the sky of Destiny Garden, a gentle miniature island game inspired by 1980s children's fairy-tale animation. The engine will place a glass bottle and tiny island below it. Original traditional opaque gouache background with visible quiet brush marks, rich but restrained pigments, no lens effects. Sky only. Broad distinctly shaped cloud masses sweep in layers from the outer left and right thirds, with dusky violet and periwinkle undersides, muted rose midtones and delicate apricot, pale yellow and cream along the illuminated edges. A few loose high wisps in an indigo-lavender sky. Keep cloud silhouettes soft and richly modeled, with two or three main brush-painted banks across the lower-middle half of the image. The middle third is quieter so a crescent moon added in Unity will read. The lowest 12 percent of the image is a continuous smooth dusty mauve and peach atmospheric wash, fading gently into the cloud banks. Match the side edge hues, and paint all the way out to every edge. Wide landscape canvas around 2:1. No sea, land, silhouettes, mountains, sun, moon, stars, objects, borders, grain specks, writing or text.
```

## 検証

- Unity 6000.3.23f1 の macOS・WebGL ビルドは両方 `Succeeded (0 errors)`。macOS で瓶を正面と背面から確認した。プレビューは専用の保存先を使い、既存の macOS ジャーナル21件は作業前後で SHA-256 が一致した。
- Chrome の WebGL で日暮れ、月夜、小雨を描画し、読み込んだ三つのジャーナルの元のバイト列が一致した。1280×720、960×720、420×720 のキャンバスでタイトルと入場後の描画を確認した。
- 初回の一投、願い、届け先、24投の保存から動きを抑えて25投目を追加する操作を通した。844×390 の横長タッチ再現では、メニューを閉じる操作の次のタップで一投だけを保存した。スマートフォン実機は未確認。
- Unity の表示用 EditMode テストは `16/16`。`dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release` は `278/278`。`pnpm typecheck` と `pnpm build` は成功し、`pnpm test` は `183/183`。
