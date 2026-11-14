using System;
using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class CareTests
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
            var s = WorldState.CreateInitial(); s.Plants.Clear(); s.Ecosystem = EcosystemState.CreateCurrent(); s.WorldRevision = 1;
            for (int c = 0; c < 256; c++) { s.SoilWater[c] = GardenMapData.SoilCapacity[c] * moisture / 1000; s.Ecosystem.Habitat.Humus[c] = 100; }
            return s;
        }
        private static PlantState Plant(WorldState s, int c, string species = "daisy", int growth = 1000)
        {
            var p = new PlantState { PlantId = species + c, Species = species, CellId = c, Growth = growth,
                Stage = growth >= 2500 ? PlantStage.Flower : growth >= 750 ? PlantStage.Leaf : PlantStage.Sprout };
            s.Plants.Add(p); s.Ecosystem.Cycles.Add(p.PlantId, new PlantCycle()); return p;
        }
        private static int Sunny(WorldState s) => Enumerable.Range(0, 256).First(c => GardenMapData.Plantable[c] == 1 && GardenMapData.LightExposureQ[c] == 1000 && RecipeResolver.ShelterQEast(c) == 1000);
        private static WorldState Habitat(string label)
        {
            var s = Soil(label == "水辺" ? 800 : label == "乾いた草地" ? 200 : 600);
            if (label == "水辺") for (int c = 0; c < 256; c++) s.Ecosystem.WetAge[c] = 6;
            if (label == "草木の陰") Plant(s, Sunny(s), "sunflower");
            return s;
        }
        private static EcologyContext Context(WorldState s, string recipe, int cell, WeatherDefinition weather = null, bool matched = false)
        {
            weather = weather ?? Weathers.All[0]; var dice = RecipeResolver.Resolve(recipe, cell, s); Assert.IsTrue(dice.Eligible);
            return new EcologyContext { RoundId = "care-round", CurrentRecipeId = recipe, Dice = dice, Weather = weather, PrayerAnswered = matched,
                RecentRecipeIds = new[] { recipe }, Sim = Simulation.Resolve(s, dice, weather, n => "dice-seed-" + n, "care-round") };
        }
        private static EcologyResult Apply(EcologyContext ctx) => Ecology.Apply(ctx.Sim.PostState, ctx, new byte[64], n => "eco-child-" + n);
        private static List<JournalEntry> Json(IList<JournalEntry> entries) => entries.Select(e => new JournalEntry { Kind = e.Kind, RunId = e.RunId, RoundId = e.RoundId,
            Payload = (Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(e.Payload)).Parse() }).ToList();

        [TestCase("水辺")][TestCase("岩陰")][TestCase("草木の陰")][TestCase("乾いた草地")][TestCase("花の草地")]
        public void EachExistingHabitat_CanBeTheActualPreparedDiceDestination(string label)
        {
            var s = Habitat(label); string hash = Hashing.StateHash(s);
            var f = FaceGenerator.Generate(s, Weathers.All[0], "habitat", label, new EntropyBuffer(new byte[256]));
            Assert.AreEqual(label, LivingEcosystem.Habitat(s, CareTargets.Anchor(f))); Assert.AreEqual(label, CareTargets.Name(f.ResolvedSiteId));
            Assert.IsNull(f.TargetNote); Assert.AreEqual(6, f.Faces.Length); Assert.GreaterOrEqual(f.Faces.Select(RecipeCatalog.ElementOf).Distinct().Count(), 3);
            foreach (var group in f.Faces.GroupBy(x => x)) Assert.LessOrEqual(group.Count(), 2);
            foreach (var recipe in f.Resolved) Assert.AreEqual(f.AnchorCellId, recipe.AnchorCellId);
            Assert.AreEqual(hash, Hashing.StateHash(s));
        }
        [Test]
        public void AnAbsentHabitat_FallsBackExplicitly_WithoutInventingOrReclassifyingGround()
        {
            var s = Soil(); var f = FaceGenerator.Generate(s, Weathers.All[0], "habitat", "水辺", new EntropyBuffer(new byte[256]));
            Assert.AreEqual(f.DrawnSiteId, f.ResolvedSiteId); Assert.That(f.TargetNote, Does.Contain("水辺はまだない"));
            Assert.AreEqual(Sites.ById(f.DrawnSiteId).CenterCellId, f.AnchorCellId);
        }
        [Test]
        public void NextFocus_DoesNotMovePublishedFaces_AndCellAndIntentSurviveJsonRestore()
        {
            var m = new Memory(); var entropy = new FixedEntropy("care-pinned"); var ids = new SequentialIds(); var c = new GardenCoordinator(m, entropy, ids); c.CreateRun();
            c.SetCareFocus("habitat", "岩陰"); var f = c.Prepare(); int anchor = CareTargets.Anchor(f); var faces = (string[])f.Faces.Clone();
            c.SetCareFocus("habitat", "花の草地"); Assert.AreEqual(anchor, CareTargets.Anchor(c.CurrentFaces)); CollectionAssert.AreEqual(faces, c.CurrentFaces.Faces);
            c.CommitIntent("face", "2"); c = new GardenCoordinator(m, entropy, ids); c.Restore(new JournalReadResult { ChainValid = true, Entries = Json(m.Entries) });
            Assert.AreEqual(anchor, CareTargets.Anchor(c.CurrentFaces)); Assert.AreEqual("face", c.LastIntentKind); Assert.AreEqual("2", c.LastIntentValue);
            CollectionAssert.AreEqual(faces, c.CurrentFaces.Faces); var result = c.Roll(); Assert.AreEqual(anchor, result.Ecology.Care.AnchorCellId);
            c.Prepare(); Assert.AreEqual("花の草地", LivingEcosystem.Habitat(c.State, CareTargets.Anchor(c.CurrentFaces)));
        }

        [Test]
        public void PlottedMagic_UsesTheSelectedSoilFromTheNextPreparationAndAfterRestore()
        {
            var journal = new Memory();
            var entropy = new FixedEntropy("plotted-magic");
            var ids = new SequentialIds();
            var garden = new GardenCoordinator(journal, entropy, ids);
            garden.CreateRun();
            var published = garden.Prepare();
            int plot = Enumerable.Range(0, GardenMapData.CellCount).First(cell => cell != published.AnchorCellId && GardenMapData.Plantable[cell] == 1);
            string destination = CareTargets.CellSiteId(plot);
            Assert.Throws<ArgumentException>(() => garden.SetCareFocus(CareTargets.CellMode, "cell:0"));
            garden.SetCareFocus(CareTargets.CellMode, destination);
            Assert.AreSame(published, garden.CurrentFaces);
            garden.CommitIntent("none", null);
            Assert.AreEqual(published.AnchorCellId, garden.Roll().Ecology.Care.AnchorCellId);
            garden = new GardenCoordinator(journal, entropy, ids);
            garden.Restore(new JournalReadResult { ChainValid = true, Entries = Json(journal.Entries) });
            Assert.AreEqual(destination, garden.PreferenceSiteId);
            var prepared = garden.Prepare();
            Assert.AreEqual(plot, prepared.AnchorCellId);
            Assert.AreEqual(6, prepared.Faces.Length);
            garden.CommitIntent("none", null);
            var roll = garden.Roll();
            Assert.AreEqual(plot, roll.Ecology.Care.AnchorCellId);
            Assert.AreEqual(0, roll.Simulation.WaterBalanceResidual);
        }
        [TestCase("水辺")][TestCase("岩陰")][TestCase("草木の陰")][TestCase("乾いた草地")][TestCase("花の草地")]
        public void EveryEligibleDiceRecipe_UsesItsRealFootprintAndWaterBalance_InEachHabitat(string label)
        {
            var s = Habitat(label); var faces = FaceGenerator.Generate(s, Weathers.All[1], "habitat", label, new EntropyBuffer(new byte[256]));
            int anchor = CareTargets.Anchor(faces), played = 0;
            foreach (string recipe in RecipeCatalog.Order)
            {
                var dice = RecipeResolver.Resolve(recipe, anchor, s); if (!dice.Eligible) continue;
                var ctx = Context(s, recipe, anchor, Weathers.All[1]); var result = Apply(ctx); played++;
                Assert.AreEqual(recipe, result.Care.RecipeId); Assert.AreEqual(anchor, result.Care.AnchorCellId);
                Assert.AreEqual(0, ctx.Sim.WaterBalanceResidual); Assert.AreEqual(ctx.Sim.PostState.TotalWater(), result.PostState.TotalWater());
                foreach (var link in result.Relations.Where(x => x.Kind == "care-nectar" || x.Kind == "care-algae"))
                    Assert.GreaterOrEqual(CareRules.ExtraField(dice, ctx.Weather, link.ToCell, ElementId.Light), 100);
                if (result.Care.Impacts.Count == 0) Assert.That(result.Care.Summary(), Does.Contain("変化はなかった"));
                foreach (var impact in result.Care.Impacts)
                    foreach (int cell in impact.Cells) Assert.AreEqual(LivingEcosystem.Habitat(s, cell), impact.Habitat);
                if (dice.Element == ElementId.Seed)
                    CollectionAssert.AreEquivalent(dice.SeedCells, result.Care.Impacts.SelectMany(x => x.Cells));
                else if (ctx.Care.Touched[anchor]) Assert.IsTrue(result.Care.Impacts.Any(x => x.Habitat == label));
            }
            Assert.GreaterOrEqual(played, 6);
        }
        [Test]
        public void AnIntent_DoesNotAlterThePublishedDieOrThePhysicalReceipt()
        {
            RollOutcome Play(bool wish)
            {
                var c = new GardenCoordinator(new Memory(), new FixedEntropy("care-fair"), new SequentialIds()); c.CreateRun(); c.SetCareFocus("habitat", "岩陰"); c.Prepare();
                c.CommitIntent(wish ? "face" : "none", wish ? "1" : null); return c.Roll();
            }
            var a = Play(false); var b = Play(true);
            Assert.AreEqual(a.FaceId, b.FaceId); Assert.AreEqual(a.RecipeId, b.RecipeId);
            Assert.AreEqual(Hashing.StateHash(a.Simulation.PostState), Hashing.StateHash(b.Simulation.PostState));
            Assert.AreEqual(a.Ecology.Care.AnchorCellId, b.Ecology.Care.AnchorCellId); Assert.AreEqual(a.Ecology.Care.RetainedWaterDelta, b.Ecology.Care.RetainedWaterDelta);
            Assert.AreEqual(a.Ecology.Care.ExistingGrowthDelta, b.Ecology.Care.ExistingGrowthDelta);
        }
        [Test]
        public void WeatherAlone_DoesNotCreateDiceContactOrPrayerTargets()
        {
            var s = Soil(); int c = Sunny(s); Plant(s, c);
            var ctx = new EcologyContext { RoundId = "control", CurrentRecipeId = "water_drop", Weather = Weathers.All[2], PrayerAnswered = true,
                Dice = new ResolvedRecipe { RecipeId = "water_drop", Element = ElementId.Water, AnchorCellId = c }, RecentRecipeIds = new[] { "water_drop" } };
            ctx.Sim = Simulation.Resolve(s, ctx.Dice, ctx.Weather, null); var result = Apply(ctx);
            Assert.IsFalse(ctx.Care.Touched.Any(x => x)); Assert.AreEqual(0, result.Care.RetainedWaterDelta); Assert.AreEqual(0, result.Care.ExistingGrowthDelta);
            Assert.IsNull(result.Visit(Ecology.Spirit)); Assert.IsEmpty(result.Care.Impacts);
        }
        [Test]
        public void InvalidFocus_IsRejectedBeforeSaving_AndOldGardensCannotOptInSilently()
        {
            var m = new Memory(); var c = new GardenCoordinator(m, new FixedEntropy("invalid"), new SequentialIds()); c.CreateRun(); int count = m.Entries.Count;
            Assert.Throws<ArgumentException>(() => c.SetCareFocus("habitat", "unknown")); Assert.Throws<ArgumentException>(() => c.SetCareFocus("fixed", "unknown"));
            Assert.Throws<ArgumentException>(() => c.SetCareFocus("bogus", null)); Assert.AreEqual(count, m.Entries.Count);
            c.State.Ecosystem.Habitat.LinkedCare = false; Assert.Throws<ArgumentException>(() => c.SetCareFocus("habitat", "岩陰"));
        }
        [Test]
        public void LuckySeven_UsesThePinnedHabitatCell_AndTheSameEcologicalNeeds()
        {
            var s = Habitat("岩陰"); var f = FaceGenerator.Generate(s, Weathers.All[0], "habitat", "岩陰", new EntropyBuffer(new byte[256]));
            int anchor = CareTargets.Anchor(f); string chosen = LuckySeven.PickRecipe(s, Weathers.All[0], anchor);
            int score = FaceGenerator.ComputeLocalNeedQ(RecipeResolver.Resolve(chosen, anchor, s), s, Weathers.All[0]);
            foreach (string recipe in RecipeCatalog.Order)
            { var r = RecipeResolver.Resolve(recipe, anchor, s); if (r.Eligible) Assert.LessOrEqual(FaceGenerator.ComputeLocalNeedQ(r, s, Weathers.All[0]), score); }
            Assert.AreEqual(anchor, LuckySeven.ResolveAmplified(chosen, anchor, s).AnchorCellId);
        }
        [Test]
        public void RipeWindSeeds_RespondOnlyToTheEffectiveLocalWind_AndTravelWithinItsReach()
        {
            var s = Soil(); int near = Sunny(s); int far = Enumerable.Range(0, 256).First(c => GardenMapData.Plantable[c] == 1 && LivingEcosystem.Distance(c, near) > 10);
            var p = Plant(s, near, "dandelion"); var other = Plant(s, far, "dandelion");
            s.Ecosystem.Cycles[p.PlantId].Seeds = s.Ecosystem.Cycles[other.PlantId].Seeds = 3;
            var ctx = Context(s, "wind_passage", near); var result = Apply(ctx);
            var seeds = result.PostState.Ecosystem.Succession.SeedBank.Where(x => x.Carrier == Ecology.Fluff).ToList(); Assert.IsNotEmpty(seeds);
            foreach (var seed in seeds)
            { Assert.AreEqual(p.PlantId, seed.ParentPlantId); Assert.AreEqual("dandelion", seed.Species); Assert.That(seed.CellId % 16 - near % 16, Is.InRange(2, CareRules.WindDistance(ctx.Dice, ctx.Weather, near))); }
            Assert.IsFalse(result.Visit(Ecology.Fluff).Cells.Contains(far)); Assert.Greater(result.Care.Impacts.Sum(x => x.WindSeeds), 0);
        }
        [Test]
        public void RockShelterAndFieldFalloff_WeakenWindAndLight_EvenOnTheSeventhFace()
        {
            var s = Soil(); int sheltered = Enumerable.Range(0, 256).First(c => GardenMapData.Plantable[c] == 1 && RecipeResolver.ShelterQEast(c) == 250);
            var ordinary = RecipeResolver.Resolve("wind_passage", sheltered, s); var lucky = LuckySeven.ResolveAmplified("wind_passage", sheltered, s);
            Assert.AreEqual(250, CareRules.ExtraField(ordinary, Weathers.All[0], sheltered, ElementId.Wind));
            Assert.AreEqual(250, CareRules.ExtraField(lucky, Weathers.All[0], sheltered, ElementId.Wind));
            Assert.Less(CareRules.WindDistance(lucky, Weathers.All[0], sheltered), 5);
            var light = LuckySeven.ResolveAmplified("light_beam", sheltered, s);
            Assert.LessOrEqual(CareRules.ExtraField(light, Weathers.All[0], sheltered, ElementId.Light), 500 * GardenMapData.LightExposureQ[sheltered] / 1000);
        }
        [Test]
        public void LightNectar_UsesOnlyAnIlluminatedHealthyFlowersActualNutrient_AndCanBeEaten()
        {
            var s = Soil(); int c = Sunny(s), far = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && LivingEcosystem.Distance(c, n) > 5);
            var p = Plant(s, c, growth: 2500); var other = Plant(s, far, growth: 2500); SuccessionRules.RefreshNectar(s, Weathers.All[1]);
            int n0 = s.Ecosystem.Nutrients[c], f0 = s.Ecosystem.Succession.Flowers[p.PlantId].Nectar;
            var ctx = Context(s, "light_beam", c, Weathers.All[1]); var r = new EcologyResult { PostState = s }; CareRules.LightNectar(s, ctx, r);
            Assert.AreEqual(f0 + 1, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar); Assert.AreEqual(n0 - 1, s.Ecosystem.Nutrients[c]);
            Assert.AreEqual(1, s.Ecosystem.Succession.Flowers[other.PlantId].Nectar);
            Assert.AreEqual(1, r.Relations.Single(x => x.Kind == "care-nectar").Amount);
            Assert.IsTrue(SuccessionRules.Forage(s, p, Ecology.Bee, r)); Assert.AreEqual(f0, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar);
        }
        [TestCase("empty-nutrients")][TestCase("full-nectar")][TestCase("stressed")][TestCase("wrong-moisture")]
        public void LightCannotSupplyNectar_WhenARealRequirementIsMissing(string missing)
        {
            var s = Soil(); int c = Sunny(s); var p = Plant(s, c, growth: 2500); SuccessionRules.RefreshNectar(s, Weathers.All[1]);
            if (missing == "empty-nutrients") s.Ecosystem.Nutrients[c] = 0;
            if (missing == "full-nectar") s.Ecosystem.Succession.Flowers[p.PlantId].Nectar = 6;
            if (missing == "stressed") s.Ecosystem.Cycles[p.PlantId].Stress = 12;
            if (missing == "wrong-moisture") s.SoilWater[c] = 0;
            int before = s.Ecosystem.Succession.Flowers[p.PlantId].Nectar, nutrients = s.Ecosystem.Nutrients[c]; var r = new EcologyResult { PostState = s };
            CareRules.LightNectar(s, Context(s, "light_beam", c), r);
            Assert.AreEqual(before, s.Ecosystem.Succession.Flowers[p.PlantId].Nectar); Assert.AreEqual(nutrients, s.Ecosystem.Nutrients[c]); Assert.IsEmpty(r.Relations);
        }
        [Test]
        public void LocalLight_SupportsPondFood_OnlyWhereWetHistoryAndNutrientsAlreadyExist()
        {
            var s = Habitat("水辺"); int c = Sunny(s); var ctx = Context(s, "light_beam", c); var r = new EcologyResult { PostState = s };
            CommunityRules.GrowPond(s, ctx, r); Assert.AreEqual(1, r.Relations.Count(x => x.Kind == "care-algae"));
            Assert.AreEqual(c, r.Relations.Single(x => x.Kind == "care-algae").ToCell);
            s.Ecosystem.Nutrients[c] = 0; r.Relations.Clear(); CommunityRules.GrowPond(s, ctx, r); Assert.IsFalse(r.Relations.Any(x => x.Kind == "care-algae"));
            s.Ecosystem.Nutrients[c] = 10; s.Ecosystem.WetAge[c] = 0; r.Relations.Clear(); CommunityRules.GrowPond(s, ctx, r); Assert.IsFalse(r.Relations.Any(x => x.Kind == "care-algae"));
        }
        [Test]
        public void WaterReceipt_TracksDownhillEffects_AndSeparatesWeatherFromTheDice()
        {
            var s = Soil(200); var ctx = Context(s, "water_drop", 85, Weathers.All[2]); string hash = Hashing.StateHash(s);
            var m = CareMeasure.Measure(ctx); var baseline = Simulation.Resolve(s, new ResolvedRecipe { Element = ElementId.Water }, ctx.Weather, null);
            Assert.AreEqual(ctx.Sim.PostState.TotalWater() - baseline.PostState.TotalWater(), m.WaterDelta.Sum()); Assert.AreEqual(hash, Hashing.StateHash(s));
            Assert.IsTrue(Enumerable.Range(0, 256).Any(c => m.WaterDelta[c] != 0 && !ctx.Sim.Evidence.DiceWaterCells.Contains(c)));
            var r = Apply(ctx); Assert.AreEqual(m.WaterDelta.Sum(), r.Care.RetainedWaterDelta); Assert.AreEqual(0, ctx.Sim.WaterBalanceResidual);
            Assert.AreEqual(ctx.Sim.PostState.TotalWater(), r.PostState.TotalWater());
        }
        [Test]
        public void WindCanReduceGrowthInDryGround_AndTheReceiptDoesNotPresentThatAsABenefit()
        {
            var s = Soil(200); int c = Sunny(s); Plant(s, c); var ctx = Context(s, "wind_passage", c); var r = Apply(ctx);
            Assert.Less(r.Care.RetainedWaterDelta, 0); Assert.Less(r.Care.ExistingGrowthDelta, 0); Assert.That(r.Care.Summary(), Does.Contain("少なかった"));
        }
        [Test]
        public void AnsweredPrayer_FeedsOnlyPlantsReachedByThisDice_NotDistantWeatherGrowth()
        {
            var s = Soil(); int c = Sunny(s); int far = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && LivingEcosystem.Distance(c, n) > 8);
            Plant(s, c, growth: 250); Plant(s, far, growth: 250); var ctx = Context(s, "light_beam", c, Weathers.All[1], true); var r = Apply(ctx);
            Assert.IsNotEmpty(r.Effects.Where(x => x.Kind == Ecology.Spirit));
            foreach (var effect in r.Effects.Where(x => x.Kind == Ecology.Spirit)) { Assert.IsTrue(ctx.Care.Touched[effect.CellId]); Assert.AreNotEqual(far, effect.CellId); }
        }
        [TestCase("水辺")][TestCase("岩陰")][TestCase("草木の陰")][TestCase("乾いた草地")][TestCase("花の草地")]
        public void SeedInput_HasAnActualOriginAndReceipt_InEveryHabitat(string label)
        {
            var s = Habitat(label); int c = Enumerable.Range(0, 256).First(n => GardenMapData.Plantable[n] == 1 && s.PlantAt(n) == null && LivingEcosystem.Habitat(s, n) == label);
            var ctx = Context(s, "seed_single", c); var r = Apply(ctx); var p = r.PostState.Plants.Single(x => x.PlantId == "dice-seed-0");
            Assert.AreEqual("seedInput", p.OriginKind); Assert.AreEqual("care-round", p.SeedSourceRoundId); Assert.NotNull(Flora.Get(p.Species));
            Assert.AreEqual(1, r.Care.Impacts.Sum(x => x.Planted)); Assert.IsTrue(r.Care.Impacts.Any(x => x.Habitat == label && x.Cells.Contains(c)));
        }
        [Test]
        public void EcologicalNeeds_RespondToLocalLife_WithoutRequiringAChangeToThePublishedDie()
        {
            var s = Soil(); int c = Sunny(s); var p = Plant(s, c, "dandelion"); var wind = RecipeResolver.Resolve("wind_passage", c, s);
            int before = FaceGenerator.ComputeLocalNeedQ(wind, s, Weathers.All[0]); s.Ecosystem.Cycles[p.PlantId].Seeds = 3;
            Assert.Greater(FaceGenerator.ComputeLocalNeedQ(wind, s, Weathers.All[0]), before);
            s.Ecosystem.Cycles[p.PlantId].Stress = 12; Assert.AreEqual(before, FaceGenerator.ComputeLocalNeedQ(wind, s, Weathers.All[0]));
            var seed = RecipeResolver.Resolve("seed_single", c + 1, s); int empty = FaceGenerator.ComputeLocalNeedQ(seed, s, Weathers.All[0]);
            foreach (int cell in RecipeResolver.SeedBandCells(c + 1, 0, 1)) s.Ecosystem.Succession.SeedBank.Add(new RestingSeed { CellId = cell, Species = "dandelion" });
            Assert.Less(FaceGenerator.ComputeLocalNeedQ(seed, s, Weathers.All[0]), empty);
        }
        [Test]
        public void GuidanceAndRecordedReceipt_AreReadOnly_AndDoNotReinterpretALaterGarden()
        {
            var s = Soil(); int c = Sunny(s); var r = Apply(Context(s, "light_beam", c)); var payload = CareReport.FromPayload((Dictionary<string, object>)new SimpleJsonParser(CanonicalJson.Write(r.Care.ToPayload())).Parse());
            string text = payload.Describe(), hash = Hashing.StateHash(s); var f = FaceGenerator.Generate(s, Weathers.All[0], "habitat", "岩陰", new EntropyBuffer(new byte[256]));
            Assert.That(CareTargets.PreparedPlan(s, f, Weathers.All[0]), Does.Contain(CareTargets.Position(f.AnchorCellId)));
            foreach (string label in HabitatRules.Labels) Assert.That(CareTargets.Guidance(s, label), Does.Contain(label));
            Assert.AreEqual(hash, Hashing.StateHash(s)); s.Plants.Clear(); Array.Clear(s.SoilWater, 0, 256); Assert.AreEqual(text, payload.Describe());
        }
        [Test]
        public void V7Gardens_KeepTheirExactInitialAnd96RollHashes_AndDoNotAcquireTheNewFlag()
        {
            var s = WorldState.CreateInitial(); s.Ecosystem = EcosystemState.CreateCurrent(); s.Ecosystem.Habitat.LinkedCare = false;
            Assert.AreEqual("fb35c4d8310fb9f7fd49b6f2db0bbf10bfff142a8cdd5f841d385e4e863e11fc", Hashing.StateHash(s));
            Assert.IsFalse(HabitatSerializer.ToPayload(s.Ecosystem.Habitat).ContainsKey("linkedCare"));
            var m = new Memory(); var ids = new SequentialIds(); m.Append("RunStarted", ids.NewId("run"), null, new Dictionary<string, object> { ["initialState"] = WorldStateSerializer.ToPayload(s) });
            var c = new GardenCoordinator(m, new FixedEntropy("v7-golden"), ids); c.Restore(new JournalReadResult { ChainValid = true, Entries = Json(m.Entries) });
            for (int i = 0; i < 96; i++) { c.Prepare(); c.CommitIntent("none", null); c.Roll(); }
            Assert.AreEqual("4c849755daac0320dd5c2da45f9419a5fb2c8b182326b817572f60eed60b07e6", Hashing.StateHash(c.State)); Assert.IsFalse(CareRules.Enabled(c.State));
        }
        [Test]
        public void HabitatFocusedPlay_StaysBounded_AndPinnedFacesAndReceiptsSurvivePeriodicRestore()
        {
            for (int garden = 0; garden < 2; garden++)
            {
                List<string> Play(bool resume)
                {
                    var m = new Memory(); var ids = new SequentialIds(); var entropy = new FixedEntropy("care-garden-" + garden); var c = new GardenCoordinator(m, entropy, ids); c.CreateRun();
                    var hashes = new List<string>(); int fallback = 0, nectar = 0, algae = 0; var seen = new HashSet<string>();
                    for (int i = 0; i < 192; i++)
                    {
                        c.SetCareFocus("habitat", HabitatRules.Labels[i % 5]); var f = c.Prepare(); int anchor = CareTargets.Anchor(f);
                        if (f.TargetNote != null) fallback++;
                        c.CommitIntent("none", null);
                        if (resume && i % 24 == 23)
                        { c = new GardenCoordinator(m, entropy, ids); c.Restore(new JournalReadResult { ChainValid = true, Entries = Json(m.Entries) }); Assert.AreEqual(anchor, CareTargets.Anchor(c.CurrentFaces)); }
                        var r = c.Roll(); Assert.AreEqual(anchor, r.Ecology.Care.AnchorCellId); Assert.AreEqual(r.RecipeId, r.Ecology.Care.RecipeId); seen.Add(r.RecipeId);
                        Assert.AreEqual(0, r.Simulation.WaterBalanceResidual); Assert.AreEqual(r.Simulation.PostState.TotalWater(), c.State.TotalWater());
                        Assert.IsTrue(c.State.Ecosystem.Nutrients.All(x => x >= 0 && x <= 100)); Assert.IsTrue(c.State.Ecosystem.Habitat.Humus.All(x => x >= 0 && x <= 100));
                        Assert.LessOrEqual(c.State.Ecosystem.Succession.SeedBank.Count, 96); Assert.LessOrEqual(c.State.Ecosystem.Succession.Community.Families.Count, 10);
                        foreach (var impact in r.Ecology.Care.Impacts)
                        { nectar += impact.Nectar; algae += impact.Algae; foreach (int cell in impact.Cells) Assert.AreEqual(impact.Habitat, LivingEcosystem.Habitat(r.Simulation.Evidence.StateBefore, cell)); }
                        var saved = HistoryProjector.Project(Json(m.Entries.Skip(Math.Max(0, m.Entries.Count - 9)).ToList()), c.RunId).Last(x => x.Resolved);
                        Assert.AreEqual(r.Ecology.Care.Describe(), saved.Care.Describe());
                        hashes.Add(Hashing.StateHash(c.State) + CanonicalJson.Write(r.Ecology.Care.ToPayload()));
                    }
                    TestContext.WriteLine($"care garden {garden}, restore={resume}: plants={c.State.Plants.Count}, recipes={seen.Count}, fallback={fallback}, nectar={nectar}, algae={algae}");
                    Assert.GreaterOrEqual(seen.Count, 6); return hashes;
                }
                CollectionAssert.AreEqual(Play(false), Play(true));
            }
        }
    }
}
