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
    public class LuckySevenTests
    {
        private static byte[] Words(params uint[] words)
        {
            var bytes = new List<byte>();
            foreach (uint w in words)
                bytes.AddRange(new[] { (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w });
            return bytes.ToArray();
        }

        [Test]
        public void RollD7_MapsWordsAndRejectsTheTopRemainder()
        {
            Assert.AreEqual(1, LuckySeven.RollD7(Words(0)));
            Assert.AreEqual(7, LuckySeven.RollD7(Words(6)));
            Assert.AreEqual(1, LuckySeven.RollD7(Words(7)));
            // 2^32 % 7 = 4, so words >= 4294967292 are rejected and the next word is used.
            Assert.AreEqual(4, LuckySeven.RollD7(Words(0xFFFFFFFF, 3)));
            Assert.AreEqual(-1, LuckySeven.RollD7(Words(0xFFFFFFFC)));
        }

        [Test]
        public void IntentMatched_FollowsFaceAndFamilyRules()
        {
            Assert.IsTrue(LuckySeven.IntentMatched("face", "7", 7, "water_rain"));
            Assert.IsFalse(LuckySeven.IntentMatched("face", "3", 7, "water_rain"));
            Assert.IsTrue(LuckySeven.IntentMatched("family", "wind", 2, "wind_passage"));
            Assert.IsFalse(LuckySeven.IntentMatched("family", "seed", 2, "wind_passage"));
            Assert.IsFalse(LuckySeven.IntentMatched("none", null, 2, "wind_passage"));
        }

        [Test]
        public void NatureBalance_NeedsAllFourElementsInTheLastSeven()
        {
            Assert.IsTrue(NatureBalance.AllElementsWithinWindow(
                new[] { "water_drop", "wind_breeze", "light_soft", "seed_single" }));
            // Seed fell out of the 7-round window.
            Assert.IsFalse(NatureBalance.AllElementsWithinWindow(new[]
            {
                "seed_single", "water_drop", "wind_breeze", "light_soft",
                "water_rain", "wind_passage", "light_beam", "water_drop"
            }));
            Assert.IsFalse(NatureBalance.AllElementsWithinWindow(new string[0]));
        }

        [Test]
        public void ResolveAmplified_DoublesStrengthOnTheSameFootprint()
        {
            var state = WorldState.CreateInitial();
            int anchor = Sites.ById("meadow").CenterCellId;

            long Total(ResolvedRecipe r) => r.WaterSchedule.Values.SelectMany(l => l).Sum(kv => (long)kv.Value);
            var rain = RecipeResolver.Resolve("water_rain", anchor, state);
            var rain2 = LuckySeven.ResolveAmplified("water_rain", anchor, state);
            Assert.AreEqual(Total(rain) * 2, Total(rain2));
            CollectionAssert.AreEqual(rain.WaterSchedule.Keys.ToList(), rain2.WaterSchedule.Keys.ToList());

            var wind = RecipeResolver.Resolve("wind_breeze", anchor, state);
            var wind2 = LuckySeven.ResolveAmplified("wind_breeze", anchor, state);
            Assert.AreEqual(wind.StrengthQ * 2, wind2.StrengthQ);
            Assert.AreEqual(wind.FieldCells.Count, wind2.FieldCells.Count);

            var seed = RecipeResolver.Resolve("seed_scatter", anchor, state);
            var seed2 = LuckySeven.ResolveAmplified("seed_scatter", anchor, state);
            Assert.IsTrue(seed.Eligible && seed2.Eligible);
            Assert.GreaterOrEqual(seed2.SeedCells.Count, seed.SeedCells.Count);
            Assert.LessOrEqual(seed2.SeedCells.Count, seed.SeedCells.Count * 2);
            CollectionAssert.IsSubsetOf(seed.SeedCells, seed2.SeedCells);
            Assert.AreEqual(seed2.SeedCells.Count, seed2.SeedCells.Distinct().Count());
        }

        [Test]
        public void IsRainbow_OnlyForLuckySevenWithABloom()
        {
            var bloomed = new SimulationResult();
            bloomed.Events.Add(new GardenEvent { Kind = "Bloomed" });
            var grew = new SimulationResult();
            grew.Events.Add(new GardenEvent { Kind = "BudFormed" });
            Assert.IsTrue(LuckySeven.IsRainbow(true, bloomed));
            Assert.IsFalse(LuckySeven.IsRainbow(false, bloomed));
            Assert.IsFalse(LuckySeven.IsRainbow(true, grew));
        }

        [Test]
        public void Coordinator_OffersSevenOnlyAfterAMatchedWish_AndRestoresIt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "journal.bin");
            var clock = new FixedClock();
            var journal = new FileJournal(path, clock);
            var coord = new GardenCoordinator(journal, new FixedEntropy("lucky"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            Assert.IsFalse(coord.LuckySevenOffered, "no chance on the first round");
            Assert.IsNull(coord.AddLuckySeven());

            // Wish for face 1 (a long shot, 1/6) until a wish comes true (lucky-seven.v2: only long shots earn it).
            RollOutcome outcome = null;
            for (int i = 0; i < 60; i++)
            {
                coord.CommitIntent("face", "1");
                outcome = coord.Roll();
                Assert.NotNull(outcome);
                coord.Prepare();
                if (outcome.IntentMatched) break;
                Assert.AreEqual(outcome.SeasonMetNow, coord.LuckySevenOffered,
                    "a missed wish offers the seventh face only when the season's wish came true (season.v1)");
            }
            Assert.IsTrue(outcome.IntentMatched, "fixture should produce a matched wish within 60 rolls");
            Assert.IsTrue(coord.LuckySevenOffered);

            string seventh = coord.AddLuckySeven();
            Assert.NotNull(seventh);
            Assert.AreEqual(7, coord.CurrentFaces.Faces.Length);
            Assert.AreEqual(seventh, coord.CurrentFaces.Faces[6]);
            Assert.IsNull(coord.AddLuckySeven(), "only one seventh face per round");

            // The seventh face is journaled before the intent and the roll.
            var kinds = journal.ReadAll().Entries.Select(e => e.Kind).ToList();
            Assert.AreEqual("LuckySevenAdded", kinds.Last());

            // Restore mid-round keeps the published seven faces.
            var restored = new GardenCoordinator(new FileJournal(path, clock), new FixedEntropy("unused"), new SequentialIds());
            restored.Restore(new FileJournal(path, clock).ReadAll());
            Assert.AreEqual(CoordinatorPhase.Prepared, restored.Phase);
            CollectionAssert.AreEqual(coord.CurrentFaces.Faces, restored.CurrentFaces.Faces);
            Assert.IsTrue(restored.LuckySevenOffered);

            coord.CommitIntent("face", "7");
            var seven = coord.Roll();
            Assert.NotNull(seven);
            Assert.AreEqual(7, seven.FaceCount);
            Assert.That(seven.FaceId, Is.InRange(1, 7));
            Assert.AreEqual(seven.FaceId == 7, seven.LuckySeven);
            Assert.AreEqual(seven.FaceId == 7, seven.IntentMatched);

            var history = HistoryProjector.Project(journal.ReadAll().Entries, coord.RunId);
            var last = history.Last();
            Assert.AreEqual(7, last.FaceCount);
            Assert.AreEqual(seven.LuckySeven, last.LuckySeven);
            Assert.AreEqual(seven.IntentMatched, last.IntentMatched);
            Directory.Delete(dir, true);
        }

        private sealed class MemoryJournal : IJournalWriter
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

        /// <summary>
        /// Measurement, not a pass/fail rule: how often the special moments occur for a player
        /// who always wishes for the most common element and always takes the Lucky Seven.
        /// Rates are written to the test output for the design record.
        /// </summary>
        [Test]
        public void Measure_SpecialMomentRates()
        {
            const int runs = 6, rollsPerRun = 120;
            int rolls = 0, matched = 0, offered = 0, sevenRolled = 0, rainbows = 0, butterflyRounds = 0;
            var residentRounds = new Dictionary<string, int>();
            var seedsBy = new Dictionary<string, int>();
            int ecoGrowth = 0, finalPlants = 0, finalFlowers = 0, harmonySum = 0, mycoGrowth = 0;
            var stars = new Dictionary<int, int>();
            for (int run = 0; run < runs; run++)
            {
                var journal = new MemoryJournal();
                var coord = new GardenCoordinator(journal, new FixedEntropy("measure-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                var rolled = new List<string>();
                for (int i = 0; i < rollsPerRun; i++)
                {
                    if (coord.LuckySevenOffered) { offered++; coord.AddLuckySeven(); }
                    string best = coord.CurrentFaces.Faces
                        .GroupBy(f => RecipeCatalog.ElementOf(f).ToString().ToLowerInvariant())
                        .OrderByDescending(g => g.Count()).First().Key;
                    coord.CommitIntent("family", best);
                    var o = coord.Roll();
                    if (o == null) { coord.Prepare(); continue; }
                    rolls++;
                    if (o.IntentMatched) matched++;
                    if (o.LuckySeven) sevenRolled++;
                    if (PrayerStar.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology)) rainbows++; // rainbow.v3 (the game rule)
                    rolled.Add(o.RecipeId);
                    if (NatureBalance.AllElementsWithinWindow(rolled)) butterflyRounds++;
                    foreach (var v in o.Ecology.Visits)
                        residentRounds[v.Kind] = residentRounds.TryGetValue(v.Kind, out int c) ? c + 1 : 1;
                    foreach (var ef in o.Ecology.Effects)
                    {
                        if (ef.Seed) seedsBy[ef.Kind] = seedsBy.TryGetValue(ef.Kind, out int c2) ? c2 + 1 : 1;
                        ecoGrowth += ef.Amount;
                        if (ef.Kind == Ecology.Mycorrhiza) mycoGrowth += ef.Amount;
                    }
                    harmonySum += o.Ecology.Harmony.Value;
                    stars[o.Ecology.Harmony.Star] = stars.TryGetValue(o.Ecology.Harmony.Star, out int sc) ? sc + 1 : 1;
                    coord.Prepare();
                }
                finalPlants += coord.State.Plants.Count;
                finalFlowers += coord.State.Plants.Count(pl => pl.Stage == PlantStage.Flower);
            }
            string report = $"rolls={rolls} wishMatched={matched} ({100.0 * matched / rolls:0.0}%) " +
                            $"sevenOffered={offered} ({100.0 * offered / rolls:0.0}%) sevenRolled={sevenRolled} " +
                            $"rainbows={rainbows} (1 per {(rainbows > 0 ? rolls / (double)rainbows : double.PositiveInfinity):0} rolls) " +
                            $"butterflyRounds={butterflyRounds} ({100.0 * butterflyRounds / rolls:0.0}%)";
            report += $" | residents(% of rolls): " + string.Join(", ",
                          residentRounds.OrderBy(k => k.Key).Select(k => $"{k.Key} {100.0 * k.Value / rolls:0}")) +
                      " | ecoSeeds: " + string.Join(", ", seedsBy.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")) +
                      $" | ecoGrowth/roll {ecoGrowth / (double)rolls:0.0}" +
                      $" | after {rollsPerRun} rolls: plants {finalPlants / (double)runs:0.0}, flowers {finalFlowers / (double)runs:0.0}" +
                      $" | harmony mean {harmonySum / (double)rolls:0.00}, stars " +
                      string.Join(", ", stars.OrderBy(k => k.Key).Select(k => $"{k.Key}:{100.0 * k.Value / rolls:0}%")) +
                      $" | mycorrhiza growth/roll {mycoGrowth / (double)rolls:0.0}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-MEASURE " + report);
            Assert.Greater(rolls, 0);
        }

        [Test]
        public void PrayerSpirits_VisitOnlyPlantsTheRollReached()
        {
            var coord = new GardenCoordinator(new MemoryJournal(), new FixedEntropy("prayer"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int withTargets = 0;
            for (int i = 0; i < 30; i++)
            {
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                var cells = PrayerSpirits.ReachedPlantCells(o.Simulation);
                Assert.LessOrEqual(cells.Count, PrayerSpirits.MaxSpirits);
                Assert.AreEqual(cells.Count, cells.Distinct().Count());
                var plantCells = new HashSet<int>(o.Simulation.PostState.Plants.Select(pl => pl.CellId));
                foreach (int c in cells) Assert.IsTrue(plantCells.Contains(c), "spirit target without a plant: " + c);
                if (cells.Count > 0) withTargets++;
                coord.Prepare();
            }
            Assert.Greater(withTargets, 0);
            Assert.AreEqual(0, PrayerSpirits.ReachedPlantCells(null).Count);
        }

        [Test]
        public void SevenFaceRolls_AreExactlyUniformOverAcceptedWords()
        {
            // Accepted words are [0, 4294967292), an exact multiple of 7, so every face
            // has the same count of words. Check a full block at both ends of the range.
            var counts = new int[8];
            for (uint w = 0; w < 7000; w++) counts[LuckySeven.RollD7(Words(w))]++;
            for (uint w = 4294967292u - 7000; w < 4294967292u; w++) counts[LuckySeven.RollD7(Words(w))]++;
            Assert.AreEqual(0, counts[0]);
            for (int f = 1; f <= 7; f++) Assert.AreEqual(2000, counts[f], "face " + f);
            Assert.AreEqual(0u, 4294967292u % 7u);
        }
    }
}
