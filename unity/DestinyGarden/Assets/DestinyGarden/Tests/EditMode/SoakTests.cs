using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// Long, varied play (random wishes, sites, Lucky Seven taken or not) checking every invariant after every roll,
    /// restoring from the journal at many points and comparing with live play, and timing a roll and the journal
    /// work the app does each roll. The policy's choices come from a seeded System.Random, so the run is repeatable.
    /// </summary>
    public class SoakTests
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

        private static readonly string[] Sites = { "inlet", "shade", "meadow" };
        private static readonly string[] Families = { "water", "wind", "light", "seed" };

        private static void Decide(GardenCoordinator c, Random rng)
        {
            if (c.LuckySevenOffered && rng.Next(3) > 0) c.AddLuckySeven();
            int k = rng.Next(4);
            if (k == 0) c.CommitIntent("none", null);
            else if (k == 1) c.CommitIntent("face", (1 + rng.Next(c.CurrentFaces.Faces.Length)).ToString());
            else c.CommitIntent("family", Families[rng.Next(4)]);
        }

        private static void CheckInvariants(RollOutcome o, List<int> faces, string where)
        {
            var s = o.Ecology.PostState;
            Assert.AreEqual(o.Simulation.PostState.TotalWater(), s.TotalWater(), where + " water");
            Assert.AreEqual(s.Plants.Count, s.Plants.Select(p => p.CellId).Distinct().Count(), where + " one plant per cell");
            Assert.AreEqual(s.Plants.Count, s.Plants.Select(p => p.PlantId).Distinct().Count(), where + " unique ids");
            foreach (var p in s.Plants)
            {
                Assert.AreEqual(1, GardenMapData.Plantable[p.CellId], where + " plantable " + p.CellId);
                Assert.That(p.Growth, Is.InRange(0, 2500), where + " growth");
                Assert.AreEqual(p.Growth >= 2500, p.Stage == PlantStage.Flower, where + " flower iff 2500 at " + p.CellId);
                Assert.NotNull(Flora.Get(p.Species), where + " species " + p.CellId);
                if (p.BloomRolls > 0 || p.Pollinated) Assert.AreEqual(PlantStage.Flower, p.Stage, where + " bloom marks only on flowers");
                if (p.IdleRolls > 0) Assert.AreNotEqual(PlantStage.Flower, p.Stage, where + " idle only before flowering");
                Assert.Less(p.IdleRolls, Lifecycle.WitherAfter + 1, where + " idle bounded");
            }
            var star = o.Ecology.PrayerStar;
            Assert.AreEqual(PrayerStar.CountLuckies(faces), star.LuckyCount, where + " lucky count");
            Assert.That(star.Points, Is.InRange(5, 8));
            Assert.AreEqual(star.Points >= 8, PrayerStar.IsRainbow(o.LuckySeven, o.Simulation, o.Ecology));
            Assert.LessOrEqual(o.Ecology.Effects.Count(e => e.Seed && e.Kind != Lifecycle.FruitKind), Ecology.MaxSeedsPerRoll);
            Assert.LessOrEqual(o.Ecology.Effects.Count(e => e.Kind == Lifecycle.ReturnKind), Lifecycle.MaxReturnsPerRoll);
            Assert.LessOrEqual(o.Ecology.Effects.Count(e => e.Kind == Lifecycle.WitherKind), Lifecycle.MaxWithersPerRoll);
            Assert.NotNull(Seasons.Get(o.SeasonGoalId), where + " season goal");
            Assert.That(o.FaceId, Is.InRange(1, o.FaceCount));
        }

        private static string Snapshot(GardenCoordinator c) =>
            string.Join("|", Hashing.StateHash(c.State), c.Phase, c.RolledRecipes.Count, c.SeasonGoalId, c.SeasonMet,
                c.LuckySevenOffered, c.CurrentFaces != null ? string.Join(",", c.CurrentFaces.Faces) : "-", c.PreferenceMode, c.PreferenceSiteId);

        [Test]
        public void Soak_LongVariedPlay_KeepsEveryInvariant_AndRestoresAnywhere()
        {
            const int runs = 3, rolls = 400;
            int nulls = 0, exhausted = 0, restores = 0;
            long worstTicks = 0, totalTicks = 0; int timed = 0;
            for (int run = 0; run < runs; run++)
            {
                var rng = new Random(9000 + run);
                var journal = new Memory();
                var coord = new GardenCoordinator(journal, new FixedEntropy("soak-" + run), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                var faces = new List<int>();
                for (int i = 0; i < rolls; i++)
                {
                    if (rng.Next(10) == 0) coord.SetCareFocus(rng.Next(2) == 0 ? "auto" : "fixed", Sites[rng.Next(3)]);
                    Decide(coord, rng);
                    var sw = Stopwatch.StartNew();
                    var o = coord.Roll();
                    sw.Stop();
                    totalTicks += sw.ElapsedTicks; timed++; worstTicks = Math.Max(worstTicks, sw.ElapsedTicks);
                    if (o == null) { nulls++; coord.Prepare(); continue; }
                    faces.Add(o.FaceId);
                    if (o.Ecology.Exhausted) exhausted++;
                    CheckInvariants(o, faces, $"run {run} roll {i}");
                    coord.Prepare();
                    // Restore from the journal at many points: the restored garden must be the live one.
                    if (rng.Next(25) == 0)
                    {
                        var restored = new GardenCoordinator(new Memory(), new FixedEntropy("unused"), new SequentialIds());
                        restored.Restore(new JournalReadResult { ChainValid = true, Entries = journal.Entries.ToList() });
                        Assert.AreEqual(Snapshot(coord), Snapshot(restored), $"run {run} roll {i} restore");
                        restores++;
                    }
                }
            }
            double ms(long t) => t * 1000.0 / Stopwatch.Frequency;
            string report = $"rolls={runs * rolls} unresolved={nulls} ecologyExhausted={exhausted} restoresCompared={restores} " +
                            $"roll ms avg {ms(totalTicks) / timed:0.00} worst {ms(worstTicks):0.0}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-SOAK " + report);
            Assert.Greater(restores, 10);
        }

        [Test]
        public void Soak_SameChoices_SameGarden()
        {
            string Play()
            {
                var rng = new Random(77);
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("soak-det"), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                for (int i = 0; i < 150; i++) { Decide(coord, rng); if (coord.Roll() == null) { } coord.Prepare(); }
                return Hashing.StateHash(coord.State) + coord.SeasonGoalId + coord.RolledRecipes.Count;
            }
            Assert.AreEqual(Play(), Play());
        }

        /// <summary>What the app does with the journal each roll (read all, verify the chain, project the history), timed on disk.</summary>
        [Test]
        public void Measure_JournalCostInLongPlay()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-soak-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "journal.bin");
            var clock = new FixedClock();
            var journal = new FileJournal(path, clock);
            var coord = new GardenCoordinator(journal, new FixedEntropy("soak-journal"), new SequentialIds());
            coord.CreateRun();
            coord.Prepare();
            var lines = new List<string>();
            for (int i = 1; i <= 300; i++)
            {
                coord.CommitIntent("none", null);
                coord.Roll();
                coord.Prepare();
                if (i % 100 == 0)
                {
                    // Cold: opening the journal at start of play (read and verify everything once).
                    var sw = Stopwatch.StartNew();
                    var read = new FileJournal(path, clock).ReadAll();
                    var history = HistoryProjector.Project(read.Entries, coord.RunId);
                    sw.Stop();
                    Assert.IsTrue(read.ChainValid);
                    // Warm: what each roll costs in play (the open journal has read everything before; after one
                    // more roll it verifies only that roll's frames). Read and projection are timed apart.
                    journal.ReadAll();
                    coord.CommitIntent("none", null);
                    coord.Roll();
                    coord.Prepare();
                    var warm = Stopwatch.StartNew();
                    var again = journal.ReadAll();
                    warm.Stop();
                    var proj = Stopwatch.StartNew();
                    var history2 = HistoryProjector.Project(again.Entries, coord.RunId);
                    proj.Stop();
                    Assert.IsTrue(again.ChainValid);
                    Assert.Less(read.Entries.Count, again.Entries.Count);
                    lines.Add($"{i} rolls: journal {new FileInfo(path).Length / 1024} KiB, cold read+verify+project {sw.ElapsedMilliseconds} ms, " +
                              $"warm read {warm.Elapsed.TotalMilliseconds:F1} ms + project {proj.Elapsed.TotalMilliseconds:F1} ms, history {history2.Count}");
                }
            }
            string report = string.Join(" | ", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-JOURNAL " + report);
            Directory.Delete(dir, true);
        }
    }
}
