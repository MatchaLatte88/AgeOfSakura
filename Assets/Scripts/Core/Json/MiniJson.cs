using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AgeOfSakura.Core
{
    public sealed class MiniJsonException : Exception
    {
        public MiniJsonException(string message) : base(message) { }
    }

    /// <summary>
    /// Small dependency-free JSON reader/writer. Objects become Dictionary&lt;string, object&gt;,
    /// arrays List&lt;object&gt;, numbers long (integral) or double. Exists so save data and
    /// definitions can be parsed and unit-tested outside the Unity runtime.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var parser = new Parser(json);
            var value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Fail("Unexpected trailing content");
            return value;
        }

        public static string Serialize(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, bool pretty, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d)) throw new MiniJsonException("Cannot serialize NaN/Infinity");
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> dict:
                    WriteObject(sb, dict, pretty, depth);
                    break;
                case IEnumerable<object> list:
                    WriteArray(sb, list, pretty, depth);
                    break;
                default:
                    throw new MiniJsonException($"Cannot serialize type {value.GetType().Name}");
            }
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> dict, bool pretty, int depth)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            bool first = true;
            foreach (var pair in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                WriteString(sb, pair.Key);
                sb.Append(pretty ? ": " : ":");
                Write(sb, pair.Value, pretty, depth + 1);
            }
            NewLine(sb, pretty, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable<object> list, bool pretty, int depth)
        {
            bool first = true;
            sb.Append('[');
            foreach (var item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, pretty, depth + 1);
                Write(sb, item, pretty, depth + 1);
            }
            if (!first) NewLine(sb, pretty, depth);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
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
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string text;
            private int pos;

            public Parser(string text) { this.text = text; }

            public bool AtEnd => pos >= text.Length;

            public MiniJsonException Fail(string message) => new MiniJsonException($"{message} at position {pos}");

            public void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Fail("Unexpected end of input");
                char c = text[pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Fail($"Unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>();
                pos++; // {
                SkipWhitespace();
                if (Peek('}')) { pos++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || text[pos] != '"') throw Fail("Expected object key");
                    string key = ParseString();
                    SkipWhitespace();
                    if (!Peek(':')) throw Fail("Expected ':'");
                    pos++;
                    if (result.ContainsKey(key)) throw Fail($"Duplicate key '{key}'");
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (Peek(',')) { pos++; continue; }
                    if (Peek('}')) { pos++; return result; }
                    throw Fail("Expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                pos++; // [
                SkipWhitespace();
                if (Peek(']')) { pos++; return result; }
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (Peek(',')) { pos++; continue; }
                    if (Peek(']')) { pos++; return result; }
                    throw Fail("Expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                pos++; // opening quote
                while (true)
                {
                    if (AtEnd) throw Fail("Unterminated string");
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Fail("Unterminated escape");
                    char e = text[pos++];
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
                            if (pos + 4 > text.Length) throw Fail("Truncated \\u escape");
                            if (!int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Fail("Invalid \\u escape");
                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default: throw Fail($"Invalid escape '\\{e}'");
                    }
                }
            }

            private object ParseNumber()
            {
                int start = pos;
                if (text[pos] == '-') pos++;
                bool integral = true;
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (c >= '0' && c <= '9') { pos++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { integral = false; pos++; continue; }
                    break;
                }
                string token = text.Substring(start, pos - start);
                if (integral && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                pos = start;
                throw Fail($"Invalid number '{token}'");
            }

            private void ExpectLiteral(string literal)
            {
                if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0) throw Fail($"Expected '{literal}'");
                pos += literal.Length;
            }

            private bool Peek(char c) => pos < text.Length && text[pos] == c;
        }
    }

    /// <summary>
    /// Typed, path-aware accessor over a parsed JSON object. Wrong or missing values throw a
    /// <see cref="MiniJsonException"/> naming the exact path, so bad data fails loudly.
    /// </summary>
    public sealed class JObj
    {
        private readonly IDictionary<string, object> data;
        public string Path { get; }

        public JObj(object raw, string path)
        {
            data = raw as IDictionary<string, object>;
            Path = path;
            if (data == null) throw new MiniJsonException($"{path}: expected a JSON object");
        }

        public bool Has(string key) => data.ContainsKey(key) && data[key] != null;

        public string Str(string key)
        {
            var v = Require(key);
            if (v is string s) return s;
            throw Wrong(key, "a string");
        }

        public string StrOrNull(string key) => Has(key) ? Str(key) : null;

        public long Long(string key)
        {
            var v = Require(key);
            if (v is long l) return l;
            if (v is double d && d == Math.Floor(d)) return (long)d;
            throw Wrong(key, "an integer");
        }

        public int Int(string key) => checked((int)Long(key));

        public int Int(string key, int fallbackWhenMissing) => Has(key) ? Int(key) : fallbackWhenMissing;

        public double Double(string key)
        {
            var v = Require(key);
            if (v is double d) return d;
            if (v is long l) return l;
            throw Wrong(key, "a number");
        }

        public double Double(string key, double fallbackWhenMissing) => Has(key) ? Double(key) : fallbackWhenMissing;

        public bool Bool(string key, bool fallbackWhenMissing)
        {
            if (!Has(key)) return fallbackWhenMissing;
            if (data[key] is bool b) return b;
            throw Wrong(key, "a boolean");
        }

        public JObj Obj(string key) => new JObj(Require(key), Path + "." + key);

        public bool HasObj(string key) => Has(key);

        public List<JObj> ObjList(string key)
        {
            var list = RequireList(key);
            var result = new List<JObj>(list.Count);
            for (int i = 0; i < list.Count; i++) result.Add(new JObj(list[i], $"{Path}.{key}[{i}]"));
            return result;
        }

        /// <summary>Like <see cref="ObjList"/>, but a missing key yields an empty list.</summary>
        public List<JObj> ObjListOrEmpty(string key) => Has(key) ? ObjList(key) : new List<JObj>();

        public List<string> StrList(string key, bool optional = false)
        {
            if (optional && !Has(key)) return new List<string>();
            var list = RequireList(key);
            var result = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is string s)) throw new MiniJsonException($"{Path}.{key}[{i}]: expected a string");
                result.Add(s);
            }
            return result;
        }

        private List<object> RequireList(string key)
        {
            var v = Require(key);
            if (v is List<object> list) return list;
            throw Wrong(key, "an array");
        }

        private object Require(string key)
        {
            if (!data.TryGetValue(key, out var v) || v == null) throw new MiniJsonException($"{Path}.{key}: missing required value");
            return v;
        }

        private MiniJsonException Wrong(string key, string expected) => new MiniJsonException($"{Path}.{key}: expected {expected}");
    }
}
