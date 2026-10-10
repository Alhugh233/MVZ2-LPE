#nullable enable

using System.Collections.Generic;
using System.Text;

namespace LanguagePackEnhance;

/// <summary>
/// 极简 JSON 解析器（仅用于解析 Mod 自带语言包的简化格式，避免引入第三方依赖）。
/// </summary>
internal static class MiniJson
{
    public static object? Parse(string text)
    {
        int index = 0;
        return ParseValue(text, ref index);
    }

    private static object? ParseValue(string s, ref int i)
    {
        Skip(s, ref i);
        if (i >= s.Length)
            return null;
        char c = s[i];
        switch (c)
        {
            case '{': return ParseObject(s, ref i);
            case '[': return ParseArray(s, ref i);
            case '"': return ParseString(s, ref i);
            case 't': i += 4; return true;
            case 'f': i += 5; return false;
            case 'n': i += 4; return null;
            default: return ParseNumber(s, ref i);
        }
    }

    private static void Skip(string s, ref int i)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                i += 2;
                while (i < s.Length && s[i] != '\n')
                    i++;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/'))
                    i++;
                i += 2;
                continue;
            }
            break;
        }
    }

    private static Dictionary<string, object?> ParseObject(string s, ref int i)
    {
        var dict = new Dictionary<string, object?>();
        i++;
        while (i < s.Length)
        {
            Skip(s, ref i);
            if (i >= s.Length || s[i] == '}')
            {
                i++;
                break;
            }
            if (s[i] == ',')
            {
                i++;
                continue;
            }
            var key = ParseString(s, ref i);
            Skip(s, ref i);
            if (i < s.Length && s[i] == ':')
                i++;
            dict[key] = ParseValue(s, ref i);
        }
        return dict;
    }

    private static List<object?> ParseArray(string s, ref int i)
    {
        var list = new List<object?>();
        i++;
        while (i < s.Length)
        {
            Skip(s, ref i);
            if (i >= s.Length || s[i] == ']')
            {
                i++;
                break;
            }
            if (s[i] == ',')
            {
                i++;
                continue;
            }
            list.Add(ParseValue(s, ref i));
        }
        return list;
    }

    private static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        if (i < s.Length && s[i] == '"')
            i++;
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"')
                break;
            if (c == '\\' && i < s.Length)
            {
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            var hex = s.Substring(i, 4);
                            i += 4;
                            sb.Append((char)System.Convert.ToInt32(hex, 16));
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static object ParseNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            i++;
        var sub = s.Substring(start, i - start);
        if (long.TryParse(sub, out var l))
            return l;
        if (double.TryParse(sub, out var d))
            return d;
        return 0L;
    }

    public static Dictionary<string, object?>? Obj(object? o) => o as Dictionary<string, object?>;
    public static List<object?>? Arr(object? o) => o as List<object?>;
    public static string Str(object? o) => o as string ?? string.Empty;
}
