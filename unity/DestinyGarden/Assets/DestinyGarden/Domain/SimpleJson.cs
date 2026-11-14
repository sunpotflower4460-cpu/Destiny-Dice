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
            if (_s[_i] == '-') _i++;
            // ASCII digits only, parsed the same on every device language (not char.IsDigit / current culture).
            while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
            string text = _s.Substring(start, _i - start);
            if (text.Length == 0 || text == "-") throw new FormatException("Bad number at " + start);
            return long.Parse(text, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Expect(string literal)
        {
            if (string.Compare(_s, _i, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
                throw new FormatException("Expected " + literal + " at " + _i);
            _i += literal.Length;
        }
    }
}
