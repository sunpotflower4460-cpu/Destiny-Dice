using System;
using System.Collections.Generic;
using System.Text;

namespace DestinyGarden.Domain
{
    /// <summary>Minimal JSON parser for journal payloads written by CanonicalJson (integers only).</summary>
    public sealed class SimpleJsonParser
    {
        private readonly string _s;
        private int _i;

        // Boxed small integers, shared (the saved garden is mostly small numbers: soil, ages, counts).
        private const long SmallMin = -64, SmallMax = 2560;
        private static readonly object[] Small = MakeSmall();
        private static object[] MakeSmall()
        {
            var a = new object[SmallMax - SmallMin + 1];
            for (long v = SmallMin; v <= SmallMax; v++) a[v - SmallMin] = v;
            return a;
        }

        public SimpleJsonParser(string s) { _s = s; }

        public object Parse()
        {
            object v = ParseValue();
            SkipWs();
            if (_i != _s.Length) throw new FormatException("Trailing characters in JSON at " + _i);
            return v;
        }

        private void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

        private object ParseValue()
        {
            SkipWs();
            if (_i >= _s.Length) throw new FormatException("Unexpected end of JSON");
            switch (_s[_i])
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default: return ParseNumber();
            }
        }

        private Dictionary<string, object> ParseObject()
        {
            var d = new Dictionary<string, object>();
            _i++; SkipWs();
            if (_i < _s.Length && _s[_i] == '}') { _i++; return d; }
            while (true)
            {
                SkipWs();
                string key = ParseString();
                SkipWs();
                if (_s[_i] != ':') throw new FormatException("Expected ':' at " + _i);
                _i++;
                d[key] = ParseValue();
                SkipWs();
                if (_s[_i] == ',') { _i++; continue; }
                if (_s[_i] == '}') { _i++; return d; }
                throw new FormatException("Expected ',' or '}' at " + _i);
            }
        }

        private List<object> ParseArray()
        {
            var l = new List<object>();
            _i++; SkipWs();
            if (_i < _s.Length && _s[_i] == ']') { _i++; return l; }
            while (true)
            {
                l.Add(ParseValue());
                SkipWs();
                if (_s[_i] == ',') { _i++; continue; }
                if (_s[_i] == ']') { _i++; return l; }
                throw new FormatException("Expected ',' or ']' at " + _i);
            }
        }

        private string ParseString()
        {
            if (_s[_i] != '"') throw new FormatException("Expected string at " + _i);
            _i++;
            // Fast path (almost every string the journal holds): no escapes, one Substring.
            int start = _i;
            while (_i < _s.Length)
            {
                char f = _s[_i];
                if (f == '"') { string plain = _s.Substring(start, _i - start); _i++; return plain; }
                if (f == '\\') break;
                _i++;
            }
            _i = start;
            var sb = new StringBuilder();
            while (_i < _s.Length)
            {
                char c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = _s[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)int.Parse(_s.Substring(_i, 4), System.Globalization.NumberStyles.HexNumber)); _i += 4; break;
                    default: throw new FormatException("Bad escape: \\" + e);
                }
            }
            throw new FormatException("Unterminated string");
        }

        private object ParseNumber()
        {
            int start = _i;
            bool negative = _s[_i] == '-';
            if (negative) _i++;
            // ASCII digits only, parsed the same on every device language (not char.IsDigit / current culture).
            // Accumulated directly (no Substring); overflow throws as long.Parse would.
            long value = 0;
            int digits = 0;
            while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
            {
                int digit = _s[_i] - '0';
                value = checked(value * 10 + (negative ? -digit : digit));
                _i++; digits++;
            }
            if (digits == 0) throw new FormatException("Bad number at " + start);
            return value >= SmallMin && value <= SmallMax ? Small[value - SmallMin] : value;
        }

        private void Expect(string literal)
        {
            if (string.Compare(_s, _i, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
                throw new FormatException("Expected " + literal + " at " + _i);
            _i += literal.Length;
        }
    }
}
