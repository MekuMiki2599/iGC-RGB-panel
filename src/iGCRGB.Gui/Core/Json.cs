using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace iGCRGB.Core
{
    /// <summary>极简 JSON 读写（只服务于配置文件和 iGC 预设导入）。</summary>
    public static class Json
    {
        // ---------------- 解析 ----------------

        public static object Parse(string text)
        {
            int pos = 0;
            object value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos < text.Length)
                throw new FormatException("JSON 解析失败：位置 " + pos + " 存在多余内容");
            return value;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                break;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 解析失败：内容不完整");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') return ParseNull(s, ref i);
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON 解析失败：缺少 ':'");
                i++;
                obj[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 解析失败：对象未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return obj; }
                throw new FormatException("JSON 解析失败：对象中出现非法字符");
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 解析失败：数组未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException("JSON 解析失败：数组中出现非法字符");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("JSON 解析失败：期望字符串");
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
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
                        if (i + 4 > s.Length) throw new FormatException("JSON 解析失败：\\u 转义不完整");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException("JSON 解析失败：未知转义 \\" + e);
                }
            }
            throw new FormatException("JSON 解析失败：字符串未闭合");
        }

        private static object ParseBool(string s, ref int i)
        {
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            throw new FormatException("JSON 解析失败：非法字面量");
        }

        private static object ParseNull(string s, ref int i)
        {
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            throw new FormatException("JSON 解析失败：非法字面量");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '-' || s[i] == '+'))
                i++;
            string token = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("JSON 解析失败：非法数字 '" + token + "'");
            return d;
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static object Get(object obj, string key)
        {
            Dictionary<string, object> d = AsObject(obj);
            if (d == null) return null;
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        public static string GetString(object obj, string key, string defaultValue)
        {
            object v = Get(obj, key);
            return v is string ? (string)v : defaultValue;
        }

        public static int GetInt(object obj, string key, int defaultValue)
        {
            object v = Get(obj, key);
            if (v is double) return (int)Math.Round((double)v);
            if (v is string)
            {
                int parsed;
                if (int.TryParse((string)v, out parsed)) return parsed;
            }
            return defaultValue;
        }

        public static bool GetBool(object obj, string key, bool defaultValue)
        {
            object v = Get(obj, key);
            if (v is bool) return (bool)v;
            if (v is double) return Math.Abs((double)v) > 0.0001;
            if (v is string)
            {
                string s = ((string)v).Trim().ToLowerInvariant();
                if (s == "true" || s == "1" || s == "yes") return true;
                if (s == "false" || s == "0" || s == "no") return false;
            }
            return defaultValue;
        }

        // ---------------- 输出 ----------------

        public static string Escape(string text)
        {
            if (text == null) return "";
            StringBuilder sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
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
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
