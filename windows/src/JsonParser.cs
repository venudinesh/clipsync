// A recursive descent JSON parser.
//
// Tolerant on purpose in two places: a stray byte order mark at the head of a
// file is skipped, and Parse returns null instead of throwing, because a half
// written record recovered from a crash should cost one entry rather than the
// whole store.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class JsonParser
    {
        private readonly string _s;
        private int _i;

        private JsonParser(string s) { _s = s; _i = 0; }

        /// Returns null when the text is not valid JSON. Callers treat null as
        /// "no data" and fall back to a default.
        public static JVal Parse(string text)
        {
            if (text == null) return null;
            try
            {
                JsonParser p = new JsonParser(text);
                p.SkipBom();
                p.Ws();
                JVal v = p.Value(0);
                p.Ws();
                return p._i >= p._s.Length ? v : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SkipBom()
        {
            if (_s.Length > 0 && _s[0] == '\uFEFF') _i = 1;
        }

        private void Ws()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                else break;
            }
        }

        private char Cur
        {
            get
            {
                if (_i >= _s.Length) throw new FormatException("unexpected end of JSON");
                return _s[_i];
            }
        }

        private JVal Value(int depth)
        {
            // A deep nesting guard: untrusted input from a local server should
            // not be able to overflow the stack.
            if (depth > 64) throw new FormatException("JSON nested too deep");
            char c = Cur;
            switch (c)
            {
                case '{': return Obj(depth);
                case '[': return Arr(depth);
                case '"': return JVal.Of(Text());
                case 't': Word("true"); return JVal.Of(true);
                case 'f': Word("false"); return JVal.Of(false);
                case 'n': Word("null"); return JVal.Null;
                default: return Number();
            }
        }

        private void Word(string w)
        {
            if (_i + w.Length > _s.Length || string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0)
            {
                throw new FormatException("bad literal");
            }
            _i += w.Length;
        }

        private JVal Obj(int depth)
        {
            JVal o = JVal.Object();
            _i++; // '{'
            Ws();
            if (Cur == '}') { _i++; return o; }
            while (true)
            {
                Ws();
                if (Cur != '"') throw new FormatException("object key must be a string");
                string key = Text();
                Ws();
                if (Cur != ':') throw new FormatException("expected ':'");
                _i++;
                Ws();
                o.Set(key, Value(depth + 1));
                Ws();
                char c = Cur;
                if (c == ',') { _i++; continue; }
                if (c == '}') { _i++; return o; }
                throw new FormatException("expected ',' or '}'");
            }
        }

        private JVal Arr(int depth)
        {
            JVal a = JVal.Array();
            _i++; // '['
            Ws();
            if (Cur == ']') { _i++; return a; }
            while (true)
            {
                Ws();
                a.Add(Value(depth + 1));
                Ws();
                char c = Cur;
                if (c == ',') { _i++; continue; }
                if (c == ']') { _i++; return a; }
                throw new FormatException("expected ',' or ']'");
            }
        }

        private JVal Number()
        {
            int start = _i;
            if (Cur == '-' || Cur == '+') _i++;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+') _i++;
                else break;
            }
            if (_i == start) throw new FormatException("expected a value");
            double d;
            if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out d))
            {
                throw new FormatException("bad number");
            }
            return JVal.Of(d);
        }

        private string Text()
        {
            _i++; // opening quote
            StringBuilder sb = new StringBuilder(16);
            while (true)
            {
                char c = Cur;
                _i++;
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = Cur;
                _i++;
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
                    case 'u':
                        if (_i + 4 > _s.Length) throw new FormatException("truncated \\u escape");
                        sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture));
                        _i += 4;
                        break;
                    default: throw new FormatException("bad escape");
                }
            }
        }
    }
}
