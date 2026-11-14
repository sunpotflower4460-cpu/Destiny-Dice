using System.Collections.Generic;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    public class EcologyTests
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

        private static IEnumerable<RollOutcome> Play(string seed, int rolls)
        {
            var coord = new GardenCoordinator(new Memory(), new FixedEntropy(seed), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            for (int i = 0; i < rolls; i++)
            {
                coord.CommitIntent("family", i % 2 == 0 ? "water" : "seed");
                var o = coord.Roll();
                coord.Prepare();
                if (o != null) yield return o;
            }
        }

        [Test]
        public void Ecology_IsDeterministicFromTheSavedBytes()
        {
            var a = Play("eco-det", 40).Select(o => Hashing.StateHash(o.Ecology.PostState)).ToList();
            var b = Play("eco-det", 40).Select(o => Hashing.StateHash(o.Ecology.PostState)).ToList();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Ecology_KeepsWater_CapsSeeds_AndRespectsTheMap()
        {
            int seededRolls = 0;
            foreach (var o in Play("eco-rules", 120))
            {
                var before = o.Simulation.PostState;
                var after = o.Ecology.PostState;
                Assert.AreEqual(before.TotalWater(), after.TotalWater(), "ecology never moves water");
                var seeds = o.Ecology.Effects.Where(e => e.Seed).ToList();
                Assert.LessOrEqual(seeds.Count(e => e.Kind != Lifecycle.FruitKind), Ecology.MaxSeedsPerRoll);
                Assert.LessOrEqual(seeds.Count(e => e.Kind == Lifecycle.FruitKind), Lifecycle.MaxReturnsPerRoll);
                foreach (var e in seeds)
                {
                    Assert.AreEqual(1, GardenMapData.Plantable[e.CellId], "seed on a plantable cell");
                    if (e.Kind == Lifecycle.FruitKind)
                        Assert.IsTrue(o.Ecology.Effects.Exists(x => x.Kind == Lifecycle.ReturnKind && x.CellId == e.CellId), "fruit only where its parent returned");
                    else Assert.IsNull(before.PlantAt(e.CellId), "seed only on an empty cell");
                    Assert.AreEqual("ecology", after.PlantAt(e.CellId).OriginKind);
                }
                if (seeds.Count > 0) seededRolls++;
                Assert.AreEqual(after.Plants.Count, after.Plants.Select(p => p.CellId).Distinct().Count(), "one plant per cell");
                foreach (var p in after.Plants) Assert.LessOrEqual(p.Growth, 2500);
                foreach (var e in o.Ecology.Effects.Where(e => !e.Seed && e.Kind != Ecology.Germinate && e.Kind != Lifecycle.ReturnKind && e.Kind != Lifecycle.WitherKind)) Assert.Greater(e.Amount, 0);
            }
            Assert.Greater(seededRolls, 0, "residents should carry some seeds over 120 rolls");
        }

        [Test]
        public void Ecology_Presence_IsAPureFunctionOfState()
        {
            var s = WorldState.CreateInitial();
            var w = Weathers.All[(int)WeatherId.Drizzle];
            var a = Ecology.Presence(s, w, new[] { "water_drop" }, null).Select(v => v.Kind + ":" + string.Join(",", v.Cells));
            var b = Ecology.Presence(s.Clone(), w, new[] { "water_drop" }, null).Select(v => v.Kind + ":" + string.Join(",", v.Cells));
            CollectionAssert.AreEqual(a.ToList(), b.ToList());
        }
    }
}
