using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class PrayerStarTests
    {
        private sealed class Memory : IJournalWriter
        {
            public readonly List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Seq = Entries.Count + 1, Kind = kind, RunId = runId, RoundId = roundId, Payload = payload };
                Entries.Add(e);
                return e;
            }
        }

        // water_drop / seed_single are yin, light_soft / wind_breeze are yang.
        [Test]
        public void Read_FollowsTheStarTable()
        {
            // First roll: no previous face or blessing.
            Assert.AreEqual(5, PrayerStar.Read(3, 0, "water_drop", null, false, 8).Points);
            Assert.AreEqual(6, PrayerStar.Read(3, 0, "water_drop", null, true, 8).Points, "an answered wish joins the star");
            // Same polarity, no wish: the star of life.
            Assert.AreEqual(5, PrayerStar.Read(2, 1, "water_drop", "seed_single", false, 8).Points);
            // Yin after yang: hexagram.
            var six = PrayerStar.Read(2, 1, "water_drop", "light_soft", false, 8);
            Assert.AreEqual(6, six.Points);
            Assert.IsTrue(six.YinYangUnion);
            Assert.IsFalse(six.LuckyNumber);
            // Opposite faces (3 + 4) make the lucky number, whatever the blessing.
            var seven = PrayerStar.Read(3, 4, "water_drop", "seed_single", false, 8);
            Assert.AreEqual(7, seven.Points);
            Assert.IsTrue(seven.LuckyNumber);
            Assert.AreEqual(7, seven.FaceSum);
            // prayer-star.v4: the eight is luck met by prayer — a lucky number whose wish came true, harmony >= 7.
            Assert.AreEqual(8, PrayerStar.Read(6, 1, "water_drop", "seed_single", true, 8).Points, "lucky + answered wish + full harmony");
            Assert.AreEqual(7, PrayerStar.Read(6, 1, "water_drop", "seed_single", true, 7).Points, "harmony below eight stays seven");
            Assert.AreEqual(7, PrayerStar.Read(6, 1, "wind_breeze", "water_drop", false, 8).Points, "yin and yang alone do not make the eight");
            Assert.AreEqual(7, PrayerStar.Read(6, 1, "water_drop", "seed_single", false, 8).Points, "no wish, no eight");
            // The Lucky Seven face is always a lucky number; 7 + anything is never the opposite-face sum.
            Assert.IsTrue(PrayerStar.IsLuckyNumber(7, 0));
            Assert.IsTrue(PrayerStar.IsLuckyNumber(7, 3));
            Assert.IsFalse(PrayerStar.IsLuckyNumber(3, 3));
            Assert.IsFalse(PrayerStar.IsLuckyNumber(4, 0));
        }

        [Test]
        public void LuckyNumbers_AreCounted_SeventhIsTheEight_DoubleGrowsAFullStage()
        {
            // Faces 3,4 (lucky), 3 (lucky again: 3+4), 1, 6 (lucky), 7 (lucky).
            var faces = new List<int> { 3, 4, 3, 1, 6, 7 };
            Assert.AreEqual(4, PrayerStar.CountLuckies(faces));
            Assert.AreEqual(2, PrayerStar.TrailingStreak(faces), "6 then 7: two in a row");
            Assert.AreEqual(0, PrayerStar.TrailingStreak(new List<int> { 3, 4, 2 }));

            // The seventh lucky number is the eight, even with little harmony and no yin/yang change.
            var seventh = PrayerStar.Read(4, 3, "water_drop", "seed_single", false, 0, -1, 6, 0);
            Assert.AreEqual(8, seventh.Points);
            Assert.IsTrue(seventh.SevenLuckies);
            Assert.AreEqual(7, seventh.LuckyCount);
            var sixth = PrayerStar.Read(4, 3, "water_drop", "seed_single", false, 0, -1, 5, 0);
            Assert.AreEqual(7, sixth.Points);
            Assert.IsFalse(sixth.SevenLuckies);
            Assert.AreEqual(0, PrayerStar.Read(2, 3, "water_drop", "seed_single", false, 0, -1, 6, 1).LuckyStreak, "not lucky: no streak");

            // Two in a row: a double lucky number lifts its seven plants a full stage instead of half.
            var single = PrayerStar.Read(4, 3, "water_drop", "seed_single", false, 0, -1, 1, 0);
            var dbl = PrayerStar.Read(4, 3, "water_drop", "seed_single", false, 0, -1, 1, 1);
            Assert.AreEqual(1, single.LuckyStreak); Assert.AreEqual(2, dbl.LuckyStreak);
            Assert.AreEqual(250, PrayerStar.GiftFor(single, 0) * 2, "half a stage");
            Assert.AreEqual(250, PrayerStar.GiftFor(dbl, 0), "a full stage");
        }

        [Test]
        public void InPlay_LuckyCountFollowsTheSavedFaces()
        {
            var faces = new List<int>();
            int sevens = 0;
            foreach (var o in Play(new Memory(), "lucky-count", 200))
            {
                faces.Add(o.FaceId);
                var star = o.Ecology.PrayerStar;
                Assert.AreEqual(PrayerStar.CountLuckies(faces), star.LuckyCount);
                Assert.AreEqual(PrayerStar.TrailingStreak(faces), star.LuckyStreak);
                if (star.SevenLuckies) { sevens++; Assert.AreEqual(8, star.Points); Assert.AreEqual(0, star.LuckyCount % 7); }
            }
            Assert.Greater(sevens, 0, "200 rolls reach seven lucky numbers");
        }

        [Test]
        public void Gift_GoesToTheYoungestNonFloweringPlants()
        {
            Assert.AreEqual(new[] { 60, 0, 0, 0 }, new[] { 5, 6, 7, 8 }.Select(PrayerStar.GiftGrowthFor).ToArray(), "0 = one full stage");
            Assert.AreEqual(new[] { 1, 1, 7, int.MaxValue }, new[] { 5, 6, 7, 8 }.Select(PrayerStar.GiftRecipientsFor).ToArray());
            Assert.AreEqual(new[] { 250, 240, 350, 750, 1000, 0 }, new[] { 0, 10, 400, 750, 1500, 2500 }.Select(PrayerStar.ToNextStage).ToArray());
            var s = new WorldState();
            int[] growth = { 2500, 10, 900, 10, 400, 0, 1600, 1200 };
            for (int i = 0; i < growth.Length; i++)
                s.Plants.Add(new PlantState
                {
                    PlantId = "p" + i, CellId = 100 + i, Growth = growth[i],
                    Stage = growth[i] >= 2500 ? PlantStage.Flower : growth[i] >= 1500 ? PlantStage.Bud : PlantStage.Leaf
                });
            var five = PrayerStar.Recipients(s, PrayerStar.Read(1, 0, "seed_single", null, false, 0));
            CollectionAssert.AreEqual(new[] { 105 }, five, "five: the youngest one");
            var seven = PrayerStar.Recipients(s, PrayerStar.Read(3, 4, "seed_single", "seed_single", false, 0));
            CollectionAssert.AreEqual(new[] { 105, 101, 103, 104, 102, 107, 106 }, seven, "growth order, then cell order");
            // The blessing's site comes first: with the site at cell 106, the plants within three cells lead.
            var near = PrayerStar.Recipients(s, PrayerStar.Read(1, 0, "seed_single", null, false, 0, 106));
            Assert.LessOrEqual(PrayerStar.Dist(near[0], 106), PrayerStar.SiteReach);
            var eight = PrayerStar.Recipients(s, new PrayerStarReading { Points = 8, GiftRecipients = PrayerStar.GiftRecipientsFor(8) });
            Assert.AreEqual(7, eight.Count, "the eight reaches every plant not yet in flower");
            Assert.IsFalse(eight.Contains(100));
        }

        [Test]
        public void Gift_FollowsTheRolledBlessing()
        {
            // Two young plants: one on dry soil in the sun (a sun-loving cosmos), one on wet soil in rock shade
            // (a shade-loving bellflower, loved by nobody in particular).
            int sunny = -1, shady = -1;
            for (int c = 0; c < GardenMapData.CellCount && (sunny < 0 || shady < 0); c++)
            {
                if (GardenMapData.Plantable[c] != 1 || GardenMapData.SoilCapacity[c] <= 0) continue;
                if (!Flora.IsRockShade(c) && sunny < 0) sunny = c;
                else if (Flora.IsRockShade(c) && shady < 0) shady = c;
            }
            Assert.GreaterOrEqual(sunny, 0); Assert.GreaterOrEqual(shady, 0);
            var s = new WorldState();
            s.SoilWater[shady] = GardenMapData.SoilCapacity[shady];   // wet
            s.Plants.Add(new PlantState { PlantId = "sun", CellId = sunny, Growth = 600, Stage = PlantStage.Sprout, Species = "cosmos" });
            s.Plants.Add(new PlantState { PlantId = "shade", CellId = shady, Growth = 100, Stage = PlantStage.Sprout, Species = "bellflower" });
            int First(string recipe) => PrayerStar.Recipients(s, PrayerStar.Read(1, 0, recipe, null, false, 0))[0];
            Assert.AreEqual(sunny, First("water_drop"), "water seeks the driest soil");
            Assert.AreEqual(sunny, First("light_soft"), "light seeks the sun-lover in the sun");
            Assert.AreEqual(sunny, First("wind_breeze"), "wind seeks what pollinators love (cosmos: butterflies 2, bees 1)");
            Assert.AreEqual(shady, First("seed_single"), "seed seeks the youngest");
        }

        private static List<RollOutcome> Play(IJournalWriter journal, string seed, int rolls)
        {
            var coord = new GardenCoordinator(journal, new FixedEntropy(seed), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            var list = new List<RollOutcome>();
            for (int i = 0; i < rolls; i++)
            {
                if (coord.LuckySevenOffered) coord.AddLuckySeven();
                coord.CommitIntent("family", i % 3 == 0 ? "light" : "water");
                var o = coord.Roll();
                coord.Prepare();
                if (o != null) list.Add(o);
            }
            return list;
        }

        [Test]
        public void Coordinator_ReadsTheStarFromTheSavedRolls_AndGivesOnlyItsGift()
        {
            var outcomes = Play(new Memory(), "prayer-star", 160);
            var points = new HashSet<int>();
            for (int i = 0; i < outcomes.Count; i++)
            {
                var o = outcomes[i];
                var star = o.Ecology.PrayerStar;
                Assert.NotNull(star, "every roll reads the star");
                int prevFace = i > 0 ? outcomes[i - 1].FaceId : 0;
                string prevRecipe = i > 0 ? outcomes[i - 1].RecipeId : null;
                var facesBefore = outcomes.Take(i).Select(x => x.FaceId).ToList();
                var expect = PrayerStar.Read(o.FaceId, prevFace, o.RecipeId, prevRecipe, o.IntentMatched, o.Ecology.Harmony.Value, -1,
                    PrayerStar.CountLuckies(facesBefore), PrayerStar.TrailingStreak(facesBefore));
                Assert.AreEqual(expect.Points, star.Points, "roll " + i);
                points.Add(star.Points);

                var gifts = o.Ecology.Effects.Where(e => e.Kind == PrayerStar.GiftKind).ToList();
                Assert.LessOrEqual(gifts.Count, star.GiftRecipients);
                foreach (var g in gifts)
                {
                    Assert.Greater(g.Amount, 0);
                    Assert.LessOrEqual(g.Amount, star.GiftGrowth > 0 ? star.GiftGrowth : star.Points == 7 && star.LuckyStreak < 2 ? 500 : 1000,
                        "five: +60; seven half a stage (a double lucky number a full one); otherwise one stage");
                }
                Assert.AreEqual(o.Simulation.PostState.TotalWater(), o.Ecology.PostState.TotalWater(), "the star never moves water");
                Assert.AreEqual(star.Points >= 8, PrayerStar.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology),
                    "the rainbow eight comes exactly with the eight-pointed star");
            }
            Assert.IsTrue(points.Contains(5) && points.Contains(6) && points.Contains(7),
                "160 rolls should show the five-, six- and seven-pointed stars: " + string.Join(",", points));
        }

        [Test]
        public void RainbowEight_LetsEveryFlowerSetSeed()
        {
            int eights = 0;
            for (int run = 0; run < 8 && eights == 0; run++)
                foreach (var o in Play(new Memory(), "rainbow-seed-" + run, 160))
                {
                    if (o.Ecology.PrayerStar.Points < 8) continue;
                    eights++;
                    // Flowers counted this roll (BloomRolls > 0) were in bloom under the rainbow; one opened later by the
                    // returning plants' nourishment has not been counted yet.
                    foreach (var p in o.Ecology.PostState.Plants.Where(p => p.Stage == PlantStage.Flower && p.BloomRolls > 0))
                        Assert.IsTrue(p.Pollinated, "every flower under the rainbow sets seed");
                }
            Assert.Greater(eights, 0, "fixtures should reach an eight-pointed star");
        }

        [Test]
        public void Star_IsJournaled_Projected_AndSurvivesARestore()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-star-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "journal.bin");
            var clock = new FixedClock();
            var entropy = new FixedEntropy("star-restore");
            var coord = new GardenCoordinator(new FileJournal(path, clock), entropy, new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            RollOutcome last = null;
            for (int i = 0; i < 6; i++)
            {
                coord.CommitIntent("none", null);
                last = coord.Roll();
                coord.Prepare();
            }

            var read = new FileJournal(path, clock).ReadAll();
            var genesis = read.Entries.First(e => e.Kind == "RunStarted");
            Assert.AreEqual(PrayerStar.RulesVersion, genesis.Payload["prayerStarPolicyVersion"]);
            Assert.AreEqual(Flora.BlessingRulesVersion, genesis.Payload["floraPolicyVersion"]);
            var resolved = read.Entries.Last(e => e.Kind == "Resolved");
            var eco = (Dictionary<string, object>)resolved.Payload["ecology"];
            Assert.AreEqual(LivingEcosystem.ConnectedHabitatsVersion, eco["rulesVersion"]);
            Assert.IsTrue(eco.ContainsKey("care"));
            Assert.IsTrue(eco.ContainsKey("relations"));
            Assert.IsTrue(coord.State.Ecosystem.BlessingFlora);
            var ps = (Dictionary<string, object>)eco["prayerStar"];
            Assert.AreEqual((long)last.Ecology.PrayerStar.Points, ps["points"]);
            var history = HistoryProjector.Project(read.Entries, coord.RunId);
            Assert.AreEqual(last.Ecology.PrayerStar.Points, history.Last(h => h.Resolved && h.WorldRevision > 0).PrayerStar);
            Assert.IsNotNull(history.Last(h => h.Resolved && h.WorldRevision > 0).Care);

            // A restored coordinator remembers the previous face, so the next star reads the same way.
            var restored = new GardenCoordinator(new FileJournal(path, clock), entropy, new SequentialIds());
            restored.Restore(new FileJournal(path, clock).ReadAll());
            Assert.AreEqual(CoordinatorPhase.Prepared, restored.Phase);
            Assert.AreEqual(Hashing.StateHash(coord.State), Hashing.StateHash(restored.State));
            Assert.IsTrue(restored.State.Ecosystem.BlessingFlora);
            restored.CommitIntent("none", null);
            var next = restored.Roll();
            var expect = PrayerStar.Read(next.FaceId, last.FaceId, next.RecipeId, last.RecipeId, false, next.Ecology.Harmony.Value);
            Assert.AreEqual(expect.Points, next.Ecology.PrayerStar.Points);
            Assert.AreEqual(expect.LuckyNumber, next.Ecology.PrayerStar.LuckyNumber);
            Directory.Delete(dir, true);
        }

        [Test]
        public void Restore_OnlyUsesFacesFromCompletedRollsForTheStar()
        {
            var journal = new Memory();
            var ids = new SequentialIds();
            var coord = new GardenCoordinator(journal, new FixedEntropy("star-interrupted"), ids);
            coord.CreateRun();
            coord.Prepare();
            coord.CommitIntent("none", null);
            var complete = coord.Roll();
            coord.Prepare();
            coord.CommitIntent("none", null);
            journal.Append("OutcomeCommitted", coord.RunId, coord.CurrentRoundId, new Dictionary<string, object>
            {
                ["recipeId"] = "light_soft",
                ["faceId"] = complete.FaceId == 1 ? 2 : 1
            });
            journal.Append("InterruptedUnresolved", coord.RunId, coord.CurrentRoundId, new Dictionary<string, object>());

            var restored = new GardenCoordinator(new Memory(), new FixedEntropy("after-interruption"), ids);
            restored.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            CollectionAssert.AreEqual(new[] { complete.RecipeId }, restored.RolledRecipes);
            restored.Prepare();
            restored.CommitIntent("none", null);
            var next = restored.Roll();
            Assert.AreEqual(complete.FaceId + next.FaceId, next.Ecology.PrayerStar.FaceSum);
        }

        /// <summary>How often each star appears for a player who wishes for the commonest element. Report only.</summary>
        [Test]
        public void Measure_StarRates()
        {
            const int runs = 6, rollsPerRun = 120;
            var counts = new Dictionary<int, int>();
            int rolls = 0, rainbows = 0, rainbowsV2 = 0, gift = 0, sevenLuckies = 0, doubles = 0;
            for (int run = 0; run < runs; run++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("star-measure-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                for (int i = 0; i < rollsPerRun; i++)
                {
                    if (coord.LuckySevenOffered) coord.AddLuckySeven();
                    string best = coord.CurrentFaces.Faces
                        .GroupBy(f => RecipeCatalog.ElementOf(f).ToString().ToLowerInvariant())
                        .OrderByDescending(g => g.Count()).First().Key;
                    coord.CommitIntent("family", best);
                    var o = coord.Roll();
                    coord.Prepare();
                    if (o == null) continue;
                    rolls++;
                    int p = o.Ecology.PrayerStar.Points;
                    counts[p] = counts.TryGetValue(p, out int c) ? c + 1 : 1;
                    if (PrayerStar.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology)) rainbows++;
                    if (LuckySeven.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology)) rainbowsV2++;
                    gift += o.Ecology.Effects.Where(e => e.Kind == PrayerStar.GiftKind).Sum(e => e.Amount);
                    if (o.Ecology.PrayerStar.SevenLuckies) sevenLuckies++;
                    if (o.Ecology.PrayerStar.LuckyStreak >= 2) doubles++;
                }
            }
            string report = $"rolls={rolls} stars " +
                            string.Join(", ", counts.OrderBy(k => k.Key).Select(k => $"{k.Key}:{100.0 * k.Value / rolls:0.0}%")) +
                            $" | rainbow v3 1 per {(rainbows > 0 ? rolls / (double)rainbows : double.PositiveInfinity):0} rolls" +
                            $" (v2 alone 1 per {(rainbowsV2 > 0 ? rolls / (double)rainbowsV2 : double.PositiveInfinity):0})" +
                            $" | star gift growth/roll {gift / (double)rolls:0.0}" +
                            $" | seven luckies {sevenLuckies} (1 per {(sevenLuckies > 0 ? rolls / (double)sevenLuckies : double.PositiveInfinity):0} rolls)," +
                            $" double lucky {doubles} (1 per {(doubles > 0 ? rolls / (double)doubles : double.PositiveInfinity):0})";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-STAR " + report);
            Assert.Greater(rolls, 0);
        }
    }
}
