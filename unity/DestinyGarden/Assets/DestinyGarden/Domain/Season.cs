using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>What the garden asks for in one season (season.v1).</summary>
    public sealed class SeasonGoal
    {
        public string Id;
        public string LabelJa;
    }

    /// <summary>
    /// Seasons (season.v1, docs/unity-garden/ECOLOGY.md §13). Every twelve rolls is a season. At its first roll the
    /// garden asks for one thing it lacks (from a fixed list, judged on the saved state before that roll; the
    /// season index turns the choice). Meeting it in the season brings the seventh face to the next round (the
    /// Lucky Seven, each face still 1/7). No chance is involved. Inspired by round objectives (Wingspan) and staged
    /// restoration goals (Terra Nil): a direction for the player's wishes and chosen places.
    /// </summary>
    public static class Seasons
    {
        public const string RulesVersion = "season.v1";
        public const int Length = 12;
        public const int BloomingTarget = 5;
        public const int HarmonyTarget = 7;

        public static readonly SeasonGoal[] All =
        {
            new SeasonGoal { Id = "bloom", LabelJa = "咲いている花を5種に" },
            new SeasonGoal { Id = "butterfly", LabelJa = "蝶を呼ぶ" },
            new SeasonGoal { Id = "wetland", LabelJa = "水辺の群落をつくる" },
            new SeasonGoal { Id = "meadow", LabelJa = "花の野をつくる" },
            new SeasonGoal { Id = "nectar", LabelJa = "蜜の花畑をつくる" },
            new SeasonGoal { Id = "thicket", LabelJa = "実りの藪をつくる" },
            new SeasonGoal { Id = "harmony", LabelJa = "島の調和を7に" },
        };

        public static SeasonGoal Get(string id)
        {
            foreach (var g in All) if (g.Id == id) return g;
            return null;
        }

        /// <summary>Season of the roll that makes this world revision (revision 1..12 is season 0).</summary>
        public static int IndexOf(int revisionAfterRoll) => Math.Max(0, revisionAfterRoll - 1) / Length;

        /// <summary>Rolls left in the season after the roll that made this revision (0 on its last roll).</summary>
        public static int RollsLeft(int revisionAfterRoll) => Length - 1 - Math.Max(0, revisionAfterRoll - 1) % Length;

        /// <summary>Whether a garden state (with its ecology reading) already has what the goal asks.</summary>
        public static bool IsMet(string id, WorldState s, IList<Community> communities, int harmony, bool butterflies)
        {
            switch (id)
            {
                case "bloom": return Flora.BloomingSpecies(s) >= BloomingTarget;
                case "butterfly": return butterflies;
                case "harmony": return harmony >= HarmonyTarget;
                default:
                    if (communities != null)
                        foreach (var c in communities) if (c.Kind == id) return true;
                    return false;
            }
        }

        /// <summary>
        /// The goal of a season: the goals the garden does not yet meet (judged on the state before the season's
        /// first roll), turned by the season index. If it already meets all of them, the list turns the same way.
        /// </summary>
        public static SeasonGoal Pick(int seasonIndex, WorldState before, IList<string> recentRecipeIds, WeatherDefinition weather)
        {
            var communities = Communities.Find(before);
            bool butterflies = Ecology.Presence(before, weather, recentRecipeIds, null).Exists(v => v.Kind == Ecology.Butterfly);
            int harmony = Harmony.Read(recentRecipeIds, 0, Flora.BloomingSpecies(before)).Value;
            var open = new List<SeasonGoal>();
            foreach (var g in All) if (!IsMet(g.Id, before, communities, harmony, butterflies)) open.Add(g);
            var from = open.Count > 0 ? open : new List<SeasonGoal>(All);
            return from[Math.Max(0, seasonIndex) % from.Count];
        }
    }
}
