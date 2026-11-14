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
        public int GiftGrowth;      // growth given to each recipient (five points); 0 = one full stage (six to eight)
        public int GiftRecipients;  // how many plants may receive (int.MaxValue = every non-flowering plant)
        public int AnchorCellId = -1; // prayer-star.v2: the blessing's site; plants near it receive first
        public int LuckyCount;        // lucky.v1: lucky numbers in this garden so far, this one included
        public int LuckyStreak;       // lucky.v1: lucky numbers in a row ending with this roll (0 when not lucky)
        public bool SevenLuckies;     // lucky.v1: this lucky number is the 7th, 14th, ... of the garden: the eight
        public int LuckyPlantCellId = -1; // prayer-star.v4: the plant this lucky number marked (-1 when none)
        public bool Zorome;          // v5: the same face as the previous roll (ゾロ目)
        public bool Triple;          // v5: the same face three times in a row (三つぞろい)
        public int FaceTotal;        // v5: the sum of every face of this garden so far, this one included
        public bool Total77;         // v5: this roll's face carried the total past a multiple of 77
        /// <summary>v5: buds open on a lucky number or a ゾロ目 (v6: and when the total reaches a multiple of 77).</summary>
        public bool OpensBuds => LuckyNumber || Zorome || Total77;
        /// <summary>v6: a 三つぞろい brings the seventh face to the next round (in v5 a total of 77 did too).</summary>
        public bool EarnsSeventh => Triple;
        /// <summary>v6: a 三つぞろい or a total of 77 opens every bud of the garden.</summary>
        public bool OpensAllBuds => Triple || Total77;
        public bool Wished = true;  // v6: a wish was made on this roll (the garden's seventh lucky number is the eight only then)
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
    ///   the wish) while the island's harmony is at least seven.
    /// </summary>
    public static class PrayerStar
    {
        // v3 (lucky.v1): seven luckies, double lucky. v4: the eight is luck met by prayer — a lucky number on a roll whose
        // wish came true (in a harmonious garden), or the garden's seventh lucky number; a yin-yang turn alone stays six.
        // v5 (lucky signs): ゾロ目 (the same face twice) opens a bud; 三つぞろい (three times) opens every bud and brings
        // the seventh face; the garden's face total passing a multiple of 77 brings the seventh face. Odds never change.
        // v6 (completion balance): the garden's seventh lucky number is the eight only on a roll with a wish; a total
        // of 77 opens every bud instead of bringing the seventh face; the five's gift goes first to a plant near the site
        // that it lifts to its next stage (so the gift is seen). Odds never change.
        public const string RulesVersion = "prayer-star.v6";

        /// <summary>The N of a recorded "prayer-star.vN" (0 when unknown).</summary>
        public static int VersionOf(string rulesVersion)
        {
            const string prefix = "prayer-star.v";
            if (rulesVersion == null || !rulesVersion.StartsWith(prefix, StringComparison.Ordinal)) return 0;
            return int.TryParse(rulesVersion.Substring(prefix.Length), out int n) ? n : 0;
        }
        public const int LuckyTotal = 77;
        public const string GiftKind = "star";          // EcologyEffect.Kind for the star's gift
        public const int LuckySum = 7;
        public const int OctagramHarmony = 8;  // v4: the island in full harmony (7 in v1–v3)

        public const int SiteReach = 3;               // plants within this distance of the blessing's site come first
        public const int FiveGift = 60;

        /// <summary>prayer-star.v2: five gives +60; six and eight lift each recipient one full growth stage, seven half a stage.</summary>
        public static int GiftGrowthFor(int points) => points == 5 ? FiveGift : 0;

        /// <summary>Five and six: one plant. Seven: seven plants. Eight: every plant not yet in flower.</summary>
        public static int GiftRecipientsFor(int points) =>
            points >= 8 ? int.MaxValue : points == 7 ? 7 : points >= 5 ? 1 : 0;

        /// <summary>Growth that lifts a plant to the next stage threshold (250 / 750 / 1500 / 2500).</summary>
        public static int ToNextStage(int growth) =>
            growth < 250 ? 250 - growth : growth < 750 ? 750 - growth : growth < 1500 ? 1500 - growth : Math.Max(0, 2500 - growth);

        /// <summary>
        /// The gift one recipient receives: five +60; six and eight a full stage; seven half a stage (at least 60),
        /// or a full stage for a double lucky number (two in a row, lucky.v1).
        /// </summary>
        public static int GiftFor(PrayerStarReading star, int growth)
        {
            int step = ToNextStage(growth);
            // prayer-star.v4/v5: on a lucky number or a ゾロ目 a bud opens — a full stage to bloom (luck is seen as flowers).
            if (star.OpensBuds && growth >= 1500) return step;
            if (star.GiftGrowth > 0) return star.GiftGrowth;
            return star.Points == 7 && star.LuckyStreak < 2 ? Math.Min(step, Math.Max(FiveGift, step / 2)) : step;
        }

        public static bool IsLuckyNumber(int faceId, int previousFaceId)
        {
            if (faceId == LuckySeven.FaceId) return true;
            return faceId >= 1 && faceId <= 6 && previousFaceId >= 1 && previousFaceId <= 6 &&
                   faceId + previousFaceId == LuckySum;
        }

        /// <summary>The face that would make the lucky number on the next roll (7 - last face), 0 when none does.</summary>
        public static int Dist(int a, int b) => Math.Abs(a % 16 - b % 16) + Math.Abs(a / 16 - b / 16);

        public static int FaceCompletingSeven(int lastFaceId) =>
            lastFaceId >= 1 && lastFaceId <= 6 ? LuckySum - lastFaceId : 0;

        /// <param name="faceId">Face rolled this time (1..7).</param>
        /// <param name="previousFaceId">Face of the previous resolved roll in this run, 0 for none.</param>
        /// <param name="recipeId">Recipe rolled this time.</param>
        /// <param name="previousRecipeId">Recipe of the previous resolved roll, null for none.</param>
        /// <param name="harmonyValue">This roll's harmony reading (0..8).</param>
        public const int LuckiesForRainbow = 7;   // lucky.v1: every seventh lucky number of a garden is the eight

        /// <summary>Lucky numbers in a run's faces (oldest first): each face that makes seven with the one before, or a 7.</summary>
        public static int CountLuckies(IList<int> faces)
        {
            int n = 0;
            if (faces == null) return 0;
            for (int i = 0; i < faces.Count; i++) if (IsLuckyNumber(faces[i], i > 0 ? faces[i - 1] : 0)) n++;
            return n;
        }

        /// <summary>Lucky numbers in a row at the end of a run's faces.</summary>
        /// <summary>v5: how many of the last faces are the same face (0 for none, 1 when the last differs from the one before).</summary>
        public static int SameRun(IList<int> faces)
        {
            if (faces == null || faces.Count == 0) return 0;
            int n = 1;
            for (int i = faces.Count - 1; i > 0 && faces[i] == faces[i - 1]; i--) n++;
            return n;
        }

        /// <summary>v5: the sum of the faces.</summary>
        public static int Total(IList<int> faces)
        {
            int t = 0;
            if (faces != null) foreach (int f in faces) t += f;
            return t;
        }

        public static int TrailingStreak(IList<int> faces)
        {
            int n = 0;
            if (faces == null) return 0;
            for (int i = faces.Count - 1; i >= 0 && IsLuckyNumber(faces[i], i > 0 ? faces[i - 1] : 0); i--) n++;
            return n;
        }

        public static PrayerStarReading Read(int faceId, int previousFaceId, string recipeId,
            string previousRecipeId, bool wishAnswered, int harmonyValue, int anchorCellId = -1,
            int luckyCountBefore = 0, int luckyStreakBefore = 0, int sameRunBefore = 0, int faceTotalBefore = 0, bool wished = true)
        {
            var r = new PrayerStarReading { WishAnswered = wishAnswered, AnchorCellId = anchorCellId, Wished = wished };
            if (recipeId != null) { r.HasElement = true; r.Element = RecipeCatalog.ElementOf(recipeId); }
            if (recipeId != null && previousRecipeId != null)
                r.YinYangUnion = Harmony.IsYin(RecipeCatalog.ElementOf(recipeId)) !=
                                 Harmony.IsYin(RecipeCatalog.ElementOf(previousRecipeId));
            r.LuckyNumber = IsLuckyNumber(faceId, previousFaceId);
            r.FaceSum = previousFaceId > 0 ? faceId + previousFaceId : 0;
            bool joined = r.YinYangUnion || r.WishAnswered;
            // lucky.v1: count the garden's lucky numbers; the seventh (and every seventh after) is the eight.
            r.LuckyCount = luckyCountBefore + (r.LuckyNumber ? 1 : 0);
            r.LuckyStreak = r.LuckyNumber ? luckyStreakBefore + 1 : 0;
            r.SevenLuckies = r.LuckyNumber && r.LuckyCount % LuckiesForRainbow == 0;
            r.Points = r.LuckyNumber && ((r.SevenLuckies && wished) || (r.WishAnswered && harmonyValue >= OctagramHarmony)) ? 8
                : r.LuckyNumber ? 7
                : joined ? 6
                : 5;
            r.GiftGrowth = GiftGrowthFor(r.Points);
            r.GiftRecipients = GiftRecipientsFor(r.Points);
            // v5: lucky signs from the faces alone.
            r.Zorome = previousFaceId > 0 && faceId == previousFaceId;
            r.Triple = r.Zorome && sameRunBefore >= 2;
            r.FaceTotal = faceTotalBefore + Math.Max(0, faceId);
            r.Total77 = faceId > 0 && r.FaceTotal / LuckyTotal > faceTotalBefore / LuckyTotal;
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
            foreach (var p in young)
            {
                // prayer-star.v4: on a lucky number the buds come first, wherever they are (they open into flowers);
                // then the blessing's site (prayer-star.v2), then the blessing's own preference.
                bool waitingBud = star.OpensBuds && p.Stage == PlantStage.Bud;
                bool far = star.AnchorCellId >= 0 && Dist(p.CellId, star.AnchorCellId) > SiteReach;
                // prayer-star.v6: near the site, the five's gift goes first to a plant it lifts to the next stage.
                bool unseen = star.GiftGrowth > 0 && ToNextStage(p.Growth) > star.GiftGrowth;
                prio[p] = (star.OpensBuds && !waitingBud ? 1 << 20 : 0) + (far ? 1 << 16 : 0) + (unseen ? 1 << 12 : 0) + Priority(s, p, star);
            }
            young.Sort((a, b) =>
            {
                int c = prio[a].CompareTo(prio[b]);
                if (c != 0) return c;
                return a.Growth != b.Growth ? a.Growth.CompareTo(b.Growth) : a.CellId.CompareTo(b.CellId);
            });
            // v5: a 三つぞろい opens every bud (they come first in the order above); v6: so does a total of 77.
            int take = star.GiftRecipients;
            if (star.OpensAllBuds) { int buds = 0; foreach (var p in young) if (p.Stage == PlantStage.Bud) buds++; take = Math.Max(take, buds); }
            var cells = new List<int>();
            for (int i = 0; i < young.Count && i < take; i++) cells.Add(young[i].CellId);
            return cells;
        }

        internal static void ApplyGift(EcologyResult result, EcologyContext context, Action<int, int> nourish)
        {
            if (context.FaceId <= 0) return;
            var recent = context.RecentRecipeIds;
            string previous = recent != null && recent.Count >= 2 ? recent[recent.Count - 2] : null;
            // prayer-star.v3: the anchor (the site the blessing reached) and the garden's lucky count and streak.
            var star = Read(context.FaceId, context.PreviousFaceId, context.CurrentRecipeId,
                previous, context.PrayerAnswered, result.Harmony.Value, context.AnchorCellId,
                context.LuckyCountBefore, context.LuckyStreakBefore, context.SameRunBefore, context.FaceTotalBefore, !context.NoWish);
            result.PrayerStar = star;
            var recipients = Recipients(result.PostState, star);
            foreach (int cell in recipients)
                nourish(cell, GiftFor(star, result.PostState.PlantAt(cell).Growth));
            // prayer-star.v4: a lucky number leaves its mark on the plant its gift reached first (the lucky plant).
            if (star.LuckyNumber && recipients.Count > 0)
            {
                var first = result.PostState.PlantAt(recipients[0]);
                if (first != null) { first.LuckyMarks++; star.LuckyPlantCellId = first.CellId; }
            }
        }

        /// <summary>
        /// rainbow.v3: the rainbow eight rises exactly when the prayer star becomes eight-pointed. (rainbow.v2,
        /// the Lucky Seven rolled while the garden answered, fired once in fifteen rolls in long play once the
        /// garden kept budding; the Lucky Seven now always makes the lucky number, and the eight from there.)
        /// </summary>
        public static bool IsRainbow(bool luckySevenRolled, SimulationResult sim, EcologyResult eco) =>
            eco?.PrayerStar != null ? eco.PrayerStar.Points >= 8 : LuckySeven.IsRainbow(luckySevenRolled, sim, eco);

        public static Dictionary<string, object> ToPayload(PrayerStarReading r) => new Dictionary<string, object>
        {
            ["rulesVersion"] = RulesVersion,
            ["points"] = (long)r.Points,
            ["luckyNumber"] = r.LuckyNumber,
            ["yinYangUnion"] = r.YinYangUnion,
            ["wishAnswered"] = r.WishAnswered,
            ["faceSum"] = (long)r.FaceSum,
            ["giftGrowth"] = (long)r.GiftGrowth,
            ["giftStage"] = r.GiftGrowth == 0,
            ["luckyCount"] = (long)r.LuckyCount,
            ["luckyStreak"] = (long)r.LuckyStreak,
            ["sevenLuckies"] = r.SevenLuckies,
            ["anchorCellId"] = (long)r.AnchorCellId,
            ["giftElement"] = r.HasElement ? r.Element.ToString().ToLowerInvariant() : null,
            ["luckyPlantCellId"] = (long)r.LuckyPlantCellId,
            ["zorome"] = r.Zorome,
            ["triple"] = r.Triple,
            ["faceTotal"] = (long)r.FaceTotal,
            ["total77"] = r.Total77
        };
    }
}
