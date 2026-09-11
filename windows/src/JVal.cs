// A small JSON value model, writer and parser.
//
// The app talks to local inference servers over JSON and stores its own records
// as JSON inside an encrypted envelope, so this carries both jobs. Hand rolled
// rather than pulled in: the whole build has to stay a single executable with no
// dependency beyond the .NET Framework that ships with Windows.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClipSyncAI
{
    internal enum JKind { Null, Bool, Num, Str, Arr, Obj }

    internal sealed class JVal
    {
        public JKind Kind;
        public bool Bool;
        public double Num;
        public string Str;
        public List<JVal> Arr;
        public List<string> Keys;
        public Dictionary<string, JVal> Map;

        public static readonly JVal Null = new JVal { Kind = JKind.Null };

        public static JVal Of(bool v) { return new JVal { Kind = JKind.Bool, Bool = v }; }
        public static JVal Of(double v) { return new JVal { Kind = JKind.Num, Num = v }; }
        public static JVal Of(long v) { return new JVal { Kind = JKind.Num, Num = v }; }
        public static JVal Of(string v)
        {
            if (v == null) return Null;
            return new JVal { Kind = JKind.Str, Str = v };
        }

        public static JVal Array()
        {
            return new JVal { Kind = JKind.Arr, Arr = new List<JVal>() };
        }

        public static JVal Object()
        {
            return new JVal
            {
                Kind = JKind.Obj,
                Keys = new List<string>(),
                Map = new Dictionary<string, JVal>(StringComparer.Ordinal)
            };
        }

        public JVal Set(string key, JVal value)
        {
            if (Kind != JKind.Obj) throw new InvalidOperationException("not an object");
            if (!Map.ContainsKey(key)) Keys.Add(key);
            Map[key] = value ?? Null;
            return this;
        }

        public JVal Set(string key, string value) { return Set(key, Of(value)); }
        public JVal Set(string key, bool value) { return Set(key, Of(value)); }
        public JVal Set(string key, double value) { return Set(key, Of(value)); }
        public JVal Set(string key, long value) { return Set(key, Of(value)); }

        public JVal Add(JVal value)
        {
            if (Kind != JKind.Arr) throw new InvalidOperationException("not an array");
            Arr.Add(value ?? Null);
            return this;
        }

        /// Member lookup that never throws. A record written by an older build
        /// is missing keys the current one reads, and that has to degrade to a
        /// default rather than lose the record.
        public JVal this[string key]
        {
            get
            {
                if (Kind != JKind.Obj) return Null;
                JVal v;
                return Map.TryGetValue(key, out v) ? v : Null;
            }
        }

        public bool Has(string key)
        {
            return Kind == JKind.Obj && Map.ContainsKey(key);
        }

        public int Count
        {
            get
            {
                if (Kind == JKind.Arr) return Arr.Count;
                if (Kind == JKind.Obj) return Keys.Count;
                return 0;
            }
        }

        public JVal At(int i)
        {
            if (Kind != JKind.Arr || i < 0 || i >= Arr.Count) return Null;
            return Arr[i];
        }

        public string AsString(string fallback)
        {
            if (Kind == JKind.Str) return Str;
            if (Kind == JKind.Num) return Num.ToString("R", CultureInfo.InvariantCulture);
            if (Kind == JKind.Bool) return Bool ? "true" : "false";
            return fallback;
        }

        public string AsString() { return AsString(""); }

        public double AsNum(double fallback)
        {
            if (Kind == JKind.Num) return Num;
            if (Kind == JKind.Str)
            {
                double d;
                if (double.TryParse(Str, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            return fallback;
        }

        public long AsLong(long fallback) { return (long)AsNum(fallback); }
        public int AsInt(int fallback) { return (int)AsNum(fallback); }

        public bool AsBool(bool fallback)
        {
            if (Kind == JKind.Bool) return Bool;
            if (Kind == JKind.Num) return Num != 0;
            return fallback;
        }

        public IEnumerable<JVal> Items()
        {
            if (Kind != JKind.Arr) yield break;
            for (int i = 0; i < Arr.Count; i++) yield return Arr[i];
        }

        public override string ToString() { return JsonWriter.Write(this); }
    }
}
