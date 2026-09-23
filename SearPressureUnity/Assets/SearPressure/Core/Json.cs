using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SearPressure
{
    // A small JSON reader/writer. Objects are Dictionary<string, object>, arrays are List<object>,
    // numbers are double, plus string, bool and null. Used for the game data and the save file.
    public static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Read(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Read(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = ReadString(s, ref i);
                    Ws(s, ref i); i++; // ':'
                    d[k] = Read(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d; // '}'
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Read(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l; // ']'
                }
            }
            if (c == '"') return ReadString(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (true)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Write(object v)
        {
            var sb = new StringBuilder();
            WriteTo(sb, v);
            return sb.ToString();
        }

        static void WriteTo(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(Num(d)); break;
                case float f: sb.Append(Num(f)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> d:
                    {
                        sb.Append('{'); bool first = true;
                        foreach (var kv in d) { if (!first) sb.Append(','); first = false; WriteString(sb, kv.Key); sb.Append(':'); WriteTo(sb, kv.Value); }
                        sb.Append('}'); break;
                    }
                case System.Collections.IEnumerable e:
                    {
                        sb.Append('['); bool first = true;
                        foreach (var x in e) { if (!first) sb.Append(','); first = false; WriteTo(sb, x); }
                        sb.Append(']'); break;
                    }
                default: WriteString(sb, v.ToString()); break;
            }
        }

        public static string Num(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    // Convenience accessors for parsed JSON trees.
    public static class J
    {
        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        public static List<object> Arr(object o) => o as List<object>;
        public static object Get(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v : null;
        public static string Str(object o, string k, string def = null) => Get(o, k) is string s ? s : def;
        public static double Num(object o, string k, double def = 0) => Get(o, k) is double d ? d : def;
        public static bool Bool(object o, string k) => Get(o, k) is bool b && b;
        public static bool Has(object o, string k) => o is Dictionary<string, object> d && d.ContainsKey(k) && d[k] != null;
        public static List<string> Strs(object o) { var l = new List<string>(); if (o is List<object> a) foreach (var x in a) l.Add(x as string); return l; }
        public static List<double> Nums(object o) { var l = new List<double>(); if (o is List<object> a) foreach (var x in a) l.Add(x is double d ? d : 0); return l; }
    }
}
