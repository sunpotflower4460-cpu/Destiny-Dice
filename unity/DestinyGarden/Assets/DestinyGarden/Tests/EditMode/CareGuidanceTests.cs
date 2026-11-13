using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class CareGuidanceTests
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
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.Ecosystem = EcosystemState.CreateCurrent();
            for (int c = 0; c < 256; c++) { s.SoilWater[c] = GardenMapData.SoilCapacity[c] * moisture / 1000; s.Ecosystem.Habitat.Humus[c] = 100; }
            return s;
        }
        private static int Sunny() => Enumerable.Range(0, 256).First(c => GardenMapData.Plantable[c] == 1 && GardenMapData.LightExposureQ[c] == 1000 && RecipeResolver.ShelterQEast(c) == 1000);
        private static PlantState Plant(WorldState s, int c, string species = "iris", bool flower = false)
        {
            var p = new PlantState { PlantId = species + c, Species = species, CellId = c, Growth = flower ? 2500 : 1000, Stage = flower ? PlantStage.Flower : PlantStage.Leaf };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); return p;
        }
        private static PreparedFaces Faces(int c) => new PreparedFaces { AnchorCellId = c, ResolvedSiteId = "meadow", Faces = new[]
            { "water_drop", "wind_passage", "light_beam", "water_rain", "wind_breeze", "light_soft" } };
        private static List<JournalEntry> Json(IList<JournalEntry> entries) => entries.Select(e => new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
            Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() }).ToList();

        [Test]
        public void PublishedFaceOutlook_MatchesTheRealNaturalResult_WithoutChangingTheGardenOrFaces()
        {
            var s = Soil(180); int c = Sunny(); var p = Plant(s, c); var faces = Faces(c); string hash = Hashing.StateHash(s); var published = (string[])faces.Faces.Clone();
            var plan = CareOutlook.Build(s, faces, Weathers.All[0]); var row = plan.Rows.Single(x => x.Concern.SubjectId == p.PlantId);
            foreach (var option in row.Faces)
            {
                var actual = Simulation.Resolve(s, RecipeResolver.Resolve(option.RecipeId, c, s), Weathers.All[0], n => "real-" + n);
                Assert.AreEqual(Flora.Moisture(actual.PostState, c), option.Moisture);
            }
            Assert.IsTrue(row.Faces.Any(x => x.Improves)); Assert.IsTrue(row.Faces.Any(x => x.Worsens));
            Assert.AreEqual(hash, Hashing.StateHash(s)); CollectionAssert.AreEqual(published, faces.Faces);
            Assert.That(plan.Describe(), Does.Contain("ほかの条件"));
        }
        [Test]
        public void AHealthyPlantsBand_IsAlsoWatched_ForHarmIntroducedByAPublishedFace()
        {
            var s = Soil(870); int c = Sunny(); Plant(s, c, "daisy", true);
            var row = CareOutlook.Build(s, Faces(c), Weathers.All[0]).Rows.Single();
            Assert.AreEqual("moisture-watch", row.Concern.Kind);
            Assert.IsTrue(row.Faces.Any(x => x.Worsens)); Assert.IsFalse(row.Faces.Any(x => x.Improves));
        }
        [Test]
        public void WaterThatOvershootsFromTooDryToTooWet_IsNotCalledAnImprovement()
        {
            var s = Soil(60); int c = Sites.ById("inlet").CenterCellId; Plant(s, c, "poppy");
            var row = CareOutlook.Build(s, Faces(c), Weathers.All[0]).Rows.Single(); var water = row.Faces.Single(x => x.RecipeId == "water_drop");
            Assert.Greater(water.Moisture, row.Concern.MoistMax);
            Assert.IsTrue(water.Worsens); Assert.IsFalse(water.Improves);
        }
        [Test]
        public void WeatherAloneReturningToTheBand_IsDistinguishedFromAnImprovementByTheDice()
        {
            var s = Soil(910); var baseline = Simulation.Resolve(s, new ResolvedRecipe { Element = ElementId.Water }, Weathers.All[0], null).PostState;
            int c = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && Flora.Moisture(baseline, n) <= 900);
            Plant(s, c, "daisy", true); var plan = CareOutlook.Build(s, Faces(c), Weathers.All[0]); var row = plan.Rows.Single();
            Assert.That(row.WeatherMoisture, Is.InRange(row.Concern.MoistMin, row.Concern.MoistMax));
            Assert.That(plan.Describe(), Does.Contain("天候だけでも")); Assert.IsFalse(row.Faces.Any(x => x.Improves));
        }
        [Test]
        public void SeventhFace_UsesActualAmplification_AndDuplicateFacesKeepSeparateSlots()
        {
            var s = Soil(180); int c = Sunny(); Plant(s, c); var f = Faces(c);
            f.Faces = new[] { "water_drop", "water_drop", "wind_passage", "wind_passage", "light_beam", "light_soft", "wind_breeze" };
            var plan = CareOutlook.Build(s, f, Weathers.All[0]); var row = plan.Rows.Single();
            Assert.AreEqual(7, row.Faces.Count); var repeated = row.Faces.Where(x => x.RecipeId == "water_drop").ToList();
            Assert.AreEqual(2, repeated.Count); Assert.AreEqual(repeated[0].Moisture, repeated[1].Moisture); Assert.AreEqual(repeated[0].Improves, repeated[1].Improves);
            var actual = Simulation.Resolve(s, LuckySeven.ResolveAmplified("wind_breeze", c, s), Weathers.All[0], null);
            Assert.AreEqual(Flora.Moisture(actual.PostState, c), row.Faces[6].Moisture); Assert.AreEqual(7, row.Faces[6].FaceId);
        }
        [TestCase("rest")][TestCase("space")]
        public void ASeedWaitingForRestOrSpace_DoesNotBecomeAMoistureSuccessForecast(string waiting)
        {
            var s = Soil(); int c = Sunny();
            s.Ecosystem.Succession.SeedBank.Add(new RestingSeed { SeedId = "sleep", Species = "primrose", CellId = c, Age = 3 });
            if (waiting == "space") Plant(s, c, "daisy");
            var plan = CareOutlook.Build(s, Faces(c), Weathers.All[0]); var row = plan.Rows.Single(x => x.Concern.SubjectId == "sleep");
            Assert.AreEqual("seed-wait", row.Concern.Kind); Assert.IsFalse(row.Concern.MoistureGoal); Assert.IsFalse(row.Faces.Any(x => x.Improves));
            Assert.That(row.Concern.Advice, Does.Contain(waiting == "rest" ? "休息" : "空き地")); Assert.AreEqual(1, s.Ecosystem.Succession.SeedBank.Count);
        }
        [Test]
        public void ASeedWhoseMoistureCanImprove_StillReportsOtherRequirements()
        {
            var s = Soil(180); int c = Sunny(); s.Ecosystem.Succession.SeedBank.Add(new RestingSeed { SeedId = "sleep", Species = "iris", CellId = c, Age = 3 });
            var plan = CareOutlook.Build(s, Faces(c), Weathers.All[0]); var row = plan.Rows.Single();
            Assert.AreEqual("seed-moisture", row.Concern.Kind); Assert.IsTrue(row.Faces.Any(x => x.Improves));
            Assert.That(row.Concern.Advice, Does.Contain("上限")); Assert.AreEqual(0, s.Plants.Count);
        }
        [Test]
        public void MissingNutrients_AreNotPresentedAsSomethingLightOrWaterAloneWillSupply()
        {
            var s = Soil(); int c = Sunny(); Plant(s, c, "daisy", true); s.Ecosystem.Nutrients[c] = 0;
            var row = CareOutlook.Build(s, Faces(c), Weathers.All[0]).Rows.Single();
            Assert.AreEqual("nutrient-wait", row.Concern.Kind); Assert.IsNull(row.Concern.Reaches); Assert.IsFalse(row.Faces.Any(x => x.Improves));
            Assert.That(row.Concern.Advice, Does.Contain("水や光だけで養分は増えない"));
        }
        [Test]
        public void AYoungAquaticFamily_IsPrioritised_WithoutPromisingThatMoistureAloneRestoresItsHabitat()
        {
            var s = Soil(550); int c = Sunny(); s.Ecosystem.Succession.Community.Families.Add(new ResidentFamily { FamilyId = "pond", Kind = LivingEcosystem.Frog, CellId = c, Adults = 1, Brood = 2 });
            var plan = CareOutlook.Build(s, Faces(c), Weathers.All[0]); var row = plan.Rows.First();
            Assert.AreEqual("pond", row.Concern.SubjectId); Assert.AreEqual(750, row.Concern.MoistMin);
            Assert.That(row.Concern.Advice, Does.Contain("覆い・餌")); Assert.That(row.Concern.Advice, Does.Contain("幼生は移せない"));
        }
        [TestCase(ElementId.Light)][TestCase(ElementId.Wind)]
        public void FoodAndDispersalOpportunities_CountOnlyPublishedFacesWithSufficientAdditionalField(ElementId element)
        {
            var s = Soil(); int c = Sunny(); var p = Plant(s, c, element == ElementId.Light ? "daisy" : "dandelion", element == ElementId.Light);
            if (element == ElementId.Light) s.Ecosystem.Succession.Flowers.Add(p.PlantId, new FlowerResource());
            else s.Ecosystem.Cycles[p.PlantId].Seeds = 3;
            var plan = CareOutlook.Build(s, Faces(c), Weathers.All[0]); var row = plan.Rows.Single();
            Assert.AreEqual(element, row.Concern.Reaches); Assert.AreEqual(2, row.Faces.Count(x => x.Reaches));
            Assert.IsFalse(row.Faces.Any(x => x.Improves)); Assert.That(plan.Describe(), Does.Contain("保証ではない"));
        }
        [Test]
        public void ConcernSelection_IsBoundedAndStableUnderPlantAndSeedEnumerationOrder()
        {
            var s = Soil(180); int index = 0;
            foreach (int c in Enumerable.Range(0, 256).Where(n => GardenMapData.Plantable[n] == 1).Take(30))
            { Plant(s, c); s.Ecosystem.Succession.SeedBank.Add(new RestingSeed { SeedId = "s" + index++, CellId = c, Species = "primrose", Age = 3 }); }
            var a = CareOutlook.ReadConcerns(s).Select(x => x.SubjectId).ToArray(); s.Plants.Reverse(); s.Ecosystem.Succession.SeedBank.Reverse();
            var b = CareOutlook.ReadConcerns(s); CollectionAssert.AreEqual(a, b.Select(x => x.SubjectId)); Assert.LessOrEqual(b.Count, 15);
            Assert.IsTrue(b.GroupBy(x => x.Habitat).All(g => g.Count() <= 3));
        }
        [TestCase(true)][TestCase(false)]
        public void GardensWithoutLinkedCare_DoNotReceiveANewForecast(bool hasEcosystem)
        {
            var s = Soil(); if (hasEcosystem) s.Ecosystem.Habitat.LinkedCare = false; else s.Ecosystem = null;
            Assert.IsEmpty(CareOutlook.ReadConcerns(s)); Assert.AreEqual("", CareOutlook.Build(s, Faces(Sunny()), Weathers.All[0]).Describe());
        }
        [Test]
        public void PreparedJsonRestore_RebuildsTheSameOutlookWithoutDrawingOrChangingThePublishedRound()
        {
            var m = new Memory(); var ids = new SequentialIds(); var entropy = new FixedEntropy("outlook-restore"); var c = new GardenCoordinator(m, entropy, ids); c.CreateRun();
            for (int i = 0; i < 4; i++) { c.Prepare(); c.CommitIntent("none", null); c.Roll(); }
            c.SetCareFocus("habitat", "岩陰"); c.Prepare();
            var plan = CareOutlook.Build(c.State, c.CurrentFaces, c.CurrentWeather); Assert.IsNotEmpty(plan.Rows);
            string a = plan.Describe(); int records = m.Entries.Count;
            var restored = new GardenCoordinator(m, entropy, ids); restored.Restore(new JournalReadResult { ChainValid = true, Entries = Json(m.Entries) });
            Assert.AreEqual(a, CareOutlook.Build(restored.State, restored.CurrentFaces, restored.CurrentWeather).Describe());
            Assert.AreEqual(records, m.Entries.Count); Assert.AreEqual(CoordinatorPhase.Prepared, restored.Phase);
        }
        [Test]
        public void ReadingGuidanceAndFollowups_DoesNotChangeAnyOf96Rolls_OrTheV8GoldenHashes()
        {
            List<string> Play(bool inspect)
            {
                var m = new Memory(); var ids = new SequentialIds(); var c = new GardenCoordinator(m, new FixedEntropy("v8-golden"), ids); c.CreateRun();
                Assert.AreEqual("668ca78cd1c4d4153e0b7ad093ac0475dd8a800e2d3f1d6e263209442996e8ae", Hashing.StateHash(c.State)); var hashes = new List<string>();
                for (int i = 0; i < 96; i++)
                {
                    c.Prepare(); string before = Hashing.StateHash(c.State); int entries = m.Entries.Count;
                    if (inspect)
                    {
                        var outlook = CareOutlook.Build(c.State, c.CurrentFaces, c.CurrentWeather);
                        foreach (string habitat in HabitatRules.Labels) outlook.Describe(habitat);
                        CareAftercare.Describe(CareAftercare.Project(HistoryProjector.Project(m.Entries, c.RunId)));
                        Assert.AreEqual(before, Hashing.StateHash(c.State)); Assert.AreEqual(entries, m.Entries.Count);
                    }
                    c.CommitIntent("none", null); var result = c.Roll(); hashes.Add(Hashing.StateHash(c.State) + CanonicalJson.Write(result.Ecology.Care.ToPayload()));
                }
                Assert.AreEqual("4e19b9158377f916864048584c6eb45b08700076a0842a61be4c90d7cb3ca297", Hashing.StateHash(c.State));
                TestContext.WriteLine($"guidance inspect={inspect}: rolls={hashes.Count}, final-plants={c.State.Plants.Count}"); return hashes;
            }
            CollectionAssert.AreEqual(Play(false), Play(true));
        }

        private static RoundSummary Round(int revision, int? touched = null, string recipe = "water_drop", string habitat = "水辺", string kind = null, int at = 85)
        {
            var r = new RoundSummary { RoundId = "r" + revision, WorldRevision = revision, Resolved = true, RecipeId = recipe };
            if (touched.HasValue) r.Care = new CareReport { RecipeId = recipe, Impacts = new List<CareImpact> { new CareImpact { Habitat = habitat, Cells = new List<int> { touched.Value } } } };
            if (kind != null) r.Relations.Add(new EcologicalLink { Kind = kind, ToCell = at, FromCell = at, Actor = "seedbank", Species = "iris", Amount = 1 });
            return r;
        }
        [TestCase("seed-awakened")][TestCase("brood-lost")]
        public void LaterPositiveAndNegativeEvents_AreShownAsThePlacesSubsequentObservation(string kind)
        {
            var notes = CareAftercare.Project(new[] { Round(1, 85), Round(4, kind: kind) }); Assert.AreEqual(1, notes.Count);
            Assert.AreEqual("r1", notes[0].EarlierRoundId); Assert.AreEqual("r4", notes[0].LaterRoundId);
            Assert.That(CareAftercare.Describe(notes), Does.Contain("その3投後")); Assert.That(CareAftercare.Describe(notes), Does.Contain("ほかの投や天候"));
        }
        [Test]
        public void SameRollAndNeighbouringGround_DoNotBecomeFollowups()
        {
            Assert.IsEmpty(CareAftercare.Project(new[] { Round(1, 85, kind: "seed-awakened") }));
            Assert.IsEmpty(CareAftercare.Project(new[] { Round(1, 85), Round(2, kind: "seed-awakened", at: 86) }));
        }
        [Test]
        public void EachEventUsesTheLatestPriorContact_AndNeverTheCurrentContact()
        {
            var notes = CareAftercare.Project(new[] { Round(1, 85), Round(2, 85, "light_beam", kind: "recovered"), Round(3, kind: "fruited") });
            Assert.AreEqual(2, notes.Count); Assert.AreEqual("r2", notes[0].EarlierRoundId); Assert.AreEqual("r1", notes[1].EarlierRoundId);
            Assert.AreEqual("light_beam", notes[0].RecipeId); Assert.AreEqual("water_drop", notes[1].RecipeId);
        }
        [TestCase(12, true)][TestCase(13, false)]
        public void Followups_HaveABoundedRevisionWindow_EvenWithGaps(int age, bool appears)
        {
            var notes = CareAftercare.Project(new[] { Round(1, 85), Round(age + 1, kind: "seed-awakened") }); Assert.AreEqual(appears ? 1 : 0, notes.Count);
        }
        [Test]
        public void DuplicateRoundsAndRelations_DoNotDuplicateNotes_AndInputOrderIsNotMutated()
        {
            var a = Round(1, 85); var b = Round(2, kind: "seed-awakened"); b.Relations.Add(b.Relations[0]); var history = new List<RoundSummary> { b, a, b };
            var notes = CareAftercare.Project(history); Assert.AreEqual(1, notes.Count); Assert.AreSame(b, history[0]); Assert.AreSame(a, history[1]);
        }
        [Test]
        public void FilteringUsesTheSavedContactHabitat_AndMaterialisedNotesDoNotChangeWithTheInput()
        {
            var a = Round(1, 85); var b = Round(2, kind: "habitat-shifted"); b.Relations[0].FromHabitat = "水辺"; b.Relations[0].ToHabitat = "乾いた草地";
            var notes = CareAftercare.Project(new[] { a, b }); string description = CareAftercare.Describe(notes, "水辺");
            a.Care.Impacts[0].Habitat = "花の草地"; b.Relations[0].Kind = "unrecorded";
            Assert.AreEqual(description, CareAftercare.Describe(notes, "水辺")); Assert.AreEqual("", CareAftercare.Describe(notes, "花の草地"));
            Assert.That(description, Does.Contain("乾いた草地"));
        }
        [Test]
        public void UnresolvedRoundsAndLegacyRecords_DoNotInventContactsOrOutcomes()
        {
            var unresolved = Round(2, kind: "seed-awakened"); unresolved.Resolved = false;
            Assert.IsEmpty(CareAftercare.Project(new[] { Round(1, 85), unresolved }));
            Assert.IsEmpty(CareAftercare.Project(new[] { Round(1), Round(2, kind: "seed-awakened") }));
            Assert.AreEqual("", CareAftercare.Describe(new CareAftercareNote[0]));
        }
        [Test]
        public void LongFollowupHistory_IsBounded_AndAZeroDisplayLimitStaysEmpty()
        {
            var history = new List<RoundSummary>(); for (int i = 1; i <= 60; i++) history.Add(Round(i, 85, kind: "seed-awakened"));
            var notes = CareAftercare.Project(history); Assert.AreEqual(CareAftercare.MaxNotes, notes.Count); Assert.AreEqual(60, notes[0].LaterRevision);
            Assert.AreEqual("", CareAftercare.Describe(notes, max: 0));
        }
        [Test]
        public void Followups_ComeFromJsonRestoredJournalFacts_AndNeverCrossVisitorRuns()
        {
            var state = Soil(); var m = new Memory();
            void Save(string run, RoundSummary r)
            {
                var eco = new EcologyResult { PostState = state, Care = r.Care, Relations = r.Relations };
                m.Append("Resolved", run, r.RoundId, new Dictionary<string, object> { ["worldRevision"] = r.WorldRevision,
                    ["postState"] = WorldStateSerializer.ToPayload(state), ["ecology"] = Ecology.ToPayload(eco) });
            }
            Save("a", Round(1, 85)); Save("b", Round(2, kind: "seed-awakened"));
            Assert.IsEmpty(CareAftercare.Project(HistoryProjector.Project(Json(m.Entries), "a")));
            Assert.IsEmpty(CareAftercare.Project(HistoryProjector.Project(Json(m.Entries), "b")));
            Save("a", Round(3, kind: "seed-awakened")); var notes = CareAftercare.Project(HistoryProjector.Project(Json(m.Entries), "a"));
            Assert.AreEqual(1, notes.Count); Assert.AreEqual(3, notes[0].LaterRevision); Assert.AreEqual("r1", notes[0].EarlierRoundId);
        }
    }
}
