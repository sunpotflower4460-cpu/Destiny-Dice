# Destiny Garden — Official Unity Game Jam 2026 submission kit

This page holds everything to paste into the itch.io project page and the jam
submission form. Upload and submission are done by the developer.

- Jam: https://itch.io/jam/official-unity-game-jam-2026
- Theme: **Lucky Number**
- Deadline: **2026-11-14 12:00 UTC = 11/14 (Sat) 21:00 JST**. No late submissions.
- Upload file: `unity/DestinyGarden/Builds/DestinyGarden-WebGL.zip` (`index.html` at the zip root)
- Build files must not change after the deadline and during judging.
  Page text and screenshots may still be edited.

---

## 1. Title

Destiny Garden

## 2. Short description (English)

> **A garden in a bottle that becomes your own.** It begins with a sealed glass vessel
> floating on a glowing sea: bare earth, a spring and a little river, four first shoots.
>
> Each roll of the die is a prayer. The sky and the state of your garden decide what the
> six faces will bring — water, wind, light or seed — and where it falls. Choose a face,
> make a wish, and roll.
>
> Then the island answers as one living whole. Fifteen kinds of flowers each grow only
> where they can live: some take hard ground, some need soft, wet or well-drained soil,
> some love the shade. Dandelions break the earth for lilies; sunflowers and lavender
> drain it; irises hold the water. Bees and butterflies visit the flowers they love and
> carry their seed onward; birds drop berries; fluff rides the wind. Moss, then grass,
> then meadow, then trees spread from the water — and when the garden is mature, a small
> house by the spring. Underground, the roots share what they have. No two gardens grow
> the same.
>
> Made for the Official Unity Game Jam 2026. Theme: **Lucky Number** — every roll is the
> luck of this moment. Harmony is read from the last eight rolls (yin and yang, the four
> blessings and the fifth that appears when all are present); the island's star grows
> from five points to seven to eight. When a wish comes true, the next roll may add a
> 7th face, the **Lucky Seven**, rolled fair at 1 in 7 — and if the garden answers it,
> a rainbow figure-8 rises into the air above the bottle.

Note on the text: the game UI and oracle messages are in **Japanese**; the title
card has an English summary. Add this line to the page:

> Language: in-game text is Japanese (with an English summary on the title screen).
> Everything is played with the big roll button; the controls below explain the rest.

## 3. Controls (English)

Mouse or touch only. No keyboard needed. Click the title card to begin.

| Button (Japanese label) | What it does |
| --- | --- |
| **サイコロを振る** (big button, bottom centre) | Roll the die. |
| Face pills (row above the roll button, coloured by blessing: blue water, green wind, gold light, brown seed) | This round's faces. Tap one to wish for that number. A blessing on several faces comes up more often. |
| **★ 七つめの面を加える** (gold chip, only after a wish came true) | Add the 7th face, the Lucky Seven, to this roll. All seven faces are then 1 in 7. |
| **願いをかける** (left of the roll button) | Opens the wish drawer; shows your current wish. |
| In the drawer: **意図なし / 水 / 風 / 光 / 種** | Wish for no element, or for water / wind / light / seed. Wishing never changes the faces. |
| In the drawer: **おまかせ / 入口 / 岩陰 / ひらけた土** | Where the next roll's blessing lands (auto / inlet / rock shade / open soil). |
| **庭の気配** (panel, bottom right) | Which small lives are in the garden right now, and its harmony ☯ n/8. |
| **記録** (right menu) | Your last rolls, oracles, and what the small lives did. |
| **庭の歩み** (right menu) | The flowers in bloom now, the harmony, and a graph of water and growth with the honest "by chance" line for wishes. |
| **書き出し** (right menu) | Export records. Not available in the web version. |
| **次の来場者** (right menu) | Start a new garden. |
| **設定** (right menu) | Volume, mute, reduced motion, hide UI for 4 seconds. |
| **遊び方** (right menu) | Show the title card again. |

Your garden is saved in the browser and is still there when you come back.
Sound starts after your first click or tap (browser rule).

## 4. Team

<!-- 要記入: itch.io の名前 / ハンドル（本人が記入） -->
- (fill in)

## 5. Credits

All 3D shapes, plants, creatures, textures, shaders and sounds are made in code for this game. No
Asset Store packages, no external models, textures, music or sound files.

- **Font: Noto Serif JP** (Regular) — Google / Adobe, SIL Open Font License 1.1.
  https://github.com/notofonts/noto-cjk — license text is bundled at
  `Assets/DestinyGarden/Resources/Fonts/NotoSerifJP-OFL.txt`.
