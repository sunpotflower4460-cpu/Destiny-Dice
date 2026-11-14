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
    /// <summary>framed-json.v2: compressed frames and size-capped segments, readable alongside v1 journals.</summary>
    public class JournalSegmentTests
    {
        private static string NewDir() => Path.Combine(Path.GetTempPath(), "dg-seg-" + Guid.NewGuid().ToString("N"));

        private static GardenCoordinator Play(IJournalWriter journal, string seed, int rolls)
        {
            var coord = new GardenCoordinator(journal, new FixedEntropy(seed), new SequentialIds());
            coord.CreateRun();
            for (int i = 0; i < rolls; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            return coord;
        }

        [Test]
        public void Compression_RoundTrips_AndRejectsDamage()
        {
            var rng = new System.Random(7);
            foreach (int n in new[] { 0, 1, 3, 4, 5, 300, 70000 })
            {
                var data = new byte[n];
                for (int i = 0; i < n; i++) data[i] = (byte)(i % 97 < 40 ? 'a' + i % 7 : rng.Next(256));
                CollectionAssert.AreEqual(data, FrameCompression.Decompress(FrameCompression.Compress(data)), "length " + n);
            }
            byte[] json = Encoding.UTF8.GetBytes(CanonicalJson.Write(WorldStateSerializer.ToPayload(WorldState.CreateInitial())));
            byte[] packed = FrameCompression.Compress(json);
            CollectionAssert.AreEqual(json, FrameCompression.Decompress(packed));
            Assert.Less(packed.Length, json.Length / 2, "the garden's JSON compresses well");
            CollectionAssert.AreEqual(packed, FrameCompression.Compress(json), "deterministic");
            Assert.Throws<FormatException>(() => FrameCompression.Decompress(packed.Take(packed.Length - 1).ToArray()));
            var bad = (byte[])packed.Clone(); bad[0] ^= 0x10;
            Assert.Throws<FormatException>(() => FrameCompression.Decompress(bad));
        }

        [Test]
        public void SmallSegments_GiveTheSameJournal_AsOneFile()
        {
            string dir = NewDir();
            var one = new FileJournal(Path.Combine(dir, "one", "journal.v1.bin"), new FixedClock());
            var split = new FileJournal(Path.Combine(dir, "split", "journal.v1.bin"), new FixedClock(), 48 * 1024);
            var a = Play(one, "same", 40);
            var b = Play(split, "same", 40);
            Assert.AreEqual(1, one.SegmentCount());
            Assert.Greater(split.SegmentCount(), 3, "the journal rotated into several segments");
            var ra = new FileJournal(Path.Combine(dir, "one", "journal.v1.bin"), new FixedClock()).ReadAll();
            var rb = new FileJournal(Path.Combine(dir, "split", "journal.v1.bin"), new FixedClock(), 48 * 1024).ReadAll();
            Assert.IsTrue(rb.ChainValid);
            Assert.IsFalse(rb.HasIncompleteTail);
            CollectionAssert.AreEqual(ra.Entries.Select(e => e.EntryHash).ToList(), rb.Entries.Select(e => e.EntryHash).ToList(), "the same chain");
            var restored = new GardenCoordinator(new FileJournal(Path.Combine(dir, "split", "journal.v1.bin"), new FixedClock(), 48 * 1024),
                new FixedEntropy("x"), new SequentialIds());
            restored.Restore(rb);
            Assert.AreEqual(Hashing.StateHash(b.State), Hashing.StateHash(restored.State));
            // Every segment but the last is about the segment size; the last is the only one that grows.
            for (int k = 0; k < split.SegmentCount() - 1; k++)
                Assert.Less(new FileInfo(split.SegmentPath(k)).Length, 48 * 1024 + 64 * 1024);
            long whole = Enumerable.Range(0, split.SegmentCount()).Sum(k => new FileInfo(split.SegmentPath(k)).Length);
            TestContext.WriteLine($"40 rolls: {whole / 1024} KiB in {split.SegmentCount()} segments");
            Directory.Delete(dir, true);
        }

        /// <summary>A journal written in framed-json.v1 (uncompressed frames, one file) by an earlier build.</summary>
        private static void WriteV1(string path, IList<JournalEntry> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var fs = new FileStream(path, FileMode.Create))
                foreach (var e in entries)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(CanonicalJson.Write(new Dictionary<string, object>
                    {
                        ["seq"] = e.Seq, ["kind"] = e.Kind, ["runId"] = e.RunId, ["roundId"] = e.RoundId, ["createdAt"] = e.CreatedAt,
                        ["prevHash"] = e.PrevHash, ["entryHash"] = e.EntryHash, ["payload"] = e.Payload
                    }));
                    fs.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                    fs.Write(bytes, 0, bytes.Length);
                    using (var sha = SHA256.Create()) { var d = sha.ComputeHash(bytes); fs.Write(d, 0, d.Length); }
                }
        }

        [Test]
        public void V1Journal_IsRead_AndContinuesInV2()
        {
            string dir = NewDir();
            string src = Path.Combine(dir, "src", "journal.v1.bin");
            Play(new FileJournal(src, new FixedClock()), "old", 12);
            var entries = new FileJournal(src, new FixedClock()).ReadAll().Entries;
            string old = Path.Combine(dir, "old", "journal.v1.bin");
            WriteV1(old, entries);
            var journal = new FileJournal(old, new FixedClock());
            Assert.IsNull(journal.Recovery, "a v1 journal needs no recovery");
            var read = journal.ReadAll();
            Assert.IsTrue(read.ChainValid);
            CollectionAssert.AreEqual(entries.Select(e => e.EntryHash).ToList(), read.Entries.Select(e => e.EntryHash).ToList());
            var coord = new GardenCoordinator(journal, new FixedEntropy("more"), new SequentialIds());
            coord.Restore(read);
            for (int i = 0; i < 5; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            var after = new FileJournal(old, new FixedClock()).ReadAll();
            Assert.IsTrue(after.ChainValid);
            Assert.Greater(after.Entries.Count, read.Entries.Count, "new compressed frames follow the old ones");
            Directory.Delete(dir, true);
        }

        [Test]
        public void BrokenMiddleSegment_KeepsEverything_AndContinuesFromTheVerifiedPrefix()
        {
            string dir = NewDir();
            string path = Path.Combine(dir, "journal.v1.bin");
            var split = new FileJournal(path, new FixedClock(), 32 * 1024);
            Play(split, "broken", 30);
            int segments = split.SegmentCount();
            Assert.Greater(segments, 3);
            var all = new FileJournal(path, new FixedClock(), 32 * 1024).ReadAll();
            string middle = split.SegmentPath(segments / 2);
            byte[] bytes = File.ReadAllBytes(middle);
            bytes[bytes.Length / 2] ^= 0x5A;
            File.WriteAllBytes(middle, bytes);

            var journal = new FileJournal(path, new FixedClock(), 32 * 1024);
            StringAssert.Contains("broken chain", journal.Recovery);
            var read = journal.ReadAll();
            Assert.IsTrue(read.ChainValid);
            Assert.Less(read.Entries.Count, all.Entries.Count);
            for (int i = 0; i < read.Entries.Count; i++) Assert.AreEqual(all.Entries[i].EntryHash, read.Entries[i].EntryHash);
            Assert.AreEqual(segments - segments / 2 - 1 + 1, Directory.GetFiles(dir, "*.corrupt-*").Length,
                "the damaged segment is copied and every later one moved aside, whole");
            var coord = new GardenCoordinator(journal, new FixedEntropy("after"), new SequentialIds());
            coord.Restore(read);
            if (coord.State == null) coord.CreateRun();
            if (coord.Phase == CoordinatorPhase.Prepared) coord.CommitIntent("none", null);
            if (coord.Phase == CoordinatorPhase.IntentCommitted) coord.Roll();
            coord.Prepare();
            coord.CommitIntent("none", null);
            coord.Roll();
            Assert.IsTrue(new FileJournal(path, new FixedClock(), 32 * 1024).ReadAll().ChainValid, "the garden goes on after recovery");
            Directory.Delete(dir, true);
        }

        [Test]
        public void MissingMiddleSegment_IsDetectedAndLaterSegmentsAreKeptIntact()
        {
            string dir = NewDir();
            try
            {
                string path = Path.Combine(dir, "journal.v1.bin");
                var split = new FileJournal(path, new FixedClock(), 1);
                var entries = new List<JournalEntry>();
                for (int i = 0; i < 5; i++)
                    entries.Add(split.Append("test", "run", null, new Dictionary<string, object> { ["value"] = i }));
                Assert.IsTrue(split.ReadAll().ChainValid, "also populate the incremental cache");
                string missing = split.SegmentPath(1);
                string later = split.SegmentPath(3);
                string last = split.SegmentPath(4);
                byte[] laterBytes = File.ReadAllBytes(later);
                byte[] lastBytes = File.ReadAllBytes(last);
                File.Move(missing, missing + ".external-backup");
                File.Move(split.SegmentPath(2), split.SegmentPath(2) + ".external-backup");

                var broken = split.ReadAll();
                Assert.IsFalse(broken.ChainValid);
                StringAssert.Contains("Missing segment 1", broken.Error);
                Assert.AreEqual(1, broken.Entries.Count);
                var payload = new Dictionary<string, object> { ["value"] = 5 };
                Assert.Throws<IOException>(() => split.Append("test", "run", null, payload));
                Assert.IsFalse(File.Exists(missing));

                var restored = new FileJournal(path, new FixedClock(), 1);
                StringAssert.Contains("Missing segment 1", restored.Recovery);
                Assert.AreEqual(entries[0].EntryHash, restored.HeadHash);
                CollectionAssert.AreEqual(laterBytes, File.ReadAllBytes(Directory.GetFiles(dir, Path.GetFileName(later) + ".corrupt-*").Single()));
                CollectionAssert.AreEqual(lastBytes, File.ReadAllBytes(Directory.GetFiles(dir, Path.GetFileName(last) + ".corrupt-*").Single()));
                var appended = restored.Append("test", "run", null, payload);
                Assert.AreEqual(2, appended.Seq);
                Assert.IsTrue(new FileJournal(path, new FixedClock(), 1).ReadAll().ChainValid);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void CutOffFirstWriteOfANewSegment_IsSetAside()
        {
            string dir = NewDir();
            string path = Path.Combine(dir, "journal.v1.bin");
            var split = new FileJournal(path, new FixedClock(), 32 * 1024);
            Play(split, "torn", 20);
            int segments = split.SegmentCount();
            int entries = new FileJournal(path, new FixedClock(), 32 * 1024).ReadAll().Entries.Count;
            File.WriteAllBytes(split.SegmentPath(segments), new byte[] { 0x10, 0x00, 0x00, 0x40, 1, 2, 3 });

            var journal = new FileJournal(path, new FixedClock(), 32 * 1024);
            StringAssert.Contains("incomplete last frame", journal.Recovery);
            var read = journal.ReadAll();
            Assert.IsTrue(read.ChainValid);
            Assert.AreEqual(entries, read.Entries.Count);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.torn-*").Length);
            Directory.Delete(dir, true);
        }
    }
}
