# Visual PRD — Destiny Garden (G3)

Refs: `refs/hero.jpg` (garden board), `refs/dice.jpg` (dice close-up). Original concept art generated for this project.

## Target look
- A floating square garden board (16x16) in dark teal mist; nothing beyond the board except fog and drifting pollen sparkles.
- Palette: deep teal background (#0d2b33 range), moss green, wet dark soil browns, glowing cyan water, one warm amber key light from upper left, pale pink flower accent.
- Soil plots read as moist mounds separated by mossy ridges; dry soil is pale tan, wet soil is dark and saturated. Surface water is a translucent cyan layer.
- Standing grey rocks with moss at the solid cells; plants grow in 5 readable stages (seed dot, sprout, leaves, bud, glowing pale flower).
- The dice is a white stone cube with glowing teal pips; a thin golden ring marks the focus cell after a roll.

## Camera and light
- Fixed three-quarter camera: position above and south of the board, looking at its centre; board occupies most of the frame in landscape.
- Warm directional key light (clear 1.25 / cloudy 0.85 / drizzle 0.6 intensity), cool ambient, linear fog matched to the background colour.
- URP with Bloom so water, pips, focus ring and flowers glow softly. No screen shake.

## Motion and effects (all tied to saved facts)
- Dice tumbles briefly, then settles; the face panel and sentence carry the logical result.
- Water input: cyan splash particles at the actual injection cells. Light: warm glow at the footprint. Wind: eastward streaks. Seed: small sparkle at placed cells.
- Focus: golden ring pulses at the selected message's focus cell, then fades to the quiet garden.
- Weather: drizzle shows gentle rain particles; idle time never advances the model.

## Acceptance checks
- Screenshot reads as: dark misty surround, readable soil moisture differences, visible rocks/plants/water, glowing accents restrained to water/pips/ring/flower.
- UI text is legible over the scene; the six faces, weather, site and sentence are identifiable without opening panels.
