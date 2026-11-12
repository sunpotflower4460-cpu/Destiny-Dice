using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    internal sealed class FixedEntropy : IEntropyProvider
    {
        private readonly byte[] _seed;
        private int _counter;
        public string SourceName => "local-fixed-fixture";
        public FixedEntropy(string seed) { _seed = Encoding.UTF8.GetBytes(seed); }
        public byte[] GetBytes(int length)
        {
            var bytes = new byte[length];
            int filled = 0;
            while (filled < length)
            {
                using (var sha = SHA256.Create())
                {
                    var input = new List<byte>(_seed);
                    input.AddRange(BitConverter.GetBytes(_counter++));
                    byte[] block = sha.ComputeHash(input.ToArray());
                    int take = Math.Min(block.Length, length - filled);
                    Array.Copy(block, 0, bytes, filled, take);
                    filled += take;
                }
            }
            return bytes;
        }
    }

    internal sealed class FixedClock : IClock
    {
        public string NowIso8601() => "2026-11-12T00:00:00.0000000Z";
    }

    internal sealed class SequentialIds : IIdSource
    {
        private int _n;
        public string NewId(string prefix) => prefix + "-" + (++_n);
    }

    public class CoreTests
    {
        [Test]
        public void UniformBelow_OneConsumesNothing_AndRejectionWorks()
        {
            var buf = new EntropyBuffer(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 5 });
            Assert.AreEqual(0, buf.UniformBelow(1));
            Assert.AreEqual(0, buf.Offset);
            // n=3: limit = 4294967295, word 0xFFFFFFFF rejected, next word 5 -> 5%3=2
            Assert.AreEqual(2, buf.UniformBelow(3));
            Assert.AreEqual(1, buf.RejectedWords);
            Assert.AreEqual(8, buf.Offset);
        }

        [Test]
        public void RollD6_Rejects252AndAbove()
        {
            int face = EntropyBuffer.RollD6(new byte[] { 252, 255, 7 }, out int consumed, out int rejected);
            Assert.AreEqual(2, face); // 1 + 7%6
            Assert.AreEqual(3, consumed);
            Assert.AreEqual(2, rejected);
        }

        [Test]
        public void InitialWeather_IsAlwaysClear()
        {
            var bytes = new FixedEntropy("weather").GetBytes(32);
            Assert.AreEqual(WeatherId.Clear, WeatherSelection.Select(bytes, 0));
        }

        [Test]
        public void FaceGeneration_ConstraintsHold()
        {
            var entropy = new FixedEntropy("faces");
            var state = WorldState.CreateInitial();
            var buf = new EntropyBuffer(entropy.GetBytes(256));
            var faces = FaceGenerator.Generate(state, Weathers.All[0], "auto", null, buf);
            Assert.IsNotNull(faces);
            Assert.AreEqual(6, faces.Faces.Length);
            var families = faces.Faces.Select(RecipeCatalog.ElementOf).ToList();
            Assert.GreaterOrEqual(families.Distinct().Count(), 3, "at least 3 families");
            foreach (var g in families.GroupBy(f => f)) Assert.LessOrEqual(g.Count(), 3, "family cap 3");
            foreach (var g in faces.Faces.GroupBy(f => f)) Assert.LessOrEqual(g.Count(), 2, "recipe cap 2");
            foreach (var c in faces.Candidates.Where(c => c.Eligible))
                Assert.That(c.RecipeWeight, Is.InRange(100, 300));
        }

        [Test]
        public void Simulation_WaterBalanceIsZero_ForWaterRecipe()
        {
            var state = WorldState.CreateInitial();
            var recipe = RecipeResolver.Resolve("water_drop", 85, state);
            var result = Simulation.Resolve(state, recipe, Weathers.All[0], n => "plant-x");
            Assert.AreEqual(0, result.WaterBalanceResidual);
            Assert.AreEqual(1200, result.DiceWaterInjected);
            Assert.AreEqual(1, result.PostState.WorldRevision);
        }

        [Test]
        public void Simulation_SameInputSameStateHash()
        {
            var a = WorldState.CreateInitial();
            var b = WorldState.CreateInitial();
            var ra = Simulation.Resolve(a, RecipeResolver.Resolve("water_rain", 154, a), Weathers.All[2], n => "p");
            var rb = Simulation.Resolve(b, RecipeResolver.Resolve("water_rain", 154, b), Weathers.All[2], n => "p");
            Assert.AreEqual(Hashing.StateHash(ra.PostState), Hashing.StateHash(rb.PostState));
            Assert.AreEqual(0, ra.WaterBalanceResidual);
            Assert.Greater(ra.WeatherWaterInjected, 0, "drizzle injects weather water");
        }

        [Test]
        public void Coordinator_FullLoop_JournalRestoreKeepsSameState()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "journal.bin");
            var clock = new FixedClock();
            var journal = new FileJournal(path, clock);
            var coord = new GardenCoordinator(journal, new FixedEntropy("loop"), new SequentialIds());
            coord.CreateRun();
            string hashAfter = null;
            for (int i = 0; i < 4; i++)
            {
                var faces = coord.Prepare();
                Assert.IsNotNull(faces, "prepare " + i);
                coord.CommitIntent("none", null);
                var outcome = coord.Roll();
                Assert.IsNotNull(outcome, "roll " + i);
                hashAfter = outcome.StateHashAfter;
            }
            Assert.AreEqual(4, coord.State.WorldRevision);

            var journal2 = new FileJournal(path, clock);
            var read = journal2.ReadAll();
            Assert.IsTrue(read.ChainValid, read.Error);
            Assert.IsFalse(read.HasIncompleteTail);
            var restored = new GardenCoordinator(journal2, new FixedEntropy("unused"), new SequentialIds());
            restored.Restore(read);
            Assert.AreEqual(4, restored.State.WorldRevision);
            Assert.AreEqual(hashAfter, Hashing.StateHash(restored.State));
            Directory.Delete(dir, true);
        }

        [Test]
        public void Journal_IncompleteTailIsReportedNotHidden()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "journal.bin");
            var journal = new FileJournal(path, new FixedClock());
            journal.Append("RunStarted", "run-1", null, new Dictionary<string, object> { ["x"] = 1 });
            using (var fs = new FileStream(path, FileMode.Append)) fs.Write(new byte[] { 0x10, 0x00 }, 0, 2); // truncated frame header
            var read = journal.ReadAll();
            Assert.AreEqual(1, read.Entries.Count);
            Assert.IsTrue(read.HasIncompleteTail);
            Directory.Delete(dir, true);
        }
    }
}
