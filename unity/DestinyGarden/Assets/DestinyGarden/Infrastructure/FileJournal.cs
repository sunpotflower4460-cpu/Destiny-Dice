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
    /// Append-only framed journal. Frame = [int32 LE payloadLength][canonical JSON][SHA-256(payload)].
    /// A single writer instance serializes seq/prevHash assignment through durable append.
    /// Incomplete tail frames are reported, never silently truncated.
    /// Frame byte format version: framed-json.v1 (fixed in G1).
    ///
    /// Recovery when opened (nothing is discarded): an incomplete last frame (a write cut off by the process
    /// ending) is moved to a "torn" side file before new frames are added, so later frames stay readable. A
    /// journal whose chain is broken is copied whole to a "corrupt" side file and continues from its verified
    /// prefix. Either is reported in <see cref="Recovery"/>; the side files are kept beside the journal.
    /// </summary>
    public sealed class FileJournal : IJournalWriter, IJournalReader
    {
        private readonly string _path;
        private readonly IClock _clock;
        private readonly object _gate = new object();
        public string HeadHash { get; private set; } = "";
        public long NextSeq { get; private set; } = 1;

        // Frames already read and verified (append-only file): later reads only parse and verify what was added.
        // If the file is shorter than what was verified, or its last verified frame no longer matches, it is read
        // again from the start, so a replaced or truncated journal is still verified in full.
        private readonly List<JournalEntry> _verified = new List<JournalEntry>();
        private long _verifiedLength;
        private byte[] _verifiedTailDigest;
        private long _lastGoodLength;  // bytes of whole, verified frames at the end of the last read

        /// <summary>What was set aside when the journal was opened (null when nothing needed recovery).</summary>
        public string Recovery { get; private set; }

        public FileJournal(string path, IClock clock)
        {
            _path = path; _clock = clock;
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
                byte[] bytes = Encoding.UTF8.GetBytes(CanonicalJson.Write(new Dictionary<string, object>
                {
                    ["seq"] = entry.Seq, ["kind"] = entry.Kind, ["runId"] = entry.RunId,
                    ["roundId"] = entry.RoundId, ["createdAt"] = entry.CreatedAt,
                    ["prevHash"] = entry.PrevHash, ["entryHash"] = entry.EntryHash,
                    ["payload"] = entry.Payload
                }));
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using (var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    long before = fs.Length;
                    try
                    {
                        byte[] len = BitConverter.GetBytes(bytes.Length);
                        if (!BitConverter.IsLittleEndian) Array.Reverse(len);
                        fs.Write(len, 0, 4);
                        fs.Write(bytes, 0, bytes.Length);
                        using (var sha = SHA256.Create())
                        {
                            byte[] digest = sha.ComputeHash(bytes);
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

        /// <summary>
        /// Keeps a damaged journal readable without losing anything: the damaged bytes (or, for a broken chain,
        /// the whole original file) are copied to a side file first, then the journal ends at its last whole,
        /// verified frame.
        /// </summary>
        private JournalReadResult SetAsideDamage(JournalReadResult read)
        {
            lock (_gate)
            {
                long good = _lastGoodLength;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
                string side = _path + (read.ChainValid ? ".torn-" : ".corrupt-") + stamp;
                for (int n = 1; File.Exists(side); n++) side = _path + (read.ChainValid ? ".torn-" : ".corrupt-") + stamp + "-" + n;
                using (var src = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    using (var dst = new FileStream(side, FileMode.CreateNew, FileAccess.Write))
                    {
                        // A torn tail keeps its own bytes; a broken chain keeps the whole file as it was found.
                        src.Seek(read.ChainValid ? good : 0, SeekOrigin.Begin);
                        src.CopyTo(dst);
                        dst.Flush(true);
                    }
                    src.SetLength(good);
                    src.Flush(true);
                }
                Recovery = (read.ChainValid ? "incomplete last frame" : "broken chain (" + read.Error + ")") +
                    " set aside to " + Path.GetFileName(side) + "; continuing from " + good + " bytes";
                ResetVerified();
                return ReadIncremental();
            }
        }

        private void ResetVerified()
        {
            _verified.Clear();
            _verifiedLength = 0;
            _verifiedTailDigest = null;
        }

        private JournalReadResult ReadIncremental()
        {
            var result = new JournalReadResult { ChainValid = true };
            if (!File.Exists(_path)) { ResetVerified(); return result; }
            long fileLength = new FileInfo(_path).Length;
            byte[] all;
            int pos;
            using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bool usable = _verifiedLength > 0 && fileLength >= _verifiedLength && _verifiedTailDigest != null;
                if (usable)
                {
                    // The last verified frame's digest must still be where it was.
                    var tail = new byte[32];
                    fs.Seek(_verifiedLength - 32, SeekOrigin.Begin);
                    usable = ReadFully(fs, tail, 32) && Hashing.ToHex(tail) == Hashing.ToHex(_verifiedTailDigest);
                }
                if (!usable) ResetVerified();
                long start = _verifiedLength;
                all = new byte[fileLength - start];
                fs.Seek(start, SeekOrigin.Begin);
                if (!ReadFully(fs, all, all.Length)) { ResetVerified(); fs.Seek(0, SeekOrigin.Begin); all = new byte[fileLength]; ReadFully(fs, all, all.Length); }
                pos = 0;
            }
            string expectedPrev = _verified.Count > 0 ? _verified[_verified.Count - 1].EntryHash : "";
            long expectedSeq = _verified.Count + 1;
            var fresh = new List<JournalEntry>();
            int verifiedInChunk = 0;
            byte[] lastDigest = null;
            _lastGoodLength = _verifiedLength;
            while (pos < all.Length)
            {
                if (pos + 4 > all.Length) { result.HasIncompleteTail = true; break; }
                int len = all[pos] | (all[pos + 1] << 8) | (all[pos + 2] << 16) | (all[pos + 3] << 24);
                if (len <= 0 || (long)pos + 4 + len + 32 > all.Length) { result.HasIncompleteTail = true; break; }
                var payloadBytes = new byte[len];
                Array.Copy(all, pos + 4, payloadBytes, 0, len);
                string digestHex;
                using (var sha = SHA256.Create()) digestHex = Hashing.ToHex(sha.ComputeHash(payloadBytes));
                var storedDigest = new byte[32];
                Array.Copy(all, pos + 4 + len, storedDigest, 0, 32);
                if (digestHex != Hashing.ToHex(storedDigest))
                {
                    result.ChainValid = false;
                    result.Error = "Frame digest mismatch at offset " + (_verifiedLength + pos);
                    result.Entries.AddRange(_verified); result.Entries.AddRange(fresh); // the verified prefix, as before
                    return result;
                }
                JournalEntry entry;
                try
                {
                    var parsed = (Dictionary<string, object>)new SimpleJsonParser(
                        Encoding.UTF8.GetString(payloadBytes)).Parse();
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
                                           ex is IndexOutOfRangeException || ex is ArgumentException)
                {
                    result.ChainValid = false;
                    result.Error = "Unreadable frame at offset " + (_verifiedLength + pos) + ": " + ex.GetType().Name;
                    result.Entries.AddRange(_verified); result.Entries.AddRange(fresh);
                    return result;
                }
                if (entry.Seq != expectedSeq || entry.PrevHash != expectedPrev || !EntryHashMatches(payloadBytes, entry))
                {
                    result.ChainValid = false;
                    result.Error = "Chain mismatch at seq " + entry.Seq;
                    result.Entries.AddRange(_verified); result.Entries.AddRange(fresh);
                    return result;
                }
                expectedPrev = entry.EntryHash;
                expectedSeq++;
                fresh.Add(entry);
                pos += 4 + len + 32;
                verifiedInChunk = pos;
                _lastGoodLength = _verifiedLength + pos;
                lastDigest = storedDigest;
            }
            // Keep what is now verified (a broken chain above returned before this; an incomplete tail stops here).
            _verified.AddRange(fresh);
            _verifiedLength += verifiedInChunk;
            if (lastDigest != null) _verifiedTailDigest = lastDigest;
            result.Entries.AddRange(_verified);
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
