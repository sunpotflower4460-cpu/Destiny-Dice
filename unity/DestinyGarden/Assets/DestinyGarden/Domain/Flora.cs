using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>One garden species and the conditions it needs (ecology.v3, ECOLOGY.md §7).</summary>
    public sealed class SpeciesTraits
    {
        public string Id, LabelJa;
        public int MaxHardness;               // grows only where soil hardness <= this (0..1000)
        public int DrainMin, DrainMax;        // drainage window (0..1000)
        public int MoistMin, MoistMax;        // soil moisture window, per mille of capacity
        public int Light;                     // 0 needs sun, 1 either, 2 needs shade
        public int Butterfly, Bee;            // affinity: -1 avoided, 0 neutral, 1 liked, 2 loved
        public int Softens;                   // lowers hardness of its own and neighbouring cells
        public int Drains;                    // raises (or, if negative, lowers) drainage nearby
        public bool Berries, WindSeed;
    }

    /// <summary>
    /// Flora (ecology.v3): species, the soil conditions they need and change, and how a new
    /// seed becomes a species. Soil conditions are derived from the saved state (no extra state):
    /// the ground's own height and rocks, moisture, and the plants that soften the soil or change
    /// its drainage. Pioneers make room for others, so the garden finds its own balance.
    /// </summary>
    public static class Flora
    {
        public const string RulesVersion = "flora.v2";
        public const int LeaningWindow = 8;   // flora.v2: the blessings of the last eight rolls lean germination
        public const string Pioneer = "dandelion";
        public const int PioneerStepsBackAt = 4;  // flora.v2: where this many species fit, the pioneer's base weight halves
        public const int CrowdingReach = 2;       // flora.v2: same species within this distance crowd a seed (x3/(3+n))
        public const int ThriveGrowth = 4;

        private static SpeciesTraits T(string id, string ja, int hard, int dMin, int dMax, int mMin, int mMax, int light,
            int butterfly, int bee, int softens = 0, int drains = 0, bool berries = false, bool wind = false) =>
            new SpeciesTraits
            {
                Id = id, LabelJa = ja, MaxHardness = hard, DrainMin = dMin, DrainMax = dMax, MoistMin = mMin, MoistMax = mMax,
                Light = light, Butterfly = butterfly, Bee = bee, Softens = softens, Drains = drains, Berries = berries, WindSeed = wind
            };

        /// <summary>Catalogue order is fixed (it is part of the rules: ties and draws follow it).</summary>
        public static readonly SpeciesTraits[] All =
        {
            T("cosmos", "コスモス", 520, 350, 1000, 0, 800, 0, 2, 1),
            T("daisy", "デイジー", 800, 0, 1000, 150, 900, 1, 1, 1),
            T("bellflower", "ホタルブクロ", 650, 0, 1000, 300, 1000, 2, 0, 1),   // flora.v2: 500 -> 650 (rock shade is harder)
            T("poppy", "ポピー", 900, 450, 1000, 0, 700, 0, 0, 2),
            T("sunflower", "ヒマワリ", 480, 0, 1000, 300, 1000, 0, 1, 2, drains: 150),
            T("foxglove", "ジギタリス", 650, 0, 1000, 300, 1000, 2, -1, 2),     // flora.v2: 480 -> 650
            T("anemone", "アネモネ", 520, 0, 1000, 250, 900, 1, 0, 1),
            T("iris", "アイリス", 650, 0, 520, 500, 1000, 1, 1, 1, drains: -150),
            T("forgetmenot", "ワスレナグサ", 480, 0, 600, 450, 1000, 1, 1, 0),
            T("buttercup", "キンポウゲ", 720, 0, 1000, 350, 1000, 1, 1, 1),
            T("lavender", "ラベンダー", 820, 550, 1000, 0, 650, 0, 2, 2, drains: 100),
            T("wildrose", "野ばら", 720, 0, 1000, 200, 900, 1, 1, 1, berries: true),
            T("dandelion", "タンポポ", 1000, 0, 1000, 0, 1000, 1, 0, 1, softens: 150, wind: true),
            T("strawberry", "野イチゴ", 520, 0, 1000, 300, 1000, 1, 0, 1, softens: 100, berries: true),
            T("lily", "ユリ", 380, 400, 1000, 300, 900, 1, 2, 0),
        };

        public static SpeciesTraits Get(string id)
        {
            if (id == null) return null;
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return -1;
        }

        private static int X(int c) => c % 16;
        private static int Y(int c) => c / 16;
        private static int Dist(int a, int b) => Math.Abs(X(a) - X(b)) + Math.Abs(Y(a) - Y(b));

        public static bool IsRockShade(int c)
        {
            if (GardenMapData.LightExposureQ[c] < 1000) return true;
            int x = X(c), y = Y(c);
            return (x > 0 && GardenMapData.Solid[c - 1] == 1) || (x < 15 && GardenMapData.Solid[c + 1] == 1) ||
                   (y > 0 && GardenMapData.Solid[c - 16] == 1) || (y < 15 && GardenMapData.Solid[c + 16] == 1);
        }

        /// <summary>Soil moisture in per mille of capacity (0 for cells without soil).</summary>
        public static int Moisture(WorldState s, int c) =>
            GardenMapData.SoilCapacity[c] > 0 ? (int)Math.Min(1000L, 1000L * s.SoilWater[c] / GardenMapData.SoilCapacity[c]) : 0;

        /// <summary>The ground's own hardness before any plant: higher ground and rock edges are harder.</summary>
        public static int BaseHardness(int c) =>
            350 + GardenMapData.CellHeight[c] * 400 / 1140 + (IsRockShade(c) ? 150 : 0);

        private static int Weight(PlantState p) => p.Stage >= PlantStage.Leaf ? 2 : p.Stage >= PlantStage.Sprout ? 1 : 0;

        /// <summary>Hardness 0..1000: base, softened by wet soil and by softening plants within one cell.</summary>
        public static int Hardness(WorldState s, int c)
        {
            int h = BaseHardness(c);
            if (Moisture(s, c) >= 600) h -= 80;
            foreach (var p in s.Plants)
            {
                var t = Get(p.Species);
                if (t == null || t.Softens == 0 || Dist(p.CellId, c) > 1) continue;
                h -= t.Softens * Weight(p) / 2;
            }
            return Math.Max(0, Math.Min(1000, h));
        }

        /// <summary>Drainage 0..1000: higher ground drains better; some plants improve or hold water.</summary>
        public static int Drainage(WorldState s, int c)
        {
            int d = 300 + GardenMapData.CellHeight[c] * 500 / 1140;
            foreach (var p in s.Plants)
            {
                var t = Get(p.Species);
                if (t == null || t.Drains == 0 || Dist(p.CellId, c) > 1) continue;
                d += t.Drains * Weight(p) / 2;
            }
            return Math.Max(0, Math.Min(1000, d));
        }

        public static bool Fits(SpeciesTraits t, WorldState s, int c)
        {
            if (Hardness(s, c) > t.MaxHardness) return false;
            int d = Drainage(s, c);
            if (d < t.DrainMin || d > t.DrainMax) return false;
            int m = Moisture(s, c);
            if (m < t.MoistMin || m > t.MoistMax) return false;
            bool shade = IsRockShade(c);
            if (t.Light == 0 && shade) return false;
            if (t.Light == 2 && !shade) return false;
            return true;
        }

        public static List<SpeciesTraits> FittingAt(WorldState s, int c)
        {
            var list = new List<SpeciesTraits>();
            foreach (var t in All) if (Fits(t, s, c)) list.Add(t);
            return list;
        }

        /// <summary>
        /// A species for a plant that has none yet. Fitting species are weighted by how the seed came:
        /// the parent flower's species is inherited when it fits; birds bring berry plants; the wind
        /// brings wind-sown seeds; the gardener's die can bring any fitting species. Draws come only
        /// from the given picker (saved ecology bytes); initial plants use their id (no chance).
        /// When nothing fits, the pioneer dandelion takes the place.
        /// </summary>
        /// <summary>
        /// flora.v2: how strongly the recent blessings lean a seed toward a species (0..8 per matching blessing kind).
        /// Water: moisture lovers (from 300‰). Light: sun lovers. Wind: wind-sown seeds and flowers pollinators love
        /// (bee + butterfly at least 3). Seed: plants that spread themselves (berries, wind-sown seeds).
        /// </summary>
        public const int AnsweredWishLeaning = 10; // flora.v2: a wish that came true settles into this roll's seeds

        /// <summary>Whether a species is one the blessing leans toward (the same matches as Leaning).</summary>
        public static bool Suits(SpeciesTraits t, ElementId e)
        {
            if (t == null) return false;
            switch (e)
            {
                case ElementId.Water: return t.MoistMin >= 300;                 // moisture lovers
                case ElementId.Light: return t.Light == 0;                      // sun lovers
                case ElementId.Wind: return t.WindSeed || Math.Max(0, t.Bee) + Math.Max(0, t.Butterfly) >= 3; // wind-sown, pollinator flowers
                case ElementId.Seed: return t.Berries || t.WindSeed;            // plants that spread themselves
            }
            return false;
        }

        public static int Leaning(SpeciesTraits t, IList<string> recentRecipeIds)
        {
            if (t == null || recentRecipeIds == null) return 0;
            int water = 0, light = 0, wind = 0, seed = 0;
            for (int i = Math.Max(0, recentRecipeIds.Count - LeaningWindow); i < recentRecipeIds.Count; i++)
                switch (RecipeCatalog.ElementOf(recentRecipeIds[i]))
                {
                    case ElementId.Water: water++; break;
                    case ElementId.Light: light++; break;
                    case ElementId.Wind: wind++; break;
                    case ElementId.Seed: seed++; break;
                }
            int w = 0;
            if (Suits(t, ElementId.Water)) w += water;
            if (Suits(t, ElementId.Light)) w += light;
            if (Suits(t, ElementId.Wind)) w += wind;
            if (Suits(t, ElementId.Seed)) w += seed;
            return w;
        }

        public static string ChooseSpecies(WorldState s, PlantState plant, string carrier, string parentSpecies, Func<int, int> pick,
            IList<string> recentRecipeIds = null, string answeredRecipeId = null, int anchorCellId = -1)
        {
            // prayer-star.v2: near the blessing's site, the recent blessings lean a seed twice as strongly.
            int leaningScale = anchorCellId >= 0 && Dist(plant.CellId, anchorCellId) <= PrayerStar.SiteReach ? 2 : 1;
            var fit = FittingAt(s, plant.CellId);
            if (fit.Count == 0) return "dandelion";
            var weights = new int[fit.Count];
            for (int i = 0; i < fit.Count; i++)
            {
                var t = fit[i];
                // flora.v2: the pioneer takes the ground where little else fits, and steps back where many do.
                int w = t.Id == Pioneer && fit.Count >= PioneerStepsBackAt ? 1 : 2;
                if (parentSpecies != null && t.Id == parentSpecies) w += 10;
                if (carrier == "bird" && t.Berries) w += 6;
                if (carrier == "fluff" && t.WindSeed) w += 8;
                if (carrier == "butterfly") w += Math.Max(0, t.Butterfly) * 2;
                if (carrier == "bee") w += Math.Max(0, t.Bee) * 2;
                w += Leaning(t, recentRecipeIds) * leaningScale;
                // An answered wish: this roll's seeds lean strongly toward what its blessing favours.
                if (answeredRecipeId != null && Suits(t, RecipeCatalog.ElementOf(answeredRecipeId))) w += AnsweredWishLeaning;
                // flora.v2: crowding. The more of the same species close by, the less a seed becomes it.
                int same = 0;
                foreach (var other in s.Plants)
                    if (other != plant && other.Species == t.Id && Dist(other.CellId, plant.CellId) <= CrowdingReach) same++;
                w = Math.Max(1, w * 3 / (3 + same));
                weights[i] = w;
            }
            if (plant.OriginKind == "initialMap" || pick == null)
            {
                uint h = 2166136261u;
                foreach (char ch in plant.PlantId ?? "") { h ^= ch; h *= 16777619u; }
                return fit[(int)(h % (uint)fit.Count)].Id;
            }
            int total = 0;
            foreach (int w in weights) total += w;
            int v = pick(total);
            if (v < 0)
            {
                int best = 0;
                for (int i = 1; i < weights.Length; i++) if (weights[i] > weights[best]) best = i;
                return fit[best].Id;
            }
            for (int i = 0, acc = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (v < acc) return fit[i].Id;
            }
            return fit[fit.Count - 1].Id;
        }

        /// <summary>Distinct species currently in bloom.</summary>
        public static int BloomingSpecies(WorldState s)
        {
            var set = new HashSet<string>();
            foreach (var p in s.Plants) if (p.Stage == PlantStage.Flower && p.Species != null) set.Add(p.Species);
            return set.Count;
        }
    }
}
