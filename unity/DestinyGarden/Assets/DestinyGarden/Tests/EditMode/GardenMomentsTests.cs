using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class GardenMomentsTests
    {
        private sealed class Memory : IJournalWriter
        {
            public List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => ""; public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string k, string run, string round, Dictionary<string, object> p)
            { var e = new JournalEntry { Kind = k, RunId = run, RoundId = round, Payload = p, Seq = NextSeq }; Entries.Add(e); return e; }
        }
        private static WorldState Soil(int moisture = 600)
        {
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.WorldRevision = 7; s.Ecosystem = EcosystemState.CreateCurrent();
            for (int c = 0; c < 256; c++) s.SoilWater[c] = GardenMapData.SoilCapacity[c] * moisture / 1000;
            return s;
        }
        private static PlantState Plant(WorldState s, int c, PlantStage stage = PlantStage.Flower, string species = "daisy")
        {
            var p = new PlantState { PlantId = "p-" + c, CellId = c, Species = species, Stage = stage, Growth = stage == PlantStage.Flower ? 2500 : 900 };
            s.Plants.Add(p); return p;
        }
        private static EcologicalLink Link(string kind, int cell = 85, string actor = "bee", string species = "daisy", int amount = 1) =>
            new EcologicalLink { Kind = kind, Actor = actor, Species = species, ToCell = cell, FromCell = cell, Amount = amount };
        private static List<GardenMoment> Read(WorldState state, params EcologicalLink[] links) => GardenMoments.Read(state, links, new GardenEvent[0], "light_beam", "r7");
        private static string Signature(IList<GardenMoment> moments) => string.Join("\n", moments.Select(m => m.Kind + "|" + m.CellId + "|" + m.Habitat + "|" + m.Headline + "|" + m.Detail + "|" + m.Caption + "|" + m.MemoryKey));

        [Test]
        public void LightAndAVisit_FormASceneOnlyFromTheSameRecordedFlower()
        {
            var s = Soil(); var care = Link("care-nectar", actor: "light_beam"); var visit = Link("nectar-fed");
            var moment = Read(s, care, visit).Single();
            Assert.AreEqual("care-nectar", moment.Kind); Assert.That(moment.Headline, Does.Contain("蜜を補ったデイジー"));
            Assert.That(moment.Headline, Does.Contain("花蜂")); Assert.AreEqual(85, moment.CellId); Assert.That(moment.Detail, Does.Contain("養分"));
        }
        [TestCase("other-cell")][TestCase("other-species")][TestCase("zero-meal")]
        public void AVisitElsewhereOrWithoutARecordedMeal_IsNotJoinedToTheLight(string difference)
        {
            var visit = Link("nectar-fed"); if (difference == "other-cell") visit.ToCell = visit.FromCell = 86;
            if (difference == "other-species") visit.Species = "wildrose"; if (difference == "zero-meal") visit.Amount = 0;
            var moment = Read(Soil(), Link("care-nectar", actor: "light_beam"), visit).Single(m => m.CellId == 85);
            Assert.That(moment.Headline, Does.Not.Contain("花蜂"));
        }
        [Test]
        public void AlgaeGrowthAndAYoungMeal_UseActualPoolEvents_AndDoNotClaimTheNewAlgaeWasTheMeal()
        {
            var moment = Read(Soil(800), Link("algae-grew", actor: CommunityRules.Algae, species: null, amount: 2),
                Link("care-algae", actor: "light_beam", species: null), Link("algae-eaten", actor: CommunityRules.Tadpole, species: null)).Single();
            Assert.AreEqual("algae-eaten", moment.Kind); Assert.That(moment.Headline, Does.Contain("藻の育った水辺"));
            Assert.That(moment.Headline, Does.Contain("オタマジャクシ")); Assert.That(moment.Detail, Does.Contain("ダイスの光"));
        }
        [Test]
        public void FeedingAlone_DoesNotInventGrowthOrLight()
        {
            var moment = Read(Soil(), Link("algae-eaten", actor: CommunityRules.Tadpole, species: null), Link("algae-grew", 86, CommunityRules.Algae, null)).Single(m => m.CellId == 85);
            Assert.That(moment.Headline, Does.Not.Contain("育った水辺")); Assert.IsNull(moment.Detail);
        }
        [Test]
        public void RecoveryCanBeJoinedToActualPredationOnTheSameLeaf()
        {
            var predator = Link("predation", actor: LivingEcosystem.Ladybird, species: null, amount: 4);
            var moment = Read(Soil(), Link("recovered", actor: "habitat", species: "violet"), predator).Single();
            Assert.That(moment.Headline, Does.Contain("小虫の減った葉")); Assert.That(moment.Headline, Does.Contain("スミレ"));
            Assert.That(moment.Detail, Does.Contain("テントウムシ"));
            predator.ToCell = 86; Assert.IsNull(Read(Soil(), Link("recovered", actor: "habitat", species: "violet"), predator).Single(m => m.CellId == 85).Detail);
        }
        [Test]
        public void HumusAndNutrientSentences_RequireTheActualDecomposerAndAmounts()
        {
            var h = Link("humus-formed", actor: Ecology.Worm, species: null); var n = Link("decomposed", actor: Ecology.Worm, species: null, amount: 2);
            var moment = Read(Soil(), h, n).Single(); Assert.That(moment.Headline, Does.Contain("ミミズ")); Assert.That(moment.Detail, Does.Contain("養分も戻った"));
            n.Actor = Ecology.Mushroom; Assert.IsNull(Read(Soil(), h, n).Single().Detail);
        }
        [TestCase("frog", "オタマジャクシ")][TestCase("dragonfly", "ヤゴ")][TestCase("butterfly", "蝶")]
        public void AnActualEmergence_NamesTheRecordedLifeCycle(string kind, string young)
        {
            var moment = Read(Soil(), Link("resident-emerged", actor: kind, species: null)).Single();
            Assert.That(moment.Headline, Does.Contain(young)); Assert.AreEqual("resident-emerged", moment.Kind);
        }
        [Test]
        public void PresenceAndCurrentAdultCounts_DoNotInventAnEmergence()
        {
            var s = Soil(); s.Ecosystem.Succession.Community.Families.Add(new ResidentFamily { FamilyId = "f", Kind = LivingEcosystem.Frog, CellId = 85, Adults = 2 });
            var eco = new EcologyResult { PostState = s, Visits = new List<EcologyVisit> { new EcologyVisit { Kind = LivingEcosystem.Frog, Cells = new List<int> { 85 } } } };
            var moment = GardenMoments.FromOutcome(new RollOutcome { Ecology = eco, RecipeId = "water_drop" }).Single();
            Assert.AreEqual("recorded-roll", moment.Kind); Assert.That(moment.Headline, Does.Not.Contain("カエル"));
        }
        [TestCase("care-nectar")][TestCase("algae-eaten")][TestCase("predation")][TestCase("seed-buried")]
        public void AZeroResourceTransfer_DoesNotBecomeAnAccomplishedEvent(string kind)
        {
            Assert.AreEqual("recorded-roll", Read(Soil(), Link(kind, amount: 0)).Single().Kind);
        }
        [TestCase("brood-lost")][TestCase("returned-to-soil")][TestCase("seed-expired")]
        public void RecordedLossesRemainVisible_WhenNoMoreLitterCouldBeStored(string kind)
        {
            Assert.AreEqual(kind, Read(Soil(), Link(kind, amount: 0)).Single().Kind);
        }
        [Test]
        public void AClonalCrossing_UsesItsRecordedBoundary_AndTheMomentDoesNotFollowFutureStateChanges()
        {
            var s = Soil(); var link = Link("clonal-rooted", actor: "rhizome", species: "mint"); link.FromCell = 84;
            link.FromHabitat = "花の草地"; link.ToHabitat = "草木の陰"; var moment = Read(s, link).Single();
            string before = Signature(new[] { moment }); link.ToHabitat = "水辺"; Array.Clear(s.SoilWater, 0, 256); s.Plants.Clear();
            Assert.AreEqual(before, Signature(new[] { moment })); Assert.That(moment.Detail, Does.Contain("花の草地から、草木の陰"));
            Assert.AreEqual(84, moment.FromCell); Assert.AreEqual(85, moment.CellId);
        }
        [Test]
        public void AFlowerThatRestedOrWasReplaced_DoesNotBecomeAnOpenFlowerFocus()
        {
            var s = Soil(); var p = Plant(s, 85, PlantStage.Leaf); var events = new[] { new GardenEvent { Kind = "Bloomed", CellId = 85, PlantId = p.PlantId } };
            var result = GardenMoments.Read(s, new[] { Link("flower-rest") }, events, "light_beam", "r");
            Assert.AreEqual("flower-rest", result.Single().Kind);
            p.Stage = PlantStage.Flower; p.PlantId = "replacement";
            Assert.AreEqual("recorded-roll", GardenMoments.Read(s, new EcologicalLink[0], events, "light_beam", "r").Single().Kind);
        }
        [Test]
        public void ANewSeedAndGermination_AreJoinedOnlyByTheSamePlantId()
        {
            var s = Soil(); var p = Plant(s, 85, PlantStage.Sprout); p.Growth = 250;
            var placed = new GardenEvent { Kind = "SeedPlaced", CellId = 85, PlantId = p.PlantId };
            var events = new[] { placed, new GardenEvent { Kind = "Germinated", CellId = 85, PlantId = p.PlantId } };
            var moment = GardenMoments.Read(s, new EcologicalLink[0], events, "seed_single", "r").Single();
            Assert.AreEqual("Germinated", moment.Kind); Assert.That(moment.Detail, Does.Contain("この投で蒔かれた"));
            placed.PlantId = "elsewhere"; Assert.IsNull(GardenMoments.Read(s, new EcologicalLink[0], events, "seed_single", "r").Single().Detail);
        }
        [Test]
        public void TheSceneIsNotAlwaysTheFirstCellOrTheHappiestEvent_AndEachCellHasOneMoment()
        {
            var events = new[] { Link("nectar-fed", 85), Link("pollinated", 85), Link("brood-lost", 86, LivingEcosystem.Frog, null), Link("seed-awakened", 87) };
            var a = Read(Soil(), events); var b = Read(Soil(), events.Reverse().ToArray());
            Assert.AreEqual("brood-lost", a[0].Kind); Assert.AreEqual(a.Count, a.Select(x => x.CellId).Distinct().Count()); Assert.AreEqual(Signature(a), Signature(b));
        }
        [Test]
        public void AQuietRollOnlyReportsItsRecordedOperation_WithoutMakingUpASuccess()
        {
            var a = GardenMoments.Read(Soil(), new EcologicalLink[0], new GardenEvent[0], "wind_passage", "r").Single();
            Assert.AreEqual(-1, a.CellId); Assert.That(a.Headline, Does.Contain("面が出た")); Assert.That(a.Headline, Does.Not.Contain("種"));
            var b = GardenMoments.Read(Soil(), new EcologicalLink[0], new GardenEvent[0], "water_drop", "r", diceWater: 1200).Single();
            Assert.That(b.Headline, Does.Contain("注がれた")); Assert.That(b.Headline, Does.Not.Contain("水辺が生まれ"));
        }
        [TestCase("水辺")][TestCase("岩陰")][TestCase("草木の陰")][TestCase("乾いた草地")][TestCase("花の草地")]
        public void EachHabitat_IsDescribedFromThatRoundsActualSnapshot(string label)
        {
            var s = Soil(label == "水辺" ? 800 : label == "乾いた草地" ? 200 : 600);
            if (label == "水辺") for (int n = 0; n < 256; n++) s.Ecosystem.WetAge[n] = 6;
            if (label == "草木の陰") Plant(s, 68, PlantStage.Leaf, "sunflower");
            int c = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && LivingEcosystem.Habitat(s, n) == label);
            var moment = Read(s, Link("decomposed", c, Ecology.Worm, null, 2)).Single();
            Assert.AreEqual(label, moment.Habitat); Assert.That(GardenMoments.Describe(new[] { moment }, label), Does.Contain("ミミズ"));
        }
        [Test]
        public void RecentProjection_MatchesLiveScenes_AfterJsonRestore_AndKeepsRunAndRoundBoundaries()
        {
            var m = new Memory(); var ids = new SequentialIds(); var c = new GardenCoordinator(m, new FixedEntropy("actual-scenes"), ids); c.CreateRun();
            var live = new Dictionary<string, string>();
            for (int i = 0; i < 32; i++)
            {
                c.Prepare(); c.CommitIntent("none", null); string round = c.CurrentRoundId; var outcome = c.Roll();
                int count = m.Entries.Count, focus = outcome.Message.FocusCellId; string hash = Hashing.StateHash(c.State);
                live[round] = Signature(GardenMoments.FromOutcome(outcome, round));
                Assert.AreEqual(hash, Hashing.StateHash(c.State)); Assert.AreEqual(count, m.Entries.Count); Assert.AreEqual(focus, outcome.Message.FocusCellId);
            }
            var json = m.Entries.Select(e => new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
                Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() }).ToList();
            var recent = GardenMoments.Recent(json, c.RunId, 3); Assert.AreEqual(3, recent.Select(x => x.RoundId).Distinct().Count());
            foreach (var group in recent.GroupBy(x => x.RoundId)) Assert.AreEqual(live[group.Key], Signature(group.ToList()));
            Assert.AreEqual(32, recent[0].Revision); Assert.IsEmpty(GardenMoments.Recent(json, "other-run"));
            Assert.IsEmpty(GardenMoments.Recent(json.Where(e => e.Kind != "Resolved").ToList(), c.RunId));
            Assert.IsEmpty(GardenMoments.Recent(json, c.RunId, 0));
            TestContext.WriteLine($"actual scenes: {live.Count} live rounds; latest {recent.Select(x => x.RoundId).Distinct().Count()} rounds match JSON history");
        }
    }
}
