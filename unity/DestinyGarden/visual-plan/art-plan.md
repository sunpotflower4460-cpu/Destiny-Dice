# Art Plan — Destiny Garden (from refs/hero.jpg + refs/dice.jpg to build-final.png)

Date: 2026-11-12. Inputs: `/tmp/game/brief.md`, `visual-plan/PRD.md`,
`visual-plan/refs/hero.jpg` (garden target), `visual-plan/refs/dice.jpg` (dice target),
`visual-plan/refs/build-final.png` (current build, drizzle, 8 rolls, 1600x900),
`visual-plan/refs/build-round3.png` (same framing, cloudy).
Code surveyed: `Assets/DestinyGarden/Presentation/GardenView.cs`,
`ProceduralArt.cs`, `GardenUi.cs`, `GardenBootstrap.cs`,
`Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset`,
`ProjectSettings/ProjectSettings.asset`.

## 0. Binding constraints (do not break while applying this plan)

- Code-only art: procedural `Texture2D` pixels, displaced-sphere rocks, primitive
  plants, URP Lit/Unlit. No imported meshes, textures, fonts, or shader graphs.
- `Domain/GardenMapData.cs` is generated (header: do not edit). The map has
  **3 solid cells** and **~55 soil cells in the centre 8x8 (x 4..11, y 4..11)**;
  the other ~198 cells are stone. Hero density must come from *presentation
  dressing* (frame, apron, decorative rocks outside the 16x16), never from
  editing `Solid` / `SoilCapacity`.
- Honest water (PRD: effects tied to saved facts): never draw glowing surface
  water where `WorldState.SurfaceWater == 0`. A permanent dark *channel bed*
  (static map height) is allowed; a permanent glowing stream is not.
- Project colour space is **Linear** (`m_ActiveColorSpace: 1`), URP HDR on.
  Every hex below is an sRGB display target. Set material/light/fog/UI colours
  from hex with `ColorUtility.TryParseHtmlString`, or paste the listed
  `linear Color(...)` floats. **Exception:** `ProceduralArt.MakeTex` uses
  `new Texture2D(size, size, RGBA32, true)` (sRGB texture), so texel endpoints
  take the listed `texel(...)` floats (= hex/255), *not* the linear floats.
  Mixing these two up is the single easiest way to miss every target below.
- QA captures (`GardenBootstrap.CaptureRoutine`) switch the canvas to
  ScreenSpaceCamera, so ACES + Bloom hit the UI in screenshots. Judge final UI
  brightness in normal ScreenSpaceOverlay play; expect captures to read
  ~1 stop darker and softer than the shipped overlay.

### What build-final.png currently reads as (measured from code + image)

| Area | Current build | Hero target |
|---|---|---|
| Camera | Edge-on (azimuth 0°), elevation 42.4°, dist 21.7, FOV 41 (`GardenView.cs:112-115`) | 3/4 diamond, elevation ~33-36° |
| Board | 198 identical grey-blue slabs dominate; soil is a small flat patch | Soil plots + moss ridges dominate; stone is terrace |
| Rocks | 3 smooth grey eggs, centre only | ~14 craggy pillars, tallest ~3 cells, moss caps |
| Water | Opaque cyan ellipses ("coins"), LDR, no bloom halo | Translucent cyan stream, HDR core, soft halo |
| Dice | Scale 1.45 at cell 15, clipped bottom-right, pips proud dots, 3 blank faces | ~1 cell, on moss pad, inset glowing pip pools |
| Light | Flat, dim, far edge fogged; shadow atlas 256 = one black blob | Warm key upper-left vs cool fill, long soft shadows |
| Air | Flat dark void; sky dome / mist / far rocks / shaft invisible | Layered teal mist, light shaft, pollen sparkles |
| UI | Sharp flat rectangles, unbacked blurry text, white on mustard | Rounded dark-glass panels, clear hierarchy |

## 1. Palette (exact values)

### 1.1 Sky, fog, air

| Token | sRGB hex | linear `Color(r,g,b)` | Applied to |
|---|---|---|---|
| Fog / camera bg | `#0B2730` | `(0.003, 0.020, 0.030)` | `GardenView.Mist`, `RenderSettings.fogColor`, `cam.backgroundColor` (`GardenView.cs:21,103,114`) |
| Sky zenith | `#041014` | `(0.001, 0.005, 0.007)` | `ProceduralArt.SkyGradient` top (texel `(0.02,0.06,0.08)`) |
| Sky horizon | `#1E4F5F` | `(0.013, 0.078, 0.114)` | `SkyGradient` bottom (texel `(0.12,0.31,0.37)`) |
| Mist near | `#BFE8EE` | `(0.521, 0.807, 0.855)` | Mist quads `_BaseColor`, alpha 0.32 (see §3.6) |
| Mist far | `#6FA8B4` | `(0.159, 0.392, 0.456)` | Far mist ring, alpha 0.22 |
| Light shaft warm | `#FFDFA8` | `(1.000, 0.738, 0.392)` | Shaft `_BaseColor`, alpha 0.07 clear / 0.04 cloudy / off drizzle |
| Pollen gold | `#FFE9A8` | `(1.000, 0.815, 0.392)` | Pollen start colour, HDR x1.6 (see §4.9) |
| Rain streak | `#A8D4E8` | `(0.392, 0.658, 0.807)` | Rain start colour, alpha 0.38 |

### 1.2 Soil (dry / moist / wet) and moss

Soil is texture x moisture tint (`GardenView.Show`, `GardenView.cs:480-487`).
Current tints are the root defect: `DrySoil (0.95,0.82,0.62)` linear displays
as `#F9EACE` and `WetSoil (0.42,0.32,0.24)` as `#AD9986`, so "wet" never gets
dark. Replace with a 3-stop ramp (dry -> moist -> wet), not a 2-stop lerp.

