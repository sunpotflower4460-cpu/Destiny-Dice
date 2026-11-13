using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class SeasonTests
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
        public void Seasons_AreTwelveRolls()
        {
            Assert.AreEqual(0, Seasons.IndexOf(1)); Assert.AreEqual(0, Seasons.IndexOf(12)); Assert.AreEqual(1, Seasons.IndexOf(13));
            Assert.AreEqual(11, Seasons.RollsLeft(1)); Assert.AreEqual(0, Seasons.RollsLeft(12)); Assert.AreEqual(11, Seasons.RollsLeft(13));
        }

        [Test]
        public void Pick_AsksForSomethingTheGardenLacks()
        {
            var s = WorldState.CreateInitial();
            var g = Seasons.Pick(0, s, new string[0], Weathers.All[0]);
            Assert.IsFalse(Seasons.IsMet(g.Id, s, Communities.Find(s), 0, false), "the first wish is something the bare garden lacks");
            Assert.AreEqual(g.Id, Seasons.Pick(0, s, new string[0], Weathers.All[0]).Id, "deterministic");
        }

        [Test]
        public void InPlay_OneGoalPerSeason_MetOnce_BringsTheSeventhFace_AndSurvivesRestore()
        {
            var journal = new Memory();
            var coord = new GardenCoordinator(journal, new FixedEntropy("season"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            var bySeason = new Dictionary<int, string>();
            var metCount = new Dictionary<int, int>();
            int met = 0;
            RollOutcome last = null;
            for (int i = 0; i < 120; i++)
            {
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                coord.Prepare();
                last = o;
                int season = Seasons.IndexOf(o.Ecology.PostState.WorldRevision);
                if (bySeason.TryGetValue(season, out string g)) Assert.AreEqual(g, o.SeasonGoalId, "one goal per season");
                else bySeason[season] = o.SeasonGoalId;
                Assert.NotNull(Seasons.Get(o.SeasonGoalId));
                if (o.SeasonMetNow)
                {
                    met++;
                    metCount[season] = metCount.TryGetValue(season, out int c) ? c + 1 : 1;
                    Assert.IsTrue(coord.LuckySevenOffered, "a season's wish come true brings the seventh face (no wish was made)");
                }
            }
            Assert.IsTrue(metCount.Values.All(c => c == 1), "met at most once a season");
            Assert.Greater(met, 0, "some seasons' wishes come true in 120 rolls");
            TestContext.WriteLine($"seasons={bySeason.Count} met={met} goals={string.Join(",", bySeason.OrderBy(k => k.Key).Select(k => k.Value))}");

            var resumed = new GardenCoordinator(new Memory(), new FixedEntropy("other"), new SequentialIds());
            resumed.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries });
            Assert.AreEqual(coord.SeasonGoalId, resumed.SeasonGoalId);
            Assert.AreEqual(coord.SeasonMet, resumed.SeasonMet);
            Assert.AreEqual(coord.LuckySevenOffered, resumed.LuckySevenOffered);
        }
    }
}
