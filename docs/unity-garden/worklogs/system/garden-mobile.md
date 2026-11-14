# Destiny Garden のスマートフォン対応（2026-11-14）

## 対応範囲

- Unity の `GardenScene` を iOS と Android のネイティブアプリとしてビルドする。横持ちの左右回転を許可する。
- タイトル、庭の操作、メニュー、祈りの星の常時表示を `Screen.safeArea` に合わせる。画面サイズや端末の向きの変化に追従し、背景は画面全体に描く。 [Unity の安全領域](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screen/safearea)
- タッチ入力は既存の Unity Input System を使う。庭の進行と保存の形式は共通の実装を使う。

## ローカルでビルド

Unity 6000.3.23f1 の Hub で iOS Build Support と Android Build Support を入れる。Android には SDK（API 36）、NDK、OpenJDK が必要。iOS には Xcode が必要。 [Unity の Android 開発環境](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/sdksetup) · [Unity の iOS ビルド手順](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/iphone/ios-introducing/how-unity-builds-ios-applications)

macOS でリポジトリのルートから実行する。Unity Editor がこのプロジェクトを開いている場合は、Editor を閉じてから実行する。

```sh
unity_editor="/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity"
garden_project="$PWD/unity/DestinyGarden"

"$unity_editor" -batchmode -nographics -quit -projectPath "$garden_project" \
  -buildTarget Android -executeMethod DestinyGarden.EditorTools.BuildScripts.BuildAndroid -logFile -

"$unity_editor" -batchmode -nographics -quit -projectPath "$garden_project" \
  -buildTarget iOS -executeMethod DestinyGarden.EditorTools.BuildScripts.BuildIosSimulator -logFile -

"$unity_editor" -batchmode -nographics -quit -projectPath "$garden_project" \
  -buildTarget iOS -executeMethod DestinyGarden.EditorTools.BuildScripts.BuildIosXcodeProject -logFile -
```

- Android の試遊用 APK は `unity/DestinyGarden/Builds/Android/DestinyGarden.apk`。ARM64 の Android 端末にインストールする。
- iPhone シミュレーター用は `Builds/iOS-Simulator/Unity-iPhone.xcodeproj`。Xcode の `Unity-iPhone` スキームで iPhone シミュレーターを選んで実行する。
- iPhone 実機用は `Builds/iOS/Unity-iPhone.xcodeproj`。Xcode の `Unity-iPhone` スキームで開発チームと端末を選んで実行する。バンドル ID `com.example.destinygarden.dev` は既存の開発用の値。

## 検証

- Android は Unity で APK のビルドに成功。ARM64、最小 API 25、対象 API 36、横画面の設定を確認した。APK Signature Scheme v2、ZIP の整合性とアラインメントを検証した。接続済みの Android 端末はなく、端末上の動作確認は未実施。
- iPhone 17 Pro シミュレーター（iOS 26.2、Xcode 26.2）で起動した。タップで入場、メニューの開閉、一投、バックグラウンドからの復帰、アプリ終了後の庭の再開、左右の横持ちを確認した。ドラッグでの回転、ピンチでの拡大、視点を戻す操作も確認した。再開後のジャーナルには解決済みの一投だけがあり、ハッシュチェーンが一致した。
- iPhone 実機用は DeviceSDK で Xcode プロジェクトを生成し、Xcode 26.2 の ARM64 Release ビルドが `BUILD SUCCEEDED`。開発署名を使ったインストールと実機上の動作は未確認。
- Unity EditMode の表示テストは `17/17`。pnpm 10 の型検査とビルドが成功し、テストは `183/183`。作業前からあった macOS のジャーナル21件のハッシュは一致した。
- PR #41 をベースに、別 worktree の専用ブランチで作業した。Claude Code の PR #42（`5a92602`）との試験マージで競合はなかった。
