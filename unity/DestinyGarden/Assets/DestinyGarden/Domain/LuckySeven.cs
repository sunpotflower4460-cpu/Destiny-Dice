using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// Lucky Seven (lucky-seven.v1). After a roll whose wish came true, the next prepared
    /// round may add a published 7th face before the intent is committed. The 7th face is
    /// the eligible recipe with the highest local need at the resolved site (ties keep
    /// RecipeCatalog order), applied at double strength. A 7-face round is rolled as a fair
    /// D7 from the saved outcome bytes, so every face is exactly 1/7.
    /// </summary>
    public static class LuckySeven
    {
        public const string RulesVersion = "lucky-seven.v1";
        public const int FaceId = 7;
        public const int Amplification = 2;
        public const string LabelJa = "幸運の七";

        /// <summary>Most-needed eligible recipe at the site. Null only if nothing is eligible.</summary>
        public static string PickRecipe(WorldState state, WeatherDefinition weather, string resolvedSiteId)
        {
            int anchor = Sites.ById(resolvedSiteId).CenterCellId;
            string best = null;
            int bestNeed = int.MinValue;
            foreach (string recipeId in RecipeCatalog.Order)
            {
                var resolved = RecipeResolver.Resolve(recipeId, anchor, state);
                if (!resolved.Eligible) continue;
                int need = FaceGenerator.ComputeLocalNeedQ(resolved, state, weather);
                if (need > bestNeed) { bestNeed = need; best = recipeId; }
            }
            return best;
        }

        /// <summary>
        /// Same footprint as the normal recipe at double strength: water amounts x2,
        /// wind/light strength x2 (the simulation still caps fields at 1000), seeds x2
        /// as far as the seed band has empty plantable cells (never fewer than normal).
        /// </summary>
        public static ResolvedRecipe ResolveAmplified(string recipeId, int anchorCellId, WorldState state)
        {
            var r = RecipeResolver.Resolve(recipeId, anchorCellId, state);
            if (!r.Eligible) return r;
            switch (r.Element)
            {
                case ElementId.Water:
                    {
                        var doubled = new Dictionary<int, List<KeyValuePair<int, int>>>();
                        foreach (var kv in r.WaterSchedule)
                        {
                            var list = new List<KeyValuePair<int, int>>();
                            foreach (var cell in kv.Value)
                                list.Add(new KeyValuePair<int, int>(cell.Key, cell.Value * Amplification));
                            doubled[kv.Key] = list;
                        }
                        r.WaterSchedule = doubled;
                        break;
                    }
                case ElementId.Wind:
                case ElementId.Light:
                    r.StrengthQ *= Amplification;
                    break;
                case ElementId.Seed:
                    {
                        int minD = recipeId == "seed_single" ? 0 : 2;
                        int maxD = recipeId == "seed_single" ? 1 : 3;
                        int want = r.SeedCells.Count * Amplification;
                        var cells = new List<int>();
                        foreach (int c in RecipeResolver.SeedBandCells(anchorCellId, minD, maxD))
                        {
                            if (state.PlantAt(c) != null) continue;
                            cells.Add(c);
                            if (cells.Count == want) break;
                        }
                        r.SeedCells = cells;
                        break;
                    }
            }
            return r;
        }

        /// <summary>Fair D7 from saved bytes (big-endian uint32 rejection sampling). -1 when exhausted.</summary>
        public static int RollD7(byte[] outcomeBytes)
        {
            int v = new EntropyBuffer(outcomeBytes).UniformBelow(7);
            return v < 0 ? -1 : v + 1;
        }

        /// <summary>Same rule as the history view: face wish = same face, family wish = same element.</summary>
        public static bool IntentMatched(string intentKind, string intentValue, int faceId, string recipeId)
        {
            if (intentValue == null || recipeId == null) return false;
            if (intentKind == "face") return faceId.ToString() == intentValue;
            if (intentKind == "family")
                return RecipeCatalog.ElementOf(recipeId).ToString().ToLowerInvariant() == intentValue;
            return false;
        }

        /// <summary>rainbow.v1: the Lucky Seven was rolled and a flower bloomed in the natural simulation.</summary>
        public static bool IsRainbow(bool luckySevenRolled, SimulationResult sim)
        {
            if (!luckySevenRolled || sim == null) return false;
            foreach (var e in sim.Events) if (e.Kind == "Bloomed") return true;
            return false;
        }

        /// <summary>
        /// rainbow.v2: the Lucky Seven was rolled and the garden answered in the same roll: a bud
        /// formed or a flower bloomed (by the weather and blessing, or by its small lives), or the
        /// island reached the seven- or eight-pointed star of harmony.
        /// </summary>
        public static bool IsRainbow(bool luckySevenRolled, SimulationResult sim, EcologyResult eco)
        {
            if (!luckySevenRolled) return false;
            if (IsRainbow(true, sim)) return true;
            if (eco == null) return false;
            foreach (var e in eco.Events) if (e.Kind == "Bloomed" || e.Kind == "BudFormed") return true;
            if (sim != null) foreach (var e in sim.Events) if (e.Kind == "BudFormed") return true;
            return eco.Harmony != null && eco.Harmony.Star >= 7;
        }
    }

    /// <summary>
    /// Nature balance (nature-balance.v1): butterflies visit while water, wind, light and
    /// seed have all been rolled within the last seven resolved rounds.
    /// </summary>
    public static class NatureBalance
    {
        public const int Window = 7;

        public static bool AllElementsWithinWindow(IList<string> rolledRecipeIdsOldestFirst)
        {
            if (rolledRecipeIdsOldestFirst == null) return false;
            var seen = new HashSet<ElementId>();
            int start = Math.Max(0, rolledRecipeIdsOldestFirst.Count - Window);
            for (int i = start; i < rolledRecipeIdsOldestFirst.Count; i++)
                seen.Add(RecipeCatalog.ElementOf(rolledRecipeIdsOldestFirst[i]));
            return seen.Count == 4;
        }
    }
}

