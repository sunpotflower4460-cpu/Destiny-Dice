using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class CommunityTests
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
        private static WorldState Soil(int moisture = 600)
        {
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.WorldRevision = 1; s.Ecosystem = EcosystemState.CreateCurrent(); s.Ecosystem.Habitat = null;
            for (int c = 0; c < 256; c++) s.SoilWater[c] = GardenMapData.SoilCapacity[c] * moisture / 1000;
            return s;
        }
        private static List<int> Cluster(int count = 3)
        {
            int start = Enumerable.Range(0, 256).First(c => c % 16 < 16 - count && Enumerable.Range(c, count).All(n => GardenMapData.Plantable[n] == 1));
            return Enumerable.Range(start, count).ToList();
        }
        private static PlantState Plant(WorldState s, int cell, string species, bool flower = true)
        {
            var p = new PlantState { PlantId = species + cell, Species = species, CellId = cell, Stage = flower ? PlantStage.Flower : PlantStage.Leaf, Growth = flower ? 2500 : 1000 };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); s.Ecosystem.CoverAge[cell] = 8;
            s.Ecosystem.Succession.Flowers.Add(p.PlantId, new FlowerResource { Nectar = flower ? 6 : 0 }); return p;
        }
        private static EcologyContext Context(WorldState s) => new EcologyContext { RoundId = "r" + s.WorldRevision, Weather = Weathers.All[0],
            CurrentRecipeId = "water_drop", RecentRecipeIds = new[] { "water_drop" } };
        private static EcologyResult Result(WorldState s) => new EcologyResult { PostState = s };
        private static ResidentFamily Family(WorldState s, string kind, int cell, int brood = 0, int age = 0)
        {
            var f = new ResidentFamily { FamilyId = kind + ":" + cell, Kind = kind, CellId = cell, Brood = brood, BroodAge = age };
            s.Ecosystem.Succession.Community.Families.Add(f); return f;
        }

        [Test]
        public void Settlement_NeedsThreeConsecutiveSuitableSteps_AndAnActualHostPlant()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[0], "clover", false); Plant(s, cells[1], "daisy");
            for (int i = 0; i < 2; i++) { CommunityRules.BeforeActivity(s, Context(s), Result(s)); s.WorldRevision++; }
            Assert.IsFalse(s.Ecosystem.Succession.Community.Families.Any(f => f.Kind == Ecology.Butterfly));
            var r = Result(s); CommunityRules.BeforeActivity(s, Context(s), r);
            Assert.IsTrue(r.Relations.Any(x => x.Kind == "resident-settled" && x.Actor == Ecology.Butterfly));
            Assert.AreEqual(cells[0], s.Ecosystem.Succession.Community.Families.Single(f => f.Kind == Ecology.Butterfly).CellId);
            var noHost = Soil(); Plant(noHost, cells[0], "daisy");
            for (int i = 0; i < 5; i++) { CommunityRules.BeforeActivity(noHost, Context(noHost), Result(noHost)); noHost.WorldRevision++; }
            Assert.IsFalse(noHost.Ecosystem.Succession.Community.Families.Any(f => f.Kind == Ecology.Butterfly));
        }

        [Test]
        public void AFailedHabitat_BreaksTheSettlementStreak()
        {
            var s = Soil(); var cells = Cluster(); var host = Plant(s, cells[0], "clover", false); Plant(s, cells[1], "daisy");
            for (int i = 0; i < 2; i++) { CommunityRules.BeforeActivity(s, Context(s), Result(s)); s.WorldRevision++; }
            host.Stage = PlantStage.Seed; CommunityRules.BeforeActivity(s, Context(s), Result(s)); s.WorldRevision++;
            host.Stage = PlantStage.Leaf; CommunityRules.BeforeActivity(s, Context(s), Result(s));
            Assert.IsFalse(s.Ecosystem.Succession.Community.Families.Any(f => f.Kind == Ecology.Butterfly));
            Assert.AreEqual(1, s.Ecosystem.Succession.Community.Prospects.Single(p => p.Kind == Ecology.Butterfly).Streak);
        }

        [Test]
        public void PondFood_UsesExistingNutrientsAndBiomass_AndNeverMovesWater()
        {
            var s = Soil(800); int c = Cluster()[0]; var m = s.Ecosystem.Succession.Community; s.Ecosystem.WetAge[c] = 6;
            long water = s.TotalWater(); int n = s.Ecosystem.Nutrients[c]; var r = Result(s);
            CommunityRules.GrowPond(s, Context(s), r);
            Assert.AreEqual(n - 1, s.Ecosystem.Nutrients[c]); Assert.AreEqual(1, m.Algae[c]); Assert.AreEqual(1, m.AquaticPrey[c]);
            Assert.AreEqual(2, r.Relations.Single(x => x.Kind == "algae-grew").Amount);
            Assert.AreEqual(1, r.Relations.Single(x => x.Kind == "pond-food-fed").Amount);
            Assert.AreEqual(water, s.TotalWater());
            m.Algae[c] = 12; m.AquaticPrey[c] = 8; n = s.Ecosystem.Nutrients[c];
            CommunityRules.GrowPond(s, Context(s), Result(s));
            Assert.AreEqual(n, s.Ecosystem.Nutrients[c]); Assert.AreEqual(12, m.Algae[c]); Assert.AreEqual(8, m.AquaticPrey[c]);
        }

        [Test]
        public void ABriefWetPatch_DoesNotConjureThePondFoodWeb()
        {
            var s = Soil(800); var r = Result(s); CommunityRules.GrowPond(s, Context(s), r);
            Assert.IsTrue(s.Ecosystem.Succession.Community.Algae.All(x => x == 0));
            Assert.IsTrue(s.Ecosystem.Succession.Community.AquaticPrey.All(x => x == 0));
            Assert.IsFalse(r.Relations.Any(x => x.Kind == "algae-grew"));
        }

        [Test]
        public void Tadpoles_EatAlgae_WhileDragonflyNymphsEatAquaticPrey()
        {
            foreach (string kind in new[] { LivingEcosystem.Frog, Ecology.Dragonfly })
            {
                var s = Soil(800); int c = Cluster()[0]; s.Ecosystem.WetAge[c] = 6; s.Ecosystem.CoverAge[c] = 8;
                var m = s.Ecosystem.Succession.Community; m.Algae[c] = 6; m.AquaticPrey[c] = 6;
                var f = Family(s, kind, c, 2, 1); var r = Result(s); CommunityRules.AfterActivity(s, r);
                Assert.AreEqual(2, f.BroodAge);
                if (kind == LivingEcosystem.Frog)
                { Assert.AreEqual(4, m.Algae[c]); Assert.IsTrue(r.Relations.Any(x => x.Kind == "algae-eaten" && x.Actor == CommunityRules.Tadpole)); }
                else { Assert.IsTrue(r.Relations.Any(x => x.Kind == "pond-prey-eaten" && x.Actor == CommunityRules.Nymph && x.Amount == 2)); }
                Assert.IsTrue(m.Algae.All(x => x >= 0)); Assert.IsTrue(m.AquaticPrey.All(x => x >= 0));
            }
        }

        [Test]
        public void HungryJuveniles_DoNotDevelop_AndDoNotLiveForeverWithoutFood()
        {
            var s = Soil(800); int c = Cluster()[0]; s.Ecosystem.WetAge[c] = 6; s.Ecosystem.CoverAge[c] = 8;
            var f = Family(s, LivingEcosystem.Frog, c, 2, 1);
            for (int i = 0; i < 2; i++) { CommunityRules.AfterActivity(s, Result(s)); s.WorldRevision++; Assert.AreEqual(1, f.BroodAge); }
            var r = Result(s); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(0, f.Brood); Assert.IsTrue(r.Relations.Any(x => x.Kind == "brood-lost" && x.Amount == 2));
            Assert.AreEqual(2, s.Ecosystem.Litter[c]);
        }

        [Test]
        public void Caterpillars_UseHostLeaves_AndPreserveTheLivingRoot()
        {
            var s = Soil(); var cells = Cluster(); var host = Plant(s, cells[0], "clover", false); var flower = Plant(s, cells[1], "daisy");
            var f = Family(s, Ecology.Butterfly, cells[0], 2, 1); var r = Result(s);
            SuccessionRules.Forage(s, flower, Ecology.Butterfly, r); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(960, host.Growth); Assert.AreEqual(2, f.BroodAge);
            Assert.AreEqual(40, r.Relations.Single(x => x.Kind == "host-leaf-eaten").Amount);
            host.Growth = 760; f.BroodAge = 1; r = Result(s); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(750, host.Growth); Assert.AreEqual(PlantStage.Leaf, host.Stage); Assert.AreEqual(1, f.BroodAge);
        }

        [Test]
        public void HoverflyLarvae_EatRealAphids_WhileTheAdultsFeedOnNectar()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[0], "clover", false); var flower = Plant(s, cells[1], "daisy");
            s.Ecosystem.Aphids[cells[0]] = 2; var f = Family(s, LivingEcosystem.Hoverfly, cells[0], 2, 1); var r = Result(s);
            SuccessionRules.Forage(s, flower, LivingEcosystem.Hoverfly, r); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(0, s.Ecosystem.Aphids[cells[0]]); Assert.AreEqual(2, f.BroodAge); Assert.AreEqual(0, f.AdultHunger);
            Assert.IsTrue(r.Relations.Any(x => x.Kind == "predation" && x.Actor == CommunityRules.HoverflyLarva && x.Amount == 2));
        }

        [Test]
        public void EveryManagedKind_CanCompleteItsActualJuvenileStages()
        {
            foreach (string kind in CommunityRules.Kinds)
            {
                var s = Soil(800); var cells = Cluster(); var host = Plant(s, cells[0], "clover", false); var flower = Plant(s, cells[1], "daisy");
                s.Ecosystem.WetAge[cells[0]] = 6; s.Ecosystem.Aphids[cells[0]] = 12;
                var m = s.Ecosystem.Succession.Community; m.Algae[cells[0]] = 12; m.AquaticPrey[cells[0]] = 8;
                var f = Family(s, kind, cells[0], 2); var stages = new HashSet<string>(); bool emerged = false;
                for (int i = 0; i < CommunityRules.Development(kind); i++)
                {
                    stages.Add(CommunityRules.YoungKind(f));
                    var r = Result(s); CommunityRules.GrowPond(s, Context(s), r);
                    // The supplied food receipt comes from consuming actual flower nectar.
                    if (kind == Ecology.Butterfly || kind == LivingEcosystem.Hoverfly)
                    {
                        s.Ecosystem.Succession.Flowers[flower.PlantId].Nectar = Math.Min(6, s.Ecosystem.Succession.Flowers[flower.PlantId].Nectar + 2);
                        SuccessionRules.Forage(s, flower, kind, r);
                    }
                    CommunityRules.AfterActivity(s, r); s.WorldRevision++;
                    emerged |= r.Relations.Any(x => x.Kind == "resident-emerged" && x.Actor == kind);
                }
                Assert.IsTrue(emerged, kind + " needs to complete development with enough real food");
                Assert.Greater(f.Adults, 1); Assert.LessOrEqual(f.Adults, 3); Assert.Greater(stages.Count, 1);
                if (kind == Ecology.Butterfly) Assert.Contains(CommunityRules.Chrysalis, stages.ToList());
                Assert.GreaterOrEqual(host.Growth, 750);
            }
        }

        [Test]
        public void OneConsumedMeal_CannotFeedTwoFamilies()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[0], "clover", false); Plant(s, cells[2], "clover", false);
            var flower = Plant(s, cells[1], "daisy"); var a = Family(s, Ecology.Butterfly, cells[0]); var b = Family(s, Ecology.Butterfly, cells[2]);
            var r = Result(s); Assert.IsTrue(SuccessionRules.Forage(s, flower, Ecology.Butterfly, r)); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(1, new[] { a, b }.Count(f => f.AdultHunger == 0));
            Assert.AreEqual(1, r.Relations.Count(x => x.Kind == "brood-laid"));
        }

        [Test]
        public void AdultsCanRelocate_ButAquaticBroodsDoNotTeleportOutOfADryingPond()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[1], "clover", false); Plant(s, cells[2], "daisy");
            var f = Family(s, Ecology.Butterfly, cells[0]); f.BadHabitat = 1; var r = Result(s); CommunityRules.AfterActivity(s, r);
            Assert.AreEqual(cells[1], f.CellId); Assert.IsTrue(r.Relations.Any(x => x.Kind == "refuge-moved"));
            s = Soil(800); s.Ecosystem.WetAge[cells[1]] = 6; s.Ecosystem.CoverAge[cells[1]] = 8;
            f = Family(s, LivingEcosystem.Frog, cells[0], 2, 1); f.BadHabitat = 1;
            CommunityRules.AfterActivity(s, Result(s));
            Assert.AreEqual(cells[0], f.CellId); Assert.AreEqual(2, f.Brood, "offspring stay in their own nursery");
        }

        [Test]
        public void HabitatLoss_HasAGracePeriod_AndEventuallyReleasesTheSite()
        {
            var s = Soil(); int c = Cluster()[0]; var f = Family(s, Ecology.Dragonfly, c, 2, 1); var r = Result(s);
            for (int i = 0; i < 3; i++) { CommunityRules.AfterActivity(s, r); s.WorldRevision++; Assert.Contains(f, s.Ecosystem.Succession.Community.Families); }
            CommunityRules.AfterActivity(s, r);
            Assert.IsFalse(s.Ecosystem.Succession.Community.Families.Contains(f));
            Assert.IsTrue(r.Relations.Any(x => x.Kind == "resident-left")); Assert.IsTrue(r.Relations.Any(x => x.Kind == "brood-lost"));
        }

        [Test]
        public void ActiveVisits_RequireEstablishedAdults_AndStayNearTheirHomes()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[0], "clover", false); Plant(s, cells[1], "daisy");
            Assert.AreEqual(0, CommunityRules.RestrictVisits(s, Ecology.Butterfly, new List<int> { cells[1] }).Count);
            var f = Family(s, Ecology.Butterfly, cells[0]); f.Adults = 2;
            var near = CommunityRules.RestrictVisits(s, Ecology.Butterfly, new List<int> { cells[1], 255 });
            Assert.IsTrue(near.All(c => LivingEcosystem.Distance(c, f.CellId) <= CommunityRules.Reach(f.Kind)));
            Assert.AreEqual(2, CommunityRules.VisitLimit(s, Ecology.Butterfly, 3));
        }

        [Test]
        public void HoverflyAdults_CanFeedWithoutPrey_ButDoNotStartAnUnfedNursery()
        {
            var s = Soil(); var cells = Cluster(); Plant(s, cells[0], "clover", false); Plant(s, cells[1], "daisy");
            var f = Family(s, LivingEcosystem.Hoverfly, cells[0]);
            var result = Ecology.Apply(s, Context(s), new byte[64], null);
            var after = result.PostState.Ecosystem.Succession.Community.Families.Single(x => x.FamilyId == f.FamilyId);
            Assert.IsTrue(result.Effects.All(e => e.Amount >= 0));
            Assert.IsTrue(result.Visits.Any(x => x.Kind == LivingEcosystem.Hoverfly));
            Assert.IsTrue(result.Relations.Any(x => x.Kind == "nectar-fed" && x.Actor == LivingEcosystem.Hoverfly));
            Assert.AreEqual(0, after.AdultHunger); Assert.AreEqual(0, after.Brood);
        }

        [Test]
        public void Frogs_RemainVisibleWhenTheirFoodIsAquaticRatherThanAphids()
        {
            var s = Soil(800); int c = Cluster()[0]; s.Ecosystem.WetAge[c] = 6; s.Ecosystem.CoverAge[c] = 8;
            s.Ecosystem.Succession.Community.AquaticPrey[c] = 4; Family(s, LivingEcosystem.Frog, c);
            Assert.IsTrue(Ecology.Presence(s, Weathers.All[0], new string[0], null).Any(x => x.Kind == LivingEcosystem.Frog));
        }

        [Test]
        public void Memory_IsDeepCloned_AndItsCanonicalOrderSurvivesJson()
        {
            var s = Soil(); int c = Cluster()[0]; var f = Family(s, LivingEcosystem.Frog, c, 2, 3);
            var m = s.Ecosystem.Succession.Community; m.Algae[c] = 5; m.AquaticPrey[c] = 4;
            m.Prospects.Add(new SettlementSite { Kind = Ecology.Butterfly, CellId = c + 1, Streak = 2 });
            var clone = s.Clone(); clone.Ecosystem.Succession.Community.Algae[c] = 0;
            clone.Ecosystem.Succession.Community.Families[0].Brood = 0; clone.Ecosystem.Succession.Community.Prospects[0].Streak = 0;
            Assert.AreEqual(5, m.Algae[c]); Assert.AreEqual(2, f.Brood); Assert.AreEqual(2, m.Prospects[0].Streak);
            var restored = WorldStateSerializer.FromPayload((Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(WorldStateSerializer.ToPayload(s))).Parse());
            Assert.AreEqual(Hashing.StateHash(s), Hashing.StateHash(restored));
            Assert.IsFalse(SuccessionSerializer.ToPayload(new SuccessionState()).ContainsKey("community"));
        }

        [Test]
        public void Observation_ReportsStoredStagesAndNeeds_WithoutMutatingTheGarden()
        {
            var s = Soil(800); int c = Cluster()[0]; s.Ecosystem.WetAge[c] = 6; s.Ecosystem.CoverAge[c] = 8;
            var f = Family(s, LivingEcosystem.Frog, c, 2, 3); f.AdultHunger = 4;
            var hash = Hashing.StateHash(s); var reading = CommunityInspection.Read(s).Single();
            Assert.AreEqual("オタマジャクシ", reading.YoungLabel); Assert.AreEqual(2, reading.Young);
            Assert.That(reading.FoodStatus, Does.Contain("餌が足りない"));
            Assert.That(CommunityInspection.Describe(s), Does.Contain("水辺の食べもの"));
            foreach (string place in EcologyInspection.Habitats) Assert.IsTrue(CommunityInspection.Read(s, place).All(x => x.Habitat == place));
            Assert.AreEqual(hash, Hashing.StateHash(s));
            s.SoilWater[c] = 0;
            Assert.That(CommunityInspection.Read(s).Single().HabitatStatus, Does.Contain("幼い命がこの場所に残っている"));
        }

        [Test]
        public void LifeStages_DoNotArtificiallyMultiplyResidentDiversity()
        {
            var visits = new[] { new EcologyVisit { Kind = Ecology.Butterfly }, new EcologyVisit { Kind = "butterfly-eggs" },
                new EcologyVisit { Kind = CommunityRules.Caterpillar }, new EcologyVisit { Kind = CommunityRules.Chrysalis },
                new EcologyVisit { Kind = Ecology.Spirit } };
            Assert.AreEqual(1, CommunityRules.ResidentKinds(visits));
            Assert.AreEqual(1, CommunityRules.ResidentKinds(new[] { new EcologyVisit { Kind = LivingEcosystem.Hoverfly },
                new EcologyVisit { Kind = CommunityRules.HoverflyLarva }, new EcologyVisit { Kind = "hoverfly-pupa" } }));
        }

        [Test]
        public void V5Garden_HasItsOriginalInitialAnd96RollHashes()
        {
            var s = WorldState.CreateInitial(); s.Ecosystem = EcosystemState.CreateInitial(); s.Ecosystem.Succession = new SuccessionState();
            Assert.AreEqual("16ccb702f7f652d45a96f64b40f5ecda03687a306d8e4755db7e408b83c4c967", Hashing.StateHash(s));
            var m = new Memory(); var ids = new SequentialIds(); m.Append("RunStarted", ids.NewId("run"), null,
                new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(s) });
            var coord = new GardenCoordinator(m, new FixedEntropy("v5-golden"), ids);
            coord.Restore(new JournalReadResult { ChainValid = true, Entries = m.Entries });
            for (int i = 0; i < 96; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            Assert.AreEqual("9d88938fac694561e4850779ba321d1a8737498c7d31a87a8f28603e9b5def47", Hashing.StateHash(coord.State));
            Assert.IsNull(coord.State.Ecosystem.Succession.Community);
        }

        [Test]
        public void LongRunningGardens_PreserveFamiliesAndFoodAcrossActualJsonRestore()
        {
            var seen = new HashSet<string>(); var events = new HashSet<string>();
            for (int garden = 0; garden < 2; garden++)
            {
                List<string> Play(bool resume)
                {
                    var memory = new Memory(); var ids = new SequentialIds(); var entropy = new FixedEntropy("community-" + garden);
                    var coord = new GardenCoordinator(memory, entropy, ids);
                    var initial = WorldState.CreateInitial(); initial.Ecosystem = EcosystemState.CreateCurrent(); initial.Ecosystem.Habitat = null;
                    memory.Append("RunStarted", ids.NewId("run"), null, new Dictionary<string, object>
                    { ["initialState"] = WorldStateSerializer.ToPayload(initial), ["ecologyPolicyVersion"] = CommunityRules.RulesVersion });
                    coord.Restore(new JournalReadResult { ChainValid = true, Entries = memory.Entries });
                    Assert.AreEqual(CommunityRules.RulesVersion, memory.Entries[0].Payload["ecologyPolicyVersion"]);
                    var hashes = new List<string>(); int births = 0, settlements = 0, maxFamilies = 0;
                    for (int roll = 0; roll < 384; roll++)
                    {
                        coord.Prepare(); coord.CommitIntent("none", null); var result = coord.Roll(); Assert.NotNull(result);
                        var s = coord.State; var c = s.Ecosystem.Succession.Community;
                        Assert.AreEqual(result.Simulation.PostState.TotalWater(), s.TotalWater());
                        Assert.LessOrEqual(c.Families.Count, 10); Assert.IsTrue(c.Families.GroupBy(f => f.Kind).All(g => g.Count() <= 2));
                        Assert.AreEqual(c.Families.Count, c.Families.Select(f => f.FamilyId).Distinct().Count());
                        Assert.IsTrue(c.Prospects.GroupBy(p => p.Kind).All(g => g.Count() <= 6));
                        Assert.IsTrue(c.Algae.All(x => x >= 0 && x <= 12)); Assert.IsTrue(c.AquaticPrey.All(x => x >= 0 && x <= 8));
                        foreach (var f in c.Families)
                        {
                            seen.Add(f.Kind); Assert.That(f.Adults, Is.InRange(0, 3)); Assert.That(f.Brood, Is.InRange(0, 2));
                            Assert.That(f.BroodAge, Is.InRange(0, CommunityRules.Development(f.Kind) - 1));
                            Assert.That(f.AdultHunger, Is.InRange(0, 6)); Assert.That(f.NurseryHunger, Is.InRange(0, 3));
                            Assert.That(f.BadHabitat, Is.InRange(0, 4));
                        }
                        foreach (var link in result.Ecology.Relations) events.Add(link.Kind);
                        births += result.Ecology.Relations.Count(x => x.Kind == "resident-emerged");
                        settlements += result.Ecology.Relations.Count(x => x.Kind == "resident-settled"); maxFamilies = Math.Max(maxFamilies, c.Families.Count);
                        hashes.Add(Hashing.StateHash(s));
                        if (resume && roll % 48 == 47)
                        {
                            // Round-trip the real journal through JSON, not an in-memory copy of its dictionaries.
                            var records = new List<JournalEntry>();
                            foreach (var e in memory.Entries)
                                records.Add(new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
                                    Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() });
                            coord = new GardenCoordinator(memory, entropy, ids);
                            coord.Restore(new JournalReadResult { ChainValid = true, Entries = records });
                            Assert.AreEqual(hashes.Last(), Hashing.StateHash(coord.State));
                        }
                    }
                    Assert.Greater(settlements, 0); Assert.Greater(births, 0);
                    TestContext.WriteLine($"garden {garden}, resume={resume}: settled={settlements}, emerging-events={births}, max-families={maxFamilies}, plants={coord.State.Plants.Count}");
                    return hashes;
                }
                CollectionAssert.AreEqual(Play(false), Play(true));
            }
            TestContext.WriteLine("resident kinds: " + string.Join(",", seen.OrderBy(x => x)));
            TestContext.WriteLine("community events: " + string.Join(",", events.OrderBy(x => x)));
            Assert.GreaterOrEqual(seen.Count, 3);
            foreach (string kind in new[] { "resident-settled", "brood-laid", "resident-emerged", "host-leaf-eaten", "algae-grew" }) Assert.Contains(kind, events.ToList());
        }
    }
}
