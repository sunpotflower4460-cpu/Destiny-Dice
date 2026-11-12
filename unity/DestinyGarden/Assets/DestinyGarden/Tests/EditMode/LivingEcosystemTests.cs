using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class LivingEcosystemTests
    {
        private sealed class Memory : IJournalWriter
        {
            public List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var entry = new JournalEntry { Kind = kind, RunId = runId, RoundId = roundId, Payload = payload, Seq = NextSeq };
                Entries.Add(entry); return entry;
            }
        }

        private static WorldState Soil(int moisture = 600)
        {
            var s = WorldState.CreateInitial(); s.Plants.Clear();
            s.WorldRevision = 1; s.Ecosystem = EcosystemState.CreateInitial();
            for (int c = 0; c < GardenMapData.CellCount; c++) s.SoilWater[c] = GardenMapData.SoilCapacity[c] * moisture / 1000;
            return s;
        }
        private static int Cell(WorldState s, string species) => Enumerable.Range(0, GardenMapData.CellCount)
            .First(c => GardenMapData.Plantable[c] == 1 && Flora.Fits(Flora.Get(species), s, c));
        private static PlantState Plant(WorldState s, int cell, string species, int growth = 2500)
        {
            var p = new PlantState { PlantId = species + "-" + cell, CellId = cell, Species = species, Growth = growth,
                Stage = growth >= 2500 ? PlantStage.Flower : PlantStage.Leaf };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); return p;
        }
        private static EcologyResult Step(WorldState s, string recipe = "water_drop", WeatherDefinition weather = null, byte[] bytes = null) =>
            Ecology.Apply(s, new EcologyContext { RoundId = "fixture", CurrentRecipeId = recipe, Weather = weather ?? Weathers.All[0],
                RecentRecipeIds = new[] { "water_drop", "wind_breeze", "light_soft", "seed_single" } }, bytes ?? new byte[64], null);

        [Test]
        public void Decomposers_ConvertFiniteLitter_WithoutCreatingMatterOrMovingWater()
        {
            var s = Soil();
            int c = Enumerable.Range(0, 256).First(x => GardenMapData.Plantable[x] == 1 && Flora.IsRockShade(x));
            s.Ecosystem.Litter[c] = 32;
            var hash = Hashing.StateHash(s);
            var r = Step(s);
            var e = r.PostState.Ecosystem;
            Assert.Greater(r.Relations.Count(x => x.Kind == "decomposed"), 1);
            int amount = r.Relations.Where(x => x.Kind == "decomposed").Sum(x => x.Amount);
            Assert.AreEqual(32 - amount, e.Litter[c]);
            Assert.AreEqual(18 + amount, e.Nutrients[c]);
            Assert.AreEqual(s.TotalWater(), r.PostState.TotalWater());
            Assert.AreEqual(hash, Hashing.StateHash(s), "input remains untouched");
        }

        [Test]
        public void RootNodules_EnrichNeighbours_OnlyWhenTheLegumeIsSuited()
        {
            var s = Soil(400); int c = Cell(s, "clover");
            Plant(s, c, "clover", 1000);
            var r = Step(s);
            var fixedN = r.Relations.Where(x => x.Kind == "nitrogen-fixed").ToList();
            Assert.Greater(fixedN.Count, 1);
            Assert.IsTrue(fixedN.All(x => LivingEcosystem.Distance(c, x.ToCell) <= 1 && x.Amount == 2));
            s.SoilWater[c] = GardenMapData.SoilCapacity[c];
            Assert.IsFalse(Step(s).Relations.Any(x => x.Kind == "nitrogen-fixed"));
        }

        [Test]
        public void Fungi_ShareExistingNutrients_AndAvoidSaturatedRoots()
        {
            var s = Soil();
            int c = Enumerable.Range(0, 254).First(x => x % 16 < 14 && GardenMapData.Plantable[x] == 1 &&
                GardenMapData.Plantable[x + 1] == 1 && GardenMapData.Plantable[x + 2] == 1);
            Plant(s, c, "dandelion", 1000); Plant(s, c + 1, "dandelion", 1000); Plant(s, c + 2, "dandelion", 1000);
            s.Ecosystem.Nutrients[c] = 0; s.Ecosystem.Nutrients[c + 2] = 30;
            var r = Step(s);
            var shared = r.Relations.Where(x => x.Kind == "nutrient-shared").ToList();
            Assert.AreEqual(1, shared.Count);
            Assert.AreEqual(c + 2, shared[0].FromCell); Assert.AreEqual(c, shared[0].ToCell); Assert.AreEqual(3, shared[0].Amount);
            int consumed = r.Effects.Count(x => x.Kind == Ecology.Thrive);
            Assert.AreEqual(s.Ecosystem.Nutrients.Sum() - consumed, r.PostState.Ecosystem.Nutrients.Sum());
            s.SoilWater[c + 1] = GardenMapData.SoilCapacity[c + 1];
            Assert.IsFalse(LivingEcosystem.Networks(s).SelectMany(n => n).Contains(c + 1));
        }

        [Test]
        public void Pollination_PrecedesFruit_AndBirdsCarryTheExactParentSpecies()
        {
            var s = Soil(); var parent = Plant(s, Cell(s, "wildrose"), "wildrose");
            var life = s.Ecosystem.Cycles[parent.PlantId]; life.BloomAge = 2; life.Pollen = 1;
            var first = Step(s);
            Assert.IsTrue(first.Relations.Any(x => x.Kind == "pollinated"));
            Assert.IsTrue(first.Relations.Any(x => x.Kind == "fruited"));
            Assert.IsFalse(first.Has(Ecology.Bird), "a bird needs ripe fruit, not merely a flower");
            var second = Step(first.PostState);
            var seeds = second.Effects.Where(x => x.Seed).ToList();
            Assert.Greater(seeds.Count, 0);
            Assert.IsTrue(seeds.All(x => x.Kind == Ecology.Bird));
            foreach (var seed in seeds)
            {
                Assert.AreEqual("wildrose", second.PostState.PlantAt(seed.CellId).Species);
                Assert.IsTrue(Flora.Fits(Flora.Get("wildrose"), first.PostState, seed.CellId));
                Assert.IsTrue(second.Relations.Any(x => x.Kind == "seed-dispersed" && x.FromCell == parent.CellId && x.ToCell == seed.CellId));
            }
            Assert.AreEqual(first.PostState.Ecosystem.Cycles[parent.PlantId].Seeds - seeds.Count,
                second.PostState.Ecosystem.Cycles[parent.PlantId].Seeds);
        }

        [Test]
        public void Wind_OnlyCarriesRipeWindSeeds_AndStaysInsideTheEastwardCorridor()
        {
            var s = Soil(); var parent = Plant(s, Cell(s, "dandelion"), "dandelion");
            s.Ecosystem.Cycles[parent.PlantId] = new PlantCycle { Seeds = 3, Fruited = true };
            var r = Step(s, "wind_breeze");
            Assert.IsTrue(r.Has(Ecology.Fluff));
            Assert.Greater(r.Effects.Count(x => x.Seed), 0);
            foreach (var seed in r.Effects.Where(x => x.Seed))
            {
                Assert.AreEqual("dandelion", r.PostState.PlantAt(seed.CellId).Species);
                Assert.That(seed.CellId % 16 - parent.CellId % 16, Is.InRange(2, 5));
                Assert.LessOrEqual(Math.Abs(seed.CellId / 16 - parent.CellId / 16), 1);
            }
            s.Plants[0].Species = "daisy";
            Assert.IsFalse(Step(s, "wind_breeze").Has(Ecology.Fluff));
        }

        [Test]
        public void WetlandVisitors_RequirePersistentWetness_AndLocalRefuges()
        {
            var s = Soil(750); int c = Cell(s, "daisy");
            s.SurfaceWater[c] = 10;
            Assert.IsFalse(LivingEcosystem.Presence(s, Weathers.All[1], null).Any(x => x.Kind == Ecology.Dragonfly));
            s.Ecosystem.WetAge[c] = 4;
            Assert.IsTrue(LivingEcosystem.Presence(s, Weathers.All[1], null).Any(x => x.Kind == Ecology.Dragonfly));
            Assert.IsFalse(LivingEcosystem.Presence(s, Weathers.All[1], null).Any(x => x.Kind == LivingEcosystem.Frog));
            s.Ecosystem.CoverAge[c] = 6; s.Ecosystem.Aphids[c] = 4;
            Assert.IsTrue(LivingEcosystem.Presence(s, Weathers.All[1], null).Any(x => x.Kind == LivingEcosystem.Frog));
        }

        [Test]
        public void Predators_NeedCoverAndPrey_AndReduceTheActualColony()
        {
            var s = Soil(); int c = Cell(s, "daisy"); Plant(s, c, "daisy", 1000);
            s.Ecosystem.Aphids[c] = 8;
            Assert.IsFalse(LivingEcosystem.Presence(s, Weathers.All[0], null).Any(x => x.Kind == LivingEcosystem.Ladybird));
            s.Ecosystem.CoverAge[c] = 8;
            var r = Step(s);
            Assert.IsTrue(r.Has(LivingEcosystem.Ladybird));
            int eaten = r.Relations.Where(x => x.Kind == "predation" && x.ToCell == c).Sum(x => x.Amount);
            Assert.Greater(eaten, 0);
            Assert.AreEqual(9 - eaten, r.PostState.Ecosystem.Aphids[c]);
        }

        [Test]
        public void Canopies_CreateNewShade_AndDisappearWhenTheCanopyIsGone()
        {
            var s = Soil(400);
            int c = Enumerable.Range(0, 255).First(x => x % 16 < 15 && GardenMapData.Plantable[x] == 1 &&
                GardenMapData.Plantable[x + 1] == 1 && !Flora.IsRockShade(x));
            Assert.IsFalse(Flora.IsShade(s, c));
            var p = Plant(s, c + 1, "sunflower", 1000);
            Assert.IsTrue(Flora.IsShade(s, c));
            Assert.AreEqual("草木の陰", LivingEcosystem.Habitat(s, c));
            Assert.IsFalse(Flora.Fits(Flora.Get("cosmos"), s, c));
            s.Plants.Remove(p);
            Assert.IsFalse(Flora.IsShade(s, c));
        }

        [Test]
        public void Stress_CanRecover_AndLifetimesReturnMatterAndOpenSpace()
        {
            var s = Soil(400); var plant = Plant(s, Cell(s, "lavender"), "lavender", 1000);
            s.Ecosystem.Cycles[plant.PlantId].Stress = 11;
            s.SoilWater[plant.CellId] = GardenMapData.SoilCapacity[plant.CellId];
            var stressed = Step(s);
            Assert.IsTrue(stressed.Relations.Any(x => x.Kind == "stressed"));
            stressed.PostState.SoilWater[plant.CellId] = GardenMapData.SoilCapacity[plant.CellId] * 400 / 1000;
            var recovered = Step(stressed.PostState);
            Assert.IsTrue(recovered.Relations.Any(x => x.Kind == "recovered"));
            recovered.PostState.Ecosystem.Cycles[plant.PlantId].Age = PlantStrategies.Get(plant.Species).Longevity - 1;
            var retired = Step(recovered.PostState);
            Assert.IsNull(retired.PostState.PlantAt(plant.CellId));
            Assert.IsFalse(retired.PostState.Ecosystem.Cycles.ContainsKey(plant.PlantId));
            Assert.GreaterOrEqual(retired.PostState.Ecosystem.Litter[plant.CellId], 12);
            Assert.IsTrue(retired.Relations.Any(x => x.Kind == "returned-to-soil"));
        }

        [Test]
        public void Flowers_RestAndCanBloomAgain_WithoutChangingTheirSpecies()
        {
            var s = Soil(); var p = Plant(s, Cell(s, "daisy"), "daisy");
            s.Ecosystem.Cycles[p.PlantId].BloomAge = PlantStrategies.Get(p.Species).BloomRounds - 1;
            var r = Step(s);
            Assert.AreEqual(PlantStage.Leaf, r.PostState.PlantAt(p.CellId).Stage);
            Assert.AreEqual("daisy", r.PostState.PlantAt(p.CellId).Species);
            Assert.IsTrue(r.Relations.Any(x => x.Kind == "flower-rest"));
            Assert.Greater(r.PostState.Ecosystem.Litter[p.CellId], 0);
        }

        [Test]
        public void ExhaustedEntropy_IsReported_AndDoesNotInventDispersedSeeds()
        {
            var s = Soil(); int c = Cell(s, "daisy");
            var p = Plant(s, c, "daisy"); p.Species = null; p.OriginKind = "seedInput";
            var r = Step(s, bytes: new byte[0]);
            Assert.IsTrue(r.Exhausted);
            Assert.IsFalse(r.Effects.Any(x => x.Seed));
            Assert.AreEqual(Hashing.StateHash(r.PostState), Hashing.StateHash(Step(s, bytes: new byte[0]).PostState));
        }

        [Test]
        public void SaveRoundTrip_DeepClonesMemory_AndKeepsOldStateHashes()
        {
            var old = WorldState.CreateInitial();
            var oldPayload = WorldStateSerializer.ToPayload(old);
            Assert.IsFalse(oldPayload.ContainsKey("ecosystem"));
            Assert.AreEqual(Hashing.StateHash(old), Hashing.StateHash(WorldStateSerializer.FromPayload(oldPayload)));
            var s = Soil(); var p = Plant(s, Cell(s, "daisy"), "daisy");
            s.Ecosystem.Litter[p.CellId] = 17; s.Ecosystem.Cycles[p.PlantId].Pollen = 3;
            var clone = s.Clone(); clone.Ecosystem.Litter[p.CellId] = 1; clone.Ecosystem.Cycles[p.PlantId].Pollen = 0;
            Assert.AreEqual(17, s.Ecosystem.Litter[p.CellId]); Assert.AreEqual(3, s.Ecosystem.Cycles[p.PlantId].Pollen);
            var serialized = CanonicalJson.Write(WorldStateSerializer.ToPayload(s));
            var restored = WorldStateSerializer.FromPayload((Dictionary<string, object>)new SimpleJsonParser(serialized).Parse());
            Assert.AreEqual(Hashing.StateHash(s), Hashing.StateHash(restored));
        }

        [Test]
        public void LegacyGarden_ContinuesWithItsOriginalRulesAndGoldenHashes()
        {
            // Collected by running unmodified ac12b67 with the same provider and IDs.
            var state = WorldState.CreateInitial();
            Assert.AreEqual("c27986226e09f89eca6906985d1f65739ee8e9a369dec7cc3df958a2658513b1", Hashing.StateHash(state));
            var memory = new Memory(); var ids = new SequentialIds();
            string runId = ids.NewId("run");
            memory.Append("RunStarted", runId, null, new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(state) });
            var coord = new GardenCoordinator(memory, new FixedEntropy("legacy-golden"), ids);
            coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
            for (int i = 0; i < 24; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            Assert.IsNull(coord.State.Ecosystem, "old gardens never silently opt into v4");
            Assert.AreEqual("b7448dd6b371601884ca452f99b9956fd115ab8ae75f374c3a500ff856999d60", Hashing.StateHash(coord.State));
            Assert.AreEqual(Ecology.RulesVersion, ((Dictionary<string, object>)memory.Entries.Last().Payload["ecology"])["rulesVersion"]);
            Assert.IsTrue(coord.State.Plants.All(p => Flora.IndexOf(p.Species) < Flora.LegacySpeciesCount));
        }

        [Test]
        public void MultiGardenLongRun_IsDeterministic_BoundedAndResumable()
        {
            var ever = new HashSet<string>(); var residents = new HashSet<string>(); var links = new HashSet<string>();
            for (int garden = 0; garden < 4; garden++)
            {
                var memory = new Memory(); var entropy = new FixedEntropy("ecosystem-" + garden); var ids = new SequentialIds();
                var coord = new GardenCoordinator(memory, entropy, ids); coord.CreateRun();
                Assert.AreEqual(LivingEcosystem.RulesVersion, memory.Entries[0].Payload["ecologyPolicyVersion"]);
                int firstSeeds = 0;
                for (int round = 0; round < 240; round++)
                {
                    coord.Prepare(); coord.CommitIntent("none", null); var result = coord.Roll(); Assert.NotNull(result);
                    var r = result.Ecology; var e = r.PostState.Ecosystem;
                    Assert.AreEqual(result.Simulation.PostState.TotalWater(), r.PostState.TotalWater());
                    Assert.LessOrEqual(r.Effects.Count(x => x.Seed), 2);
                    Assert.AreEqual(r.PostState.Plants.Count, r.PostState.Plants.Select(x => x.CellId).Distinct().Count());
                    foreach (var array in new[] { e.Litter, e.Nutrients }) Assert.IsTrue(array.All(x => x >= 0 && x <= 100));
                    Assert.IsTrue(e.Aphids.All(x => x >= 0 && x <= 12));
                    Assert.IsTrue(e.CoverAge.All(x => x >= 0 && x <= 48));
                    Assert.IsTrue(e.WetAge.All(x => x >= 0 && x <= 12));
                    Assert.AreEqual(r.PostState.Plants.Count, e.Cycles.Count, "no stale individuals in ecological memory");
                    foreach (var p in r.PostState.Plants)
                    {
                        Assert.That(p.Growth, Is.InRange(0, 2500)); Assert.IsNotNull(Flora.Get(p.Species)); ever.Add(p.Species);
                    }
                    foreach (var visit in r.Visits) residents.Add(visit.Kind);
                    foreach (var link in r.Relations) links.Add(link.Kind);
                    if (round < 120) firstSeeds += r.Effects.Count(x => x.Seed);
                    if (round == 119)
                    {
                        // Use the same deterministic provider/id cursors after restore: any extra draw or id would diverge.
                        string stateHash = Hashing.StateHash(coord.State);
                        coord = new GardenCoordinator(memory, entropy, ids);
                        coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
                        Assert.AreEqual(stateHash, Hashing.StateHash(coord.State));
                    }
                }
                Assert.Greater(firstSeeds, 0, "reproduction occurs in each garden");
                var projected = HistoryProjector.Project(memory.Entries, coord.RunId).Last(x => x.Resolved);
                var payload = (Dictionary<string, object>)memory.Entries.Last().Payload["ecology"];
                Assert.AreEqual(EcologyObservations.Highlight(projected.Relations), EcologyObservations.Highlight(ReadLinks(payload)));
                TestContext.WriteLine($"garden {garden}: {coord.State.Plants.Count} living plants, {firstSeeds} dispersed seeds in first 120 rolls");
            }
            TestContext.WriteLine($"species {ever.Count}/24: {string.Join(",", ever.OrderBy(x => x))}");
            TestContext.WriteLine("residents: " + string.Join(",", residents.OrderBy(x => x)));
            TestContext.WriteLine("relations: " + string.Join(",", links.OrderBy(x => x)));
            Assert.GreaterOrEqual(ever.Count, 15);
            foreach (string kind in new[] { "pollinated", "fruited", "seed-dispersed", "decomposed", "predation", "nitrogen-fixed", "nutrient-shared", "flower-rest", "returned-to-soil" })
                Assert.Contains(kind, links.ToList());
        }

        private static List<EcologicalLink> ReadLinks(Dictionary<string, object> payload)
        {
            // Project the exact saved payload, including across the JSON integer representation.
            var data = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(payload)).Parse();
            var run = new JournalEntry { Kind = "Resolved", RunId = "r", RoundId = "round", Payload = new Dictionary<string, object>
            {
                ["worldRevision"] = 1, ["postState"] = WorldStateSerializer.ToPayload(Soil()), ["ecology"] = data
            } };
            return HistoryProjector.Project(new[] { run }, "r")[0].Relations;
        }
    }
}
