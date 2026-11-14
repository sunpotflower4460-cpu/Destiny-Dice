using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class PrayerCommunityTests
    {
        private sealed class Memory : IJournalWriter
        {
            public string HeadHash => "";
            public long NextSeq => 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload) => new JournalEntry();
        }

        private static PlantState P(int cell, string species, PlantStage stage) =>
            new PlantState { PlantId = "p" + cell, CellId = cell, Species = species, Stage = stage, Growth = stage == PlantStage.Flower ? 2500 : 1600 };

        [Test]
        public void Find_GroupsThreeOrMoreBuddingOrFloweringPlantsOfOneKind()
        {
            var s = new WorldState();
            // Three sun-loving flowers (cosmos, poppy, lavender) side by side: a flower meadow.
            s.Plants.Add(P(100, "cosmos", PlantStage.Flower));
            s.Plants.Add(P(101, "poppy", PlantStage.Flower));
            s.Plants.Add(P(117, "lavender", PlantStage.Flower));
            // A budding sun-lover next to them does not belong yet; one two cells away is not joined.
            s.Plants.Add(P(116, "cosmos", PlantStage.Bud));
            s.Plants.Add(P(103, "poppy", PlantStage.Flower));
            // Two moisture lovers only: no wetland. A leaf-stage sun-lover far away does not count.
            s.Plants.Add(P(40, "iris", PlantStage.Flower));
            s.Plants.Add(P(41, "buttercup", PlantStage.Bud));
            s.Plants.Add(P(200, "cosmos", PlantStage.Leaf));
            var found = Communities.Find(s);
            var meadow = found.Single(c => c.Element == ElementId.Light);
            CollectionAssert.AreEqual(new[] { 100, 101, 117 }, meadow.Cells);
            Assert.AreEqual("花の野", meadow.LabelJa);
            Assert.IsFalse(found.Any(c => c.Element == ElementId.Water && c.Cells.Contains(40) && c.Cells.Count < 3));
            Assert.IsFalse(found.Any(c => c.Cells.Contains(200) || c.Cells.Contains(116)), "only flowers in bloom belong");
        }

        [Test]
        public void SpreadWeight_LeansSeedsNearACommunityTowardItsKind()
        {
            var meadow = new Community { Element = ElementId.Light, Kind = "meadow", Cells = new List<int> { 100, 101, 117 } };
            var list = new List<Community> { meadow };
            Assert.AreEqual(Communities.SpreadLeaning, Communities.SpreadWeight(list, Flora.Get("cosmos"), 101));
            Assert.AreEqual(0, Communities.SpreadWeight(list, Flora.Get("iris"), 101), "only species of the community's kind");
            Assert.AreEqual(0, Communities.SpreadWeight(list, Flora.Get("cosmos"), 160), "only near it");
            Assert.IsFalse(Communities.Belongs(Flora.Get("dandelion"), ElementId.Seed), "dandelions do not make berry thickets");
            Assert.IsFalse(Communities.Belongs(Flora.Get("dandelion"), ElementId.Wind), "nor nectar patches");
            Assert.IsTrue(Communities.Belongs(Flora.Get("wildrose"), ElementId.Seed));
        }

        [Test]
        public void Communities_FormAnswerAndCallTheirCreatures_InPlay()
        {
            int answered = 0, rolls = 0, withCommunity = 0;
            var kinds = new HashSet<string>();
            for (int run = 0; run < 4; run++)
            {
                var coord = TestGardens.LegacyPrayer(new Memory(), new FixedEntropy("community-" + run), new SequentialIds());
                coord.Prepare();
                for (int i = 0; i < 80; i++)
                {
                    coord.CommitIntent("none", null);
                    var o = coord.Roll();
                    coord.Prepare();
                    rolls++;
                    var eco = o.Ecology;
                    if (eco.Communities.Count > 0) withCommunity++;
                    foreach (var c in eco.Communities) kinds.Add(c.Kind);
                    var rolled = RecipeCatalog.ElementOf(o.RecipeId);
                    foreach (var c in eco.Answered)
                    {
                        answered++;
                        Assert.AreEqual(rolled, c.Element, "only the rolled blessing's communities answer");
                    }
                    foreach (var e in eco.Effects.Where(e => e.Kind == Communities.AnswerKind))
                        Assert.That(e.Amount, Is.InRange(1, Communities.AnswerGrowth));
                    if (eco.Answered.Count == 0) Assert.IsFalse(eco.Effects.Any(e => e.Kind == Communities.AnswerKind));
                    // A nectar patch calls butterflies (the presence is judged on the state after the simulation).
                    if (Communities.Find(o.Simulation.PostState).Any(c => c.Element == ElementId.Wind && c.Cells.Count >= Communities.CallsCreaturesAt) &&
                        o.Simulation.PostState.Plants.Any(p => p.Stage == PlantStage.Flower && (Flora.Get(p.Species)?.Butterfly ?? 1) >= 1))
                        Assert.IsTrue(eco.Has(Ecology.Butterfly), "butterflies come to a nectar patch");
                }
            }
            Assert.Greater(answered, 0, "communities answer their blessing in play");
            TestContext.WriteLine($"rolls={rolls} withCommunity={withCommunity} answered={answered} kinds={string.Join(",", kinds)}");
        }
    }
}
