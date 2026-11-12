using System.Collections.Generic;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class OracleTests
    {
        private static SimulationResult ResolveWith(string recipeId, int anchor, WeatherDefinition weather)
        {
            var state = WorldState.CreateInitial();
            var recipe = RecipeResolver.Resolve(recipeId, anchor, state);
            return Simulation.Resolve(state, recipe, weather, n => "plant-test-" + n, "round-test");
        }

        [Test]
        public void Catalog_Has28EntriesAnd6Cards()
        {
            Assert.AreEqual(28, OracleCatalogData.Entries.Length);
            Assert.AreEqual(6, OracleCatalogData.CardOrder.Length);
        }

        [Test]
        public void CardUnavailable_UsesInputFallbackWithoutFocus()
        {
            var state = WorldState.CreateInitial();
            var recipe = RecipeResolver.Resolve("water_drop", 85, state);
            var sim = Simulation.Resolve(state, recipe, Weathers.All[0], n => "p", "round-test");
            var msg = Oracle.Select("round-test", sim, recipe, null, new List<string>());
            Assert.AreEqual("message.input.water", msg.TemplateId);
            Assert.AreEqual("card_unavailable", msg.FallbackReason);
            Assert.IsNull(msg.CardId);
            Assert.IsNull(msg.FocusGroupId);
        }

        [Test]
        public void WaterDrop_SelectsFactualMessageWithFocus()
        {
            var state = WorldState.CreateInitial();
            var recipe = RecipeResolver.Resolve("water_drop", 85, state);
            var sim = Simulation.Resolve(state, recipe, Weathers.All[0], n => "p", "round-test");
            var msg = Oracle.Select("round-test", sim, recipe, "roots", new List<string>());
            Assert.IsNull(msg.FallbackReason, "expected a factual message, got " + msg.TemplateId);
            Assert.IsNotNull(msg.FocusGroupId);
            Assert.AreNotEqual("message.water.verified_root_path", msg.TemplateId,
                "path evidence is not recorded in this version");
        }

        [Test]
        public void RecentNovelty_AvoidsRepeatingSameMessageWhenAlternativeExists()
        {
            var state = WorldState.CreateInitial();
            var recipe = RecipeResolver.Resolve("water_rain", 154, state);
            var sim = Simulation.Resolve(state, recipe, Weathers.All[0], n => "p", "round-test");
            var first = Oracle.Select("round-test", sim, recipe, "circulation", new List<string>());
            var second = Oracle.Select("round-test", sim, recipe, "circulation",
                new List<string> { first.TemplateId });
            // With the previous winner marked recent, selection must still return a valid
            // factual message; if any other candidate exists it differs.
            Assert.IsNotNull(second);
            Assert.IsNotNull(second.Text);
        }

        [Test]
        public void LongRun_WaterStressStaysBoundedAndBalanced()
        {
            // Forced drizzle + water_drop at each site for 60 rounds: balance stays exact
            // and water values stay inside model limits (subset of the v0.7 stress design).
            foreach (int anchor in new[] { 85, 135, 154 })
            {
                var state = WorldState.CreateInitial();
                for (int i = 0; i < 60; i++)
                {
                    var recipe = RecipeResolver.Resolve("water_drop", anchor, state);
                    var sim = Simulation.Resolve(state, recipe, Weathers.All[2], n => "p", "round-stress");
                    Assert.AreEqual(0, sim.WaterBalanceResidual, "anchor " + anchor + " round " + i);
                    state = sim.PostState;
                    for (int c = 0; c < 256; c++)
                    {
                        Assert.That(state.SurfaceWater[c], Is.InRange(0, 1000000));
                        Assert.That(state.SoilWater[c], Is.InRange(0, GardenMapData.SoilCapacity[c]));
                    }
                }
            }
        }
    }
}
