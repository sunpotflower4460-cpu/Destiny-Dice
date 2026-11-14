using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class GardenMemoriesTests
    {
        private sealed class Journal : IJournalWriter
        {
            public List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string run, string round, Dictionary<string, object> payload)
            { var e = new JournalEntry { Seq = NextSeq, Kind = kind, RunId = run, RoundId = round, Payload = payload }; Entries.Add(e); return e; }
            public void Start(WorldState s) => Append("RunStarted", "run", null, new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(s) });
            public void Resolve(WorldState s, string plant = null, string kind = null, int cell = 85, params EcologicalLink[] links)
            {
                string round = "r" + s.WorldRevision;
                Append("OutcomeCommitted", "run", round, new Dictionary<string, object> { ["recipeId"] = "light_beam" });
                var events = new List<object>();
                if (kind != null) events.Add(new Dictionary<string, object> { ["kind"] = kind, ["plantId"] = plant, ["cellId"] = cell, ["tick"] = 40 });
                Append("Resolved", "run", round, new Dictionary<string, object> { ["postState"] = WorldStateSerializer.ToPayload(s), ["events"] = events,
                    ["ecology"] = new Dictionary<string, object> { ["relations"] = links.Select(l => (object)new Dictionary<string, object> {
                        ["kind"] = l.Kind, ["species"] = l.Species, ["actor"] = l.Actor, ["amount"] = l.Amount, ["toCell"] = l.ToCell, ["fromCell"] = l.FromCell }).ToList() } });
            }
            public GardenMemory Read(string key) => GardenMemories.Project(Entries, "run", key);
        }
        private static WorldState Soil(int revision = 0)
        { var s = WorldState.CreateInitial(); s.Plants.Clear(); s.WorldRevision = revision; s.Ecosystem = EcosystemState.CreateCurrent(); return s; }
        private static PlantState Plant(WorldState s, string id = "parent", int cell = 85, string origin = "initialMap")
        { var p = new PlantState { PlantId = id, Species = "daisy", CellId = cell, Stage = PlantStage.Sprout, OriginKind = origin }; s.Plants.Add(p); return p; }
        private static RestingSeed Seed(WorldState s, string id = "seed:1", string parent = "parent", int cell = 86, string carrier = "bird")
        {
            var seed = new RestingSeed { SeedId = id, Species = "daisy", ParentPlantId = parent, ParentCellId = 85, CellId = cell,
                BornRevision = s.WorldRevision, SourceRoundId = "r" + s.WorldRevision, Carrier = carrier };
            s.Ecosystem.Succession.SeedBank.Add(seed); return seed;
        }
        private static EcologicalLink Link(string kind, int cell = 85, string species = "daisy", int amount = 1) =>
            new EcologicalLink { Kind = kind, ToCell = cell, FromCell = 85, Species = species, Actor = "bee", Amount = amount };
        private static PlantState Child(WorldState s, string id = "child", string parent = "parent", string seed = "seed:1", int cell = 86, string carrier = "bird")
        {
            var p = Plant(s, id, cell, seed == null ? "vegetative" : "ecology");
            s.Ecosystem.Succession.Ancestry[id] = new PlantAncestry { ParentPlantId = parent, SeedId = seed, Carrier = carrier, BornRevision = s.WorldRevision }; return p;
        }
        [Test]
        public void ExactIndividual_FollowsDiceSeedGrowthAndReturn_ThroughASameSpeciesReplacement()
        {
            var j = new Journal(); var s = Soil(); j.Start(s); s.WorldRevision = 1; var p = Plant(s, "dice-plant", origin: "seedInput");
            j.Resolve(s, p.PlantId, "SeedPlaced"); s.WorldRevision = 2; j.Resolve(s, p.PlantId, "Germinated");
            s.WorldRevision = 3; j.Resolve(s, p.PlantId, "Bloomed"); s.WorldRevision = 4; s.Plants.Clear(); Plant(s, "replacement");
            j.Resolve(s, p.PlantId, "ReturnedToSoil"); s.WorldRevision = 5; j.Resolve(s, "replacement", "LeafOpened");
            var m = j.Read("p:dice-plant");
            Assert.That(m.Origin.Headline, Does.Contain("ダイス")); Assert.AreEqual(1, m.Origin.Revision);
            CollectionAssert.AreEqual(new[] { "Germinated", "Bloomed", "ReturnedToSoil" }, m.Steps.Select(x => x.Kind));
            Assert.IsTrue(m.Returned); Assert.IsFalse(m.Present); Assert.AreEqual(85, m.Focus.CellId);
            Assert.AreEqual(3, m.LastSeenRevision); Assert.AreEqual(5, m.LatestRevision);
            Assert.That(j.Read("p:replacement").Describe(), Does.Not.Contain("ダイスが届けた"));
        }
        [TestCase("bird", "小鳥")][TestCase("fluff", "風")]
        public void ExactSeedAndAncestry_JoinParentArrivalAndChild_AfterTheParentHasReturned(string carrier, string word)
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1; Seed(s, carrier: carrier); j.Resolve(s);
            s.WorldRevision = 2; s.Plants.Clear(); j.Resolve(s, "parent", "ReturnedToSoil");
            s.WorldRevision = 3; s.Ecosystem.Succession.SeedBank.Clear(); Child(s, carrier: carrier); j.Resolve(s, "child", "Germinated", 86);
            var parent = j.Read("p:parent"); var seed = j.Read("s:seed:1"); var child = j.Read("p:child");
            Assert.AreEqual("s:seed:1", parent.Next.Single().Key); Assert.AreEqual(1, parent.SeedCount); Assert.AreEqual(1, parent.ChildCount); Assert.IsTrue(parent.Returned);
            Assert.That(seed.Origin.Headline, Does.Contain(word)); Assert.AreEqual(1, seed.Origin.Revision); Assert.AreEqual("p:parent", seed.ParentKey);
            Assert.AreEqual("p:child", seed.Next.Single().Key); Assert.IsTrue(seed.Next.Single().Present); Assert.AreEqual(3, seed.Steps.Single().Revision);
            Assert.AreEqual("s:seed:1", child.ParentKey); Assert.IsTrue(child.Present); Assert.AreEqual(3, child.Origin.Revision);
        }
        [TestCase("stolon", "つる")][TestCase("rhizome", "地下茎")]
        public void VegetativeChildren_FollowTheirActualParent_WithoutInventingASeed(string carrier, string word)
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1; Child(s, seed: null, carrier: carrier); j.Resolve(s, "child", "VegetativeRooted", 86);
            var child = j.Read("p:child"); Assert.That(child.Origin.Headline, Does.Contain(word)); Assert.AreEqual("p:parent", child.ParentKey);
            var parent = j.Read("p:parent"); Assert.AreEqual(0, parent.SeedCount); Assert.AreEqual(1, parent.ChildCount); Assert.AreEqual("p:child", parent.Next.Single().Key);
        }
        [Test]
        public void NearbySameSpeciesAndSameCell_DoNotCreateGenealogy()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1;
            Plant(s, "unrelated", 86); Seed(s, parent: "another-parent"); j.Resolve(s);
            Assert.AreEqual(0, j.Read("p:parent").Next.Count); Assert.IsNull(j.Read("p:unrelated").ParentKey); Assert.IsNull(j.Read("s:seed:1").ParentKey);
            s.WorldRevision = 2; Child(s, "different-child", seed: "different-seed", cell: 87); j.Resolve(s);
            Assert.AreEqual(0, j.Read("s:seed:1").ChildCount);
        }
        [Test]
        public void AConflictingSeedLocation_IsNotPresentedAsGermination()
        {
            var j = new Journal(); var s = Soil(1); Seed(s); j.Resolve(s); s.WorldRevision = 2;
            Child(s, cell: 87); j.Resolve(s); Assert.AreEqual(0, j.Read("s:seed:1").ChildCount);
        }
        [Test]
        public void MissingAndOldRecords_DoNotInventADeathBirthOrExpiry()
        {
            var j = new Journal(); var s = Soil(8); Plant(s); Seed(s); j.Resolve(s);
            Assert.That(j.Read("p:parent").Origin.Headline, Does.Contain("記録された"));
            s.WorldRevision = 9; s.Plants.Clear(); s.Ecosystem.Succession.SeedBank.Clear(); j.Resolve(s);
            var m = j.Read("p:parent"); Assert.IsFalse(m.Returned); Assert.That(m.Describe(), Does.Contain("8投目"));
            Assert.That(j.Read("s:seed:1").Describe(), Does.Not.Contain("土へ還った"));
            Assert.IsNull(j.Read("p:unknown")); Assert.IsNull(j.Read("s:unknown")); Assert.IsNull(j.Read("invalid"));
        }
        [Test]
        public void ASeedRestTransition_IsRecordedOnce_WithoutInventingGermination()
        {
            var j = new Journal(); var s = Soil(1); var seed = Seed(s); j.Resolve(s);
            s.WorldRevision = 2; seed.Rested = true; j.Resolve(s); s.WorldRevision = 3; j.Resolve(s);
            var m = j.Read("s:seed:1"); Assert.AreEqual("seed-rested", m.Steps.Single().Kind); Assert.AreEqual(2, m.Steps.Single().Revision);
            Assert.IsTrue(m.Present); Assert.AreEqual(0, m.ChildCount);
        }
        [Test]
        public void RecordedFlowerCarePollinationAndFruit_StayWithTheIndividual()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1;
            j.Resolve(s, links: new[] { Link("care-nectar"), Link("pollinated"), Link("fruited"), Link("recovered"), Link("flower-rest"), Link("pollinated", 86), Link("fruited", species: "violet") });
            var m = j.Read("p:parent"); Assert.AreEqual(5, m.Steps.Count);
            Assert.That(m.Describe(), Does.Contain("この投の光")); Assert.That(m.Describe(), Does.Contain("花蜂")); Assert.That(m.Describe(), Does.Contain("次の種"));
            Assert.That(m.Steps.All(x => x.CellId == 85 && x.RecipeId == "light_beam"), Is.True);
        }
        [Test]
        public void ZeroAmounts_AndRetiringSameSpeciesReplacements_DoNotStealTheOldPlantsRelations()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1;
            j.Resolve(s, links: new[] { Link("fruited", amount: 0) }); Assert.IsEmpty(j.Read("p:parent").Steps);
            s.WorldRevision = 2; s.Plants.Clear(); Plant(s, "replacement"); j.Resolve(s, "parent", "ReturnedToSoil", links: new[] { Link("fruited") });
            Assert.IsEmpty(j.Read("p:replacement").Steps); Assert.AreEqual("ReturnedToSoil", j.Read("p:parent").Steps.Single().Kind);
        }

        [Test]
        public void TheStarMarksOnlyTheStageItAdvanced_WithoutFillingTheMemoryWithSmallGifts()
        {
            var j = new Journal();
            var s = Soil();
            var p = Plant(s);
            p.Growth = 749;
            j.Start(s);
            void RecordGift(int revision, int growth, string kind, int amount)
            {
                s.WorldRevision = revision;
                p.Growth = growth;
                p.Stage = PlantStage.Leaf;
                j.Resolve(s, p.PlantId, kind);
                var ecology = (Dictionary<string, object>)j.Entries.Last().Payload["ecology"];
                ecology["effects"] = new object[]
                {
                    new Dictionary<string, object> { ["kind"] = PrayerStar.GiftKind, ["cellId"] = p.CellId, ["amount"] = amount }
                };
            }
            RecordGift(1, 754, "LeafOpened", 5);
            for (int revision = 2; revision <= 12; revision++) RecordGift(revision, 754 + revision * 5, null, 5);
            RecordGift(13, 1505, "BudFormed", 5);

            var memory = j.Read("p:" + p.PlantId);

            CollectionAssert.AreEqual(new[] { "星の恵みで、葉を開いた。", "蕾ができた。" }, memory.Steps.Select(step => step.Headline));
            CollectionAssert.AreEqual(new[] { 1, 13 }, memory.Steps.Select(step => step.Revision));
        }
        [Test]
        public void MomentMemoryKeys_UseGrowthAndReturnIds_IncludingAReplacementAtTheSameCell()
        {
            var s = Soil(1); Plant(s, "new"); var ev = new[] { new GardenEvent { Kind = "ReturnedToSoil", PlantId = "old", CellId = 85 } };
            var moment = GardenMoments.Read(s, new[] { Link("returned-to-soil") }, ev, "water_drop", "r1").Single();
            Assert.AreEqual("p:old", moment.MemoryKey);
            Assert.IsNull(GardenMoments.Read(s, new[] { Link("fruited") }, ev, "water_drop", "r1").Single().MemoryKey);
            ev[0] = new GardenEvent { Kind = "Germinated", PlantId = "new", CellId = 85 };
            Assert.AreEqual("p:new", GardenMoments.Read(s, new[] { Link("seed-awakened") }, ev, "water_drop", "r1").Single().MemoryKey);
            Assert.IsNull(GardenMoments.Read(s, new[] { Link("seed-awakened") }, new GardenEvent[0], "water_drop", "r1").Single().MemoryKey);
        }
        [Test]
        public void SeedArrivalKeys_RequireTheExactRoundCarrierRouteSpeciesAndUnambiguousSeed()
        {
            var s = Soil(1); var seed = Seed(s); var link = Link("seed-buried", 86); link.Actor = "bird";
            Func<string, string> read = round => GardenMoments.Read(s, new[] { link }, new GardenEvent[0], "wind_passage", round).Single().MemoryKey;
            Assert.AreEqual("s:seed:1", read("r1")); Assert.IsNull(read("r2")); Assert.IsNull(read(null));
            link.Actor = "fluff"; Assert.IsNull(read("r1")); link.Actor = "bird"; link.FromCell = 84; Assert.IsNull(read("r1"));
            link.FromCell = 85; link.Species = "violet"; Assert.IsNull(read("r1")); link.Species = "daisy";
            Seed(s, "second"); Assert.IsNull(read("r1"));
        }
        [Test]
        public void Histories_AreBoundedButKeepOriginCountsAndTheMostRecentSteps()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s);
            for (int i = 1; i <= 18; i++) { s.WorldRevision = i; Child(s, "child" + i, seed: null, cell: 85 + i, carrier: "stolon"); j.Resolve(s, "parent", "Bloomed"); }
            var m = j.Read("p:parent"); Assert.AreEqual(0, m.Origin.Revision); Assert.That(m.Origin.Headline, Does.Contain("はじめ"));
            Assert.AreEqual(18, m.ChildCount); Assert.AreEqual(GardenMemories.MaxNext, m.Next.Count); Assert.AreEqual(18, m.Next[0].Revision);
            Assert.AreEqual(GardenMemories.MaxSteps, m.Steps.Count); Assert.AreEqual(11, m.Steps[0].Revision); Assert.AreEqual(18, m.Focus.Revision);
        }
        [Test]
        public void PresentDescendantsComeFirst_AndRepeatedSnapshotsDoNotMultiplyLives()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1; Child(s, seed: null); j.Resolve(s);
            s.WorldRevision = 2; Seed(s); j.Resolve(s); s.WorldRevision = 3; s.Ecosystem.Succession.SeedBank.Clear(); j.Resolve(s);
            var m = j.Read("p:parent"); Assert.AreEqual(1, m.ChildCount); Assert.AreEqual(1, m.SeedCount); Assert.IsTrue(m.Next[0].Present); Assert.AreEqual("p:child", m.Next[0].Key);
        }
        [Test]
        public void OnlyResolvedSnapshotsOfTheSameRun_AreUsed_AndProjectionDoesNotMutateTheJournal()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1; j.Resolve(s, "parent", "Bloomed");
            j.Entries.Add(new JournalEntry { Kind = "Resolved", RunId = "other", Payload = new Dictionary<string, object>() });
            j.Entries.Add(new JournalEntry { Kind = "OutcomeCommitted", RunId = "run", RoundId = "unfinished", Payload = new Dictionary<string, object> { ["recipeId"] = "seed_single" } });
            string before = CanonicalJson.Write(j.Entries.Select(e => (object)e.ToHashPayload()).ToList());
            var m = j.Read("p:parent"); Assert.AreEqual(1, m.LatestRevision); Assert.AreEqual(1, m.Steps.Count);
            m.Steps.Clear(); m.Next.Clear(); Assert.AreEqual(1, j.Read("p:parent").Steps.Count);
            Assert.AreEqual(before, CanonicalJson.Write(j.Entries.Select(e => (object)e.ToHashPayload()).ToList()));
            Assert.IsNull(GardenMemories.Project(j.Entries, "unknown-run", "p:parent"));
        }
        [Test]
        public void HabitatsAreFromEachRecordedMoment_AndRemainDetachedFromLaterGardenChanges()
        {
            var j = new Journal(); var s = Soil(); Plant(s); j.Start(s); s.WorldRevision = 1;
            for (int c = 0; c < 256; c++) { s.SoilWater[c] = GardenMapData.SoilCapacity[c] * 8 / 10; s.Ecosystem.WetAge[c] = 6; }
            j.Resolve(s, "parent", "Bloomed"); s.WorldRevision = 2; Array.Clear(s.SoilWater, 0, 256); Array.Clear(s.Ecosystem.WetAge, 0, 256); j.Resolve(s, "parent", "LeafOpened");
            var m = j.Read("p:parent"); Assert.AreEqual("水辺", m.Steps[0].Habitat); Assert.AreNotEqual("水辺", m.Steps[1].Habitat);
            string text = m.Describe(); s.Plants.Clear(); Assert.AreEqual(text, m.Describe());
        }
        [Test]
        public void RealCoordinatorHistories_KeepExactLineageAndMomentKeys_AfterJsonRestore()
        {
            var j = new Journal(); var c = new GardenCoordinator(j, new FixedEntropy("garden-memory"), new SequentialIds()); c.CreateRun();
            for (int i = 0; i < 96; i++) { c.Prepare(); c.CommitIntent("none", null); c.Roll(); }
            string stateHash = Hashing.StateHash(c.State), journal = CanonicalJson.Write(j.Entries.Select(e => (object)e.ToHashPayload()).ToList());
            var restored = j.Entries.Select(e => new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
                Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() }).ToList();
            var keys = GardenMoments.Recent(restored, c.RunId).Where(m => m.MemoryKey != null).Select(m => m.MemoryKey).Distinct().ToList();
            Assert.IsNotEmpty(keys); int paths = 0;
            foreach (string key in keys)
            {
                var a = GardenMemories.Project(j.Entries, c.RunId, key); var b = GardenMemories.Project(restored, c.RunId, key);
                Assert.NotNull(a, key); Assert.AreEqual(a.Describe(), b.Describe()); Assert.AreEqual(a.ParentKey, b.ParentKey);
                foreach (var link in b.Next) { Assert.NotNull(GardenMemories.Project(restored, c.RunId, link.Key)); paths++; }
                if (b.ParentKey != null) { Assert.NotNull(GardenMemories.Project(restored, c.RunId, b.ParentKey)); paths++; }
            }
            Assert.Greater(paths, 0); Assert.AreEqual(stateHash, Hashing.StateHash(c.State)); Assert.AreEqual(journal, CanonicalJson.Write(j.Entries.Select(e => (object)e.ToHashPayload()).ToList()));
            TestContext.WriteLine($"life histories: 96 real rounds; {keys.Count} exact identities and {paths} lineage links match JSON history without mutation");
        }
    }
}
