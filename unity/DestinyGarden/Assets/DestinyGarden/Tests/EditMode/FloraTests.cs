using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class FloraTests
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

        private static int SoilCell(System.Func<int, bool> pred)
        {
            for (int c = 0; c < GardenMapData.CellCount; c++)
                if (GardenMapData.SoilCapacity[c] > 0 && pred(c)) return c;
            return -1;
        }

        [Test]
        public void Softeners_LowerHardness_DrainersRaiseDrainage()
        {
            var s = WorldState.CreateInitial();
            s.Plants.Clear();
            int c = SoilCell(x => GardenMapData.Plantable[x] == 1 && GardenMapData.Plantable[x + 1] == 1);
            int hard0 = Flora.Hardness(s, c), drain0 = Flora.Drainage(s, c);
            s.Plants.Add(new PlantState { PlantId = "p1", CellId = c + 1, Stage = PlantStage.Leaf, Species = "dandelion" });
            Assert.AreEqual(System.Math.Max(0, hard0 - 150), Flora.Hardness(s, c));
            s.Plants.Add(new PlantState { PlantId = "p2", CellId = c, Stage = PlantStage.Leaf, Species = "sunflower" });
            Assert.AreEqual(System.Math.Min(1000, drain0 + 150), Flora.Drainage(s, c));
            // A sprout counts half.
            s.Plants[0].Stage = PlantStage.Sprout;
            Assert.AreEqual(System.Math.Max(0, hard0 - 75), Flora.Hardness(s, c));
        }

        [Test]
        public void ChooseSpecies_InheritsAFittingParent_AndAlwaysFits()
        {
            var s = WorldState.CreateInitial();
            int checkedCells = 0;
            for (int c = 0; c < GardenMapData.CellCount; c++)
            {
                if (GardenMapData.Plantable[c] == 0 || s.PlantAt(c) != null) continue;
                var seed = new PlantState { PlantId = "seed-" + c, CellId = c, OriginKind = "ecology" };
                var fit = Flora.FittingAt(s, c);
                string chosen = Flora.ChooseSpecies(s, seed, "bee", null, n => 0);
                if (fit.Count == 0) Assert.AreEqual("dandelion", chosen);
                else CollectionAssert.Contains(fit.Select(t => t.Id).ToList(), chosen);
                if (fit.Count > 1)
                {
                    var parent = fit[fit.Count - 1].Id;
                    // Inheritance weight dominates: with the top of the range it must be the parent.
                    int total = fit.Count * 2 + 10 + fit.Sum(t => System.Math.Max(0, t.Bee) * 2);
                    string inherited = Flora.ChooseSpecies(s, seed, "bee", parent, n => n - 1);
                    Assert.AreEqual(parent, inherited, "cell " + c + " total " + total);
                }
                checkedCells++;
            }
            Assert.Greater(checkedCells, 10);
        }

        [Test]
        public void Butterflies_NeverVisitFlowersTheyAvoid()
        {
            var s = WorldState.CreateInitial();
            foreach (var p in s.Plants) { p.Stage = PlantStage.Flower; p.Growth = 2500; p.Species = "foxglove"; }
            var all = new[] { "water_drop", "wind_breeze", "light_soft", "seed_single" };
            var visits = Ecology.Presence(s, Weathers.All[0], all, null);
            Assert.IsFalse(visits.Any(v => v.Kind == Ecology.Butterfly), "foxglove is avoided by butterflies");
            Assert.IsTrue(visits.Any(v => v.Kind == Ecology.Bee), "bees love foxglove");
            foreach (var p in s.Plants) p.Species = "cosmos";
            visits = Ecology.Presence(s, Weathers.All[0], all, null);
            Assert.IsTrue(visits.Any(v => v.Kind == Ecology.Butterfly));
        }

        [Test]
        public void EveryPlant_GetsAFittingSpecies_AndTheGardenDiversifies()
        {
            var mem = new Memory();
            var coord = new GardenCoordinator(mem, new FixedEntropy("flora"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            var everSeen = new HashSet<string>();
            for (int i = 0; i < 120; i++)
            {
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                coord.Prepare();
                if (o == null) continue;
                foreach (var p in o.Ecology.PostState.Plants)
                {
                    Assert.NotNull(p.Species, "species after the ecology step");
                    Assert.NotNull(Flora.Get(p.Species));
                    everSeen.Add(p.Species);
                }
                foreach (var g in o.Ecology.Effects.Where(e => e.Kind == Ecology.Germinate))
                    Assert.NotNull(Flora.Get(g.Species));
            }
            TestContext.WriteLine("species seen: " + string.Join(", ", everSeen.OrderBy(x => x)));
            UnityEngine.Debug.Log("DG-FLORA species seen (" + everSeen.Count + "): " + string.Join(", ", everSeen.OrderBy(x => x)));
            Assert.GreaterOrEqual(everSeen.Count, 5);

            // Restoring from the journal keeps every species.
            var restored = new GardenCoordinator(new Memory(), new FixedEntropy("unused"), new SequentialIds());
            restored.Restore(new JournalReadResult { ChainValid = true, Entries = mem.Entries });
            CollectionAssert.AreEquivalent(
                coord.State.Plants.Select(p => p.CellId + ":" + p.Species).ToList(),
                restored.State.Plants.Select(p => p.CellId + ":" + p.Species).ToList());
        }

        [Test]
        public void Species_IsOmittedFromStatePayloadUntilKnown()
        {
            var s = WorldState.CreateInitial();
            string before = Hashing.StateHash(s);
            var round = WorldStateSerializer.FromPayload(WorldStateSerializer.ToPayload(s));
            Assert.AreEqual(before, Hashing.StateHash(round), "old states keep their exact hash");
            s.Plants[0].Species = "daisy";
            var withSpecies = WorldStateSerializer.FromPayload(WorldStateSerializer.ToPayload(s));
            Assert.AreEqual("daisy", withSpecies.Plants.First(p => p.PlantId == s.Plants[0].PlantId).Species);
        }

        [Test]
        public void Leaning_FollowsTheRecentBlessings()
        {
            var iris = Flora.Get("iris"); var cosmos = Flora.Get("cosmos"); var rose = Flora.Get("wildrose");
            var dandelion = Flora.Get("dandelion");
            var waters = new[] { "water_drop", "water_rain", "water_drop" };
            Assert.AreEqual(3, Flora.Leaning(iris, waters), "iris loves wet soil");
            Assert.AreEqual(0, Flora.Leaning(cosmos, waters));
            Assert.AreEqual(2, Flora.Leaning(cosmos, new[] { "light_soft", "light_beam" }), "cosmos needs sun");
            Assert.AreEqual(1, Flora.Leaning(rose, new[] { "seed_single" }), "seed leans to berry plants");
            Assert.AreEqual(1, Flora.Leaning(dandelion, new[] { "wind_breeze" }), "wind leans to wind-sown seeds");
            Assert.AreEqual(2, Flora.Leaning(dandelion, new[] { "wind_breeze", "seed_single" }), "seed also leans to self-sowers");
            Assert.AreEqual(1, Flora.Leaning(Flora.Get("buttercup"), new[] { "water_drop" }), "water leans to moisture lovers (from 300)");
            Assert.AreEqual(0, Flora.Leaning(iris, null));
            // Only the last eight rolls count.
            var old = Enumerable.Repeat("water_rain", 5).Concat(Enumerable.Repeat("light_soft", 8)).ToList();
            Assert.AreEqual(0, Flora.Leaning(iris, old));
        }

        /// <summary>Weights as ChooseSpecies sees them: a picker that records the total and returns a fixed draw.</summary>
        private static string ChooseAt(WorldState s, PlantState seed, int draw, out int total)
        {
            int t = 0;
            string chosen = Flora.ChooseSpecies(s, seed, "die", null, n => { t = n; return Math.Min(draw, n - 1); },
                Array.Empty<string>());
            total = t;
            return chosen;
        }

        [Test]
        public void Pioneer_StepsBack_AndCrowdingThinsTheSameSpecies()
        {
            var s = new WorldState();
            // A plantable cell where many species fit.
            int cell = -1;
            for (int c = 0; c < GardenMapData.CellCount && cell < 0; c++)
            {
                if (GardenMapData.Plantable[c] != 1 || GardenMapData.SoilCapacity[c] <= 0) continue;
                s.SoilWater[c] = GardenMapData.SoilCapacity[c] / 2;
                if (Flora.FittingAt(s, c).Count >= Flora.PioneerStepsBackAt && Flora.FittingAt(s, c).Any(t => t.Id == Flora.Pioneer)) cell = c;
                else s.SoilWater[c] = 0;
            }
            Assert.GreaterOrEqual(cell, 0, "a cell where several species fit");
            var fit = Flora.FittingAt(s, cell);
            var seed = new PlantState { PlantId = "seed", CellId = cell, OriginKind = "ecology" };
            ChooseAt(s, seed, 0, out int total);
            Assert.AreEqual(fit.Count * 2 - 1, total, "every fitting species weighs 2, the pioneer 1");

            // Crowding: three plants of the first fitting species nearby take its weight from 2 to max(1, 2*3/6) = 1.
            string first = fit[0].Id;
            int placed = 0;
            for (int c = 0; c < GardenMapData.CellCount && placed < 3; c++)
            {
                if (c == cell) continue;
                int d = Math.Abs(c % 16 - cell % 16) + Math.Abs(c / 16 - cell / 16);
                if (d > Flora.CrowdingReach) continue;
                s.Plants.Add(new PlantState { PlantId = "n" + c, CellId = c, Stage = PlantStage.Seed, Species = first });
                placed++;
            }
            Assert.AreEqual(3, placed);
            var fitAfter = Flora.FittingAt(s, cell);
            CollectionAssert.AreEqual(fit.Select(t => t.Id).ToList(), fitAfter.Select(t => t.Id).ToList(), "seeds do not change the soil");
            ChooseAt(s, seed, 0, out int crowdedTotal);
            Assert.AreEqual(total - (first == Flora.Pioneer ? 0 : 1), crowdedTotal);
        }

        [Test]
        public void AnsweredWish_LeansThisRollsSeeds()
        {
            var s = new WorldState();
            int cell = -1;
            for (int c = 0; c < GardenMapData.CellCount && cell < 0; c++)
            {
                if (GardenMapData.Plantable[c] != 1 || GardenMapData.SoilCapacity[c] <= 0 || Flora.IsRockShade(c)) continue;
                s.SoilWater[c] = GardenMapData.SoilCapacity[c] / 2;
                if (Flora.FittingAt(s, c).Any(t => Flora.Suits(t, ElementId.Light))) cell = c; else s.SoilWater[c] = 0;
            }
            Assert.GreaterOrEqual(cell, 0);
            var seed = new PlantState { PlantId = "seed", CellId = cell, OriginKind = "ecology" };
            int plain = 0, answered = 0;
            // Under the blessing rules (recent blessings given, here none yet).
            Flora.ChooseSpecies(s, seed, "die", null, n => { plain = n; return 0; }, new string[0]);
            Flora.ChooseSpecies(s, seed, "die", null, n => { answered = n; return 0; }, new string[0], "light_soft");
            int suited = Flora.FittingAt(s, cell).Count(t => Flora.Suits(t, ElementId.Light));
            Assert.AreEqual(plain + suited * Flora.AnsweredWishLeaning, answered, "each sun-loving fitting species gains the answered wish's weight");
        }
    }
}