- **Engine:** Unity 6 (6000.3.23f1), Universal Render Pipeline, Input System, uGUI.

## 6. Generative AI disclosure (English)

<!-- 要確認: [ ] の部分は本人が事実を確認して埋めること。推測で埋めていない。
     リポジトリから分かる事実:
     - Unity版の設計 (docs/unity-garden) は 2026-11-11 21:34 JST 開始、実装 (G1〜G3) は 2026-11-12。
       ジャム期間 (11/5 開始〜11/13) の内側。
     - 同リポジトリの TypeScript アプリ (Destiny Dice, 2026-07 開始) はジャム前の別作品。ゲームのコードと
       アセットは流用していないが、「サイコロで運を測る」発想は共有している。ジャム規約は
       "original games that were not created prior to the event" なので、気になる場合は説明文に
       "Inspired by an earlier non-game prototype of mine; all game code and content were made during the jam."
       のように一文足すと誠実。
     - generated_images/ の2枚 (= visual-plan/refs/hero.jpg, dice.jpg) は画像生成AIで作った参照画。
       どのツールかはリポジトリから判別できない。ゲーム本体には入っていない。
     - このWebGL対応と提出素材は Claude Code (Claude Opus 5.5) で作業。 -->


> Generative AI disclosure: This game was designed and built during the jam
> period with the assistance of AI tools.
> **Claude Code** (Anthropic) was used to assist with game design documents (including
> the ecology rules), C# implementation, procedural visuals, debugging, the WebGL port
> and this submission text.
> [Tool name] was used to generate two concept art images that served as visual
> references for the art direction; these images are not included in the game.
> [Other tools, if any: name and use].
> The game's concept, direction, selection of all content and final decisions
> are the developer's own. Third-party assets are credited on this page.

## 7. Images

- Cover image: [cover.png](cover.png) — 1260×1000 (itch.io ratio 630:500, 2× size)
- Screenshots (1920×1080):
  - [screenshot-1.png](screenshot-1.png) — after 20 rolls in light rain: meadow, trees, flowers, a relationship line (WebGL)
  - [screenshot-2.png](screenshot-2.png) — the rainbow figure-8 in the air above the bottle (macOS build of the same code,
    QA capture `-dgWonders -dgNoUi`; the moment itself is rare in play)
  - [screenshot-3.png](screenshot-3.png) — opening: the whole bottle, bare earth and water, with the title card (WebGL)
  - [screenshot-4.png](screenshot-4.png) — roll 0: bare earth, the spring and river, four first shoots (macOS, no UI)
  - [screenshot-5.png](screenshot-5.png) — 庭の歩み: the flowers in bloom, harmony, and the honest wish meter (WebGL)

Suggested order on the page: 3, 4, 1, 2, 5 (from the bottle to a garden of your own).

## 8. itch.io page settings

- Kind of project: **HTML**
- Upload `DestinyGarden-WebGL.zip`, tick **This file will be played in the browser**
- Embed options: viewport **1280 × 720**, tick **Fullscreen button**
  (leave "Automatically start on page load" off; the player clicks to start)
- Mobile friendly: leave off unless tested on a phone
- Genre: Simulation / Classification: Games
- Tags: unity, relaxing, garden, dice, mystical, Japanese

## 9. Build and verification record (2026-11-12 night)

- Branch `feature/jam-living-island`. Zip: `unity/DestinyGarden/Builds/DestinyGarden-WebGL.zip`, 22,155,414 bytes,
  `index.html` at the root. Earlier fallbacks next to it: v1-safe, v2-ecology, v4-flora, v5-living-island (= current).
- Unity EditMode tests: 36 passed (core, Lucky Seven, ecology, harmony & mycorrhiza, flora).
- Headless Chromium: title → wish drawer → 20 rolls → Lucky Seven taken → records, journey, help → reload.
  **0 console errors**, **60 fps** at the start and after 12 rolls (1280×720, this iMac's GPU).
- Measured rates (720 rolls, policy: wish for the most common blessing, always take the 7th face):
  wish matched 45%, Lucky Seven rolled 6%, rainbow figure-8 about 1 in 36 rolls; harmony mean ≈5;
  species seen in 120 rolls: 11 of 15.
- Audio: "Audio context resumed" after the first click. Not listened to — check by ear before submitting.
- Not tested: Safari, Firefox, phones, slower GPUs, the real itch.io page.
