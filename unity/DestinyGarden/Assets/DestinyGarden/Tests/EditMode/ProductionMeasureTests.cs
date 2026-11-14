using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>Measurements for the production review: what a roll's record is made of, and how a long garden fares.</summary>
    public class ProductionMeasureTests
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

        private static int Size(object o) => Encoding.UTF8.GetByteCount(CanonicalJson.Write(o));

        [Test]
        public void Measure_SeasonWishes_InLivingGardens()
        {
            var asked = new Dictionary<string, int>(); var met = new Dictionary<string, int>();
            int offered = 0, rolls = 0;
            for (int garden = 0; garden < 6; garden++)
            {
                var coord = new GardenCoordinator(new Memory(), new FixedEntropy("season-" + garden), new SequentialIds());
                coord.CreateRun(); coord.Prepare();
                for (int i = 1; i <= 300; i++)
                {
                    if (garden % 2 == 1) coord.CommitIntent("family", RecipeCatalog.ElementOf(coord.CurrentFaces.Faces[0]).ToString().ToLowerInvariant());
                    else coord.CommitIntent("none", null);
                    var o = coord.Roll(); coord.Prepare(); rolls++;
                    if (coord.LuckySevenOffered) offered++;
                    if (o.SeasonRollsLeft == 0 || o.SeasonMetNow)
                    {
                        if (o.SeasonRollsLeft == 0) { asked.TryGetValue(o.SeasonGoalId, out int a); asked[o.SeasonGoalId] = a + 1; }
                        if (o.SeasonMet && o.SeasonRollsLeft == 0) { met.TryGetValue(o.SeasonGoalId, out int m); met[o.SeasonGoalId] = m + 1; }
                    }
                }
            }
            string report = "seasons by goal (met/asked): " + string.Join(", ", asked.OrderBy(k => k.Key).Select(kv =>
                $"{kv.Key} {(met.TryGetValue(kv.Key, out int m) ? m : 0)}/{kv.Value}")) + $" | seventh face offered {offered}/{rolls} rounds";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-SEASONS " + report);
        }

        [Test]
        public void Measure_ColdStartStages()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dg-cold-" + System.Guid.NewGuid().ToString("N"));
            string path = System.IO.Path.Combine(dir, "journal.v1.bin");
            var clock = new FixedClock();
            var coord = new GardenCoordinator(new DestinyGarden.Infrastructure.FileJournal(path, clock), new FixedEntropy("cold"), new SequentialIds());
            coord.CreateRun(); coord.Prepare();
            for (int i = 0; i < 300; i++) { coord.CommitIntent("none", null); coord.Roll(); coord.Prepare(); }
            var t = Stopwatch.StartNew();
            var journal = new DestinyGarden.Infrastructure.FileJournal(path, clock);
            var read = journal.ReadAll();
            double readMs = t.Elapsed.TotalMilliseconds;
            t.Restart();
            int n = 0; foreach (var e in read.Entries) { e.ComputeHash(); n++; }
            double hashMs = t.Elapsed.TotalMilliseconds;
            t.Restart();
            long bytesTotal = 0; for (int k = 0; k < journal.SegmentCount(); k++) bytesTotal += System.IO.File.ReadAllBytes(journal.SegmentPath(k)).Length;
            double fileMs = t.Elapsed.TotalMilliseconds;
            t.Restart();
            var restored = new GardenCoordinator(journal, new FixedEntropy("x"), new SequentialIds());
            restored.Restore(read);
            double restoreMs = t.Elapsed.TotalMilliseconds;
            t.Restart();
            var history = HistoryProjector.Project(read.Entries, restored.RunId);
            double projectMs = t.Elapsed.TotalMilliseconds;
            string report = $"300 rolls, {n} entries, {bytesTotal / 1024} KiB in {journal.SegmentCount()} segments: open+read+verify {readMs:0} ms (of which re-hashing ≈ {hashMs:0} ms), " +
                            $"file read {fileMs:0} ms, restore {restoreMs:0} ms, project {projectMs:0} ms";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-COLD " + report);
            System.IO.Directory.Delete(dir, true);
        }

        [Test]
        public void Measure_RecordSize_AndLongGardenHealth()
        {
            var lines = new List<string>();
            foreach (string mode in new[] { "none", "family" })
            for (int garden = 0; garden < 3; garden++)
            {
                var journal = new Memory();
                var coord = new GardenCoordinator(journal, new FixedEntropy("prod-" + mode + garden), new SequentialIds());
                coord.CreateRun();
                coord.Prepare();
                double worst = 0, total = 0;
                var row = new List<string>();
                for (int i = 1; i <= 300; i++)
                {
                    if (mode == "family") coord.CommitIntent("family", RecipeCatalog.ElementOf(coord.CurrentFaces.Faces[0]).ToString().ToLowerInvariant());
                    else coord.CommitIntent("none", null);
                    var sw = Stopwatch.StartNew();
                    coord.Roll();
                    coord.Prepare();
                    sw.Stop();
                    worst = System.Math.Max(worst, sw.Elapsed.TotalMilliseconds); total += sw.Elapsed.TotalMilliseconds;
                    if (i % 50 == 0)
                    {
                        var s = coord.State;
                        int flowers = s.Plants.Count(p => p.Stage == PlantStage.Flower);
                        int buds = s.Plants.Count(p => p.Stage == PlantStage.Bud);
                        int species = s.Plants.Select(p => p.Species).Distinct().Count();
                        row.Add($"{i}: plants {s.Plants.Count} flowers {flowers} buds {buds} species {species}");
                    }
                }
                if (garden == 0)
                {
                    var resolved = journal.Entries.Last(e => e.Kind == "Resolved").Payload;
                    var post = (Dictionary<string, object>)resolved["postState"];
                    var eco = (Dictionary<string, object>)resolved["ecology"];
                    var parts = new List<string> { $"Resolved {Size(resolved) / 1024.0:0.0} KiB" };
                    foreach (var kv in post.OrderByDescending(kv => Size(kv.Value)).Take(6)) parts.Add($"postState.{kv.Key} {Size(kv.Value) / 1024.0:0.0}");
                    if (post.TryGetValue("ecosystem", out object es) && es is Dictionary<string, object> esd)
                        foreach (var kv in esd.OrderByDescending(kv => Size(kv.Value)).Take(6)) parts.Add($"ecosystem.{kv.Key} {Size(kv.Value) / 1024.0:0.0}");
                    foreach (var kv in eco.OrderByDescending(kv => Size(kv.Value)).Take(5)) parts.Add($"ecology.{kv.Key} {Size(kv.Value) / 1024.0:0.0}");
                    foreach (var kv in resolved.Where(kv => kv.Key != "postState" && kv.Key != "ecology")) parts.Add($"{kv.Key} {Size(kv.Value) / 1024.0:0.0}");
                    long all = journal.Entries.Sum(e => (long)Size(e.Payload));
                    lines.Add($"[{mode}] journal payload {all / 1024 / 300.0:0.0} KiB/roll | " + string.Join(", ", parts));
                }
                lines.Add($"[{mode} {garden}] roll+prepare avg {total / 300:0.0} ms worst {worst:0} ms | " + string.Join(" | ", row));
            }
            string report = string.Join("\n", lines);
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log("DG-PROD\n" + report);
        }
    }
}
