using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// How the faces that come up shape the garden: gardens grown under four leanings of blessing
    /// (water, light, wind or seed coming up half of the time) through the same simulation, ecology and
    /// prayer star as play. Reports what each garden becomes; asserts only the rules' invariants.
    /// </summary>
    public class GardenPathsTests
    {
        private static readonly Dictionary<string, string[]> Leanings = new Dictionary<string, string[]>
        {
            ["water"] = new[] { "water_rain", "seed_single", "water_drop", "light_soft", "water_rain", "wind_breeze" },
            ["light"] = new[] { "light_beam", "seed_single", "light_soft", "water_drop", "light_beam", "wind_breeze" },
            ["wind"] = new[] { "wind_passage", "seed_single", "wind_breeze", "water_drop", "wind_passage", "light_soft" },
            ["seed"] = new[] { "seed_scatter", "water_drop", "seed_single", "light_soft", "seed_scatter", "wind_breeze" },
        };

        private sealed class Grown
        {
            public WorldState State;
            public int StarGift, Rolls;
            public Dictionary<int, int> Stars = new Dictionary<int, int>();
        }

        private static Grown Grow(string[] cycle, string seed, int rolls)
        {
            var ent = new FixedEntropy(seed);
            var g = new Grown { State = WorldState.CreateInitial() };
            var recent = new List<string>();
            int prevFace = 0, id = 0;
            for (int i = 0; i < rolls; i++)
            {
                // The order of blessings is shuffled by the seed; the leaning only sets how often each comes up.
                byte[] b = ent.GetBytes(4);
                string recipe = cycle[b[0] % cycle.Length];
                int face = 1 + b[1] % 6;
                var weather = Weathers.All[b[2] % 3];
                int anchor = Sites.All[b[3] % 3].CenterCellId;
                var resolved = RecipeResolver.Resolve(recipe, anchor, g.State);
                if (!resolved.Eligible) { recipe = "water_drop"; resolved = RecipeResolver.Resolve(recipe, anchor, g.State); }
                var sim = Simulation.Resolve(g.State, resolved, weather, n => "p" + (id++), "r" + i);
                recent.Add(recipe);
                var eco = Ecology.Apply(sim.PostState, new EcologyContext
                {
                    RoundId = "r" + i, CurrentRecipeId = recipe, RecentRecipeIds = recent, PrayerAnswered = false,
                    Sim = sim, Weather = weather, FaceId = face, PreviousFaceId = prevFace
                }, ent.GetBytes(64), n => "e" + (id++));
                Assert.AreEqual(sim.PostState.TotalWater(), eco.PostState.TotalWater());
                g.StarGift += eco.Effects.Where(e => e.Kind == PrayerStar.GiftKind).Sum(e => e.Amount);
                int p = eco.PrayerStar.Points;
                g.Stars[p] = g.Stars.TryGetValue(p, out int c) ? c + 1 : 1;
                g.State = eco.PostState;
                g.Rolls++;
                prevFace = face;
            }
            return g;
        }

        [Test]
        public void Measure_GardensFollowTheirBlessings()
        {
            const int gardens = 8, rolls = 48;
            var lines = new List<string>();
            var tops = new HashSet<string>();
            foreach (var kv in Leanings)
            {
                int plants = 0, flowers = 0, blooming = 0, sunLovers = 0, shadeLovers = 0, wetLovers = 0, named = 0;
                long moisture = 0, gift = 0;
                var species = new Dictionary<string, int>();
                for (int gi = 0; gi < gardens; gi++)
                {
                    var g = Grow(kv.Value, "path-" + gi, rolls);
                    var s = g.State;
                    plants += s.Plants.Count;
                    flowers += s.Plants.Count(p => p.Stage == PlantStage.Flower);
                    blooming += Flora.BloomingSpecies(s);
                    gift += g.StarGift;
                    for (int c = 0; c < GardenMapData.CellCount; c++) moisture += Flora.Moisture(s, c);
                    foreach (var p in s.Plants)
                    {
                        var t = Flora.Get(p.Species);
                        if (t == null) continue;
                        named++;
                        species[t.LabelJa] = species.TryGetValue(t.LabelJa, out int n) ? n + 1 : 1;
                        if (t.Light == 0) sunLovers++;
                        if (t.Light == 2) shadeLovers++;
                        if (t.MoistMin >= 400) wetLovers++;
                    }
                }
                var top = species.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(4).ToList();
                tops.Add(top[0].Key);
                lines.Add($"{kv.Key}: plants {plants / (double)gardens:0.0}, flowers {flowers / (double)gardens:0.0}, " +
                          $"blooming species {blooming / (double)gardens:0.0}, sun-lovers {100.0 * sunLovers / Math.Max(1, named):0}%, " +
                          $"shade-lovers {100.0 * shadeLovers / Math.Max(1, named):0}%, wet-lovers {100.0 * wetLovers / Math.Max(1, named):0}%, " +
                          $"soil moisture {moisture / (double)gardens / GardenMapData.CellCount:0}‰, star gift/roll {gift / (double)(gardens * rolls):0.0}, " +
                          "top " + string.Join(" ", top.Select(x => $"{x.Key}{100.0 * x.Value / Math.Max(1, named):0}%")));
            }
            string report = string.Join(" | ", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-PATHS " + report);
            Assert.Greater(lines.Count, 0);
        }
    }
}
