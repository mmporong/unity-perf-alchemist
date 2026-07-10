using System.Collections.Generic;
using System.Globalization;
using System.Text;

// MiniJson 단위 테스트(계약 §6)를 위해 Core 내부 타입을 테스트 어셈블리에 공개.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UPA.Tests.Core")]

namespace UnityPerformanceAlchemist.Core.Loop
{
    /// <summary>
    /// 최소 재귀하강 JSON 파서 — Core에 JSON 라이브러리 금지 계약(§2)에 따른 내장 구현.
    /// 지원: 문자열/숫자/불리언/null/중첩 오브젝트/배열, \uXXXX 이스케이프.
    /// 결과 타입: Dictionary&lt;string, object&gt; / List&lt;object&gt; / string / double / bool / null.
    /// </summary>
    internal static class MiniJson
    {
        // 전체 문자열이 단일 JSON 값이어야 성공 (앞뒤 공백 허용, 뒤에 잡문 있으면 실패).
        internal static bool TryParse(string json, out object value)
        {
            value = null;
            if (json == null)
            {
                return false;
            }
            int index = 0;
            if (!TryParseValue(json, ref index, out value))
            {
                value = null;
                return false;
            }
            SkipWhitespace(json, ref index);
            if (index != json.Length)
            {
                value = null;
                return false;
            }
            return true;
        }

        // index 위치부터 JSON 값 1개를 파싱. 성공 시 index는 값 끝 다음을 가리킨다.
        // ToggleHypothesisParser가 잡문 속 후보 '{' 위치마다 호출하는 진입점.
        internal static bool TryParseValue(string s, ref int index, out object value)
        {
            value = null;
            SkipWhitespace(s, ref index);
            if (index >= s.Length)
            {
                return false;
            }
            char c = s[index];
            switch (c)
            {
                case '{':
                    return TryParseObject(s, ref index, out value);
                case '[':
                    return TryParseArray(s, ref index, out value);
                case '"':
                    if (TryParseString(s, ref index, out string str))
                    {
                        value = str;
                        return true;
                    }
                    return false;
                case 't':
                    if (MatchLiteral(s, ref index, "true"))
                    {
                        value = true;
                        return true;
                    }
                    return false;
                case 'f':
                    if (MatchLiteral(s, ref index, "false"))
                    {
                        value = false;
                        return true;
                    }
                    return false;
                case 'n':
                    // value는 이미 null
                    return MatchLiteral(s, ref index, "null");
                default:
                    return TryParseNumber(s, ref index, out value);
            }
        }

        private static bool TryParseObject(string s, ref int index, out object value)
        {
            value = null;
            var dict = new Dictionary<string, object>();
            index++; // '{'
            SkipWhitespace(s, ref index);
            if (index < s.Length && s[index] == '}')
            {
                index++;
                value = dict;
                return true;
            }
            while (true)
            {
                SkipWhitespace(s, ref index);
                if (index >= s.Length || s[index] != '"')
                {
                    return false;
                }
                if (!TryParseString(s, ref index, out string key))
                {
                    return false;
                }
                SkipWhitespace(s, ref index);
                if (index >= s.Length || s[index] != ':')
                {
                    return false;
                }
                index++; // ':'
                if (!TryParseValue(s, ref index, out object item))
                {
                    return false;
                }
                dict[key] = item;
                SkipWhitespace(s, ref index);
                if (index >= s.Length)
                {
                    return false;
                }
                if (s[index] == ',')
                {
                    index++;
                    continue;
                }
                if (s[index] == '}')
                {
                    index++;
                    value = dict;
                    return true;
                }
                return false;
            }
        }

        private static bool TryParseArray(string s, ref int index, out object value)
        {
            value = null;
            var list = new List<object>();
            index++; // '['
            SkipWhitespace(s, ref index);
            if (index < s.Length && s[index] == ']')
            {
                index++;
                value = list;
                return true;
            }
            while (true)
            {
                if (!TryParseValue(s, ref index, out object item))
                {
                    return false;
                }
                list.Add(item);
                SkipWhitespace(s, ref index);
                if (index >= s.Length)
                {
                    return false;
                }
                if (s[index] == ',')
                {
                    index++;
                    continue;
                }
                if (s[index] == ']')
                {
                    index++;
                    value = list;
                    return true;
                }
                return false;
            }
        }

        private static bool TryParseString(string s, ref int index, out string result)
        {
            result = null;
            var sb = new StringBuilder();
            index++; // 여는 따옴표
            while (index < s.Length)
            {
                char c = s[index++];
                if (c == '"')
                {
                    result = sb.ToString();
                    return true;
                }
                if (c == '\\')
                {
                    if (index >= s.Length)
                    {
                        return false;
                    }
                    char escape = s[index++];
                    switch (escape)
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
                            if (index + 4 > s.Length)
                            {
                                return false;
                            }
                            if (!ushort.TryParse(s.Substring(index, 4), NumberStyles.HexNumber,
                                                 CultureInfo.InvariantCulture, out ushort code))
                            {
                                return false;
                            }
                            sb.Append((char)code);
                            index += 4;
                            break;
                        default:
                            return false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return false; // 닫는 따옴표 없이 끝남
        }

        private static bool TryParseNumber(string s, ref int index, out object value)
        {
            value = null;
            int start = index;
            if (index < s.Length && s[index] == '-')
            {
                index++;
            }
            while (index < s.Length && (char.IsDigit(s[index]) || s[index] == '.'
                   || s[index] == 'e' || s[index] == 'E' || s[index] == '+' || s[index] == '-'))
            {
                index++;
            }
            if (index == start)
            {
                return false;
            }
            string token = s.Substring(start, index - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                index = start;
                return false;
            }
            value = d;
            return true;
        }

        private static bool MatchLiteral(string s, ref int index, string literal)
        {
            if (index + literal.Length > s.Length)
            {
                return false;
            }
            if (string.CompareOrdinal(s, index, literal, 0, literal.Length) != 0)
            {
                return false;
            }
            index += literal.Length;
            return true;
        }

        private static void SkipWhitespace(string s, ref int index)
        {
            while (index < s.Length && char.IsWhiteSpace(s[index]))
            {
                index++;
            }
        }
    }
}
