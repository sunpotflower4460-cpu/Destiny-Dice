using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>prayer-star.v5: lucky signs from the faces alone — ゾロ目, 三つぞろい, the total of 77.</summary>
    public class LuckySignTests
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

        [Test]
        public void Signs_AreReadFromTheFaces()
        {
            var zorome = PrayerStar.Read(3, 3, "water_drop", "water_drop", false, 0, -1, 0, 0, 1, 20);
            Assert.IsTrue(zorome.Zorome); Assert.IsFalse(zorome.Triple); Assert.IsTrue(zorome.OpensBuds); Assert.IsFalse(zorome.EarnsSeventh);
            var triple = PrayerStar.Read(3, 3, "water_drop", "water_drop", false, 0, -1, 0, 0, 2, 20);
            Assert.IsTrue(triple.Triple); Assert.IsTrue(triple.EarnsSeventh);
            Assert.IsFalse(PrayerStar.Read(3, 0, "water_drop", null, false, 0).Zorome, "the first roll has no previous face");
            var total = PrayerStar.Read(4, 2, "water_drop", "seed_single", false, 0, -1, 0, 0, 1, 75);
            Assert.AreEqual(79, total.FaceTotal); Assert.IsTrue(total.Total77);
            Assert.IsFalse(total.EarnsSeventh, "prayer-star.v6: a total of 77 opens every bud instead");
            Assert.IsTrue(total.OpensBuds); Assert.IsTrue(total.OpensAllBuds); Assert.IsTrue(triple.OpensAllBuds); Assert.IsFalse(zorome.OpensAllBuds);
            Assert.IsFalse(PrayerStar.Read(1, 2, "water_drop", "seed_single", false, 0, -1, 0, 0, 1, 70).Total77);
            Assert.IsTrue(PrayerStar.Read(6, 2, "water_drop", "seed_single", false, 0, -1, 0, 0, 1, 150).Total77, "154 too");
            Assert.AreEqual(3, PrayerStar.SameRun(new List<int> { 1, 4, 4, 4 }));
            Assert.AreEqual(1, PrayerStar.SameRun(new List<int> { 4, 5 }));
            Assert.AreEqual(14, PrayerStar.Total(new List<int> { 4, 5, 5 }));
        }

        [Test]
        public void Zorome_OpensABud_AndTriple_OpensEveryBud()
        {
            var s = new WorldState();
            int[] growth = { 1600, 1700, 1800, 300, 100 };
            for (int i = 0; i < growth.Length; i++)
                s.Plants.Add(new PlantState { PlantId = "p" + i, CellId = 100 + i, Growth = growth[i], Stage = growth[i] >= 1500 ? PlantStage.Bud : PlantStage.Leaf });
            var zorome = PrayerStar.Read(2, 2, "seed_single", "seed_single", false, 0, -1, 0, 0, 1, 10);
            var one = PrayerStar.Recipients(s, zorome);
            Assert.AreEqual(1, one.Count, "a five or six star still gives one plant");
            Assert.AreEqual(PlantStage.Bud, s.PlantAt(one[0]).Stage, "a bud first");
            Assert.AreEqual(2500 - s.PlantAt(one[0]).Growth, PrayerStar.GiftFor(zorome, s.PlantAt(one[0]).Growth), "it opens");
            var triple = PrayerStar.Read(2, 2, "seed_single", "seed_single", false, 0, -1, 0, 0, 2, 10);
            var all = PrayerStar.Recipients(s, triple);
            Assert.IsTrue(new[] { 100, 101, 102 }.All(all.Contains), "every bud opens");
        }

        [Test]
        public void EarningSigns_BringTheSeventhFace_AndSurviveARestore()
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("signs"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int zorome = 0, triple = 0, total77 = 0, earnedThenOffered = 0, earned = 0, rolls = 300;
            for (int i = 0; i < rolls; i++)
            {
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                var star = o.Ecology.PrayerStar;
                if (star.Zorome) zorome++;
                if (star.Triple) triple++;
                if (star.Total77) total77++;
                coord.Prepare();
                if (star.EarnsSeventh) { earned++; Assert.IsTrue(o.EarnedSeventh); if (coord.LuckySevenOffered) earnedThenOffered++; }
            }
            Assert.Greater(zorome, 20); Assert.Greater(total77, 5);
            Assert.AreEqual(earned, earnedThenOffered, "every earning sign brings the seventh face to the next round");
            var restored = new GardenCoordinator(new Memory(), new FixedEntropy("x"), new SequentialIds());
            restored.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            Assert.AreEqual(coord.LuckySevenOffered, restored.LuckySevenOffered);
            string report = $"{rolls} rolls: ゾロ目 {zorome}, 三つぞろい {triple}, 合計77 {total77}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-SIGNS " + report);
        }
    }
}