| Token | sRGB hex | linear tint | texel endpoint | Applied to |
|---|---|---|---|---|
| Soil dry tint | `#C9A87C` | `(0.584, 0.392, 0.202)` | — | `DrySoil` (`GardenView.cs:18`) at moisture 0.0 |
| Soil moist tint | `#7A5A3E` | `(0.195, 0.102, 0.048)` | — | New mid stop at moisture 0.45 |
| Soil wet tint | `#3E2A1D` | `(0.048, 0.023, 0.012)` | — | `WetSoil` (`GardenView.cs:19`) at moisture 0.85+ |
| Soil tex light | `#B08D5F` | — | `(0.69, 0.55, 0.37)` | `ProceduralArt.SoilTexture` light end (`ProceduralArt.cs:60`) |
| Soil tex dark | `#6E5136` | — | `(0.43, 0.32, 0.21)` | `SoilTexture` dark end |
| Moss shadow | `#2A4A26` | `(0.023, 0.068, 0.019)` | `(0.16, 0.29, 0.15)` | `MossTexture` dark end (`ProceduralArt.cs:81`) |
| Moss mid / ridge | `#4A7A36` / `#5C8A3E` | `(0.068,0.195,0.037)` / `(0.107,0.254,0.048)` | `(0.29,0.48,0.21)` / `(0.36,0.54,0.24)` | `MossTexture` light end; new `_mossMat` for soil-cell rims |
| Moss sunlit tip | `#7AB648` | `(0.195, 0.468, 0.065)` | `(0.48, 0.71, 0.28)` | Sparse tip speckle in `MossTexture` (detail > 0.82) |
| Leaf shadow / lit | `#2E5E2A` / `#6FB84E` | tint `(0.42, 0.95, 0.38)`* | — | `_leafMat.color` (`GardenView.cs:80`); *tint multiplies MossTexture, keep >1 green |
| Stem | `#3E7038` | `(0.048, 0.162, 0.040)` | — | `_stemMat.color` (`GardenView.cs:83`) |

Moisture mapping (replace `moisture * 1.2f` lerp): `m = SoilWater / SoilCapacity`;
`m < 0.45`: `Lerp(dry, moist, m/0.45)`; else `Lerp(moist, wet, (m-0.45)/0.40)`,
clamped. Wet smoothness: lerp `_Smoothness` cannot vary per-cell on a shared
material without MPB; add `_block.SetFloat("_Smoothness", Lerp(0.05, 0.55, m))`
in the same MPB so wet mounds catch the key light like hero's damp soil.

### 1.3 Stone, wood, channel bed

| Token | sRGB hex | linear | texel | Applied to |
|---|---|---|---|---|
| Stone shadow | `#3A4446` | `(0.042, 0.058, 0.061)` | `(0.23, 0.27, 0.27)` | `StoneTexture` dark end (`ProceduralArt.cs:70`) |
| Stone mid | `#6E7A7C` | `(0.156, 0.195, 0.202)` | `(0.43, 0.48, 0.49)` | `StoneTexture` light end |
| Stone sunlit | `#9AA6A4` | `(0.323, 0.381, 0.371)` | `(0.60, 0.65, 0.64)` | Top-face lift only: rock tint, see below |
| Rock tint | `#B8C2BE` | `(0.48, 0.54, 0.52)` | — | `_rockMat.color` (`GardenView.cs:78`), replaces `(0.85,0.88,0.88)` |
| Apron slab tint range | `#5E6E6E` centre | `(0.112, 0.156, 0.156)` | — | Stone-cell MPB jitter 0.72..1.08 x tint (`GardenView.cs:490-492`) |
| Wood dark / mid / lit | `#4A3524` / `#6E5236` / `#8A6A48` | — | `(0.29,0.21,0.14)` / `(0.43,0.32,0.21)` / `(0.54,0.42,0.28)` | `WoodTexture` (`ProceduralArt.cs:89`), plank edge x0.45 |
| Channel bed (dry) | `#1E3A3E` | `(0.013, 0.042, 0.048)` | — | New non-emissive bed discs on low stone cells (§4.5) |
| Channel wet stone | `#2E4E52` | `(0.027, 0.076, 0.084)` | — | Bed tint when any neighbour is wet (damp halo, no glow) |

### 1.4 Water glow, dice, flower, gold (HDR emitters)

LDR colours cannot cross the Bloom threshold reliably after ACES. All four
hero glows must be authored HDR (values > 1 in at least one channel).

| Token | sRGB base | HDR value to set | Applied to |
|---|---|---|---|
| Water gradient deep | `#0E6E8C` | `_BaseColor (0.02, 0.45, 0.70, 0.78)` | Edge of water disc via `GlowSprite` alpha falloff |
| Water glow core | `#2FD8E8` | `_BaseColor (0.08, 1.55, 1.85, 0.82)` | `_waterMat` (`GardenView.cs:91-95`); pulse alpha 0.74..0.88 only |
| Water hot centre | `#9FF4FF` | `(0.55, 1.90, 2.10)` | Centre texel lift: multiply `GlowSprite` centre RGB x1.35 |
| Pip glow | `#2EE6D6` | `_EmissionColor (0.10, 2.80, 2.35)` (= base x3.2) | Pip material (`GardenView.cs:357-359`), was x2.2 |
| Pip socket (fake cavity) | `#0A3A3C` | `(0.003, 0.042, 0.045)`, no emission | New dark disc behind each pip (§4.7) |
| Dice stone | `#EDE8DC` | tint `(0.847, 0.807, 0.716)` | Dice material colour (`GardenView.cs:352`), was `(1.25,1.22,1.15)` |
| Focus ring gold | `#FFC766` | `_EmissionColor (2.40, 1.42, 0.38)` | Ring material (`GardenView.cs:366-368`), was `(1.5,1.0,0.4)` |
| Flower petal | `#F6C9D9` | albedo `(0.922,0.584,0.694)`, emission `(0.85, 0.38, 0.48)` | `_petalMat` (`GardenView.cs:86-89`) |
| Flower centre | `#FFF3C9` | emission `(1.60, 1.25, 0.55)` | Flower centre sphere, separate material (was shared petal) |
| Bud | `#D98AA8` | `(0.694, 0.254, 0.392)`, emission `(0.28, 0.08, 0.14)` | `_budMat` (`GardenView.cs:85`) |
| Seed tan | `#D8B88A` | `(0.687, 0.479, 0.254)`, no emission | New `_seedMat`; Seed stage must stop using `_budMat` pink |

### 1.5 Warm key light

| Token | sRGB hex | linear | Applied to |
|---|---|---|---|
| Sun warm (clear/cloudy) | `#FFD9A0` | `(1.000, 0.694, 0.352)` | `_sun.color` (`GardenView.cs:142,547`) |
| Sun drizzle (cool, dim) | `#C4D8E8` | `(0.552, 0.687, 0.807)` | `_sun.color` when `WeatherId.Drizzle` |
| Fill cool | `#7FB2CC` | `(0.212, 0.445, 0.604)` | `_fill.color` (`GardenView.cs:151`) |
| Ambient sky (trilight) | `#33565E` | `(0.033, 0.093, 0.112)` | `RenderSettings.ambientSkyColor` |
| Ambient equator (moss bounce) | `#223E36` | `(0.016, 0.048, 0.037)` | `RenderSettings.ambientEquatorColor` |
| Ambient ground | `#101A17` | `(0.005, 0.010, 0.009)` | `RenderSettings.ambientGroundColor` |

### 1.6 UI panels / chips / accent

