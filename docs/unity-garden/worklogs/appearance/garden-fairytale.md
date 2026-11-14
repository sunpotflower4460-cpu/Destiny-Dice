# 絵本の題字と三日月（2026-11-14）

- 背の低い視点で瓶全体と空を同時に見せた。横長では題字を左、瓶を右に置く。4:3 と縦長では上下に置き、画面比率に合わせて題字の大きさを変える。
- 古い絵本の表紙をイメージして、金色の題字と花の小さな飾りを描き起こした。ボタンは金の印刷と羊皮紙を思わせる色に揃えた。
- 晴れた朝や夕暮れには淡く、夜は明るく三日月を描画する。ガラスの肩に絵筆のような反射を加え、コルクに金縁のえんじ色の帯と星印を入れた。小さな家は丸窓と色瓦で表情を加えた。
- PR #38 までの最新 main を取り込み、PR #37 の作業先で続けた。今回の差分は表示と素材。Claude Code が作業中の書体変更と共通する `GardenUi.cs` と `GardenUi.Theme.cs` は、一時ディレクトリで三方向照合した。Claude Code 側のコミット `bcc1802` にも今回の表示変更が入り、題字の画像と本文書体のコードが同居していることを確認した。

## 題字の素材

- 組み込み ImageGen で生成した。透過を保って余白とサイズを整え、832×522 の [StorybookLettering.png](../../../../unity/DestinyGarden/Assets/DestinyGarden/Resources/StorybookLettering.png) として取り込んだ。
- 生成に使ったプロンプト

```text
Create a transparent PNG logo lettering asset for the Unity game Destiny Garden. It should look like the hand-lettered cover of an original 1980s children's fairy-tale picture book, with a very gentle, inviting mood.
Spell the title exactly "Destiny Garden". Stack it on two centered lines, "Destiny" over "Garden". The dominant shapes are gracefully tapered high-contrast serif letters with soft, substantial ivory and light honey faces, fine warm ochre shading, a thin muted plum-brown outline, and a small amount of dry gouache texture. Give the initial capitals restrained calligraphic curves and the line endings a little hand-drawn character. Place two very small, sparse curlicues of muted sage leaves and dusty blush petals immediately beside the lower line, keeping the words the largest element and legible at a width of 270 pixels. The ornament should feel like the work of a 1980s book illustrator. Keep it within the lettering's visual footprint, compact and close to the words. Gently irregular brush edges, graceful typography, one or two tiny four-point gold stars only.
Transparent background with real alpha outside the tightly composed lettering and its tiny ornaments. All words fully visible with breathing room at the image edges. No card, opaque backing, banner, big frame, gradient behind the words, other text, subtitle, numeral, icon, prominent sparkles, or drop shadow across the canvas. Square or slightly landscape artwork with a compact stacked silhouette.
```

## 検証

- Unity 6000.3.23f1 の最終 macOS・WebGL ビルドは両方 `Succeeded (0 errors)`。macOS で三日月、コルクの帯と星印、家の丸窓を確認した。プレビューは専用の保存先を使う。
- Chrome でタイトルと入場を 1280×720、960×720、420×720 のキャンバスで確認した。日暮れ、夜、小雨の保存を読み込み、元のジャーナルのバイト列が一致した。縦長のタイトルは確認済みで、ゲーム内の操作は横長表示を基準にしている。
- 初回の一投、願い、届け先、24投の保存から動きを抑えて25投目を追加する操作を通した。844×390 の横長タッチ再現ではメニューを閉じたタップを投擲にせず、次のタップで一投だけを保存した。スマートフォン実機は未確認。
- macOS の既存の保存21件は作業前後で SHA-256 が一致した。
- Unity EditMode は全体 `324/324`、コルクと海の最終調整後の表示テストは `16/16`。 `dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release` は `278/278`。
- `pnpm typecheck` は終了コード0、`pnpm test` は `183 passed (183)`、`pnpm build` は `✓ built in 374ms`。
