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
> Time passes with every roll: the island moves from a clear morning through noon, gold
> and dusk into a moonlit night (one day is sixteen rolls), and the light, the sea and the
> sound change with it. A spring at the back of the island feeds the river through a small
> waterfall. All the sound is synthesized in code: the sea, the stream, wind in the leaves,
> birdsong and night insects grow with the island, a pad adds a voice with each step of
> harmony, and every flower that opens rings its own note of one shared scale.
>
> Every garden is a different island. Where the highlands rise, which way the river winds
> from its spring, where the standing stones, trees and the little house stand, which plants
> the island favours, the colour of its meadow and even the hour it begins are all drawn
> from your garden's own seed. Grass grows in wind-swept swards that lean the way this
> island's wind blows. Drag to turn the island around and see it from any side, scroll
> (or pinch) to come closer. Press "next visitor" and a different island begins.
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
| **次の来場者** (right menu) | Start a new garden on a different island. |
| **視点を戻す** (right menu) | Return the camera to the default view. |
| Drag / scroll or pinch on the island | Turn the island around; come closer or move away. (Drags that start on a button do nothing.) |
| **設定** (right menu) | Volume, mute, reduced motion, hide UI for 4 seconds. |
| **遊び方** (right menu) | Show the title card again. |
| **図鑑** (right menu) | Field guide: the flowers your garden has met, what each needs (soil, water, light) and what it does (softens soil, drains, feeds butterflies…). Unmet ones stay ？？？. |

Your garden is saved in the browser and is still there when you come back.
Sound starts after your first click or tap (browser rule).

## 4. Team

<!-- 要記入: itch.io の名前 / ハンドル（本人が記入） -->
- (fill in)

## 5. Credits

All 3D shapes, plants, creatures, textures, shaders, music and sounds are made in code for this game (no audio files). No
Asset Store packages, no external models, textures, music or sound files.

- **Font: Noto Serif JP** (Regular, SemiBold) — Google / Adobe, SIL Open Font License 1.1.
  https://github.com/notofonts/noto-cjk — license text is bundled at
  `Assets/DestinyGarden/Resources/Fonts/NotoSerifJP-OFL.txt`.
- **Font: Cormorant Garamond** (SemiBold) — The Cormorant Project Authors, SIL Open Font License 1.1.
  https://github.com/CatharsisFonts/Cormorant — license text is bundled at
  `Assets/DestinyGarden/Resources/Fonts/CormorantGaramond-OFL.txt`.
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
  - [screenshot-1.png](screenshot-1.png) — a garden after 20 rolls with the interface (WebGL)
  - [screenshot-2.png](screenshot-2.png) — the rainbow figure-8 in the air above the bottle, the sun in the sky
    (macOS build of the same code, QA capture `-dgWonders -dgNoUi -dgSeed 3333`; the moment itself is rare in play)
  - [screenshot-3.png](screenshot-3.png) — opening: the whole bottle, bare earth and water, with the title card (WebGL)
  - [screenshot-4.png](screenshot-4.png) — a different island (seed 44444): its river, spring and waterfall, tree layout (macOS, no UI)
  - [screenshot-5.png](screenshot-5.png) — the same island seen from a low angle after turning it around (macOS, no UI, `-dgYaw 235 -dgPitch 34 -dgDist 17`)

Suggested order on the page: 3, 1, 4, 5, 2 (from the bottle to your own island, from every side).
Islands in the screenshots were fixed with `-dgSeed` for the photos only; in the game the island comes from the garden's own id.

## 8. itch.io page settings

- Kind of project: **HTML**
- Upload `DestinyGarden-WebGL.zip`, tick **This file will be played in the browser**
- Embed options: viewport **1280 × 720**, tick **Fullscreen button**
  (leave "Automatically start on page load" off; the player clicks to start)
- Mobile friendly: leave off unless tested on a phone
- Genre: Simulation / Classification: Games
- Tags: unity, relaxing, garden, dice, mystical, Japanese

## 9. Build and verification record (2026-11-13 morning)

- Branch `feature/jam-living-island`. Zip: `unity/DestinyGarden/Builds/DestinyGarden-WebGL.zip`, 22,198,079 bytes,
  `index.html` at the root. Earlier fallbacks next to it: v1-safe, v2-ecology, v4-flora, v5-living-island, v6-polish, v7-day-night.
- Unity EditMode tests: 41 passed (core, Lucky Seven, ecology, harmony & mycorrhiza, flora, audio, sky clock,
  quality monitor, island seed).
- Headless Chromium: title → wish drawer → 20 rolls → Lucky Seven taken → records, journey, help → reload.
  A second script drags the island around, scrolls to zoom, checks that a drag starting on a button does nothing,
  resets the view and presses "next visitor" (the scene restarts on a new island). **0 console errors**, **60 fps**.
- A bug found by that browser test and fixed: dragging did not turn the island on a machine that reports a touchscreen device.
- Islands: 4 seeds side by side differ clearly (river course, highlands, hills, trees, stones, meadow colour);
  the garden's own ground is never touched (tested over 40 seeds).
- Sound was measured offline (see v7 notes) and **not listened to by ear**.
- Not tested: real touch devices (pinch and one-finger drag are written but only simulated mouse input was tried),
  Safari, Firefox, slower GPUs, the real itch.io page.