Current (`GardenUi.cs:38-41`): panel `(0.04,0.12,0.16,0.88)`, chip
`(0.10,0.22,0.28,0.95)`, active `(0.75,0.58,0.25,0.98)`, text
`(0.93,0.95,0.92)`. Keep the family, fix contrast and state separation (§5).

| Token | sRGB hex + alpha | linear RGB | Applied to |
|---|---|---|---|
| Panel glass | `#0E2A33` a0.90 | `(0.004, 0.023, 0.033)` | `PanelColor`, header/message/status backings |
| Panel deep (modal) | `#0A2028` a0.96 | `(0.003, 0.014, 0.021)` | History / graph / settings panels |
| Chip normal | `#1A3F4D` a0.96 | `(0.010, 0.050, 0.074)` | `ChipColor` |
| Chip hover/pressed | `#26586B` a0.98 | `(0.019, 0.098, 0.147)` | Button `ColorBlock.highlighted/pressed` |
| Chip selected (accent) | `#C9973F` a1.00 | `(0.584, 0.309, 0.050)` | `ChipActive` (face / intent / site selection) |
| Roll primary | `#D9A84E` a1.00 | `(0.694, 0.392, 0.076)` | Roll button (`GardenUi.cs:118`), brighter than selected |
| Roll disabled | `#3A4E57` a0.90 | `(0.042, 0.076, 0.095)` | `_rollButton` while `_busy` |
| Text primary | `#F2F0E6` | `(0.888, 0.871, 0.791)` | `TextColor` on dark chips/panels |
| Text on gold | `#241A06` | `(0.018, 0.010, 0.002)` | Label inside selected chip + roll button (was white: ~2:1, fails) |
| Text secondary / label | `#A9C4C2` | `(0.397, 0.552, 0.539)` | Section labels (`MakeLabel`) |
| Chip border (in sprite) | `#7FB0B2` a0.85 | `(0.212, 0.434, 0.445)` | `RoundedPanelSprite` rim texel (`ProceduralArt.cs:123`) |
| Graph water / growth | `#3FB6D9` / `#6FC46A` | `(0.050,0.468,0.694)` / `(0.159,0.552,0.144)` | `MakeBar` colours (`GardenUi.cs:284-287`) |

## 2. Lighting, fog, post (exact numbers)

All edits in `GardenView.Build` unless noted. Current values in parentheses.

### 2.1 Sun (warm key) — `GardenView.cs:139-146` + per-weather `Show` `:544-548`

- Colour: `#FFD9A0` clear/cloudy; `#C4D8E8` drizzle (was `(1,0.86,0.66)` /
  `(0.78,0.87,1.0)` raw linear, too pale/washed).
- Rotation: `Euler(48, -38, 0)` (was `50, -42`). With the new SW camera (§3.1)
  this puts the key at screen upper-left and throws rock shadows to screen
  lower-right, matching hero. Do not rotate the sun with weather; only
  intensity/colour change, so shadow direction stays learnable.
- Intensity: **Clear 2.20 / Cloudy 1.50 / Drizzle 0.90** (was 1.35/0.92/0.62).
  ACES at exposure 0 needs >2 for sunlit stone to reach hero's bright rims.
- Shadows: `Soft`, `shadowStrength 0.85` (was 0.75), `shadowBias 0.04`,
  `shadowNormalBias 0.06` (code-set so PC/Mobile RPAsset bias drift cannot
  acne the 0.96-scale cell cubes). `shadowNearPlane 4`, `shadowFarPlane 55`.

### 2.2 Fill (cool bounce) — `GardenView.cs:148-154`

- Colour `#7FB2CC`, rotation `Euler(30, 145, 0)` (was `38, 130`), no shadows.
- Intensity: **Clear 0.45 / Cloudy 0.55 / Drizzle 0.65** (was 0.35/0.45/0.45).
  Fill rises as sun falls so drizzle stays readable without going flat:
  key:fill ratio goes 4.9:1 (clear, hero contrast) to 1.4:1 (drizzle, soft).

### 2.3 Ambient — replace flat ambient (`GardenView.cs:107`)

