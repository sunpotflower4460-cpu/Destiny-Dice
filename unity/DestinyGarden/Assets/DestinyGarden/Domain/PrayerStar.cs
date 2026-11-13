using System;
using System.Collections.Generic;

namespace DestinyGarden.Domain
{
    /// <summary>What the star beneath the die became with one roll (prayer-star.v1).</summary>
    public sealed class PrayerStarReading
    {
        public int Points;          // 5, 6, 7 or 8
        public bool YinYangUnion;   // this roll's blessing turned yin to yang (or yang to yin)
        public bool WishAnswered;   // the wish of this roll came true
        public bool LuckyNumber;    // the Lucky Seven face, or this face + the previous face = 7
        public int FaceSum;         // this face + the previous face (0 when there was no previous roll)
        public int GiftGrowth;      // growth given to each recipient
        public int GiftRecipients;  // how many plants may receive (int.MaxValue = every non-flowering plant)
        public bool HasElement;     // prayer-star.v2: the rolled blessing steers where the gift goes
        public ElementId Element;
    }

    /// <summary>
    /// The prayer star (prayer-star.v2, docs/unity-garden/ECOLOGY.md §8). A star of light lies on the
    /// altar beneath the die. Each roll turns it into a five-, six-, seven- or eight-pointed star,
    /// decided only by facts already saved for the roll (the faces, the blessings, the wish, the harmony
    /// reading); it draws no chance of its own. The star then gives a little growth to the youngest
    /// plants that are not yet in flower, more the more points it has. Which plants come first follows the
    /// rolled blessing (prayer-star.v2): water seeks the driest soil, light the sun-lovers standing in the sun,
    /// wind the flowers that bees and butterflies love, seed the youngest seedlings. So the faces that come up
    /// steer what kind of garden grows.
    ///
    /// - 5 五芒星 (the star of life): every roll.
    /// - 6 六芒星 (yin and yang joined): the blessing changed between yin (water, seed) and yang
    ///   (light, wind) since the previous roll, or the wish came true.
    /// - 7 七芒星 (lucky number): the Lucky Seven face was rolled, or this face and the previous face
    ///   add up to seven (opposite faces of a die).
    /// - 8 八芒星 (the rainbow eight, abundance): a lucky number that also joins yin and yang (or answers
    ///   the wish) while the island is in full harmony (eight).
    /// </summary>
    public static class PrayerStar
    {
        public const string RulesVersion = "prayer-star.v2";
        public const string GiftKind = "star";          // EcologyEffect.Kind for the star's gift
        public const int LuckySum = 7;
        public const int OctagramHarmony = 8;   // prayer-star.v2: full harmony (v1: 7)

        public static int GiftGrowthFor(int points) =>
            points >= 8 ? 88 : points == 7 ? 77 : points == 6 ? 16 : points == 5 ? 5 : 0;

        public static int GiftRecipientsFor(int points) =>
            points >= 8 ? int.MaxValue : points >= 5 ? points : 0;

        public static bool IsLuckyNumber(int faceId, int previousFaceId)
        {
            if (faceId == LuckySeven.FaceId) return true;
            return faceId >= 1 && faceId <= 6 && previousFaceId >= 1 && previousFaceId <= 6 &&
                   faceId + previousFaceId == LuckySum;
        }

        /// <summary>The face that would make the lucky number on the next roll (7 - last face), 0 when none does.</summary>
        public static int FaceCompletingSeven(int lastFaceId) =>
            lastFaceId >= 1 && lastFaceId <= 6 ? LuckySum - lastFaceId : 0;

