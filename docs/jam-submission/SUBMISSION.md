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

> **A tiny sacred garden on an island in a twilight sea.** The sky and the state of
> your garden decide the six faces of the die — water, wind, light or seed — so the
> die is different every time you look at it. Choose a face, make a wish, and roll.
>
> Then the garden's small lives take over. Bees and butterflies carry pollen, a
> little bird drops seeds from somewhere, fluff rides the wind; moss glows in the
> rock shade, fireflies and mushrooms feed the soil, earthworms work below. What you
> set in motion grows into an ecosystem you never quite planned.
>
> And now and then, a wish takes shape: faint spirits carry nourishment down to the
> plants, and a seventh face appears on the die.
>
> Made for the Official Unity Game Jam 2026. Theme: **Lucky Number** — every roll
> is the luck of this moment. When a wish comes true, the next roll may add a 7th
> face, the **Lucky Seven**: the blessing the garden needs most, at double
> strength, rolled fair at 1 in 7. If the Lucky Seven makes a flower bloom, a
> rainbow figure-8 rises over the garden and angels ascend.

Note on the text: the game UI and oracle messages are in **Japanese**; the title
card has an English summary. Add this line to the page:

> Language: in-game text is Japanese (with an English summary on the title screen).
> Everything is played with the big roll button; the controls below explain the rest.

## 3. Controls (English)

Mouse or touch only. No keyboard needed. Click the title card to begin.

| Button (Japanese label) | What it does |
| --- | --- |
| **サイコロを振る** (big button, bottom centre) | Roll the die. |
| Face chips (coloured by blessing: blue water, green wind, gold light, brown seed) | This round's faces. Tap one to wish for that number. A blessing on several faces comes up more often. |
| **★ 七つめの面を加える** (gold chip, only after a wish came true) | Add the 7th face, the Lucky Seven, to this roll. All seven faces are then 1 in 7. |
| **意図なし / 水 / 風 / 光 / 種** | Wish for no element, or for water / wind / light / seed. Wishing never changes the faces. |
| **おまかせ / 入口 / 岩陰 / ひらけた土** | Where the next roll's blessing lands (auto / inlet / rock shade / open soil). |
| **庭の気配** (panel, bottom right) | Which small lives are in the garden right now. |
| **記録** (right menu) | Your last rolls, oracles, and what the small lives did. |
| **庭の歩み** (right menu) | Graph of water and growth, with the honest "by chance" line for wishes. |
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

All 3D shapes, creatures, textures and sounds are generated in code at runtime. No
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

All taken from the WebGL build running in Chromium (1920×1080), fresh garden.

- Cover image: [cover.png](cover.png) — 1260×1000 (itch.io ratio 630:500, 2× size)
- Screenshots:
  - [screenshot-1.png](screenshot-1.png) — after 10 rolls: the oracle for the rolled face, flowers in bloom
  - [screenshot-2.png](screenshot-2.png) — wishing on a number before rolling
  - [screenshot-3.png](screenshot-3.png) — the moment of a roll (blessing landing on the board)
  - [screenshot-4.png](screenshot-4.png) — drizzle weather: same garden idea, different sky, different faces
  - [screenshot-5.png](screenshot-5.png) — a new garden, roll 0

Suggested order on the page: 1, 4, 2, 3 (5 is optional).

## 8. itch.io page settings

- Kind of project: **HTML**
- Upload `DestinyGarden-WebGL.zip`, tick **This file will be played in the browser**
- Embed options: viewport **1280 × 720**, tick **Fullscreen button**
  (leave "Automatically start on page load" off; the player clicks to start)
- Mobile friendly: leave off unless tested on a phone
- Genre: Simulation / Classification: Games
- Tags: unity, relaxing, garden, dice, mystical, Japanese

## 9. Build and verification record (2026-11-12)

- Build: `BuildScripts.BuildWebGL` (Unity 6000.3.23f1, batchmode), gzip +
  decompression fallback, custom template `Assets/WebGLTemplates/DestinyGarden`.
- Rebuild: from `unity/DestinyGarden`
  `Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod DestinyGarden.EditorTools.BuildScripts.BuildWebGL`,
  then `cd Builds/WebGL && zip -qr -X ../DestinyGarden-WebGL.zip . -x ".*"`.
- Zip: 21,996,735 bytes (22.0 MB), 6 files, `index.html` at the root.
- Play-through in headless Chromium (Metal/ANGLE), served from localhost:
  load ≈ 9 s; wish on a face, roll ×3, oracle, Records, Garden journey,
  Export (web message), Settings — **0 console errors**.
- Save: after reload the header still showed "3投目まで記録" and the same faces
  (IndexedDB persistence through `autoSyncPersistentDataPath`).
- Frames 1280×720, 960×760 and 1400×600 all letterbox to 16:9, nothing cropped.
- Audio: the browser log shows "Audio context resumed" after the first click.
  Sound was **not listened to** (headless) — check by ear once before submitting.
- Not tested: Safari, Firefox, phones, the real itch.io page.

Known layout issues present before the WebGL work (not fixed, game unchanged):
a two-line oracle message touches the header line; the caption under the
"庭の歩み" graph overlaps the bars.
