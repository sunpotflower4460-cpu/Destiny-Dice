# Unityなしで生態の規則を検証する

.NET SDK 8 を用意し、リポジトリのルートで実行:

```sh
dotnet test tools/ecology-check/Ecology.Check.csproj --configuration Release
```

Unityプロジェクト内の **実際の** Domain / Application / Infrastructure と、既存のEditModeテストを直接コンパイルして実行します。
追加のゲーム実装や写経した参照モデルはありません。Unityに依存する診断ログのみ `UnityDebug.cs` で置き換えます。
`PresentationTests.cs` はUnityエンジンが必要なので除外しています。

生態v4のテストでは有限の養分、受粉から結実までの時間、種の厳密な継承、餌と隠れ場所、植物の回復と世代交代、保存復帰、
4庭×240投を検証します。改修前 `ac12b67` の実行で採取した初期状態・24投後のハッシュとも照合し、旧庭の規則を固定しています。

これはUnityのコンパイル、描画、音、入力、WebGLビルドの検証を代替しません。リリース前にはUnity 6000.3.23f1で
全EditModeテストを実行し、WebGLプレイヤーで図鑑のスクロール、新しい住人、旧ジャーナル復帰、新しい来場者を確認してください。
