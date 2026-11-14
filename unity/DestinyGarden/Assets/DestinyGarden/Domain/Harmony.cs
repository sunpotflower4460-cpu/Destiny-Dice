using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    public sealed class HarmonyReading
    {
        public int Value;            // 0..8
        public int Star;             // 0, 5, 7 or 8
        public int Yin, Yang;        // counts in the window
        public int ElementsPresent;  // 0..4
        public bool Life;            // all four blessings present: the fifth, "life", appears
    }

    /// <summary>
    /// Harmony (harmony.v1, ECOLOGY.md): a reading of the garden's balance over the last eight
    /// rolls. Water and seed are yin, light and wind are yang. When all four blessings are present,
    /// a fifth, life, appears. The reading (0..8) sets the star of the island: five points for
    /// life, seven for the lucky seven lights, eight at full harmony.
    /// </summary>
    public static class Harmony
    {
        public const string RulesVersion = "harmony.v2";
        public const int Window = 8;
        public const int ResidentKindsForPoint = 5;
        public const int BloomingSpeciesForPoint = 4; // harmony.v2: diversity of flowering species

        public static bool IsYin(ElementId e) => e == ElementId.Water || e == ElementId.Seed;

        public static int StarFor(int value) => value >= 8 ? 8 : value >= 7 ? 7 : value >= 5 ? 5 : 0;

        public static HarmonyReading ReadIsland(WorldState state, WeatherDefinition weather, IList<string> recipes) =>
            ReadIsland(state, recipes, LivingEcosystem.IslandPresence(state, weather));

        internal static HarmonyReading ReadIsland(WorldState state, IList<string> recipes, IEnumerable<EcologyVisit> presence) =>
            Read(recipes, CommunityRules.ResidentKinds(presence), Flora.BloomingSpecies(state));

        public static HarmonyReading Read(IList<string> rolledRecipeIdsOldestFirst, int residentKinds, int bloomingSpecies)
        {
            var r = new HarmonyReading();
            var seen = new HashSet<ElementId>();
            if (rolledRecipeIdsOldestFirst != null)
            {
                int start = Math.Max(0, rolledRecipeIdsOldestFirst.Count - Window);
                for (int i = start; i < rolledRecipeIdsOldestFirst.Count; i++)
                {
                    var e = RecipeCatalog.ElementOf(rolledRecipeIdsOldestFirst[i]);
                    seen.Add(e);
                    if (IsYin(e)) r.Yin++; else r.Yang++;
                }
            }
            r.ElementsPresent = seen.Count;
            r.Life = seen.Count == 4;
            int value = r.ElementsPresent;
            if (r.Yin + r.Yang > 0 && Math.Abs(r.Yin - r.Yang) <= 1) value++;
            if (r.Life) value++;
            if (residentKinds >= ResidentKindsForPoint) value++;
            if (bloomingSpecies >= BloomingSpeciesForPoint) value++;
            r.Value = Math.Min(8, value);
            r.Star = StarFor(r.Value);
            return r;
        }
    }
}