Current flat `ambientLight (0.34,0.46,0.52)` linear displays as `#9EB5BF` and
lifts every shadow to blue-grey (visible as build-final's washed slabs).
Replace with trilight, intensity 1.0 on all three:

- `RenderSettings.ambientMode = AmbientMode.Trilight`
- Sky `#33565E`, Equator `#223E36`, Ground `#101A17` (§1.5).
- Weather trim: drizzle multiplies all three by 1.15 (overcast bounce);
  clear/cloudy x1.0. Never exceed x1.2 or the warm/cool contrast collapses.

### 2.4 Fog — `GardenView.cs:102-106` (highest-leverage 2 numbers in this plan)

- Colour `#0B2730`, mode Linear, **start 26, end 58** (was 20 / 46).
- Why: current camera distance 21.7 puts the far board corner at ~33 units =
  50% fog, which is why build-final's north half looks bleached. New camera
  distance 24.0 (§3.1): near corner ~13 (0% fog), far corner ~35 (28% fog,
  gentle aerial depth), far rocks at 40-55 (44-91%, silhouettes), skybox
  unaffected. Board gameplay area (centre 8x8, distance 20-28) stays 0-6% fog.
- Drizzle: start 22 / end 50 (closer air). Clear: 28 / 62. Lerp on weather
  change over 0.8 s in `Show`; do not snap (visible pop on far rocks).

### 2.5 Post — Volume profile (`GardenView.cs:121-137`)

| Effect | Set | Was | Note |
|---|---|---|---|
| Tonemapping | ACES (keep) | ACES | Hero highlight rolloff needs ACES; Neutral goes chalky |
| Exposure (`ColorAdjustments.postExposure`) | **+0.35** | 0 (unset) | Compensates ACES midtone drop; sun 2.2 assumes this |
| Contrast | **12** | 7 | Separates soil mounds from moss ridges |
| Saturation | **14** | 10 | Moss/water chroma; above 18 petals clip to magenta |
| Color filter | `#FFF6E8` (near-white warm) | white | Keeps key warmth after ACES; optional, A/B it |
| Bloom threshold | **0.85** | 0.68 | Only HDR water/pips/ring/centre cross (§1.4) |
| Bloom intensity | **0.55** | 0.65 | Lower but on HDR sources reads *stronger* than now |
| Bloom scatter | **0.65** | 0.55 | Wider halo = hero stream glow, not neon edge |
| Bloom clamp / tint | 65472 / white (defaults, set explicitly) | unset | Prevents pip blowout to white blobs |
| Vignette intensity / smoothness | **0.42 / 0.55**, colour black | 0.34 / 0.65 | Hero corners fall to near-black teal |
| Depth of field | **Off** (both RPAssets) | off | Hero has DOF, but PRD readability wins; fog+mist fake it |
| SMAA | High (keep, `GardenView.cs:118-119`) | High | + MSAA stays as RPAsset (`m_MSAA: 1`) |

Bloom budget guard: exactly 4 emitter families may exceed 1.0 luminance —
water core, pips, focus ring, flower centre. Petals, pollen, shaft, mist stay
below threshold (soft, no halo). If a screenshot shows blooming soil or dice
body, the fix is lowering that albedo, never raising threshold past 0.95.

### 2.6 Shadow / renderer assets (one-time, both RPAssets)

Current `m_ShadowAtlasResolution: 256` is why the only visible shadow in
build-final is a black rectangle under the dice. Fixed camera = cheap shadows:

- PC: atlas **2048**, cascades **2**, distance **32** (was 256 / 4 / 50).
- Mobile: atlas **1024**, cascades **1**, distance **32**, renderScale stays 0.8.
- Casters: rocks, dice, frame beams, soil domes ON; flat cell cubes,
  water discs, leaves, mist, sky OFF (dome casters give hero's mound shadows
  without 256 cube casters). Receivers: all cells + board + dice ON.

## 3. Scale and proportion targets

World unit = 1 cell. Board cells span x/z 0..15 (centres), centre (7.5, 0, 7.5).
`BoardBase` 18.6 wide centred (7.5,-0.62,7.5), top y=-0.245; frame beams at
±8.85, top y=0.295 (`GardenView.cs:178-198`). Keep all of these; dress them.

### 3.1 Camera (landscape primary, 1600x900 reference)

- Position **`(-4.6, 13.8, -8.0)`**, lookAt **`(7.5, 0.2, 7.9)`**, FOV **39°**
  (was `(7.5,14.8,-7.6)` / `(7.5,0.2,8.4)` / 41°). Result: distance 24.0,
  elevation 35°, azimuth 38° west of south — board reads as hero's diamond,
  south + west frame faces visible, grid rows diagonal but still countable.
- Framing acceptance at 1600x900 (measure on capture): west/east frame
  corners inset **≥6%** frame width each side; north corner **15-20%** below
  top edge (message card sits above it, no overlap); south corner **≥30%**
  above bottom edge (clears the 3-row control cluster); dice fully inside
  with **≥4%** right/bottom margin. If north corner crowds the card, move
  lookAt z 7.9 -> 7.4 (board rises); never raise FOV past 42 (grid warps).
- Portrait / phone (aspect < 1.0): FOV 52, pull to distance 30
  (position `(-7.6, 17.2, -11.9)`, same lookAt/elevation/azimuth). Switch on
  aspect at `Build` + on orientation change; no per-frame lerp.
- Near 0.5 / far 120 (set explicitly; skybox + far rocks at 55 must survive).

### 3.2 Rocks (3 real + decorative ring)

Real solid cells (map-fixed, 3): keep cell-anchored. Decorative ring:
presentation-only, on frame/apron **outside** x/z 0..15 or straddling the
frame line, never on soil cells, never implying new solids.

| Tier | Height above board top | Footprint | Count | Placement |
|---|---|---|---|---|
| Hero pillar | 2.8-3.4 | 0.9-1.1 | 3 decor + tallest real | North frame: (-0.6, 15.8), (7.2, 16.4), (15.9, 15.5) in (x,z) |
| Mid pillar | 1.8-2.4 | 0.85-1.0 | 4 decor + 2 real | West frame (-0.9, 4.5), (-0.5, 10.5); East (16.2, 6.0), (15.8, 11.5) |
| Low boulder | 0.7-1.1 | 1.0-1.3 (squat) | 4 decor | South frame corners + (16.0, 1.2) flanking dice pad |
| Pebble (existing) | 0.18-0.30 | 0.30 | as now (`jitter<0.10`) | Stone cells only, keep |

Implementation scale (mesh local diameter ~1.0): hero `localScale
(1.05, 3.1±0.3, 1.0)`, base y = frame top 0.295 minus sink 0.35; mid
`(0.95, 2.1±0.3, 0.9)`; boulder `(1.25, 0.95±0.15, 1.15)`. Real cells:
`(1.10, 2.9 + jitter*0.6, 1.05)` at y 0.55 (was `(1.15, 2.5+j*0.7, 1.1)` at
0.62, `GardenView.cs:275-276`). Yaw per rock `hash*360`, tilt ±3° max
(pillars stay vertical like hero; boulders ±8°). All cast+receive shadows.

### 3.3 Soil mounds and moss ridges

- Soil-cell base cube: scale `(0.98, h, 0.98)`, material switches to new
  `_mossMat` (Lit + `MossTexture`, smoothness 0.15). The cube *is* the ridge.
- Dome (sphere primitive, radius 0.5): scale **`(0.80, 0.52, 0.80)`**,
  centre y = **h - 0.08** => crown **h + 0.18**, rim gap 0.09 of moss visible
  on all sides (was `(0.88,0.30,0.88)` at h-0.20 = 0.10 proud, invisible,
  `GardenView.cs:300-301`). Dome casts shadow ON, receives ON.
- Stone-cell cubes: scale `(0.94, h, 0.94)` + yaw jitter ±2.0° + top-height
  jitter ±0.035 (hash by cellId). Wider 0.06 gaps read as worn joints with
  fog-dark board beneath, breaking build-final's sealed checkerboard.

### 3.4 Plants — 5 readable stages (heights include cell top offset +0.06)

`g = Growth/2500` (0..1). Stem radius values are cylinder diameter scale.

| Stage | Total height | Stem | Leaves | Head | Silhouette test @1600x900 |
|---|---|---|---|---|---|
| 0 Seed | 0.10 | none | none | Sphere dia 0.17, squash y0.75, tan `#D8B88A` | 6-8 px dot, warm on dark soil |
| 1 Sprout | 0.38 | h0.34, dia 0.055 | 2 leaves, len 0.40, pitch -48° | none | Clear Y shape, canopy dia 0.48 |
| 2 Leaves | 0.72 | h0.62, dia 0.060->0.040 | 5 leaves, len 0.52+0.18g | none | Canopy dia 0.85, fills 70% of cell |
| 3 Bud | 1.02 | h0.86 | as stage 2 | Bud 0.24w x 0.36h at stem top +0.12 | Pink tip reads at full-board zoom |
| 4 Flower | 1.28 | h1.02 | as stage 2, lower pitch -58° | Head dia 0.64; centre dia 0.15 | Pale halo flower = hero foreground accent |

Leaf length = `LeafMesh` 0.32 x scale; set leaf scale `1.25 + g*0.55` for
stages 2-4 (0.40-0.58 long), `1.15` sprout. Was `0.75+g*0.65`
(`GardenView.cs:609`). Stem height formula: stage 1 fixed 0.34; stages 2-4
`0.58 + g*0.44` (was `0.30+g*0.62` shared, `:592`). Sway amplitude scale with
height: rotation ±2.6° sprout -> ±1.6° flower (tall plants sway less, PRD calm).

### 3.5 Dice and focus ring

- Dice scale **1.10** (was 1.45), `RoundedCubeMesh` roundness **0.30**
  (was 0.42 default — too pillowy; dice.jpg edges are worn but planar).
- Rest position: moss pad at **`(12.35, padTop+0.56, 1.95)`** where padTop =
  `CellHeightOf` at nearest cell (12,2) ≈ 0.30-0.55 by map height; compute as
  `CellTop(2*16+12) + (0.85, 0.56, -0.05)`-style offset, do not hardcode y.
  Was `CellTop(15)+0.78` at corner cell (15,0) = clipped (`:346-347`).
- Rest rotation `Euler(-14, 32, 7)`: top (5), camera-front (3), right (1)
  faces visible, matching dice.jpg's 3-face read. Tumble returns here.
- Moss pad: `IrregularDiscMesh` scale 2.3, `_mossMat`, y = cell top +0.02,
  plus 2 boulders (§3.2) behind it. Dice shadow must land on pad, not UI void.
- Focus ring: radius **0.62**, width **0.045**, y = cell top +0.07, 48 segments
  (was 0.58 / 0.05 / +0.10 / 44, `:373-380`). Slightly larger than cell
  inscribed circle so it survives mound crowns; pulse scale 1.00±0.05 (was ±0.07).

### 3.6 Water, mist, far field

- Water disc: base mesh radius ~0.5; per-cell scale `S = Lerp(0.90, 1.95,
  sqrt(clamp01(SurfaceWater/240)))` in XZ, y = cell top +0.045 (above bed
  +0.02, below dome crown). 240 is a presentation knee (surface values are
  ints beside soil capacity 1000): verify against a 20-roll QA histogram and
  adjust so median wet cell sits at S≈1.35. Adjacent wet cells at S>1.4
  overlap and read as hero's continuous stream without drawing fake water.
- Mist: replace 6 horizontal `Plane` sheets (`:221-234`) with **8 vertical
  quads** facing the camera azimuth (yaw 38°), sizes 9x2.2 (near) and 14x3.2
  (far), centres y 1.0/1.6, ring radius 12.5/19 around (7.5,7.5). Horizontal
  sheets seen at 35° foreshorten to nothing; vertical quads hold alpha.
- Far rocks: ring radius **24-30** (was 27-34.5, fully fogged), heights 7-13,
  base y -2.0, count 9 keep. With fog 26-58 they land at 30-65% fog =
  silhouettes, not invisible, not solid.
- Pollen volume: box 18x3x18 centred (7.5,1.8,7.5), count 140, size 0.075
  (was 110 / 0.055, `:405-410`). Hero sparkles must survive 1600px width:
  0.055 at dist 24 ≈ 2px (lost); 0.075 + HDR tint ≈ 3-4px with halo.

## 4. Ten highest-impact gaps (ranked) — code-level fixes

Ranked by screenshot-distance closed per hour of work. Each fix uses only
procedural textures/meshes + URP Lit/Unlit already in the project.

### Gap 1 — Camera: edge-on slab vs hero diamond diorama; dice clipped, board under UI

Seen: build-final board rows run screen-horizontal, foreshortening crushes the
north half, dice is cut by the right edge over the UI void. Hero: diamond
board floating in mist, two frame faces visible, dice resting on-board.
Fix (`GardenView.cs:112-115`, `BuildDice :346-348`): apply §3.1 camera
(`(-4.6,13.8,-8.0)`, look `(7.5,0.2,7.9)`, FOV 39) and §3.5 dice (scale 1.10,
pad at cell (12,2) offset). Sun rotation moves to `Euler(48,-38,0)` in the
same edit or shadows will run screen-vertical and look wrong. Accept:
framing insets in §3.1 all pass on a fresh `-dgCapture` still; no 3D object
touches the bottom 26% control band except the south frame corner.

### Gap 2 — Exposure/fog/shadows: dim, flat, far board bleached, one blob shadow

Seen: whole board sits in a narrow dark-mid band; no warm/cool separation;
north cells paler than south (fog, not light); plants/rocks cast nothing.
Hero: bright warm rims upper-left, deep teal shadows, readable depth.
Fix: apply §2 as one bundle — sun 2.20/1.50/0.90 `#FFD9A0`, trilight ambient
(replaces `:107`), fog 26-58 (replaces `:105-106`), exposure +0.35 /
contrast 12 / saturation 14, shadow atlas PC 2048 / Mobile 1024 + distance
32 (RPAssets), dome+rock+dice casters on. Order matters: raising sun before
fixing fog just brightens the bleach. Accept: sunlit stone top ≈ `#9AA6A4`
±10% sampled on capture (clear weather); far-corner fog lightening < 15%
vs centre for equal-albedo cells; each hero pillar throws a distinct soft
shadow ≥1.5 cells long toward screen lower-right.

### Gap 3 — Stone apron: 198 identical slabs vs hero moss/stream terrace

Seen: ~77% of the board is one repeating grey-blue checker (shared cube UV
0..1, tint jitter only 0.88-1.10, `:488-493`). Hero stone is mossy terrace +
wood + stream banks; soil dominates the eye. The map cannot change (§0),
so break repetition in presentation:
(a) `StoneTexture` (`ProceduralArt.cs:66-75`): endpoints to §1.3, add per-tile
edge darkening (`edge = smoothstep` on uv distance to border, x0.72 at rim)
so each slab reads worn; moss threshold 0.62 -> 0.58 with strength x2.2.
(b) Per-cell texture offset via existing MPB: `_block.SetVector("_BaseMap_ST",
new Vector4(1,1,hashX,hashY))` with hash in 0..0.75 steps of 0.25, plus tint
jitter widened to 0.72..1.08 x `#5E6E6E`, plus §3.3 yaw/height jitter.
(c) Moss creep: stone cells adjacent to soil (SoilCapacity>0 neighbour) get
`_mossMat` instead of `_stoneMat` for the 1-cell ring around the 8x8 plots
(~28 cells) — hero's green frame around soil, still honest (static map fact).
(d) Raise tuft density: `jitter>0.72` -> `>0.62` and blades 3 -> 4 at scale
0.95 (`:309-325`); pebbles keep. Accept: no two adjacent apron cells share
the same visible crack pattern at capture resolution; green ring around
plots visible without zoom.

### Gap 4 — Rocks: 3 smooth eggs vs craggy moss-capped pillars

Seen: centre eggs with no facets, no moss, no shadows; hero pillars are the
vertical rhythm of the whole image. Root cause in `RockMesh`
(`ProceduralArt.cs:150-189`): displacement `r = 0.5*(0.78+n*0.5)` at frequency
2.4 is too smooth, and UV `v=y+0.5` wraps past 1.0 so texture moss lands
randomly. Fix, mesh-side only:
(a) Displacement: `n = Fbm(dir.x*3.2+v*7.3, (dir.y*1.6+dir.z*2.8), seed, 4)`;
`r = 0.5*(0.72 + n*0.62)`; quantise `n` to 6 steps (`floor(n*6)/6`) before
use so flat shading breaks into hero-like strata; taper
`r *= 1 - 0.22*smoothstep(0.1,0.6,dir.y)` (pillars narrow at top); vertical
ridge: add `+ 0.06*sin(atan2(dir.z,dir.x)*5 + variant)` fluting.
(b) UVs: `u = atan2(z,x)/2π + 0.5`, `v = clamp01((y+0.18)/0.82)` so v is true
height; then in `StoneTexture` blend moss by height:
`if (v>0.68) lerp toward moss mid by (v-0.68)*2.4 * noiseBreakup` (caps +
upper ledges green like hero, shafts stay grey).
(c) Variants 3 -> 5 (`RockMesh(0..4)`), scales/placement per §3.2 including
the 11-rock decorative frame ring (new loop in `BuildEnvironment`, reusing
`_rockMeshes`/`_rockMat`). Accept: silhouette of each pillar shows ≥3
direction changes; moss visible only on top third; ring reads in thumbnail.

### Gap 5 — Water: opaque cyan coins vs translucent glowing stream

Seen: flat `#33B5E8`-looking ellipses, hard edges, identical size regardless
of amount, floating over slabs. Hero/dice.jpg: deep-to-hot cyan gradient,
soft halo, continuous flow. Fix:
(a) Material (`GardenView.cs:91-95`): HDR `_BaseColor (0.08,1.55,1.85,0.82)`;
keep Unlit + `GlowSprite` but regenerate sprite alpha as `a^1.6` (softer rim,
`ProceduralArt.cs:95-100`) and lift centre RGB x1.35 toward `#9FF4FF`.
Pulse in `Update` (`:469-473`) modulates **alpha 0.74..0.88 only** — pulsing
RGB across the Bloom threshold makes the whole stream strobe.
(b) Size by amount (§3.6 scale formula in `Show :495-511`); set disc scale on
create and on every `Show` while wet (amount changes without wet/dry toggle).
(c) Channel bed: at `BuildCells`, stone cells in the lowest height quartile
(`CellHeight` via `GardenMapData.CellHeight`) get a permanent bed disc
(`IrregularDiscMesh`, Lit, `#1E3A3E`, smoothness 0.6, scale 1.5, y+0.02).
Dry = dark damp stone; wet = bed darkens to `#2E4E52` (MPB when any
4-neighbour wet) under the glow disc. No bed on soil cells (mound covers it).
(d) Splash burst colour to water core HDR x0.8 so bursts bloom once, briefly.
Accept: a 3-cell wet run reads as one stream with no visible coin rims;
single low-amount cell reads as small puddle, not full-cell disc; dry
channel still traces the hero stream path in dark stone.

### Gap 6 — Soil: flat tinted squares vs mounded plots with moss ridges

Seen: soil patch is flat checker tint (pale tan to mid-brown), domes
invisible, no ridges; wet cells barely darker than dry. Hero: each plot is a
dark damp mound in a green ridge frame. Fix: (a) 3-stop moisture ramp + wet
smoothness MPB (§1.2, `Show :480-487`); (b) soil base cube -> `_mossMat`,
dome scale/position per §3.3 (`BuildCells :285-307`); (c) `SoilTexture`
endpoints to §1.2 + grain thresholds: sparkle `>0.78 x1.35` -> `>0.80 x1.22`
(current sparkle reads as noise pixels), dark clod `<0.18 x0.7` -> `<0.22
x0.78`; add faint darker centre stain (radial x0.88) so mounds look watered
from above. (d) Dome UV: sphere UVs already wrap the texture; set soil
material `_BaseMap` tiling (2,1) via MPB `_BaseMap_ST (2,1,hash,0)` so grain
scale matches hero (~8 clods per mound). Accept: at capture size, dry
(≈`#C9A87C`) vs wet (≈`#3E2A1D` x texture) plots differ by >3x luminance;
every soil cell shows green rim + crown highlight + cast shadow sliver.

### Gap 7 — Dice: clipped prop with dot stickers vs inset glowing stone (dice.jpg)

Seen: grey-white cube half off-screen, pips are small blue bumps *proud* of
the surface, 3 faces blank during tumble, shadow is a black slab. Target:
white weathered stone, pips as recessed glowing pools, thin gold ring on moss.
Fix (`BuildDice :342-400`): (a) size/pos/rot/roundness per §3.5; dice albedo
tint `#EDE8DC`, smoothness 0.42, plus moss fleck: reuse `StoneTexture` but set
dice material `_BaseMap_ST (1.6,1.6,0.13,0.41)` so flecks land sparse/small.
(b) Pips as pools: per pip add socket first — flattened cylinder
`scale (0.21, 0.012, 0.21)` (local, pre-dice-scale), `#0A3A3C` matte, centre
at `normal*0.478`; pip sphere `scale 0.155`, centre at `normal*0.462` so only
a glowing dome meniscus shows inside the dark socket rim (no CSG needed).
Orient sockets with `FromToRotation(up, normal)`. Emission HDR §1.4 (x3.2).
(c) All 6 faces, opposites sum to 7: up 5 / down 2, forward 3 / back 4,
right 1 / left 6. Layouts (local face coords): 2:`(±0.24,±0.24)` diagonal;
4: four `(±0.24,±0.24)`; 6: `(±0.24, -0.26/0/+0.26)`. Pip offset 0.24 -> 0.235
so sockets (r 0.105) never clip the rounded edge. (d) Numerals in dice.jpg
("1"/"5" engravings) are **not** reproduced: PRD says face panel carries the
logical result and code-only numeral geometry would fight the pip read.
Accept: rest pose shows 5+3+1 pools glowing with sockets; mid-tumble freeze
shows no blank face; dice + shadow fully on moss pad.

### Gap 8 — Plants: sub-readable sprigs, pink seeds, tiny flower

Seen: 5 plants barely 10px tall; seed and bud both pink dots; the one flower
is a white asterisk. Hero: sprouts read per-plot, one large pale flower is
the warm accent. Fix (`BuildPlant :555-633`): sizes per §3.4 table exactly.
(a) Seed uses new `_seedMat` tan (was `_budMat`, `:589`). (b) Stem: 3 segments
keep, diameters `0.060/0.050/0.040`, bend offset x1.6 (gentle arc visible).
(c) Leaves: count sprout 2 / later 5 keep; pitch sprout -48°, stage2
`-52°+i*3°`, flower lower leaves -58° (canopy opens under head); leaf material
tint `(0.42,0.95,0.38)` (was `(0.65,1.05,0.55)` yellow-wash, `:80`).
(d) Flower: outer 6 petals scale `(0.30,0.055,0.20)` radius 0.21 tilt 18°,
inner 5 scale `(0.22,0.05,0.15)` radius 0.12 tilt 8°, centre sphere 0.15 with
own warm HDR material (§1.4); head y = stem top +0.14. Bud scale
`(0.24,0.36,0.24)`. (e) `LeafMesh` (`ProceduralArt.cs:240-257`) widen midrib
vertices x ±0.09 -> ±0.11 and tip y 0.03 -> 0.015 (droop) so leaves catch key
light as hero's glossy sprouts; leaf smoothness 0.25 -> 0.38.
Accept: stage of any centre-8x8 plant identifiable on the 1600x900 capture
without zoom (stronger: on a 800px-wide downscale); flower is the brightest
warm object on the board after the dice pips.

### Gap 9 — Atmosphere: flat void vs layered mist, shaft, pollen, far silhouettes

Seen: solid dark background, 3 pollen pixels, nothing else — yet sky dome,
far rocks, mist planes, shaft all exist in code and are all invisible.
Fix (`BuildEnvironment :162-252`, `BuildParticles :402-430`):
(a) Sky: delete the fogged inside-out sphere approach; use skybox —
`RenderSettings.skybox` = material with `Shader.Find("Skybox/Panoramic")`,
`_MainTex = SkyGradient` (zenith/horizon §1.1), camera `clearFlags = Skybox`.
Skybox ignores fog by design, so the gradient finally shows. Keep
`backgroundColor = fog` as fallback if shader missing. (If Panoramic sampling
stretches the 64px gradient, regenerate at 64x256 vertical strip — still
code-only.) (b) Mist per §3.6 (vertical quads, `#BFE8EE` a0.32 near /
`#6FA8B4` a0.22 far, `MistSprite` alpha curve `clamp01(n*1.5-0.18)` was
`n*1.3-0.25` x0.5 — double the peak alpha, `ProceduralArt.cs:107`); drift
speed 0.12 -> 0.20 u/s tangential, wrap around centre instead of sinusoidal
wander off-ring. (c) Far rocks per §3.6 radius 24-30. (d) Shaft: keep
cylinder but radius scale (5.2, 9.0, 5.2), alpha clear 0.07 / cloudy 0.04 /
drizzle off (was fixed 0.10 clear-only, `:245,549`); align cylinder axis to
sun direction (`Quaternion.FromToRotation(up, -sunDir)`) so shaft and
shadows agree; additive feel via Unlit transparent + renderQueue 3050 keep.
(e) Pollen per §3.6 + colour HDR (`#FFE9A8` x1.6, two-tone: 70% gold, 30%
pale cyan `#BFE8EE` x1.2 via start-color gradient min/max). Rain: alpha
0.5 -> 0.38, size 0.035 -> 0.045, speed 7.5 keep (drizzle must read in
build-final conditions without veiling the board). Accept: capture corners
show gradient (not flat), ≥3 mist layers cross the board edge, far silhouettes
visible north, pollen countable (>40 specks) but none blooming.

### Gap 10 — Board edge/frame: floating slab corner vs hero wooden diorama tray

Seen: board edge is a thin dark line; wood frame exists (`:178-198`) but is
near-black and mostly cropped by the old camera. Hero: thick weathered wood
tray, moss spilling over edges, stream exiting the south side. Fix:
(a) `WoodTexture` endpoints §1.3 + plank count 4 -> 5, edge x0.55 -> x0.45;
wood material smoothness 0.15 -> 0.28 (damp sheen). BoardBase tint via MPB
not needed — texture carries it once exposure (Gap 2) lands.
(b) Beams: scale `(0.55,0.55,18.2)` -> `(0.70,0.62,18.4)`, y 0.02 -> 0.06 (lip
above apron, catches key light + casts onto board edge). Corner posts: 4
cubes 0.85³ at (±8.85,±8.85) y 0.10, wood — hero tray joinery, 4 primitives.
(c) Moss spill: 10 `IrregularDiscMesh` discs `_mossMat` scale 1.2-2.0 draped
on beam tops at hash positions + 6 on BoardBase top margin outside beams.
(d) Float cue: one large soft dark disc (Unlit black a0.35, `GlowSprite`
inverted alpha) scale 26 at y -3.2 under board = fake contact shadow in the
mist void (hero board floats over darkness). No waterfall mesh: it would
imply discharge facts; drain cell 184 glow already signals outflow honestly.
Accept: all 4 tray edges + corner posts visible in frame; wood hue reads
warm brown against teal void; board clearly floats (shadow disc + mist gap).

## 5. UI polish (GardenUi.cs — layout numbers on the 1600x900 reference)

Root defect: `RoundedSprite()` already exists (`ProceduralArt.cs:128-133`)
but `MakeChip`/`MakePanel` (`GardenUi.cs:180-190,352-370`) never use it —
hence sharp flat rectangles. All fixes below are code-only UI.

### 5.1 Surfaces

- Every chip/panel `Image.sprite = ProceduralArt.RoundedSprite()`,
  `Image.type = Sliced` (9-slice border 16 already set in sprite). Panel
  radius reads ~14px at reference res; chips same sprite (radius scales with
  height, acceptable). Add `Shadow` component to each chip/panel:
  `effectColor (0,0,0,0.45)`, `effectDistance (0,-3)`, `useGraphicAlpha true`.
- Colours per §1.6. Header (`:74-75`), message card (`:76-77`) and status
  (`:78-79`) currently float bare over the 3D scene (blurry grey in captures):
  give each a backing sliced panel (panel glass a0.90) sized text + 20px
  padding each side, text as child with 16px inset. Status panel is the
  priority: in build-final it sits *on* the board and is unreadable.
- Modal panels (880x560 / 920x480 / 520x340 keep sizes): panel-deep colour,
  + full-screen dimmer behind (black a0.45, raycast target) shown with any
  panel so the garden recedes; close on dimmer click in addition to buttons.

### 5.2 Chip states (Button ColorBlock + selection)

Selection today swaps `Image.color` directly (`:214-234`), fighting the
Button transition. Set `transition = ColorTint` with block below, and drive
selection through a stored "selected" flag that picks the row:

| State | Fill | Text | Notes |
|---|---|---|---|
| Normal | `#1A3F4D` | `#F2F0E6` | multiplier 1.0 |
| Highlighted (hover/focus) | `#26586B` | `#F2F0E6` | fade 0.08 s |
| Pressed | `#26586B` x0.85 + scale 0.97 (0.06 s) | `#F2F0E6` | scale via `transform.localScale` on down/up |
| Selected | `#C9973F` | **`#241A06`** | face rolled / intent / site; dark text = ~7:1 (was white ~2:1) |
| Selected + hover | `#D9A84E` | `#241A06` | one step brighter, still distinct from roll |
| Disabled | `#3A4E57` a0.90 | `#A9C4C2` | roll button while rolling; `interactable=false` already wired |

Roll button is its own tier: fill `#D9A84E`, text `#241A06`, height 84
(was 78), width 320 (was 300), font 30 — the only element brighter than a
selected chip, so "primary action vs current selection" never confuses.
Face chips additionally get a 6px pip-dot row? No: keep text-only (PRD: six
faces identifiable without panels — text already does this).

### 5.3 Typography (dynamic OS font stays; sizes at 1600x900 reference)

| Element | Size (was) | Weight/colour | Spacing |
|---|---|---|---|
| Header (sky/site/count) | **22** (24) | `#F2F0E6`, labels in `#A9C4C2` via rich text `<color>` | line 1.0, panel pad 20x12 |
| Message card (face + sentence) | **30** (27) | `#F2F0E6`; card tag 【…】 in `#FFC766` rich text | wrap width 920, max 2 lines, line 1.25 |
| Status line | **21** (20) | `#F2F0E6` on panel glass | single line, ellipsis never (wrap to 2) |
| Chip labels (face/intent/site/menu) | **20** (19-20) | per §5.2 | autoshrink min 16 if overflow |
| Section labels （今回の6面 etc.) | **16** (17) | `#A9C4C2` | 8px above its chip row |
| Roll button | **30** (27) | `#241A06` | — |
| History text / graph caption | **19 / 18** (keep) | `#F2F0E6` / `#A9C4C2` caption | history line 1.35 |

