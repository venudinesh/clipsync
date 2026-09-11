// JSON serialisation. Writes compact by default; pretty printing exists only
// for the settings file, which a user may reasonably want to read or repair by
// hand after an export.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClipSyncAI
{
    internal static class JsonWriter
    {
        public static string Write(JVal v)
        {
            StringBuilder sb = new StringBuilder(256);
            WriteTo(sb, v, -1, 0);
            return sb.ToString();
        }

        public static string Pretty(JVal v)
        {
            StringBuilder sb = new StringBuilder(512);
            WriteTo(sb, v, 2, 0);
            return sb.ToString();
        }

        private static void WriteTo(StringBuilder sb, JVal v, int indent, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            switch (v.Kind)
            {
                case JKind.Null:
                    sb.Append("null");
                    return;
                case JKind.Bool:
                    sb.Append(v.Bool ? "true" : "false");
                    return;
                case JKind.Num:
                    sb.Append(Number(v.Num));
                    return;
                case JKind.Str:
                    Quote(sb, v.Str);
                    return;
                case JKind.Arr:
                    if (v.Arr.Count == 0) { sb.Append("[]"); return; }
                    sb.Append('[');
                    for (int i = 0; i < v.Arr.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Break(sb, indent, depth + 1);
                        WriteTo(sb, v.Arr[i], indent, depth + 1);
                    }
                    Break(sb, indent, depth);
                    sb.Append(']');
                    return;
                default:
                    if (v.Keys.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    for (int i = 0; i < v.Keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Break(sb, indent, depth + 1);
                        Quote(sb, v.Keys[i]);
                        sb.Append(':');
                        if (indent > 0) sb.Append(' ');
                        WriteTo(sb, v.Map[v.Keys[i]], indent, depth + 1);
                    }
                    Break(sb, indent, depth);
                    sb.Append('}');
                    return;
            }
        }

        private static void Break(StringBuilder sb, int indent, int depth)
        {
            if (indent < 0) return;
            sb.Append('\n');
            sb.Append(' ', indent * depth);
        }

        /// Integers are written without a decimal point, because an id or a
        /// millisecond timestamp round tripping as 1.7568E+12 is both ugly and
        /// lossy once it goes back through a parser.
        private static string Number(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "0";
            if (d == Math.Floor(d) && Math.Abs(d) < 9.007199254740992E15)
            {
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Quote(StringBuilder sb, string s)
        {
            if (s == null) { sb.Append("null"); return; }
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // Control characters have to be escaped to stay valid
                        // JSON. Everything above them, emoji included, is
                        // written through as UTF-8 by the file layer.
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
