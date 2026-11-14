using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Presentation;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>prayer-star.v4: lucky numbers leave their mark (lucky plants); the eight is luck met by prayer; the garden's name.</summary>
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
            TestContext.WriteLine($"lucky={lucky} marked={marked} luckyPlantsNow={PrayerStarSigil.LuckyPlants(coord.State)}");
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
            Assert.AreEqual("prayer-star.v4", PrayerStar.RulesVersion);
        }

        [Test]
        public void LuckyDots_ShowTheCountWithoutWords()
        {
            string dots = PrayerStarSigil.LuckyDots(3);
            StringAssert.Contains("●●●</color>", dots);
            Assert.AreEqual(7, dots.Split('●').Length - 1, "always seven dots");
            string visible = System.Text.RegularExpressions.Regex.Replace(dots, "<[^>]+>", "");
            Assert.AreEqual(new string('●', 7), visible, "no words, no numbers: only the dots are seen");
            Assert.AreEqual(7, PrayerStarSigil.LuckyDots(9).Split('●').Length - 1);
        }

        [Test]
        public void GardenName_FollowsWhatTheGardenBecame()
        {
            var s = new WorldState();
            Assert.AreEqual("まだ名前のない庭", GardenKeepsake.Name(s, null, 0, 0));
            s.Plants.Add(new PlantState { PlantId = "a", CellId = 40, Species = "cosmos", Stage = PlantStage.Flower });
            s.Plants.Add(new PlantState { PlantId = "b", CellId = 41, Species = "daisy", Stage = PlantStage.Leaf });
            string flower = Flora.Get("cosmos").LabelJa;
            Assert.AreEqual(flower + "の庭", GardenKeepsake.Name(s, new HashSet<string>(), 0, 0), "a flower counts double");
            string withButterfly = GardenKeepsake.Name(s, new HashSet<string> { Ecology.Bee, Ecology.Butterfly }, 0, 0);
            Assert.AreEqual(flower + "と" + EcologyObservations.ResidentLabel(Ecology.Butterfly) + "の庭", withButterfly, "the most telling creature");
            StringAssert.StartsWith("虹のかかる", GardenKeepsake.Name(s, null, 3, 0));
            StringAssert.StartsWith("幸運の灯る", GardenKeepsake.Name(s, null, 0, 7));
            StringAssert.StartsWith("虹を見た", GardenKeepsake.Name(s, null, 1, 0));
            string keepsake = GardenKeepsake.SeasonKeepsake("コスモスの庭", 24, 5, 3, 1, 2);
            Assert.AreEqual("〈コスモスの庭〉 24投・ラッキーナンバー 5・幸運の株 3・八の虹 1・叶った季節 2", keepsake);
        }
    }
}