        /// <param name="faceId">Face rolled this time (1..7).</param>
        /// <param name="previousFaceId">Face of the previous resolved roll in this run, 0 for none.</param>
        /// <param name="recipeId">Recipe rolled this time.</param>
        /// <param name="previousRecipeId">Recipe of the previous resolved roll, null for none.</param>
        /// <param name="harmonyValue">This roll's harmony reading (0..8).</param>
        public static PrayerStarReading Read(int faceId, int previousFaceId, string recipeId,
            string previousRecipeId, bool wishAnswered, int harmonyValue)
        {
            var r = new PrayerStarReading { WishAnswered = wishAnswered };
            if (recipeId != null) { r.HasElement = true; r.Element = RecipeCatalog.ElementOf(recipeId); }
            if (recipeId != null && previousRecipeId != null)
                r.YinYangUnion = Harmony.IsYin(RecipeCatalog.ElementOf(recipeId)) !=
                                 Harmony.IsYin(RecipeCatalog.ElementOf(previousRecipeId));
            r.LuckyNumber = IsLuckyNumber(faceId, previousFaceId);
            r.FaceSum = previousFaceId > 0 ? faceId + previousFaceId : 0;
            bool joined = r.YinYangUnion || r.WishAnswered;
            r.Points = r.LuckyNumber && joined && harmonyValue >= OctagramHarmony ? 8
                : r.LuckyNumber ? 7
                : joined ? 6
                : 5;
            r.GiftGrowth = GiftGrowthFor(r.Points);
            r.GiftRecipients = GiftRecipientsFor(r.Points);
            return r;
        }

        /// <summary>
        /// Where the rolled blessing sends the gift first (lower = sooner). Ties fall to the youngest plant, then cell order.
        /// Water: driest soil. Light: sun-loving species in the sun, then plants in the sun. Wind: the flowers
        /// pollinators love most. Seed (or no element): youngest only.
        /// </summary>
        public static int Priority(WorldState s, PlantState p, PrayerStarReading star)
        {
            if (star == null || !star.HasElement) return 0;
            var t = Flora.Get(p.Species);
            switch (star.Element)
            {
                case ElementId.Water:
                    return Flora.Moisture(s, p.CellId);
                case ElementId.Light:
                    {
                        bool sun = !Flora.IsRockShade(p.CellId);
                        int light = t != null ? t.Light : 1;
                        return sun && light == 0 ? 0 : sun ? 1 : 2;
                    }
                case ElementId.Wind:
                    return t != null ? 4 - (Math.Max(0, t.Bee) + Math.Max(0, t.Butterfly)) : 4;
                default:
                    return 0;
            }
        }

        /// <summary>Plants not yet in flower in the blessing's order (see Priority), as many as the star gives to.</summary>
        public static List<int> Recipients(WorldState s, PrayerStarReading star)
        {
            var young = new List<PlantState>();
            foreach (var p in s.Plants) if (p.Stage != PlantStage.Flower && p.Growth < 2500) young.Add(p);
            var prio = new Dictionary<PlantState, int>();
            foreach (var p in young) prio[p] = Priority(s, p, star);
            young.Sort((a, b) =>
            {
                int c = prio[a].CompareTo(prio[b]);
                if (c != 0) return c;
                return a.Growth != b.Growth ? a.Growth.CompareTo(b.Growth) : a.CellId.CompareTo(b.CellId);
            });
            var cells = new List<int>();
            for (int i = 0; i < young.Count && i < star.GiftRecipients; i++) cells.Add(young[i].CellId);
            return cells;
        }

        /// <summary>
        /// rainbow.v3: the rainbow eight rises exactly when the prayer star becomes eight-pointed. (rainbow.v2,
        /// the Lucky Seven rolled while the garden answered, fired once in fifteen rolls in long play once the
        /// garden kept budding; the Lucky Seven now always makes the lucky number, and the eight from there.)
        /// </summary>
        public static bool IsRainbow(bool luckySevenRolled, SimulationResult sim, EcologyResult eco) =>
            eco != null && eco.PrayerStar != null && eco.PrayerStar.Points >= 8;

        public static Dictionary<string, object> ToPayload(PrayerStarReading r) => new Dictionary<string, object>
        {
            ["rulesVersion"] = RulesVersion,
            ["points"] = (long)r.Points,
            ["luckyNumber"] = r.LuckyNumber,
            ["yinYangUnion"] = r.YinYangUnion,
            ["wishAnswered"] = r.WishAnswered,
            ["faceSum"] = (long)r.FaceSum,
            ["giftGrowth"] = (long)r.GiftGrowth,
            ["giftElement"] = r.HasElement ? r.Element.ToString().ToLowerInvariant() : null
        };
    }
}