Legibility: add `Outline` (effectColor `(0,0,0,0.55)`, distance `(1,-1)`) to
header/message/status text only (chips have fills, outline would muddy them).
Dynamic font base size 24 (`:49-50`) -> request 32 so 30px text downscales
instead of upscaling (current blur source). `CanvasScaler` keep
ScaleWithScreenSize 1600x900, match **0.5** (set explicitly; width/height
balanced for phones + Steam).

### 5.4 Layout / spacing (8px grid; safe margin 24px all sides)

- Top-left: header panel at (24, top-24), auto width ~640x56 (was bare text
  760x64). Top-centre: message panel 960x104, top offset 24 (was y-72 bare).
  Header and message must not overlap below 1280px width: at aspect <1.6,
  header moves under message (stack, gap 8) — one `if` at Build on aspect.
- Right menu: chips 168x52 (was 150x48), pitch 62 (gap 10, was 58), right
  margin 24, first chip top offset 76 (clears message panel corner).
- Bottom cluster: rows bottom-up — site row y20 h52, label 8 above; intent
  row next +62; face row next +62; status panel centred above face row,
  width 1040x48, gap 10 (was y292 floating on board). Chip widths: face
  144 pitch 156 (gap 12, was 140/148), intent 116 pitch 128, site 144 pitch
  156. Left margin 24 aligns all rows (currently aligned; keep).
