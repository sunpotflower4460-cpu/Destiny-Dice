using System;
using System.IO;
using DestinyGarden.Application;
using DestinyGarden.Domain;
using DestinyGarden.Infrastructure;
using NUnit.Framework;

namespace DestinyGarden.Tests
{
    /// <summary>
    /// A journal damaged by a cut-off write or a broken chain stays playable, and nothing is discarded:
    /// the damaged bytes (or the whole original) are kept in a side file beside it.
    /// </summary>
    public class JournalRecoveryTests
    {
        private static (string dir, string path) Fresh()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dg-recovery-" + Guid.NewGuid().ToString("N"));
            return (dir, Path.Combine(dir, "journal.v1.bin"));
        }

        private static GardenCoordinator Play(string path, int rolls)
        {
            var coord = new GardenCoordinator(new FileJournal(path, new FixedClock()), new FixedEntropy("recovery"), new SequentialIds());
            coord.CreateRun();
            for (int i = 0; i < rolls; i++) { coord.Prepare(); coord.CommitIntent("none", null); coord.Roll(); }
            return coord;
        }

        [Test]
        public void CutOffLastWrite_IsSetAside_AndLaterFramesStayReadable()
        {
            var (dir, path) = Fresh();
            Play(path, 3);
            long whole = new FileInfo(path).Length;
            int count = new FileJournal(path, new FixedClock()).ReadAll().Entries.Count;
            using (var fs = new FileStream(path, FileMode.Append)) fs.Write(new byte[] { 0x40, 0x01, 0x00, 0x00, 0x7B, 0x22 }, 0, 6);

            var journal = new FileJournal(path, new FixedClock());
            Assert.NotNull(journal.Recovery);
            StringAssert.Contains("incomplete last frame", journal.Recovery);
            Assert.AreEqual(whole, new FileInfo(path).Length, "the journal ends on its last whole frame");
            var sides = Directory.GetFiles(dir, "journal.v1.bin.torn-*");
            Assert.AreEqual(1, sides.Length);
            Assert.AreEqual(6, new FileInfo(sides[0]).Length, "the cut-off bytes are kept");

            journal.Append("CareFocusChanged", "run-1", null, new System.Collections.Generic.Dictionary<string, object> { ["mode"] = "auto", ["siteId"] = null, ["targetPolicyVersion"] = "t" });
            var read = new FileJournal(path, new FixedClock()).ReadAll();
            Assert.IsTrue(read.ChainValid);
            Assert.IsFalse(read.HasIncompleteTail);
            Assert.AreEqual(count + 1, read.Entries.Count, "the frame written after recovery is readable");
            Directory.Delete(dir, true);
        }

        [Test]
        public void BrokenChain_KeepsTheOriginal_AndContinuesFromTheVerifiedPrefix()
        {
            var (dir, path) = Fresh();
            Play(path, 4);
            var all = new FileJournal(path, new FixedClock()).ReadAll();
            long original = new FileInfo(path).Length;
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0x5A; // one flipped bit in the middle of the journal
            File.WriteAllBytes(path, bytes);

            var journal = new FileJournal(path, new FixedClock());
            Assert.NotNull(journal.Recovery);
            StringAssert.Contains("broken chain", journal.Recovery);
            var sides = Directory.GetFiles(dir, "journal.v1.bin.corrupt-*");
            Assert.AreEqual(1, sides.Length);
            Assert.AreEqual(original, new FileInfo(sides[0]).Length, "the whole original is kept");
            var read = journal.ReadAll();
            Assert.IsTrue(read.ChainValid);
            Assert.Less(read.Entries.Count, all.Entries.Count);
            Assert.Greater(read.Entries.Count, 0, "the verified prefix remains");
            for (int i = 0; i < read.Entries.Count; i++) Assert.AreEqual(all.Entries[i].EntryHash, read.Entries[i].EntryHash);

            // The game goes on from what was verified.
            var coord = new GardenCoordinator(journal, new FixedEntropy("after"), new SequentialIds());
            coord.Restore(read);
            if (coord.State == null) coord.CreateRun();
            if (coord.Phase == CoordinatorPhase.Ready || coord.Phase == CoordinatorPhase.Resolved) coord.Prepare();
            Assert.IsTrue(new FileJournal(path, new FixedClock()).ReadAll().ChainValid);
            Directory.Delete(dir, true);
        }

        [Test]
        public void HealthyJournal_NeedsNoRecovery()
        {
            var (dir, path) = Fresh();
            Play(path, 2);
            var journal = new FileJournal(path, new FixedClock());
            Assert.IsNull(journal.Recovery);
            Assert.AreEqual(0, Directory.GetFiles(dir, "journal.v1.bin.torn-*").Length + Directory.GetFiles(dir, "journal.v1.bin.corrupt-*").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir).Length, "only the journal itself");
            Directory.Delete(dir, true);
        }

        [Test]
        public void Numbers_ParseTheSameInEveryLanguage()
        {
            var parsed = (System.Collections.Generic.Dictionary<string, object>)new SimpleJsonParser("{\"a\":-12,\"b\":0}").Parse();
            Assert.AreEqual(-12L, parsed["a"]);
            Assert.AreEqual(0L, parsed["b"]);
            Assert.Throws<FormatException>(() => new SimpleJsonParser("{\"a\":١٢}").Parse(), "only ASCII digits are numbers");
            // The fast paths read exactly what the canonical writer wrote: escapes, unicode, the long range.
            var original = new System.Collections.Generic.Dictionary<string, object>
            {
                ["plain"] = "庭の名前", ["escaped"] = "a\"b\\c\nd\u0001", ["max"] = long.MaxValue, ["min"] = long.MinValue + 1,
                ["small"] = 7L, ["neg"] = -64L, ["list"] = new System.Collections.Generic.List<object> { 0L, 2560L, 2561L, "x", null, true }
            };
            string text = CanonicalJson.Write(original);
            var back = new SimpleJsonParser(text).Parse();
            Assert.AreEqual(text, CanonicalJson.Write(back), "parse then write gives the same canonical text");
            Assert.Throws<OverflowException>(() => new SimpleJsonParser("{\"a\":9223372036854775808}").Parse());
            Assert.Throws<FormatException>(() => new SimpleJsonParser("{\"a\":-}").Parse());
        }
    }
}
