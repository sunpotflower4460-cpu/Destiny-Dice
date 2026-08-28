# Intention Dice — プライバシー設計メモ

## 基本方針
Intention Dice はローカルファーストです。アカウント、広告SDK、行動分析SDK、クラウド同期をv1では使用しません。実験台帳、願い本文、気分、予言、判定内容は端末内SQLiteをsource of truthとして保持します。

## 外部通信
アプリの設定・実行状況により、次の通信だけが発生し得ます。

1. **RNG provider**
   - ANU endpointが設定されている場合、乱数取得リクエストを送信します。
   - ANUが未設定または失敗した場合、RANDOM.ORGへfallbackする場合があります。
   - 最終fallbackは端末内WebCryptoで、外部通信を必要としません。
   - 願い本文・気分・予言文などのユーザー内容はRNG providerへ送信しません。

2. **任意の週次chain-head外部アンカー**
   - 事前登録で公証をONにし、`VITE_NOTARY_ENDPOINT`が設定されている場合だけ使用します。
   - 送信するフィールドは `genesisHash / headHash / headSeq / protocolVersion` のみです。
   - Workerはこれ以外のJSONキーを拒否します。
   - 願い本文、成功基準、判定、気分、session raw bits、experiment IDは送信しません。
   - 公開GETログは外部アンカーの存在時刻を確認するためのもので、ローカル台帳を「完全改竄不能」にするものではありません。

## ローカル通知
日次リマインダーと願い締切通知はOSのLocal Notificationsを使います。通知permissionはユーザー操作時だけ要求します。封印願いの本文は通知payloadへ渡しません。

## エクスポート
ユーザーが共有・保存を実行した場合のみ、完全な実験エクスポートやFinal Reportを端末の共有シートへ渡します。エクスポートには監査・再現性のため願い本文等が含まれ得るため、共有先の選択はユーザー自身が行います。

## App Store Connect のPrivacy回答について
配布buildで外部Workerを有効化するか、Workerのログ保持期間・運用主体をどうするかにより、Appleの「Data Collected」回答は変わり得ます。提出時には本番設定を基準に回答し、単純に「データ収集なし」と決め打ちしないでください。

少なくともv1コードには、広告目的のtracking、第三者広告SDK、ユーザープロファイリング、アカウント識別子の送信経路はありません。
