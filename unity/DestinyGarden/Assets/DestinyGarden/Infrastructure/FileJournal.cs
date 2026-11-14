using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DestinyGarden.Application;
using DestinyGarden.Domain;

namespace DestinyGarden.Infrastructure
{
    /// <summary>
    /// Append-only framed journal. Frame = [int32 LE length (+ flag)][stored bytes][SHA-256(stored bytes)].
    /// framed-json.v1 (G1): stored bytes are the entry's canonical JSON. framed-json.v2: when bit 30 of the length is
    /// set, the stored bytes are that JSON compressed (<see cref="FrameCompression"/>); v1 frames stay readable, and
    /// every new frame is written compressed when that is smaller.
    /// The journal is a chain of segment files — the first at the given path, then path.0001, path.0002, ... — each
    /// closed once it reaches <see cref="SegmentBytes"/>, so saving (in the browser: rewriting the changed file) only
    /// ever touches the small last segment. Frames never span segments; the hash chain runs on across them.
    /// A single writer instance serializes seq/prevHash assignment through durable append.
    ///
    /// Recovery when opened (nothing is discarded): an incomplete last frame (a write cut off by the process
    /// ending) is moved to a "torn" side file before new frames are added, so later frames stay readable. A
    /// journal whose chain is broken has the damaged segment copied whole to a "corrupt" side file (and any later
    /// segments moved aside) and continues from its verified prefix. Either is reported in <see cref="Recovery"/>.
    /// </summary>
    public sealed class FileJournal : IJournalWriter, IJournalReader
    {
        public const string FrameFormat = "framed-json.v2";
        public const long SegmentBytes = 1024 * 1024;   // a segment closes at 1 MiB (about 150 rolls, compressed)
        private const int CompressedFlag = 0x40000000;

        private readonly string _path;
        private readonly IClock _clock;
        private readonly object _gate = new object();
        public string HeadHash { get; private set; } = "";
        public long NextSeq { get; private set; } = 1;

        // Frames already read and verified (append-only files): later reads only parse and verify what was added.
        // If the segment being read is shorter than what was verified, or its last verified frame no longer
        // matches, everything is read again from the start, so a replaced or truncated journal is verified in full.
        private readonly List<JournalEntry> _verified = new List<JournalEntry>();
        private int _verifiedSegment;
        private long _verifiedOffset;
        private byte[] _verifiedTailDigest;
        private int _goodSegment;      // where the last whole, verified frame ends (segment, offset)
        private long _goodOffset;

        /// <summary>What was set aside when the journal was opened (null when nothing needed recovery).</summary>
        public string Recovery { get; private set; }

        private readonly long _segmentBytes;

        public FileJournal(string path, IClock clock) : this(path, clock, SegmentBytes) { }

        /// <summary>With a segment size of its own (tests use small segments to exercise rotation).</summary>
        public FileJournal(string path, IClock clock, long segmentBytes)
        {
            _path = path; _clock = clock; _segmentBytes = Math.Max(1, segmentBytes);
            var existing = ReadAll();
            if (!existing.ChainValid || existing.HasIncompleteTail)
            {
                existing = SetAsideDamage(existing);
            }
            if (existing.Entries.Count > 0)
            {
                var last = existing.Entries[existing.Entries.Count - 1];
                HeadHash = last.EntryHash;
                NextSeq = last.Seq + 1;
            }
        }

        /// <summary>The path of segment <paramref name="index"/> (0 is the journal's own path).</summary>
        public string SegmentPath(int index) => index == 0 ? _path : _path + "." + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>How many segment files the journal has (at least 1 once anything was written).</summary>
        public int SegmentCount()
        {
            int n = 0;
            while (File.Exists(SegmentPath(n))) n++;
            return n;
        }

