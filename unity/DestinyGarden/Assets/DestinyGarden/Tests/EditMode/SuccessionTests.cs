using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class SuccessionTests
    {
        private sealed class Memory : IJournalWriter
        {
            public List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Kind = kind, RunId = runId, RoundId = roundId, Payload = payload, Seq = NextSeq };
                Entries.Add(e); return e;
            }
        }
        private static WorldState Soil()
        {
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.WorldRevision = 1;
            s.Ecosystem = EcosystemState.CreateInitial(); s.Ecosystem.Succession = new SuccessionState();
            for (int c = 0; c < 256; c++) s.SoilWater[c] = GardenMapData.SoilCapacity[c] * 600 / 1000;
            return s;
        }
        private static int Cell(WorldState s, string species = "dandelion") => Enumerable.Range(0, 256)
            .First(c => GardenMapData.Plantable[c] == 1 && Flora.Fits(Flora.Get(species), s, c));
        private static RestingSeed Seed(WorldState s, int cell, string species = "dandelion", int age = 0)
        {
            var seed = new RestingSeed { SeedId = "seed-" + s.Ecosystem.Succession.SeedBank.Count, Species = species, CellId = cell,
                Age = age, ParentPlantId = "parent-no-longer-here", ParentCellId = cell, SourceRoundId = "parent-round", BornRevision = 0, Carrier = Ecology.Fluff };
            s.Ecosystem.Succession.SeedBank.Add(seed); return seed;
        }
        private static EcologyResult Step(WorldState s, byte[] bytes = null)
        {
            return Ecology.Apply(s, new EcologyContext { RoundId = "round-" + s.WorldRevision, Weather = Weathers.All[0],
                CurrentRecipeId = "water_drop", RecentRecipeIds = new[] { "water_drop" } }, bytes ?? new byte[64], null);
        }
        private static PlantState Plant(WorldState s, int c, string species = "daisy")
        {
            var p = new PlantState { PlantId = "plant-" + c, CellId = c, Species = species, Growth = 2500, Stage = PlantStage.Flower };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); return p;
        }

        [Test]
        public void DormantSeed_WaitsTwoSteps_ThenUsesNutrientsAndKeepsItsParentsIdentity()
        {
            var s = Soil(); int c = Cell(s); Seed(s, c);
            var first = Step(s, new byte[0]); Assert.IsNull(first.PostState.PlantAt(c));
            Assert.AreEqual(1, first.PostState.Ecosystem.Succession.SeedBank[0].Age);
            first.PostState.WorldRevision++;
            var next = Step(first.PostState, new byte[0]); var child = next.PostState.PlantAt(c);
            Assert.NotNull(child); Assert.AreEqual("dandelion", child.Species); Assert.AreEqual(PlantStage.Sprout, child.Stage);
            Assert.AreEqual("parent-round", child.SeedSourceRoundId);
            Assert.AreEqual("parent-no-longer-here", next.PostState.Ecosystem.Succession.Ancestry[child.PlantId].ParentPlantId);
            Assert.AreEqual(16, next.PostState.Ecosystem.Nutrients[c]); Assert.AreEqual(0, next.PostState.Ecosystem.Succession.SeedBank.Count);
            Assert.That(EcologyInspection.ReadPlants(next.PostState).Single().Origin, Does.Contain("今はない親株"));
            Assert.IsFalse(next.Exhausted, "waking seeds uses no chance and no new entropy");
        }

        [Test]
        public void SeedBank_WaitsForOpenGroundAndFertility_ThenRespondsToChangedConditions()
        {
            var s = Soil(); int c = Cell(s); var seed = Seed(s, c, age: 4); var parent = Plant(s, c, "dandelion");
            Assert.That(SuccessionRules.WaitingFor(s, seed), Does.Contain("空き地"));
            s.Plants.Clear(); s.Ecosystem.Cycles.Clear(); s.Ecosystem.Nutrients[c] = 0;
            Assert.That(SuccessionRules.WaitingFor(s, seed), Does.Contain("養分"));
            var r = Step(s); Assert.IsNull(r.PostState.PlantAt(c));
            r.PostState.Ecosystem.Nutrients[c] = 2; r.PostState.WorldRevision++;
            var awake = Step(r.PostState);
            Assert.NotNull(awake.PostState.PlantAt(c)); Assert.AreEqual(0, awake.PostState.Ecosystem.Nutrients[c]);
            Assert.AreEqual(parent.Species, awake.PostState.PlantAt(c).Species);
        }

        [Test]
        public void ShadeSeeds_KeepTheirSpeciesWhileWaitingForTheRightHabitat()
        {
            var s = Soil(); int c = Cell(s, "cosmos"); var seed = Seed(s, c, "violet", 3); seed.Rested = true;
            Assert.That(SuccessionRules.WaitingFor(s, seed), Does.Contain("条件"));
            var first = Step(s); Assert.IsNull(first.PostState.PlantAt(c)); Assert.AreEqual("violet", first.PostState.Ecosystem.Succession.SeedBank[0].Species);
        }

        [Test]
        public void RestSeason_IsRememberedAcrossSave_AndSpringUnlocksGermination()
        {
            var s = Soil(); s.WorldRevision = 71; int c = Cell(s, "violet"); var seed = Seed(s, c, "violet", 8);
            Assert.That(SuccessionRules.WaitingFor(s, seed), Does.Contain("休息"));
            s.WorldRevision = 72; var autumn = Step(s);
            Assert.IsTrue(autumn.PostState.Ecosystem.Succession.SeedBank[0].Rested); Assert.IsNull(autumn.PostState.PlantAt(c));
            var restored = WorldStateSerializer.FromPayload((Dictionary<string, object>)new SimpleJsonParser(
                CanonicalJson.Write(WorldStateSerializer.ToPayload(autumn.PostState))).Parse());
            Assert.IsTrue(restored.Ecosystem.Succession.SeedBank[0].Rested);
            restored.WorldRevision = 96;
            Assert.NotNull(Step(restored).PostState.PlantAt(c));
        }

        [Test]
        public void ExpiringSeeds_ReturnToTheSoil_WithoutEverBecomingAnotherSpecies()
        {
            var s = Soil(); int c = Cell(s); Seed(s, c, age: SeedStrategies.Get("dandelion").Persistence);
            var r = Step(s);
            Assert.AreEqual(0, r.PostState.Ecosystem.Succession.SeedBank.Count); Assert.IsNull(r.PostState.PlantAt(c));
            Assert.AreEqual(1, r.PostState.Ecosystem.Litter[c]); Assert.IsTrue(r.Relations.Any(x => x.Kind == "seed-expired"));
        }

        [Test]
        public void AParentsLastRipeSeeds_SurviveItsDeath_AndGrowAfterItIsGone()
        {
            var s = Soil(); int c = Cell(s, "wildrose"); var p = Plant(s, c, "wildrose");
            s.Ecosystem.Cycles[p.PlantId] = new PlantCycle { Age = PlantStrategies.Get("wildrose").Longevity - 1, Seeds = 2, Fruited = true };
            var died = Step(s);
            Assert.IsNull(died.PostState.PlantAt(c)); Assert.Greater(died.PostState.Ecosystem.Succession.SeedBank.Count, 0);
            Assert.IsTrue(died.PostState.Ecosystem.Succession.SeedBank.All(x => x.ParentPlantId == p.PlantId));
            died.PostState.WorldRevision++; var wait = Step(died.PostState); wait.PostState.WorldRevision++;
            var next = Step(wait.PostState);
            Assert.IsTrue(next.PostState.Ecosystem.Succession.Ancestry.Values.Any(x => x.ParentPlantId == p.PlantId));
            Assert.IsFalse(next.PostState.Ecosystem.Cycles.ContainsKey(p.PlantId));
        }

        [Test]
        public void SeedBudget_CapsTheBankAndEachCell_AndNeverOverwritesExistingSeeds()
        {
            var s = Soil(); int c = Cell(s); var parent = Plant(s, c, "dandelion");
            var result = new EcologyResult { PostState = s };
            Assert.IsTrue(SuccessionRules.Store(s, parent, c, "gravity", "r1", result));
            Assert.IsTrue(SuccessionRules.Store(s, parent, c, "gravity", "r1", result));
            Assert.IsFalse(SuccessionRules.Store(s, parent, c, "gravity", "r1", result), "two deposits per roll");
            result = new EcologyResult { PostState = s };
            Assert.IsTrue(SuccessionRules.Store(s, parent, c, "gravity", "r2", result));
            Assert.IsFalse(SuccessionRules.HasSpace(s, c), "three seeds per cell");
            string first = s.Ecosystem.Succession.SeedBank[0].SeedId;
            int round = 3;
            foreach (int target in Enumerable.Range(0, 256).Where(x => GardenMapData.Plantable[x] == 1))
                while (SuccessionRules.HasSpace(s, target))
                {
                    Assert.IsTrue(SuccessionRules.Store(s, parent, target, "gravity", "r" + round++, new EcologyResult { PostState = s }));
                    if (s.Ecosystem.Succession.SeedBank.Count >= SuccessionRules.MaxBank) break;
                }
            Assert.AreEqual(SuccessionRules.MaxBank, s.Ecosystem.Succession.SeedBank.Count);
            Assert.AreEqual(first, s.Ecosystem.Succession.SeedBank[0].SeedId);
            Assert.IsFalse(SuccessionRules.HasSpace(s, c));
        }

        [Test]
        public void OnlyTwoSeedsWakePerStep_AndTwoSeedsNeverOccupyTheSameCell()
        {
            var s = Soil(); var cells = Enumerable.Range(0, 256).Where(x => GardenMapData.Plantable[x] == 1).Take(4).ToList();
            foreach (int c in cells) { Seed(s, c, age: 3); Seed(s, c, age: 3); }
            var r = Step(s);
            Assert.AreEqual(2, r.PostState.Plants.Count); Assert.AreEqual(6, r.PostState.Ecosystem.Succession.SeedBank.Count);
            Assert.AreEqual(2, r.PostState.Plants.Select(x => x.CellId).Distinct().Count());
        }

        [Test]
        public void Nectar_IsFinite_AndForagingConsumesOnlyWhatFlowersActuallyHave()
        {
            var s = Soil(); var p = Plant(s, Cell(s, "daisy")); var r = new EcologyResult { PostState = s };
            SuccessionRules.RefreshNectar(s, Weathers.All[0]);
            Assert.IsTrue(SuccessionRules.Forage(s, p, Ecology.Bee, r)); Assert.IsTrue(SuccessionRules.Forage(s, p, Ecology.Butterfly, r));
            Assert.IsFalse(SuccessionRules.Forage(s, p, Ecology.Bee, r));
            Assert.AreEqual(0, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar);
            Assert.AreEqual(2, r.Relations.Where(x => x.Kind == "nectar-fed").Sum(x => x.Amount));
            SuccessionRules.RefreshNectar(s, Weathers.All[2]); Assert.AreEqual(0, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar);
            for (int i = 0; i < 10; i++) SuccessionRules.RefreshNectar(s, Weathers.All[0]);
            Assert.AreEqual(6, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar);
        }

        [Test]
        public void Pollinators_ReachTheWholeMeadow_InsteadOfOnlyTheFirstThreeCells()
        {
            var s = Soil(); var cells = Enumerable.Range(0, 256).Where(c => GardenMapData.Plantable[c] == 1 && Flora.Fits(Flora.Get("daisy"), s, c)).Take(9).ToList();
            foreach (int c in cells) Plant(s, c);
            for (int i = 0; i < 3; i++) { s = Step(s).PostState; s.WorldRevision++; }
            Assert.AreEqual(9, s.Plants.Count(p => s.Ecosystem.Cycles[p.PlantId].Pollen >= 2));
            Assert.IsTrue(s.Ecosystem.Succession.Flowers.Values.All(x => x.Nectar >= 0 && x.Nectar <= 6));
            var hash = Hashing.StateHash(s);
            var one = Ecology.Presence(s, Weathers.All[0], new string[0], null);
            var two = Ecology.Presence(s, Weathers.All[0], new string[0], null);
            CollectionAssert.AreEqual(one.Select(x => x.Kind + string.Join(",", x.Cells)), two.Select(x => x.Kind + string.Join(",", x.Cells)));
            Assert.AreEqual(hash, Hashing.StateHash(s), "route selection is read-only");
        }

        [Test]
        public void Butterflies_RerouteWhenBeesHaveAlreadyUsedTheNectar()
        {
            var s = Soil();
            var cells = Enumerable.Range(0, 256).Where(c => GardenMapData.Plantable[c] == 1 && Flora.Fits(Flora.Get("daisy"), s, c)).Take(6);
            foreach (int c in cells) Plant(s, c);
            var r = Ecology.Apply(s, new EcologyContext { RoundId = "cloudy", Weather = Weathers.All[1],
                CurrentRecipeId = "water_drop", RecentRecipeIds = new[] { "water_drop" } }, new byte[64], null);
            Assert.AreEqual(3, r.Relations.Count(x => x.Kind == "nectar-fed" && x.Actor == Ecology.Bee));
            Assert.AreEqual(3, r.Relations.Count(x => x.Kind == "nectar-fed" && x.Actor == Ecology.Butterfly));
            Assert.AreEqual(6, r.Relations.Where(x => x.Kind == "nectar-fed").Select(x => x.ToCell).Distinct().Count());
            Assert.IsTrue(r.PostState.Ecosystem.Succession.Flowers.Values.All(x => x.Nectar == 0));
        }

        [Test]
        public void Observation_ExplainsActualProblems_WithoutChangingTheGarden()
        {
            var s = Soil(); int c = Cell(s, "daisy"); var p = Plant(s, c, "lavender");
            s.SoilWater[c] = GardenMapData.SoilCapacity[c]; s.Ecosystem.Nutrients[c] = 0; s.Ecosystem.Aphids[c] = 8;
            var hash = Hashing.StateHash(s); var report = EcologyInspection.ReadPlants(s).Single();
            Assert.Contains("土の水が多すぎる", report.Needs); Assert.Contains("養分が乏しい", report.Needs); Assert.Contains("アブラムシが多い", report.Needs);
            Assert.That(EcologyInspection.Describe(s), Does.Contain("ラベンダー")); Assert.AreEqual(hash, Hashing.StateHash(s));
            foreach (string place in EcologyInspection.Habitats)
                Assert.IsTrue(EcologyInspection.ReadPlants(s, place).All(x => x.Habitat == place));
        }

        [Test]
        public void NewMemory_IsDeepCloned_CanonicallySaved_AndOmittedForV4()
        {
            var s = Soil(); var p = Plant(s, Cell(s, "daisy")); Seed(s, p.CellId);
            SuccessionRules.RefreshNectar(s, Weathers.All[0]);
            s.Ecosystem.Succession.Ancestry[p.PlantId] = new PlantAncestry { ParentPlantId = "earlier", SeedId = "s", Carrier = "wind", BornRevision = 1 };
            var clone = s.Clone(); clone.Ecosystem.Succession.SeedBank[0].Age = 99;
            clone.Ecosystem.Succession.Flowers[p.PlantId].Nectar = 0; clone.Ecosystem.Succession.Ancestry[p.PlantId].ParentPlantId = "changed";
            Assert.AreEqual(0, s.Ecosystem.Succession.SeedBank[0].Age); Assert.AreEqual(2, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar);
            Assert.AreEqual("earlier", s.Ecosystem.Succession.Ancestry[p.PlantId].ParentPlantId);
            var restored = WorldStateSerializer.FromPayload((Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(WorldStateSerializer.ToPayload(s))).Parse());
            Assert.AreEqual(Hashing.StateHash(s), Hashing.StateHash(restored));
            Assert.IsFalse(EcosystemSerializer.ToPayload(EcosystemState.CreateInitial()).ContainsKey("succession"));
        }

        [Test]
        public void V4GoldenGarden_ContinuesWithItsExactOriginalHash()
        {
            var s = WorldState.CreateInitial(); s.Ecosystem = EcosystemState.CreateInitial();
            Assert.AreEqual("e0a4eabc212de46929090ca4cdbcef2d8b51936ddc7647a4f599b34b310b5ace", Hashing.StateHash(s));
            var memory = new Memory(); var ids = new SequentialIds(); memory.Append("RunStarted", ids.NewId("run"), null,
                new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(s) });
            var coord = new GardenCoordinator(memory, new FixedEntropy("v4-golden"), ids);
            coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
            for (int i = 0; i < 48; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            Assert.AreEqual("bc0bb990d677452a3baf62eb910b15908c8e1f4f6e41de888ba58c5cd61170f2", Hashing.StateHash(coord.State));
            Assert.IsNull(coord.State.Ecosystem.Succession);
        }

        [Test]
        public void LongRunningGardens_KeepLineagesAndBudgets_AndResumeWithoutDivergence()
        {
            var species = new HashSet<string>(); var events = new HashSet<string>();
            for (int garden = 0; garden < 2; garden++)
            {
                List<string> Play(bool resume)
                {
                    var memory = new Memory(); var ids = new SequentialIds(); var entropy = new FixedEntropy("succession-" + garden);
                    var coord = new GardenCoordinator(memory, entropy, ids);
                    var initial = WorldState.CreateInitial(); initial.Ecosystem = EcosystemState.CreateInitial(); initial.Ecosystem.Succession = new SuccessionState();
                    memory.Append("RunStarted", ids.NewId("run"), null, new Dictionary<string, object>
                    { ["initialState"] = WorldStateSerializer.ToPayload(initial), ["ecologyPolicyVersion"] = SuccessionRules.RulesVersion });
                    coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
                    Assert.AreEqual(SuccessionRules.RulesVersion, memory.Entries[0].Payload["ecologyPolicyVersion"]);
                    var hashes = new List<string>(); int emerged = 0, stored = 0, maxBank = 0;
                    for (int roll = 0; roll < 384; roll++)
                    {
                        coord.Prepare(); coord.CommitIntent("none", null); var outcome = coord.Roll(); Assert.NotNull(outcome);
                        var s = coord.State; var m = s.Ecosystem.Succession; var r = outcome.Ecology;
                        Assert.AreEqual(outcome.Simulation.PostState.TotalWater(), s.TotalWater());
                        Assert.LessOrEqual(m.SeedBank.Count, SuccessionRules.MaxBank); maxBank = Math.Max(maxBank, m.SeedBank.Count);
                        Assert.AreEqual(m.SeedBank.Count, m.SeedBank.Select(x => x.SeedId).Distinct().Count());
                        Assert.IsTrue(m.SeedBank.GroupBy(x => x.CellId).All(x => x.Count() <= 3));
                        Assert.IsTrue(m.Flowers.Values.All(x => x.Nectar >= 0 && x.Nectar <= 6));
                        Assert.IsTrue(m.Ancestry.Keys.All(id => s.Plants.Any(p => p.PlantId == id)));
                        Assert.IsTrue(m.Flowers.Keys.All(id => s.Plants.Any(p => p.PlantId == id)));
                        Assert.AreEqual(s.Plants.Count, s.Plants.Select(x => x.CellId).Distinct().Count());
                        Assert.LessOrEqual(SuccessionRules.Deposits(r), 2); Assert.LessOrEqual(r.Effects.Count(x => x.Seed), 2);
                        foreach (var seed in m.SeedBank) { Assert.NotNull(Flora.Get(seed.Species)); Assert.GreaterOrEqual(seed.Age, 0); }
                        foreach (var p in s.Plants) { Assert.NotNull(p.Species); species.Add(p.Species); }
                        foreach (var link in r.Relations) events.Add(link.Kind);
                        emerged += r.Relations.Count(x => x.Kind == "seed-awakened"); stored += SuccessionRules.Deposits(r);
                        hashes.Add(Hashing.StateHash(s));
                        if (resume && roll == 95)
                        {
                            coord = new GardenCoordinator(memory, entropy, ids);
                            coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
                            Assert.AreEqual(hashes.Last(), Hashing.StateHash(coord.State));
                        }
                    }
                    Assert.Greater(emerged, 0); Assert.Greater(stored, emerged);
                    TestContext.WriteLine($"garden {garden}, resume={resume}: plants={coord.State.Plants.Count}, stored={stored}, emerged={emerged}, max-bank={maxBank}");
                    return hashes;
                }
                CollectionAssert.AreEqual(Play(false), Play(true), "restoring never changes the future trajectory with the same inputs");
            }
            TestContext.WriteLine($"species {species.Count}/24; events: {string.Join(",", events.OrderBy(x => x))}");
            Assert.GreaterOrEqual(species.Count, 16);
            foreach (string kind in new[] { "seed-buried", "seed-awakened", "seed-expired", "nectar-fed", "predation", "decomposed" }) Assert.Contains(kind, events.ToList());
        }
    }
}
