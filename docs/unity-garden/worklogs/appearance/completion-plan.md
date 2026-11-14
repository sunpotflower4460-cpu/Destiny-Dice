# 手動確認前の仕上げ — Claude Code の計画と触る場所（2026-11-14）

本人の指示: 手動確認の前に、ゲームとしての完成度・面白さ・因果関係を最大にする。今回はルールも画面も Claude Code が直接変えてよい（本人承認済み）。
Codex の公開 API・固定ハッシュのテスト（v4〜v9・従来の庭）・保存形式の互換は守る。新しい決まりは祈りの星の庭（`ctx.FaceId > 0`）か版で分ける。

## 物差し（`Tests/EditMode/ExperienceMeasureTests.cs`、6庭×48投×3つの遊び方）

main（PR #49 統合後、ecology.v9）の初期値:

| | 願わない | 多い恵みを願う | ラッキーになる面を願う | 目安 |
| --- | --- | --- | --- | --- |
| 恵みの近く（届け先3マス以内・星や精霊の届いた株）で芽・葉・つぼみ・花が進んだ回 | 84% | 91% | 89% | 80%以上 |
| 静かな回（どこにも段階の変化もしるしもない） | 6% | 3% | 5% | 10%以下 |
| 出来事の見出しがその投の場所・星の恵みの株 | 66% | 70% | 66% | 60%以上 |
| 花（12/24/48投） | 5.2/5.5/6.8 | 6.3/9.5/6.3 | 4.7/8.3/7.5 | 24投で5以上 |
| しるしの出る回 | 39% | 38% | 36% | 25〜40% |
| 七つめの面が出る回 | 15% | 30% | 24% | 12〜20% |
| 大きな出来事（虹・三つぞろい・合計77・七つの幸運） | 9.9投に1回 | 8.2投に1回 | 11.5投に1回 | 12〜20投に1回 |
| 48投までに虹を見る庭 | 83% | 100% | 83% | 祈りで差が出る |

因果の背骨（答えのある回・静かな回・見出し）は v9 で目安を満たしている。足りないのは、祈りの手ごたえ（祈り方で差が出ない、七つめの面が多い）と、
毎回の操作の弱さ（面の帯は押せない、ラッキーになる面が帯に出ない）、48投の区切り。

## 順番と触る場所

1. 計測（このPR）: `Tests/EditMode/ExperienceMeasureTests.cs`（Domain/Application のみ。.NET CI でも動く）。
2. 祈りの釣り合い: `Domain/LuckySeven.cs`（願いが七つめの面を呼ぶのは面の願いだけ）、`Domain/PrayerStar.cs`（合計77のご褒美を七つめの面からつぼみの開花へ、prayer-star.v6）。
   虹の下で咲いている花が花粉を受け取る（`Domain/LivingEcosystem.cs` の `ApplyGift` の直後、祈りの星の庭だけ。記録の種類 `rainbow-pollinated`、見出しは `Application/GardenMoments.cs`）。
3. 面を押して祈る・ラッキーになる面の印: `Presentation/GardenUi.Navigation.cs`（面の帯）、`GardenUi.cs`、`GardenBootstrap.cs`。
4. 幸運の株は長く咲く: `Domain/LivingEcosystem.cs` の花を閉じる判定に `PlantState.LuckyMarks` を足す（印は祈りの星の庭にしかないので固定ハッシュは不変）。
5. 「季節」を一つに（12投ごとは「庭の願い」）と、48投ごとの「庭のしおり」: `Domain/Season.cs`、`Presentation/PrayerStarSigil.cs`、`Presentation/GardenKeepsake.cs`、新規 `GardenKeepsakeCard.cs`。
6. 仕上げ、48投の通しプレイ、手動確認の手引き `docs/unity-garden/MANUAL_CHECK.md`。

各段階は小さな PR で main へ。Codex の作業と同じファイルを触るときは、最新の main を取り込んでから始める。