        public JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload)
        {
            lock (_gate)
            {
                var entry = new JournalEntry
                {
                    Seq = NextSeq,
                    Kind = kind,
                    RunId = runId,
                    RoundId = roundId,
                    CreatedAt = _clock.NowIso8601(),
                    PrevHash = HeadHash,
                    Payload = payload
                };
                entry.EntryHash = entry.ComputeHash();
                byte[] json = Encoding.UTF8.GetBytes(CanonicalJson.Write(new Dictionary<string, object>
                {
                    ["seq"] = entry.Seq, ["kind"] = entry.Kind, ["runId"] = entry.RunId,
                    ["roundId"] = entry.RoundId, ["createdAt"] = entry.CreatedAt,
                    ["prevHash"] = entry.PrevHash, ["entryHash"] = entry.EntryHash,
                    ["payload"] = entry.Payload
                }));
                // framed-json.v2: stored compressed when that is smaller.
                byte[] packed = FrameCompression.Compress(json);
                bool compressed = packed.Length < json.Length;
                byte[] stored = compressed ? packed : json;

                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                int segment = Math.Max(0, SegmentCount() - 1);
                string target = SegmentPath(segment);
                if (File.Exists(target) && new FileInfo(target).Length >= _segmentBytes) target = SegmentPath(segment + 1);
                using (var fs = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    long before = fs.Length;
                    try
                    {
                        byte[] len = BitConverter.GetBytes(stored.Length | (compressed ? CompressedFlag : 0));
                        if (!BitConverter.IsLittleEndian) Array.Reverse(len);
                        fs.Write(len, 0, 4);
                        fs.Write(stored, 0, stored.Length);
                        using (var sha = SHA256.Create())
                        {
                            byte[] digest = sha.ComputeHash(stored);
                            fs.Write(digest, 0, digest.Length);
                        }
                        fs.Flush(true);
                    }
                    catch
                    {
                        // This frame was never complete (e.g. disk full): take back only its own bytes, so the
                        // journal still ends on a whole frame and the next append is readable. Then report it.
                        try { fs.SetLength(before); } catch { /* the torn tail is then set aside on next open */ }
                        throw;
                    }
                }
                HeadHash = entry.EntryHash;
                NextSeq++;
                return entry;
            }
        }

        public JournalReadResult ReadAll()
        {
            lock (_gate) return ReadIncremental();
        }

        private string SideName(string of, string kind, string stamp)
        {
            string side = of + "." + kind + "-" + stamp;
            for (int n = 1; File.Exists(side); n++) side = of + "." + kind + "-" + stamp + "-" + n;
            return side;
        }

