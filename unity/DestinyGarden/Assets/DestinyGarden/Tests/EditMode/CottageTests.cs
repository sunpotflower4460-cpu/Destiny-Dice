using System;
using System.Collections.Generic;
using System.IO;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using DestinyGarden.Presentation;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public sealed class CottageTests
    {
        [TestCase(0, CottageStage.Cabin)]
        [TestCase(3, CottageStage.Cabin)]
        [TestCase(4, CottageStage.Porch)]
        [TestCase(9, CottageStage.Porch)]
        [TestCase(10, CottageStage.Extension)]
        [TestCase(17, CottageStage.Extension)]
        [TestCase(18, CottageStage.Finished)]
        [TestCase(23, CottageStage.Finished)]
        [TestCase(24, CottageStage.Styled)]
        public void HouseBuildsInOrder(int revision, CottageStage stage)
        {
            Assert.That(CottageGrowth.Project(revision, Array.Empty<RoundSummary>()).Stage, Is.EqualTo(stage));
        }

        [TestCase("water_drop", ElementId.Water)]
        [TestCase("wind_passage", ElementId.Wind)]
        [TestCase("light_soft", ElementId.Light)]
        [TestCase("seed_scatter", ElementId.Seed)]
        public void HouseWingFollowsResolvedRoundsAndStaysBuilt(string recipe, ElementId expected)
        {
            var rounds = new List<RoundSummary> { Round(0, "light_soft") };
            for (int i = 1; i <= 10; i++)
                rounds.Add(Round(i, recipe));
            for (int i = 11; i <= 35; i++)
                rounds.Add(Round(i, "wind_breeze"));
            rounds.Add(new RoundSummary { WorldRevision = 1, RecipeId = "light_beam", Resolved = false });
            Assert.That(CottageGrowth.Project(10, rounds).Extension, Is.EqualTo(expected));
            Assert.That(CottageGrowth.Project(35, rounds).Extension, Is.EqualTo(expected));
        }

        [Test]
        public void FirstResolvedFamilyBreaksATie()
        {
            var rounds = new List<RoundSummary>();
            for (int i = 1; i <= 10; i++)
                rounds.Add(Round(i, i % 2 == 1 ? "seed_single" : "water_rain"));
            rounds.Reverse();
            Assert.That(CottageGrowth.Project(10, rounds).Extension, Is.EqualTo(ElementId.Seed));
        }

        [TestCase("water_rain", CottageStyle.RainHouse)]
        [TestCase("wind_passage", CottageStyle.Windmill)]
        [TestCase("light_beam", CottageStyle.Observatory)]
        [TestCase("seed_scatter", CottageStyle.GreenRoof)]
        public void LaterRollsChooseRoofAndKeepTheEarlierWing(string recipe, CottageStyle style)
        {
            var rounds = new List<RoundSummary>();
            for (int i = 1; i <= 10; i++) rounds.Add(Round(i, "seed_single"));
            for (int i = 11; i <= 18; i++) rounds.Add(Round(i, "light_soft"));
            for (int i = 19; i <= 24; i++) rounds.Add(Round(i, recipe));
            for (int i = 25; i <= 35; i++) rounds.Add(Round(i, "water_drop"));

            Assert.That(CottageGrowth.Project(23, rounds).Style, Is.EqualTo(CottageStyle.None));
            Assert.That(CottageGrowth.Project(24, rounds).Style, Is.EqualTo(style));
            var later = CottageGrowth.Project(35, rounds);
            Assert.That(later.Style, Is.EqualTo(style));
            Assert.That(later.Extension, Is.EqualTo(ElementId.Seed));
        }

        [Test]
        public void RoofTieUsesTheFirstResolvedFamilyInItsWindow()
        {
            var rounds = new List<RoundSummary> { Round(1, "seed_single"), Round(18, "light_beam") };
            for (int i = 19; i <= 24; i++)
                rounds.Add(Round(i, i % 2 == 1 ? "wind_breeze" : "light_soft"));
            rounds.Add(new RoundSummary { WorldRevision = 23, RecipeId = "light_soft", Resolved = false });
            rounds.Add(Round(24, "unknown"));
            rounds.Reverse();

            var house = CottageGrowth.Project(24, rounds);
            Assert.That(house.Extension, Is.EqualTo(ElementId.Seed));
            Assert.That(house.Style, Is.EqualTo(CottageStyle.Windmill));
        }

        [Test]
        public void SavedRoundsRestoreTheSameHouse()
        {
            string directory = Path.Combine(Path.GetTempPath(), "dg-house-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "journal.bin");
            try
            {
                var journal = new FileJournal(path, new FixedClock());
                var coordinator = new GardenCoordinator(journal, new FixedEntropy("house"), new SequentialIds());
                coordinator.CreateRun();
                for (int i = 0; i < CottageGrowth.StyleAt; i++)
                {
                    Assert.That(coordinator.Prepare(), Is.Not.Null);
                    coordinator.CommitIntent("none", null);
                    Assert.That(coordinator.Roll(), Is.Not.Null);
                }

                byte[] saved = File.ReadAllBytes(path);
                var original = CottageGrowth.Project(coordinator.State.WorldRevision,
                    HistoryProjector.Project(journal.ReadAll().Entries, coordinator.RunId));
                var reloaded = new FileJournal(path, new FixedClock());
                var read = reloaded.ReadAll();
                Assert.That(read.ChainValid, Is.True, read.Error);
                var restored = new GardenCoordinator(reloaded, new FixedEntropy("unused"), new SequentialIds());
                restored.Restore(read);
                var restoredHouse = CottageGrowth.Project(restored.State.WorldRevision,
                    HistoryProjector.Project(read.Entries, restored.RunId));
                Assert.That(restoredHouse.Stage, Is.EqualTo(CottageStage.Styled));
                Assert.That(restoredHouse.Extension, Is.EqualTo(original.Extension));
                Assert.That(restoredHouse.Style, Is.EqualTo(original.Style));
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static RoundSummary Round(int revision, string recipe) =>
            new RoundSummary { Resolved = true, WorldRevision = revision, RecipeId = recipe };
    }
}