- Roll button: centred x, bottom 24, 320x84. Its top edge must clear status
  panel bottom by ≥12px at 900p; if the stack exceeds 300px total, shrink
  chip heights to 48 before shrinking gaps below 8.
- Touch targets: all chips ≥48px tall at reference (≥44pt class on phones
  after scaler); face chips are the wish input — never below 52.

## 6. Implementation order, budget, verification

### 6.1 Order (each step independently screenshot-able; do not batch 1+2 blind)

1. **Gap 2 lighting bundle** (§2 numbers only, no geometry) — re-capture:
   proves exposure/fog/shadow atlas before art edits chase the old grade.
2. **Gap 1 camera + dice placement** (§3.1/3.5 positions; pips still old).
3. **Gap 6 soil + Gap 3 apron** (textures §1.2/1.3, `_mossMat`, MPB offsets).
4. **Gap 4 rocks** (`RockMesh` + ring) then **Gap 10 frame**.
5. **Gap 5 water** (HDR + scale-by-amount + beds), **Gap 7 dice pools**.
6. **Gap 8 plants**, **Gap 9 atmosphere** (skybox last among 3D: it changes
   every colour judgement, so grade-check after it once more against §1).
7. **UI §5** (sprite wiring, states, backings, type) — judge in Overlay play,
   not only in ScreenSpaceCamera captures (§0).