        /// <summary>
        /// Keeps a damaged journal readable without losing anything: the damaged bytes (or, for a broken chain, the
        /// whole damaged segment, and any segments after it) go to side files first, then the journal ends at its last
        /// whole, verified frame.
        /// </summary>
        private JournalReadResult SetAsideDamage(JournalReadResult read)
        {
            lock (_gate)
            {
                int seg = _goodSegment;
                long good = _goodOffset;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
                string kind = read.ChainValid ? "torn" : "corrupt";
                // The damage is in the segment after the last good frame's, when that one ended exactly at its end.
                int damaged = seg;
                if (File.Exists(SegmentPath(seg)) && new FileInfo(SegmentPath(seg)).Length == good && File.Exists(SegmentPath(seg + 1)))
                { damaged = seg + 1; good = 0; }
                string first = SideName(SegmentPath(damaged), kind, stamp);
                using (var src = new FileStream(SegmentPath(damaged), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    using (var dst = new FileStream(first, FileMode.CreateNew, FileAccess.Write))
                    {
                        // A torn tail keeps its own bytes; a broken chain keeps the whole segment as it was found.
                        src.Seek(read.ChainValid ? good : 0, SeekOrigin.Begin);
                        src.CopyTo(dst);
                        dst.Flush(true);
                    }
                    src.SetLength(good);
                    src.Flush(true);
                }
                // Segments after the damaged one cannot continue its chain: they are moved aside whole.
                int moved = 0;
                for (int k = damaged + 1; File.Exists(SegmentPath(k)); k++)
                {
                    File.Move(SegmentPath(k), SideName(SegmentPath(k), kind, stamp));
                    moved++;
                }
                Recovery = (read.ChainValid ? "incomplete last frame" : "broken chain (" + read.Error + ")") +
                    " set aside to " + Path.GetFileName(first) + (moved > 0 ? " (and " + moved + " later segment(s))" : "") +
                    "; continuing from segment " + damaged + " at " + good + " bytes";
                ResetVerified();
                return ReadIncremental();
            }
        }

        private void ResetVerified()
        {
            _verified.Clear();
            _verifiedSegment = 0;
            _verifiedOffset = 0;
            _verifiedTailDigest = null;
        }

        private JournalReadResult ReadIncremental()
        {
            var result = new JournalReadResult { ChainValid = true };
            int segments = SegmentCount();
            if (segments == 0) { ResetVerified(); return result; }
            // Is what was verified still there? (The last verified frame's digest must still be where it was.)
            bool usable = _verifiedTailDigest != null && _verifiedSegment < segments;
            if (usable)
            {
                string at = SegmentPath(_verifiedSegment);
                usable = new FileInfo(at).Length >= _verifiedOffset;
                if (usable)
                    using (var fs = new FileStream(at, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var tail = new byte[32];
                        fs.Seek(_verifiedOffset - 32, SeekOrigin.Begin);
                        usable = ReadFully(fs, tail, 32) && Hashing.ToHex(tail) == Hashing.ToHex(_verifiedTailDigest);
                    }
            }
            if (!usable) ResetVerified();

            string expectedPrev = _verified.Count > 0 ? _verified[_verified.Count - 1].EntryHash : "";
            long expectedSeq = _verified.Count + 1;
            var fresh = new List<JournalEntry>();
            int lastSeg = _verifiedSegment;
            long lastOffset = _verifiedOffset;
            byte[] lastDigest = _verifiedTailDigest;
            _goodSegment = _verifiedSegment; _goodOffset = _verifiedOffset;

            for (int seg = _verifiedSegment; seg < segments; seg++)
            {
                long from = seg == _verifiedSegment ? _verifiedOffset : 0;
                byte[] all;
                using (var fs = new FileStream(SegmentPath(seg), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length = fs.Length;
                    all = new byte[Math.Max(0, length - from)];
                    fs.Seek(from, SeekOrigin.Begin);
                    if (!ReadFully(fs, all, all.Length)) { result.HasIncompleteTail = true; break; }
                }
                int pos = 0;
                while (pos < all.Length)
                {
                    long frameAt = from + pos;
                    if (pos + 4 > all.Length) { result.HasIncompleteTail = true; break; }
                    int raw = all[pos] | (all[pos + 1] << 8) | (all[pos + 2] << 16) | (all[pos + 3] << 24);
                    bool compressed = raw > 0 && (raw & CompressedFlag) != 0;
                    int len = raw & ~CompressedFlag;
                    if (raw <= 0 || len <= 0 || (long)pos + 4 + len + 32 > all.Length) { result.HasIncompleteTail = true; break; }
                    var storedDigest = new byte[32];
                    Array.Copy(all, pos + 4 + len, storedDigest, 0, 32);
                    string digestHex;
                    using (var sha = SHA256.Create()) digestHex = Hashing.ToHex(sha.ComputeHash(all, pos + 4, len));
                    if (digestHex != Hashing.ToHex(storedDigest))
                        return Broken(result, fresh, "Frame digest mismatch in segment " + seg + " at offset " + frameAt);
                    JournalEntry entry;
                    byte[] json;
                    try
                    {
                        if (compressed) json = FrameCompression.Decompress(all, pos + 4, len);
                        else { json = new byte[len]; Array.Copy(all, pos + 4, json, 0, len); }
                        var parsed = (Dictionary<string, object>)new SimpleJsonParser(Encoding.UTF8.GetString(json)).Parse();
                        entry = new JournalEntry
                        {
                            Seq = (long)parsed["seq"],
                            Kind = (string)parsed["kind"],
                            RunId = parsed["runId"] as string,
                            RoundId = parsed["roundId"] as string,
                            CreatedAt = (string)parsed["createdAt"],
                            PrevHash = (string)parsed["prevHash"],
                            EntryHash = (string)parsed["entryHash"],
                            Payload = (Dictionary<string, object>)parsed["payload"]
                        };
                    }
                    catch (Exception ex) when (ex is FormatException || ex is InvalidCastException ||
                                               ex is KeyNotFoundException || ex is NullReferenceException ||
                                               ex is IndexOutOfRangeException || ex is ArgumentException || ex is OverflowException)
                    {
                        return Broken(result, fresh, "Unreadable frame in segment " + seg + " at offset " + frameAt + ": " + ex.GetType().Name);
                    }
                    if (entry.Seq != expectedSeq || entry.PrevHash != expectedPrev || !EntryHashMatches(json, entry))
                        return Broken(result, fresh, "Chain mismatch at seq " + entry.Seq);
                    expectedPrev = entry.EntryHash;
                    expectedSeq++;
                    fresh.Add(entry);
                    pos += 4 + len + 32;
                    lastSeg = seg; lastOffset = from + pos; lastDigest = storedDigest;
                    _goodSegment = seg; _goodOffset = from + pos;
                }
                if (result.HasIncompleteTail)
                {
                    // Only the last segment can end mid-frame (a write cut off); anywhere else the chain is broken.
                    if (seg < segments - 1) { result.HasIncompleteTail = false; return Broken(result, fresh, "Segment " + seg + " ends mid-frame"); }
                    break;
                }
            }
            // Keep what is now verified (a broken chain above returned before this; an incomplete tail stops here).
            _verified.AddRange(fresh);
            _verifiedSegment = lastSeg; _verifiedOffset = lastOffset; _verifiedTailDigest = lastDigest;
            result.Entries.AddRange(_verified);
            return result;
        }

        private JournalReadResult Broken(JournalReadResult result, List<JournalEntry> fresh, string error)
        {
            result.ChainValid = false;
            result.Error = error;
            result.Entries.AddRange(_verified); result.Entries.AddRange(fresh); // the verified prefix, as before
            return result;
        }

        private static readonly byte[] EntryHashField = Encoding.UTF8.GetBytes("\"entryHash\":\"");

        /// <summary>
        /// The entry hash is SHA-256 of the entry's canonical JSON without its own entryHash field — which is exactly the
        /// stored frame with the field `"entryHash":"…",` taken out (the same writer, keys in canonical order). Verified
        /// from the stored bytes, without re-serializing the payload; anything unexpected falls back to the full recompute.
        /// </summary>
        internal static bool EntryHashMatches(byte[] frame, JournalEntry entry)
        {
            int at = IndexOf(frame, EntryHashField);
            int end = at + EntryHashField.Length + 64;
            if (at > 0 && end + 1 < frame.Length && frame[end] == (byte)'"' && frame[end + 1] == (byte)',' && entry.EntryHash != null)
            {
                using (var sha = SHA256.Create())
                {
                    sha.TransformBlock(frame, 0, at, null, 0);
                    sha.TransformFinalBlock(frame, end + 2, frame.Length - (end + 2));
                    if (Hashing.ToHex(sha.Hash) == entry.EntryHash) return true;
                }
            }
            return entry.ComputeHash() == entry.EntryHash;
        }

        private static int IndexOf(byte[] data, byte[] pattern)
        {
            // The field is near the start of every frame (after createdAt); search only the head.
            int limit = Math.Min(data.Length - pattern.Length, 256);
            for (int i = 0; i <= limit; i++)
            {
                int j = 0;
                while (j < pattern.Length && data[i + j] == pattern[j]) j++;
                if (j == pattern.Length) return i;
            }
            return -1;
        }

        private static bool ReadFully(Stream s, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buffer, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }
    }

    public sealed class LocalEntropy : IEntropyProvider
    {
        public string SourceName => "local";
        public byte[] GetBytes(int length)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }
    }

    public sealed class SystemClock : IClock
    {
        public string NowIso8601() => DateTime.UtcNow.ToString("o");
    }

    public sealed class GuidIdSource : IIdSource
    {
        public string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 16);
    }
}
