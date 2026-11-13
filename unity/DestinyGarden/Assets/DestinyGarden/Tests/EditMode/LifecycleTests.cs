using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class LifecycleTests
    {
        private static PlantState Flower(string id, int cell, string species, int bloomRolls) =>
            new PlantState { PlantId = id, CellId = cell, Growth = 2500, Stage = PlantStage.Flower, Species = species, BloomRolls = bloomRolls };

        [Test]
        public void Advance_CountsBloom_AndReturnsTheLongestPastFirst()
        {
            var s = new WorldState();
            s.Plants.Add(Flower("a", 10, "dandelion", 15));   // reaches 16 = span: due, 0 past
            s.Plants.Add(Flower("b", 11, "lavender", 42));    // 43 vs 40: 3 past
            s.Plants.Add(Flower("c", 12, "cosmos", 26));      // 27 vs 26: 1 past
            s.Plants.Add(new PlantState { PlantId = "d", CellId = 13, Growth = 900, Stage = PlantStage.Leaf, Species = "cosmos" });
            var gone = Lifecycle.Advance(s);
            Assert.AreEqual(Lifecycle.MaxReturnsPerRoll, gone.Count);
            Assert.AreEqual("b", gone[0].PlantId, "the one longest past its span returns first");
            CollectionAssert.AreEqual(new[] { "a", "c", "d" }, s.Plants.Select(p => p.PlantId).ToArray());
            Assert.AreEqual(16, s.PlantAt(10).BloomRolls, "the others wait for a later roll");
            Assert.AreEqual(0, s.PlantAt(13).BloomRolls, "only flowers count");
        }

        [Test]
        public void BloomRolls_IsSavedOnlyWhenCounted_AndRoundTrips()
        {
            var s = WorldState.CreateInitial();
            string before = Hashing.StateHash(s);
            var payload = WorldStateSerializer.ToPayload(s);
            Assert.IsFalse(((IEnumerable<object>)payload["plants"]).Cast<IDictionary<string, object>>().Any(p => p.ContainsKey("bloomRolls")),
                "states without counted blooms keep their old shape and hash");
            s.Plants[0].BloomRolls = 5;
            var back = WorldStateSerializer.FromPayload(WorldStateSerializer.ToPayload(s));
            Assert.AreEqual(5, back.Plants.First(p => p.PlantId == s.Plants[0].PlantId).BloomRolls);
            Assert.AreNotEqual(before, Hashing.StateHash(s));
            Assert.AreEqual(5, s.Clone().Plants[0].BloomRolls);
        }

        [Test]
        public void LongPlay_KeepsTheGardenOpen_AndTheDiceMeaningful()
        {
            var coord = new GardenCoordinator(new MemoryJournalForLifecycle(), new FixedEntropy("cycle"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int returned = 0, lateGerminations = 0, lateSeedFaces = 0;
            for (int i = 0; i < 160; i++)
            {
                if (i >= 100) lateSeedFaces += coord.CurrentFaces.Faces.Count(f => RecipeCatalog.ElementOf(f) == ElementId.Seed);
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                coord.Prepare();
                returned += o.Ecology.Effects.Count(e => e.Kind == Lifecycle.ReturnKind);
                Assert.LessOrEqual(o.Ecology.Effects.Count(e => e.Kind == Lifecycle.ReturnKind), Lifecycle.MaxReturnsPerRoll);
                foreach (var e in o.Ecology.Effects.Where(e => e.Kind == Lifecycle.ReturnKind))
                {
                    var now = o.Ecology.PostState.PlantAt(e.CellId);
                    bool fruit = o.Ecology.Effects.Exists(x => x.Kind == Lifecycle.FruitKind && x.CellId == e.CellId);
                    Assert.IsTrue(now == null || (fruit && now.Stage == PlantStage.Seed),
                        "a returned flower leaves its cell open, or its own seed when it was pollinated");
                }
                if (i >= 100) lateGerminations += o.Ecology.Effects.Count(e => e.Kind == Ecology.Germinate);
            }
            Assert.Greater(returned, 0, "flowers return to the soil in long play");
            Assert.Greater(lateGerminations, 0, "new plants still take root after 100 rolls");
            Assert.Greater(lateSeedFaces, 0, "seed faces come back to the die once places open");
        }

        [Test]
        public void Pollinated_IsSavedOnlyWhenTrue()
        {
            var s = WorldState.CreateInitial();
            string before = Hashing.StateHash(s);
            Assert.AreEqual(before, Hashing.StateHash(WorldStateSerializer.FromPayload(WorldStateSerializer.ToPayload(s))));
            s.Plants[0].Pollinated = true;
            Assert.IsTrue(WorldStateSerializer.FromPayload(WorldStateSerializer.ToPayload(s)).Plants.First(p => p.PlantId == s.Plants[0].PlantId).Pollinated);
            Assert.IsTrue(s.Clone().Plants[0].Pollinated);
        }

        [Test]
        public void OnlyPollinatedFlowers_LeaveASeedInTheirPlace()
        {
            var coord = new GardenCoordinator(new MemoryJournalForLifecycle(), new FixedEntropy("fruit"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int fruits = 0, plainReturns = 0, inherited = 0;
            for (int i = 0; i < 200; i++)
            {
                coord.CommitIntent("none", null);
                var before = coord.State.Clone();
                var o = coord.Roll();
                coord.Prepare();
                var eco = o.Ecology;
                foreach (var ret in eco.Effects.Where(e => e.Kind == Lifecycle.ReturnKind))
                {
                    var gone = o.Simulation.PostState.PlantAt(ret.CellId);
                    var fruit = eco.Effects.FirstOrDefault(e => e.Kind == Lifecycle.FruitKind);
                    // Pollinated before this roll, visited in this roll's ecology step, or under this roll's rainbow eight.
                    bool visited = (gone != null && gone.Pollinated) ||
                                   eco.Visits.Any(v => (v.Kind == Ecology.Bee || v.Kind == Ecology.Butterfly) && v.Cells.Contains(ret.CellId)) ||
                                   eco.PrayerStar.Points >= 8;
                    if (fruit != null)
                    {
                        fruits++;
                        Assert.IsTrue(visited, "only a visited flower (or one under the rainbow) bears seed");
                        Assert.AreEqual(ret.CellId, fruit.CellId, "the seed takes root where its parent stood");
                        Assert.AreEqual(fruit.Species, eco.PostState.PlantAt(fruit.CellId).Species);
                        if (fruit.Species == ret.Species) inherited++;
                    }
                    else plainReturns++;
                }
            }
            Assert.Greater(fruits, 0, "pollinated flowers bear seed over 200 rolls");
            Assert.Greater(inherited, 0, "and often their own kind");
            TestContext.WriteLine($"fruits={fruits} inherited={inherited} plainReturns={plainReturns}");
        }

        [Test]
        public void IdlePlants_RootWhereTheyFit_AndWitherWhereTheyDoNot()
        {
            var coord = new GardenCoordinator(new MemoryJournalForLifecycle(), new FixedEntropy("idle"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int withered = 0, rooted = 0;
            for (int i = 0; i < 200; i++)
            {
                coord.CommitIntent("none", null);
                var before = coord.State.Clone();
                var o = coord.Roll();
                coord.Prepare();
                var w = o.Ecology.Effects.Where(e => e.Kind == Lifecycle.WitherKind).ToList();
                Assert.LessOrEqual(w.Count, Lifecycle.MaxWithersPerRoll);
                foreach (var e in w)
                {
                    withered++;
                    var was = before.PlantAt(e.CellId);
                    Assert.NotNull(was);
                    Assert.AreNotEqual(PlantStage.Flower, was.Stage);
                    Assert.GreaterOrEqual(was.IdleRolls, Lifecycle.WitherAfter - 1, "only a plant idle for the whole span withers");
                    Assert.IsFalse(Flora.Fits(Flora.Get(was.Species), o.Simulation.PostState, e.CellId), "only where its place no longer suits it");
                }
                foreach (var e in o.Ecology.Effects.Where(e => e.Kind == Lifecycle.RootKind))
                {
                    rooted++;
                    Assert.LessOrEqual(e.Amount, Lifecycle.RootGrowth);
                }
            }
            Assert.Greater(rooted, 0, "suited plants on dry soil put down roots");
            TestContext.WriteLine($"withered={withered} rootedEffects={rooted}");
        }

        [Test]
        public void RarerWishes_CallMoreSpirits()
        {
            var coord = new GardenCoordinator(new MemoryJournalForLifecycle(), new FixedEntropy("spirits"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int faceWishSpiritsMax = 0, familySpiritsMax = 0;
            for (int i = 0; i < 300; i++)
            {
                bool face = i % 2 == 0;
                int matching;
                if (face) { coord.CommitIntent("face", "3"); matching = 1; }
                else
                {
                    string el = RecipeCatalog.ElementOf(coord.CurrentFaces.Faces[0]).ToString().ToLowerInvariant();
                    coord.CommitIntent("family", el);
                    matching = coord.MatchingFaces;
                }
                int faces = coord.CurrentFaces.Faces.Length;
                var o = coord.Roll();
                coord.Prepare();
                var spirit = o.Ecology.Visit(Ecology.Spirit);
                if (!o.IntentMatched) { Assert.IsNull(spirit); continue; }
                int allowed = System.Math.Min(PrayerSpirits.MaxSpirits, (faces + matching - 1) / matching);
                Assert.LessOrEqual(spirit.Cells.Count, allowed);
                if (face) faceWishSpiritsMax = System.Math.Max(faceWishSpiritsMax, spirit.Cells.Count);
                else familySpiritsMax = System.Math.Max(familySpiritsMax, spirit.Cells.Count);
            }
            Assert.Greater(faceWishSpiritsMax, 3, "a one-face wish can call more than three spirits");
            TestContext.WriteLine($"faceWishSpiritsMax={faceWishSpiritsMax} familySpiritsMax={familySpiritsMax}");
        }

        private sealed class MemoryJournalForLifecycle : IJournalWriter
        {
            public string HeadHash => "";
            public long NextSeq => 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload) => new JournalEntry();
        }
    }
}