### 6.2 Perf guardrails (phone + Steam, fixed camera)

Added draw calls: rock ring 11 + beds ~40 + moss spills 16 + sockets ≤21 +
mist 8 ≈ +95 small calls — fine. No new textures over 256² except optional
sky strip 64x256. Rock variants 5 (shared). Shadow casters added: domes
(~55) + ring (11) only. Bloom already on; HDR values add no cost. If Mobile
frame time regresses, first cut is bed discs on every-other low cell, never
shadow atlas below 1024 (silhouette quality is the hero gap).

### 6.3 Verification (no external API, deterministic)

For each step: `pnpm typecheck`-equivalent Unity compile (no console errors),
then headless captures via existing QA hook (`GardenBootstrap.cs:83-92`):
`-dgAutoRolls 8 -dgCapture <path>` (matches build-final conditions) plus one
clear-weather capture. Compare side-by-side with `refs/hero.jpg` at 400px
thumbnail (silhouette/palette) and 1600px (readability). Final checklist =
PRD acceptance (§ Acceptance checks) + per-gap "Accept:" lines above +
§3.1 insets + §5 contrast (selected-chip text dark on gold) + no glowing disc
on any `SurfaceWater==0` cell (pause on a dry state and confirm beds only).
Save finals as `visual-plan/refs/build-artplan.png` (drizzle, 8 rolls) and
`build-artplan-clear.png`; keep `build-final.png` untouched as the before.

### 6.4 Explicit non-goals

No garden-map/domain edits; no fake stream/waterfall facts; no dice numerals;
no DOF; no new shaders (skybox uses built-in Panoramic); no font import
(dynamic OS font + larger request size covers JP on phone + Steam); no camera
motion/shake (PRD fixed camera). Anything beyond this list needs a PRD change
first, not an art-side invention.