namespace DestinyGarden.Domain
{
    /// <summary>
    /// Answered prayer (prayer-spirits.v1): when a wish comes true, spirits are shown carrying
    /// this roll's blessing to the plants it actually reached. Presentation of saved facts only:
    /// the blessing and its amounts are already resolved; spirits add nothing.
    /// </summary>
    public static class PrayerSpirits
    {
        public const int MaxSpirits = 7;

        /// <summary>Plant cells reached by the dice blessing (water, light, wind or new seeds), cellId order.</summary>
        public static System.Collections.Generic.List<int> ReachedPlantCells(SimulationResult sim)
        {
            var result = new System.Collections.Generic.List<int>();
            if (sim == null || sim.PostState == null) return result;
            var ev = sim.Evidence;
            var seeded = new System.Collections.Generic.HashSet<int>();
            foreach (var e in sim.Events) if (e.Kind == "SeedPlaced") seeded.Add(e.CellId);
            // Plants the blessing touched directly come first, then plants that grew this roll.
            var touched = new System.Collections.Generic.SortedSet<int>();
            var grew = new System.Collections.Generic.SortedSet<int>();
            foreach (var plant in sim.PostState.Plants)
            {
                int c = plant.CellId;
                if (ev.DiceWaterCells.Contains(c) || ev.LightReceivedCells.Contains(c) ||
                    ev.WindReceivedCells.Contains(c) || seeded.Contains(c))
                    touched.Add(c);
                else if (ev.GrowthBeforeByPlant.TryGetValue(plant.PlantId, out int before) && plant.Growth > before)
                    grew.Add(c);
            }
            foreach (var set in new[] { touched, grew })
                foreach (int c in set)
                {
                    if (result.Count == MaxSpirits) return result;
                    result.Add(c);
                }
            return result;
        }
    }
}
