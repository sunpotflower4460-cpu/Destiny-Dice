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
    /// </summary>
    public sealed class FileJournal : IJournalWriter, IJournalReader
    {
        private readonly string _path;
        private readonly IClock _clock;
        private readonly object _gate = new object();
        public string HeadHash { get; private set; } = "";
        public long NextSeq { get; private set; } = 1;

        public FileJournal(string path, IClock clock)
        {
            _path = path; _clock = clock;
            var existing = ReadAll();
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
                HeadHash = entry.EntryHash;
                NextSeq++;
                return entry;
            }
        }

        public JournalReadResult ReadAll()
        {
            var result = new JournalReadResult { ChainValid = true };
            if (!File.Exists(_path)) return result;
            byte[] all = File.ReadAllBytes(_path);
            int pos = 0;
            string expectedPrev = "";
            long expectedSeq = 1;
            while (pos < all.Length)
            {
                if (pos + 4 > all.Length) { result.HasIncompleteTail = true; break; }
                int len = all[pos] | (all[pos + 1] << 8) | (all[pos + 2] << 16) | (all[pos + 3] << 24);
                if (len <= 0 || pos + 4 + len + 32 > all.Length) { result.HasIncompleteTail = true; break; }
                var payloadBytes = new byte[len];
                Array.Copy(all, pos + 4, payloadBytes, 0, len);
                string digestHex;
                using (var sha = SHA256.Create()) digestHex = Hashing.ToHex(sha.ComputeHash(payloadBytes));
                var storedDigest = new byte[32];
                Array.Copy(all, pos + 4 + len, storedDigest, 0, 32);
                if (digestHex != Hashing.ToHex(storedDigest))
                {
                    result.ChainValid = false;
                    result.Error = "Frame digest mismatch at offset " + pos;
                    return result;
                }
                var parsed = (Dictionary<string, object>)new SimpleJsonParser(
                    Encoding.UTF8.GetString(payloadBytes)).Parse();
                var entry = new JournalEntry
                {
                    Seq = (long)parsed["seq"],
                    Kind = (string)parsed["kind"],
                    RunId = (string)parsed["runId"],
                    RoundId = parsed["roundId"] as string,
                    CreatedAt = (string)parsed["createdAt"],
                    PrevHash = (string)parsed["prevHash"],
                    EntryHash = (string)parsed["entryHash"],
                    Payload = (Dictionary<string, object>)parsed["payload"]
                };
                if (entry.Seq != expectedSeq || entry.PrevHash != expectedPrev || entry.ComputeHash() != entry.EntryHash)
                {
                    result.ChainValid = false;
                    result.Error = "Chain mismatch at seq " + entry.Seq;
                    return result;
                }
                expectedPrev = entry.EntryHash;
                expectedSeq++;
                result.Entries.Add(entry);
                pos += 4 + len + 32;
            }
            return result;
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
