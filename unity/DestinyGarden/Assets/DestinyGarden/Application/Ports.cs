using System.Collections.Generic;
using DestinyGarden.Domain;

namespace DestinyGarden.Application
{
    public interface IEntropyProvider
    {
        /// <summary>Returns fresh bytes; SourceName records the real source (never relabelled).</summary>
        byte[] GetBytes(int length);
        string SourceName { get; }
    }

    public interface IClock
    {
        string NowIso8601();
    }

    public interface IIdSource
    {
        string NewId(string prefix);
    }

    /// <summary>One committed journal record. Payload is canonical-JSON-serializable data only.</summary>
    public sealed class JournalEntry
    {
        public long Seq;
        public string Kind;
        public string RunId;
        public string RoundId; // may be null for run-scoped records
        public string CreatedAt;
        public string PrevHash;
        public string EntryHash;
        public Dictionary<string, object> Payload = new Dictionary<string, object>();

        public Dictionary<string, object> ToHashPayload()
        {
            return new Dictionary<string, object>
            {
                ["seq"] = Seq,
                ["kind"] = Kind,
                ["runId"] = RunId,
                ["roundId"] = RoundId,
                ["createdAt"] = CreatedAt,
                ["prevHash"] = PrevHash,
                ["payload"] = Payload
            };
        }

        public string ComputeHash() => Hashing.Sha256Hex(CanonicalJson.WriteUtf8(ToHashPayload()));
    }

    public interface IJournalWriter
    {
        /// <summary>Serializes seq/prevHash assignment and durable append. Returns the committed entry.</summary>
        JournalEntry Append(string kind, string runId, string roundId, Dictionary<string, object> payload);
        string HeadHash { get; }
        long NextSeq { get; }
    }

    public sealed class JournalReadResult
    {
        public List<JournalEntry> Entries = new List<JournalEntry>();
        public bool ChainValid;
        public bool HasIncompleteTail;
        public string Error;
    }

    public interface IJournalReader
    {
        JournalReadResult ReadAll();
    }
}
