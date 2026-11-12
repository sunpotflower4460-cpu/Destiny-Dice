using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class HarmonyTests
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
        public void Harmony_FullBalance_IsEightAndTheEightPointedStar()
        {
            var r = Harmony.Read(new[] { "water_drop", "wind_breeze", "light_soft", "seed_single" }, 5, 7);
            Assert.AreEqual(4, r.ElementsPresent);
            Assert.IsTrue(r.Life);
            Assert.AreEqual(2, r.Yin);
            Assert.AreEqual(2, r.Yang);
            Assert.AreEqual(8, r.Value);
            Assert.AreEqual(8, r.Star);
        }

        [Test]
        public void Harmony_OneSidedGarden_IsLow()
        {
            var r = Harmony.Read(new[] { "water_drop", "water_rain", "seed_scatter" }, 2, 0);
            Assert.AreEqual(2, r.ElementsPresent);
            Assert.AreEqual(3, r.Yin);
            Assert.AreEqual(0, r.Yang);
            Assert.IsFalse(r.Life);
            Assert.AreEqual(2, r.Value);
            Assert.AreEqual(0, r.Star);
        }

        [Test]
        public void Harmony_UsesOnlyTheLastEightRolls_AndStarSteps()
        {
            // The light roll falls out of the eight-roll window.
            var rolls = new[] { "light_soft", "water_drop", "wind_breeze", "seed_single", "water_drop", "wind_breeze", "seed_single", "water_drop", "wind_breeze" };
            var r = Harmony.Read(rolls, 0, 0);
            Assert.AreEqual(3, r.ElementsPresent);
            Assert.AreEqual(5, r.Yin);
            Assert.AreEqual(3, r.Yang);
            Assert.AreEqual(3, r.Value);
            Assert.AreEqual(0, Harmony.StarFor(4));
            Assert.AreEqual(5, Harmony.StarFor(5));
            Assert.AreEqual(5, Harmony.StarFor(6));
            Assert.AreEqual(7, Harmony.StarFor(7));
            Assert.AreEqual(8, Harmony.StarFor(8));
            Assert.AreEqual(0, Harmony.Read(new string[0], 0, 0).Value);
        }

        [Test]
        public void Networks_LinkPlantsWithinTwoCells()
        {
            var s = WorldState.CreateInitial();
            var nets = Ecology.Networks(s);
            var all = nets.SelectMany(n => n).ToList();
            CollectionAssert.AreEquivalent(s.Plants.Select(p => p.CellId).ToList(), all);
            foreach (var net in nets)
                foreach (int c in net)
                    if (net.Count > 1)
                        Assert.IsTrue(net.Any(o => o != c && System.Math.Abs(o % 16 - c % 16) + System.Math.Abs(o / 16 - c / 16) <= 2));
        }

        [Test]
        public void Mycorrhiza_SharesHarmonyScaledGrowth_OnlyToYoungPlants_AndKeepsWater()
        {
            var coord = new GardenCoordinator(new Memory(), new FixedEntropy("myco"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            int shared = 0;
            for (int i = 0; i < 60; i++)
            {
                coord.CommitIntent("none", null);
                var o = coord.Roll();
                coord.Prepare();
                if (o == null) continue;
                var eco = o.Ecology;
                Assert.NotNull(eco.Harmony);
                Assert.That(eco.Harmony.Value, Is.InRange(0, 8));
                Assert.AreEqual(o.Simulation.PostState.TotalWater(), eco.PostState.TotalWater());
                var myco = eco.Effects.Where(e => e.Kind == Ecology.Mycorrhiza).ToList();
                foreach (var e in myco)
                {
                    Assert.LessOrEqual(e.Amount, eco.Harmony.Value * Ecology.MycorrhizaPerHarmony);
                    Assert.Greater(e.Amount, 0);
                }
                // Recipients are at most three per network.
                foreach (var net in eco.Networks)
                    Assert.LessOrEqual(myco.Count(e => net.Contains(e.CellId)), Ecology.MycorrhizaRecipients);
                shared += myco.Count;
            }
            Assert.Greater(shared, 0, "the network should share growth at some point in 60 rolls");
        }

        [Test]
        public void Ecology_V2_IsDeterministic_AndRecordsHarmony()
        {
            List<string> Run()
            {
                var mem = new Memory();
                var coord = new GardenCoordinator(mem, new FixedEntropy("eco-v2"), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                var hashes = new List<string>();
                for (int i = 0; i < 30; i++)
                {
                    coord.CommitIntent("family", "light");
                    var o = coord.Roll();
                    coord.Prepare();
                    if (o != null) hashes.Add(Hashing.StateHash(o.Ecology.PostState) + ":" + o.Ecology.Harmony.Value);
                }
                var history = HistoryProjector.Project(mem.Entries, coord.RunId);
                Assert.IsTrue(history.Where(h => h.WorldRevision > 0).All(h => h.Harmony >= 0));
                return hashes;
            }
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
