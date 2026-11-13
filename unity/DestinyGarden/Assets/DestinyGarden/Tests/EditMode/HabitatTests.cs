using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class HabitatTests
    {
        private sealed class Memory : IJournalWriter
        {
            public List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => ""; public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Kind = kind, RunId = runId, RoundId = roundId, Payload = payload, Seq = NextSeq };
                Entries.Add(e); return e;
            }
        }
        private static WorldState Soil(int humus = 0)
        {
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.WorldRevision = 1; s.Ecosystem = EcosystemState.CreateCurrent();
            s.Ecosystem.Habitat.LinkedCare = false;
            for (int c = 0; c < 256; c++) { s.SoilWater[c] = GardenMapData.SoilCapacity[c] * 600 / 1000; s.Ecosystem.Habitat.Humus[c] = humus; }
            return s;
        }
        private static PlantState Plant(WorldState s, int c, string species = "strawberry")
        {
            var p = new PlantState { PlantId = species + c, Species = species, CellId = c, Growth = 1000, Stage = PlantStage.Leaf };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); return p;
        }
        private static int Site(WorldState s, string species = "strawberry") => Enumerable.Range(0, 256)
            .First(c => GardenMapData.Plantable[c] == 1 && HabitatRules.Suitability(Flora.Get(species), s, c) >= 700 &&
                Enumerable.Range(0, 256).Any(n => GardenMapData.Plantable[n] == 1 && LivingEcosystem.Distance(c, n) == 1 && HabitatRules.Suitability(Flora.Get(species), s, n) >= 700));
        private static EcologyResult Result(WorldState s) => new EcologyResult { PostState = s };

        [Test]
        public void Decomposition_SplitsFiniteLitterIntoHumusAndNutrients_WithoutDuplicatingMatter()
        {
            var s = Soil(); int c = Site(s); s.Ecosystem.Litter[c] = 10; int before = s.Ecosystem.Nutrients[c];
            long water = s.TotalWater(); var r = Result(s); HabitatRules.Decompose(s, c, Ecology.Worm, 3, r);
            Assert.AreEqual(7, s.Ecosystem.Litter[c]); Assert.AreEqual(1, s.Ecosystem.Habitat.Humus[c]); Assert.AreEqual(before + 2, s.Ecosystem.Nutrients[c]);
            Assert.AreEqual(before + 10, s.Ecosystem.Litter[c] + s.Ecosystem.Nutrients[c] + s.Ecosystem.Habitat.Humus[c]);
            Assert.AreEqual(water, s.TotalWater()); Assert.AreEqual(1, r.Relations.Single(x => x.Kind == "humus-formed").Amount);
            Assert.AreEqual(2, r.Relations.Single(x => x.Kind == "decomposed").Amount);
        }

        [Test]
        public void Decomposition_RespectsBothStorageCaps_AndLeavesUnusableLitterInPlace()
        {
            var s = Soil(); int c = Site(s); s.Ecosystem.Nutrients[c] = 100; s.Ecosystem.Habitat.Humus[c] = 99; s.Ecosystem.Litter[c] = 10;
            HabitatRules.Decompose(s, c, Ecology.Worm, 3, Result(s));
            Assert.AreEqual(100, s.Ecosystem.Habitat.Humus[c]); Assert.AreEqual(100, s.Ecosystem.Nutrients[c]); Assert.AreEqual(9, s.Ecosystem.Litter[c]);
            HabitatRules.Decompose(s, c, Ecology.Worm, 3, Result(s)); Assert.AreEqual(9, s.Ecosystem.Litter[c]);
        }

        [Test]
        public void Humus_ReturnsNutrientsSlowly_OnlyUnderSuitableMoistureAndAvailableCapacity()
        {
            var s = Soil(); int c = Site(s); s.WorldRevision = c % 4; s.Ecosystem.Habitat.Humus[c] = 4;
            var r = Result(s); HabitatRules.Begin(s, r);
            Assert.AreEqual(3, s.Ecosystem.Habitat.Humus[c]); Assert.AreEqual(19, s.Ecosystem.Nutrients[c]);
            Assert.AreEqual(1, r.Relations.Single(x => x.Kind == "humus-mineralised").Amount);
            s.WorldRevision++; HabitatRules.Begin(s, Result(s)); Assert.AreEqual(3, s.Ecosystem.Habitat.Humus[c]);
            s.WorldRevision = c % 4; s.SoilWater[c] = 0; HabitatRules.Begin(s, Result(s)); Assert.AreEqual(3, s.Ecosystem.Habitat.Humus[c]);
            s.SoilWater[c] = GardenMapData.SoilCapacity[c] * 600 / 1000; s.Ecosystem.Nutrients[c] = 100;
            HabitatRules.Begin(s, Result(s)); Assert.AreEqual(3, s.Ecosystem.Habitat.Humus[c]);
        }

        [Test]
        public void SoilKeepsThePioneersWork_AfterThePioneerHasGone()
        {
            var s = Soil(); int c = Site(Soil(100)); int hard = Flora.Hardness(s, c);
            var p = Plant(s, c, "dandelion"); s.Ecosystem.Habitat.Humus[c] = 30;
            s.Plants.Remove(p); s.Ecosystem.Cycles.Remove(p.PlantId);
            Assert.AreEqual(Math.Max(0, hard - 60), Flora.Hardness(s, c));
            s.Ecosystem.Habitat.Humus[c] = 0; Assert.AreEqual(hard, Flora.Hardness(s, c));
        }

        [Test]
        public void LastingHumus_CanMakeRoomForAPlantThatCouldNotRootInTheOriginalSoil()
        {
            var s = Soil(); var lily = Flora.Get("lily");
            int c = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && Flora.Hardness(s, n) > lily.MaxHardness &&
                Flora.Hardness(s, n) <= lily.MaxHardness + 100 && Flora.Drainage(s, n) >= lily.DrainMin);
            Assert.IsFalse(Flora.Fits(lily, s, c)); s.Ecosystem.Habitat.Humus[c] = 50; Assert.IsTrue(Flora.Fits(lily, s, c));
        }

        [Test]
        public void Suitability_GradesTheSite_ButNeverOverridesHardSpeciesLimits()
        {
            var s = Soil(); var t = Flora.Get("daisy"); int c = Site(Soil(100), "daisy");
            int before = HabitatRules.Suitability(t, s, c); s.Ecosystem.Habitat.Humus[c] = 50;
            Assert.GreaterOrEqual(HabitatRules.Suitability(t, s, c), before);
            foreach (var species in Flora.All)
                for (int cell = 0; cell < 256; cell++)
                    if (GardenMapData.Plantable[cell] == 1)
                    {
                        int value = HabitatRules.Suitability(species, s, cell);
                        Assert.That(value, Is.InRange(0, 1000)); Assert.AreEqual(Flora.Fits(species, s, cell), value >= 600);
                    }
            s.SoilWater[c] = GardenMapData.SoilCapacity[c]; Assert.AreEqual(0, HabitatRules.Suitability(t, s, c));
        }

        [Test]
        public void Runners_UseTheParentsReservesNutrientsAndGrowth_AndKeepTheSameSpecies()
        {
            var s = Soil(100); int c = Site(s); var parent = Plant(s, c); s.Ecosystem.Habitat.RootReserves[parent.PlantId] = 5;
            int nutrients = s.Ecosystem.Nutrients[c]; var r = Result(s); HabitatRules.End(s, r, n => "child-" + n);
            var child = s.Plants.Single(p => p.PlantId.StartsWith("child-", StringComparison.Ordinal));
            Assert.AreEqual("strawberry", child.Species); Assert.AreEqual(HabitatRules.Vegetative, child.OriginKind); Assert.IsNull(child.SeedSourceRoundId);
            Assert.AreEqual(250, child.Growth); Assert.AreEqual(900, parent.Growth); Assert.AreEqual(nutrients - 2, s.Ecosystem.Nutrients[c]);
            Assert.AreEqual(0, s.Ecosystem.Habitat.RootReserves[parent.PlantId]);
            Assert.AreEqual(parent.PlantId, s.Ecosystem.Succession.Ancestry[child.PlantId].ParentPlantId);
            Assert.IsNull(s.Ecosystem.Succession.Ancestry[child.PlantId].SeedId); Assert.AreEqual("stolon", s.Ecosystem.Succession.Ancestry[child.PlantId].Carrier);
            Assert.AreEqual(0, s.Ecosystem.Succession.SeedBank.Count); Assert.IsTrue(r.Relations.Any(x => x.Kind == "clonal-rooted"));
            Assert.That(EcologyInspection.ReadPlants(s).Single(x => x.PlantId == child.PlantId).Origin, Does.Contain("つる"));
        }

        [Test]
        public void Mint_RhizomesHaveASeparateOriginFromSeedsAndRunners()
        {
            var s = Soil(100); int c = Site(s, "mint"); var parent = Plant(s, c, "mint"); s.Ecosystem.Habitat.RootReserves[parent.PlantId] = 5;
            HabitatRules.End(s, Result(s), n => "rhizome-child");
            var child = s.Plants.Single(x => x.PlantId == "rhizome-child");
            Assert.AreEqual("mint", child.Species); Assert.AreEqual("rhizome", s.Ecosystem.Succession.Ancestry[child.PlantId].Carrier);
            Assert.That(EcologyInspection.ReadPlants(s).Single(x => x.PlantId == child.PlantId).Origin, Does.Contain("地下茎"));
        }

        [Test]
        public void RootsWaitForFoodRecoveryAndSpace_WithoutPayingForAFailedAttempt()
        {
            var s = Soil(100); int c = Site(s); var p = Plant(s, c); s.Ecosystem.Habitat.RootReserves[p.PlantId] = 6;
            s.Ecosystem.Nutrients[c] = 1; HabitatRules.End(s, Result(s), null);
            Assert.AreEqual(1, s.Plants.Count); Assert.AreEqual(1000, p.Growth); Assert.AreEqual(1, s.Ecosystem.Nutrients[c]);
            Assert.That(HabitatInspection.RootStatus(s, p), Does.Contain("養分"));
            s.Ecosystem.Nutrients[c] = 10; s.Ecosystem.Cycles[p.PlantId].Stress = 12; HabitatRules.End(s, Result(s), null);
            Assert.AreEqual(1, s.Plants.Count); Assert.That(HabitatInspection.RootStatus(s, p), Does.Contain("回復"));
            s.Ecosystem.Cycles[p.PlantId].Stress = 0;
            for (int n = 0; n < 256; n++) if (GardenMapData.Plantable[n] == 1 && LivingEcosystem.Distance(c, n) == 1) Plant(s, n, "daisy");
            int count = s.Plants.Count; HabitatRules.End(s, Result(s), null); Assert.AreEqual(count, s.Plants.Count); Assert.AreEqual(10, s.Ecosystem.Nutrients[c]);
            Assert.That(HabitatInspection.RootStatus(s, p), Does.Contain("空き地"));
        }

        [Test]
        public void SeedEmergenceAndRootSpread_ShareTheExistingTwoBirthBudget()
        {
            var s = Soil(100); int c = Site(s); var p = Plant(s, c); s.Ecosystem.Habitat.RootReserves[p.PlantId] = 12;
            foreach (int target in Enumerable.Range(0, 256).Where(n => GardenMapData.Plantable[n] == 1 && n != c).Take(2))
                s.Ecosystem.Succession.SeedBank.Add(new RestingSeed { SeedId = "s-" + target, Species = "dandelion", CellId = target, Age = 3, ParentPlantId = "earlier" });
            var r = Result(s); var before = s.Clone(); SuccessionRules.Awaken(s, before, r, n => "seed-" + n); HabitatRules.End(s, r, n => "root-" + n);
            Assert.AreEqual(2, r.Effects.Count(x => x.Seed)); Assert.IsFalse(r.Relations.Any(x => x.Kind == "clonal-rooted")); Assert.AreEqual(1000, p.Growth);
        }

        [Test]
        public void ASettledHabitatChange_IsRecordedOnlyAfterContinuityHasGrown()
        {
            var s = Soil(); int c = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1);
            string first = LivingEcosystem.Habitat(s, c);
            for (int i = 0; i < 6; i++) { HabitatRules.End(s, Result(s), null); s.WorldRevision++; }
            Assert.AreEqual(6, HabitatInspection.Continuity(s, c));
            s.SoilWater[c] = GardenMapData.SoilCapacity[c] * 850 / 1000; s.Ecosystem.WetAge[c] = 3;
            Assert.AreEqual(0, HabitatInspection.Continuity(s, c), "old history must not be presented as the new habitat's age");
            var r = Result(s); HabitatRules.End(s, r, null);
            var changed = r.Relations.Single(x => x.Kind == "habitat-shifted" && x.ToCell == c);
            Assert.AreEqual(first, changed.FromHabitat); Assert.AreEqual("水辺", changed.ToHabitat); Assert.AreEqual(6, changed.Amount);
            Assert.AreEqual(1, HabitatInspection.Continuity(s, c));
        }

        [Test]
        public void Roots_CanCrossARealEcotone_AndTheSavedContextIsNotRecomputedLater()
        {
            WorldState s = null; PlantState parent = null; int target = -1;
            foreach (int c in Enumerable.Range(0, 254))
            {
                if (c % 16 >= 14 || Flora.IsRockShade(c) || Flora.IsRockShade(c + 1) || Enumerable.Range(c, 3).Any(n => GardenMapData.Plantable[n] != 1)) continue;
                var trial = Soil(100); var p = Plant(trial, c); Plant(trial, c + 2, "sunflower");
                if (HabitatRules.Suitability(Flora.Get(p.Species), trial, c) < 700 || HabitatRules.Suitability(Flora.Get(p.Species), trial, c + 1) < 700) continue;
                s = trial; parent = p; target = c + 1; break;
            }
            Assert.NotNull(s, "the map needs an actual grass/shade boundary for the fixture");
            for (int n = 0; n < 256; n++) if (GardenMapData.Plantable[n] == 1 && n != target && s.PlantAt(n) == null && LivingEcosystem.Distance(parent.CellId, n) == 1) Plant(s, n, "daisy");
            s.Ecosystem.Habitat.RootReserves[parent.PlantId] = 6; var r = Result(s); HabitatRules.End(s, r, n => "edge-child");
            var link = r.Relations.Single(x => x.Kind == "clonal-rooted");
            Assert.AreEqual(target, link.ToCell); Assert.AreEqual("花の草地", link.FromHabitat); Assert.AreEqual("草木の陰", link.ToHabitat);
            var payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(Ecology.ToPayload(r))).Parse();
            var entry = new JournalEntry { Kind = "Resolved", RunId = "r", RoundId = "round", Payload = new Dictionary<string, object>
                { ["worldRevision"] = s.WorldRevision, ["postState"] = WorldStateSerializer.ToPayload(s), ["ecology"] = payload } };
            s.Plants.Clear();
            var saved = HistoryProjector.Project(new[] { entry }, "r").Single().Relations.Single(x => x.Kind == "clonal-rooted");
            Assert.That(EcologyObservations.Describe(saved), Does.Contain("花の草地から草木の陰"));
        }

        [Test]
        public void Observation_ReportsRealEdgesAndRootNeeds_WithoutChangingTheState()
        {
            var s = Soil(100); int c = Site(s); var p = Plant(s, c); var hash = Hashing.StateHash(s);
            foreach (var edge in HabitatInspection.Edges(s))
            { Assert.AreNotEqual(edge.From, edge.To); Assert.Greater(edge.Contacts, 0); }
            foreach (string place in HabitatRules.Labels) Assert.IsTrue(HabitatInspection.Edges(s, place).All(e => e.From == place || e.To == place));
            Assert.That(HabitatInspection.RootStatus(s, p), Does.Contain("蓄え")); Assert.That(HabitatInspection.Describe(s), Does.Contain("腐植"));
            Assert.AreEqual(hash, Hashing.StateHash(s));
        }

        [Test]
        public void SoilMemory_IsDeepClonedAndCanonicallySaved_AndAbsentInEarlierWorlds()
        {
            var s = Soil(20); int c = Site(s); var p = Plant(s, c); s.Ecosystem.Habitat.RootReserves[p.PlantId] = 6;
            HabitatRules.End(s, Result(s), null); var clone = s.Clone();
            clone.Ecosystem.Habitat.Humus[c] = 0; clone.Ecosystem.Habitat.LastHabitat[c] = -1; clone.Ecosystem.Habitat.RootReserves.Clear();
            Assert.AreEqual(20, s.Ecosystem.Habitat.Humus[c]); Assert.GreaterOrEqual(s.Ecosystem.Habitat.LastHabitat[c], 0);
            Assert.IsTrue(s.Ecosystem.Habitat.RootReserves.ContainsKey(p.PlantId));
            var round = WorldStateSerializer.FromPayload((Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(WorldStateSerializer.ToPayload(s))).Parse());
            Assert.AreEqual(Hashing.StateHash(s), Hashing.StateHash(round));
            var old = EcosystemState.CreateCurrent(); old.Habitat = null; Assert.IsFalse(EcosystemSerializer.ToPayload(old).ContainsKey("habitat"));
        }

        [Test]
        public void V6Garden_KeepsTheExactInitialAnd96RollHashes()
        {
            var s = WorldState.CreateInitial(); s.Ecosystem = EcosystemState.CreateCurrent(); s.Ecosystem.Habitat = null;
            Assert.AreEqual("9fa9c77ef7aa922143e84873086c10ca0a05304773950b3fed2289b9c6529aaf", Hashing.StateHash(s));
            var m = new Memory(); var ids = new SequentialIds(); m.Append("RunStarted", ids.NewId("run"), null, new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(s) });
            var coord = new GardenCoordinator(m, new FixedEntropy("v6-golden"), ids); coord.Restore(new JournalReadResult { ChainValid = true, Entries = m.Entries });
            for (int i = 0; i < 96; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            Assert.AreEqual("2963c9a052d431e2a321286ef846fa2e57fd742407a2a6fcde0da25c390c5f00", Hashing.StateHash(coord.State));
            Assert.IsNull(coord.State.Ecosystem.Habitat);
        }

        [Test]
        public void LivingSoilsAndClonalPatches_StayBounded_AndSurviveJsonRestoreWithoutDivergence()
        {
            var events = new HashSet<string>(); var signatures = new List<string>();
            for (int garden = 0; garden < 2; garden++)
            {
                List<string> Play(bool restore)
                {
                    var m = new Memory(); var ids = new SequentialIds(); var entropy = new FixedEntropy("habitat-" + garden);
                    var coord = new GardenCoordinator(m, entropy, ids);
                    var initial = WorldState.CreateInitial(); initial.Ecosystem = EcosystemState.CreateCurrent(); initial.Ecosystem.Habitat.LinkedCare = false;
                    m.Append("RunStarted", ids.NewId("run"), null, new Dictionary<string, object>
                    { ["initialState"] = WorldStateSerializer.ToPayload(initial), ["ecologyPolicyVersion"] = HabitatRules.RulesVersion });
                    coord.Restore(new JournalReadResult { ChainValid = true, Entries = m.Entries });
                    Assert.AreEqual(HabitatRules.RulesVersion, m.Entries[0].Payload["ecologyPolicyVersion"]);
                    var hashes = new List<string>(); int clones = 0, changes = 0, humified = 0;
                    for (int i = 0; i < 384; i++)
                    {
                        coord.Prepare(); coord.CommitIntent("none", null); var result = coord.Roll(); Assert.NotNull(result);
                        var s = coord.State; var h = s.Ecosystem.Habitat; Assert.AreEqual(result.Simulation.PostState.TotalWater(), s.TotalWater());
                        Assert.IsTrue(h.Humus.All(x => x >= 0 && x <= 100)); Assert.IsTrue(h.StableAge.All(x => x >= 0 && x <= 96));
                        Assert.IsTrue(h.LastHabitat.All(x => x >= -1 && x <= 4)); Assert.IsTrue(h.RootReserves.Values.All(x => x >= 0 && x <= 12));
                        Assert.IsTrue(h.RootReserves.Keys.All(id => s.Plants.Any(p => p.PlantId == id)));
                        int roots = result.Ecology.Relations.Count(x => x.Kind == "clonal-rooted");
                        Assert.LessOrEqual(roots, 1); Assert.LessOrEqual(roots + result.Ecology.Effects.Count(x => x.Seed), 2);
                        Assert.AreEqual(s.Plants.Count, s.Plants.Select(p => p.CellId).Distinct().Count());
                        foreach (var child in s.Plants.Where(p => p.OriginKind == HabitatRules.Vegetative))
                        { Assert.NotNull(Flora.Get(child.Species)); Assert.IsTrue(s.Ecosystem.Succession.Ancestry.ContainsKey(child.PlantId)); }
                        foreach (var link in result.Ecology.Relations) events.Add(link.Kind);
                        clones += roots; changes += result.Ecology.Relations.Count(x => x.Kind == "habitat-shifted");
                        humified += result.Ecology.Relations.Where(x => x.Kind == "humus-formed").Sum(x => x.Amount);
                        hashes.Add(Hashing.StateHash(s));
                        if (restore && i % 64 == 63)
                        {
                            var records = m.Entries.Select(e => new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
                                Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() }).ToList();
                            coord = new GardenCoordinator(m, entropy, ids); coord.Restore(new JournalReadResult { ChainValid = true, Entries = records });
                            Assert.AreEqual(hashes.Last(), Hashing.StateHash(coord.State));
                        }
                    }
                    Assert.Greater(humified, 0); Assert.Greater(changes, 0);
                    TestContext.WriteLine($"garden {garden}, restore={restore}: plants={coord.State.Plants.Count}, clones={clones}, habitat-changes={changes}, humified={humified}, retained-humus={coord.State.Ecosystem.Habitat.Humus.Sum()}");
                    return hashes;
                }
                var straight = Play(false); CollectionAssert.AreEqual(straight, Play(true)); signatures.Add(straight.Last());
            }
            Assert.AreNotEqual(signatures[0], signatures[1]);
            foreach (string kind in new[] { "humus-formed", "humus-mineralised", "clonal-rooted", "habitat-shifted" }) Assert.Contains(kind, events.ToList());
        }
    }
}
