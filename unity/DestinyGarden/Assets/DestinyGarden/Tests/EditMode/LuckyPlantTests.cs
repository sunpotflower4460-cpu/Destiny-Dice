using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>prayer-star.v4: lucky plants and the rules of the eight.</summary>
    public class LuckyPlantTests
    {
        private sealed class Memory : IJournalWriter
        {
            public readonly List<JournalEntry> Entries = new List<JournalEntry>();
            public string HeadHash => "";
            public long NextSeq => Entries.Count + 1;
            public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
            {
                var e = new JournalEntry { Seq = Entries.Count + 1, Kind = kind, RunId = runId, RoundId = roundId, Payload = payload };
                Entries.Add(e);
                return e;
            }
        }

        private static int Marks(WorldState s) => s.Plants.Sum(p => p.LuckyMarks);

        [Test]
        public void EachLuckyNumber_MarksTheFirstPlantItsGiftReached_AndOnlyThen()
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("lucky-plants"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int lucky = 0, marked = 0;
            for (int i = 0; i < 160; i++)
            {
                coord.CommitIntent("none", null);
                var before = coord.State.Clone();
                var o = coord.Roll();
                coord.Prepare();
                var star = o.Ecology.PrayerStar;
                var post = o.Ecology.PostState;
                // Marks only ever grow on a lucky roll, by one, on the plant the star names.
                int survivingBefore = before.Plants.Where(p => post.Plants.Any(q => q.PlantId == p.PlantId)).Sum(p => p.LuckyMarks);
                int survivingAfter = post.Plants.Where(p => before.Plants.Any(q => q.PlantId == p.PlantId)).Sum(p => p.LuckyMarks)
                                     + post.Plants.Where(p => before.Plants.All(q => q.PlantId != p.PlantId)).Sum(p => p.LuckyMarks);
                if (star.LuckyNumber) lucky++;
                if (star.LuckyPlantCellId >= 0)
                {
                    marked++;
                    Assert.IsTrue(star.LuckyNumber, "only a lucky number marks a plant");
                    Assert.GreaterOrEqual(post.PlantAt(star.LuckyPlantCellId).LuckyMarks, 1);
                    Assert.IsTrue(o.Ecology.Effects.Any(e => e.Kind == PrayerStar.GiftKind && e.CellId == star.LuckyPlantCellId && e.Amount > 0),
                        "the marked plant really grew from the star's gift (prayer-star.v6)");
                    Assert.AreEqual(survivingBefore + 1, survivingAfter, "one mark per lucky number");
                }
                else Assert.AreEqual(survivingBefore, survivingAfter, "no lucky number, no new mark");
            }
            Assert.Greater(lucky, 10);
            Assert.Greater(marked, lucky / 2, "most lucky numbers find a plant to mark");
            // The marks are saved with the garden and restored exactly.
            var restored = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            restored.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            Assert.AreEqual(Hashing.StateHash(coord.State), Hashing.StateHash(restored.State));
            Assert.AreEqual(Marks(coord.State), Marks(restored.State));
            TestContext.WriteLine($"lucky={lucky} marked={marked} luckyPlantsNow={coord.State.Plants.Count(p => p.LuckyMarks > 0)}");
        }

        [Test]
        public void TheEight_IsLuckMetByPrayer()
        {
            // An answered wish on a lucky number in a harmonious garden.
            Assert.AreEqual(8, PrayerStar.Read(4, 3, "water_drop", "light_soft", true, 8).Points);
            Assert.AreEqual(7, PrayerStar.Read(4, 3, "water_drop", "light_soft", true, 7).Points, "the island must be in full harmony");
            // Yin and yang turning alone is a six, and with a lucky number stays seven.
            Assert.AreEqual(7, PrayerStar.Read(4, 3, "water_drop", "light_soft", false, 8).Points);
            // The seventh lucky number is always the eight.
            Assert.AreEqual(8, PrayerStar.Read(4, 3, "water_drop", "seed_single", false, 0, -1, 6, 0).Points);
            StringAssert.StartsWith("prayer-star.v", PrayerStar.RulesVersion);
        }
    }
}
