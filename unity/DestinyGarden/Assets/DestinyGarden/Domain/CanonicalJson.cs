using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DestinyGarden.Domain
{
    /// <summary>
    /// Minimal RFC 8785 (JCS) canonical JSON writer for the payload subset this game
    /// persists: null, bool, integer (long), string, arrays and string-keyed objects.
    /// Keys are sorted by UTF-16 code units as JCS requires for this subset; numbers
    /// are integers only (floating point payloads are rejected by design).
    /// Append and verify share this single implementation.
    /// </summary>
    public static class CanonicalJson
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        public static byte[] WriteUtf8(object value) => Encoding.UTF8.GetBytes(Write(value));

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case string s: WriteString(sb, s); break;
                case IDictionary<string, object> dict:
                    var keys = new List<string>(dict.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    sb.Append('{');
                    for (int k = 0; k < keys.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteString(sb, keys[k]);
                        sb.Append(':');
                        WriteValue(sb, dict[keys[k]]);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable seq:
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in seq)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                default:
                    throw new ArgumentException("Unsupported canonical JSON type: " + value.GetType().FullName);
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    public static class Hashing
    {
        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes));
        }

        public static string Sha256HexUtf8(string text) => Sha256Hex(Encoding.UTF8.GetBytes(text));

        public static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Deterministic hash of the dynamic world state (canonical JSON + SHA-256).</summary>
        public static string StateHash(WorldState s)
        {
            var payload = WorldStateSerializer.ToPayload(s);
            return Sha256Hex(CanonicalJson.WriteUtf8(payload));
        }
    }

    public static class WorldStateSerializer
    {
        public static Dictionary<string, object> ToPayload(WorldState s)
        {
            var plants = new List<object>();
            var sorted = new List<PlantState>(s.Plants);
            sorted.Sort((a, b) => a.CellId.CompareTo(b.CellId));
            foreach (var p in sorted)
            {
                var plant = new Dictionary<string, object>
                {
                    ["plantId"] = p.PlantId,
                    ["cellId"] = p.CellId,
                    ["growth"] = p.Growth,
                    ["growthRemainder"] = p.GrowthRemainder,
                    ["stage"] = (int)p.Stage,
                    ["originKind"] = p.OriginKind ?? "initialMap",
                    ["seedSourceRoundId"] = p.SeedSourceRoundId
                };
                // Written only when known, so states saved before ecology.v3 keep their exact hashes.
                if (p.Species != null) plant["species"] = p.Species;
                if (p.BloomRolls > 0) plant["bloomRolls"] = p.BloomRolls; // lifecycle.v1, same rule
                plants.Add(plant);
            }
            return new Dictionary<string, object>
            {
                ["worldRevision"] = s.WorldRevision,
                ["soilWater"] = ToObjList(s.SoilWater),
                ["surfaceWater"] = ToObjList(s.SurfaceWater),
                ["evaporationRemainder"] = ToObjList(s.EvaporationRemainder),
                ["plants"] = plants
            };
        }

        public static WorldState FromPayload(Dictionary<string, object> p)
        {
            var s = new WorldState { WorldRevision = ToInt(p["worldRevision"]) };
            CopyInto((IEnumerable)p["soilWater"], s.SoilWater);
            CopyInto((IEnumerable)p["surfaceWater"], s.SurfaceWater);
            CopyInto((IEnumerable)p["evaporationRemainder"], s.EvaporationRemainder);
            foreach (var item in (IEnumerable)p["plants"])
            {
                var d = (IDictionary<string, object>)item;
                s.Plants.Add(new PlantState
                {
                    PlantId = (string)d["plantId"],
                    CellId = ToInt(d["cellId"]),
                    Growth = ToInt(d["growth"]),
                    GrowthRemainder = ToInt(d["growthRemainder"]),
                    Stage = (PlantStage)ToInt(d["stage"]),
                    OriginKind = d.TryGetValue("originKind", out object ok) ? (string)ok : "initialMap",
                    SeedSourceRoundId = d.TryGetValue("seedSourceRoundId", out object sr) ? sr as string : null,
                    Species = d.TryGetValue("species", out object sp) ? sp as string : null,
                    BloomRolls = d.TryGetValue("bloomRolls", out object br) ? ToInt(br) : 0
                });
            }
            return s;
        }

        private static List<object> ToObjList(int[] a)
        {
            var l = new List<object>(a.Length);
            foreach (int v in a) l.Add(v);
            return l;
        }

        private static void CopyInto(IEnumerable src, int[] dst)
        {
            int i = 0;
            foreach (var v in src) dst[i++] = ToInt(v);
        }

        public static int ToInt(object v) => v is int i ? i : checked((int)(long)v);
    }
}
