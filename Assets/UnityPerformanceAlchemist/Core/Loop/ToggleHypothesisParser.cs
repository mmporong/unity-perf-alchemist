using System.Collections.Generic;
using System.Globalization;

namespace UnityPerformanceAlchemist.Core.Loop
{
    /// <summary>
    /// LLM이 제안한 설정 토글 가설 (계약 §2).
    /// </summary>
    public sealed class ToggleHypothesis
    {
        public string Strategy { get; }                              // 한 문장 설명
        public IReadOnlyDictionary<string, string> Settings { get; } // 설정 키 → 값(문자열 표준화)

        public ToggleHypothesis(string strategy, IReadOnlyDictionary<string, string> settings)
        {
            Strategy = strategy;
            Settings = settings;
        }
    }

    /// <summary>
    /// LLM 응답 텍스트에서 첫 번째 유효 JSON 오브젝트 {"strategy": "...", "settings": {...}} 추출.
    /// 마크다운 펜스(```json ... ```) 유무, 앞뒤 잡담 모두 허용. 실패 시 false.
    /// </summary>
    public static class ToggleHypothesisParser
    {
        private const string DefaultStrategy = "(no strategy)";

        public static bool TryParse(string llmResponse, out ToggleHypothesis hypothesis)
        {
            hypothesis = null;
            if (string.IsNullOrEmpty(llmResponse))
            {
                return false;
            }

            // 펜스 제거 전처리 없이 '{' 후보마다 파싱 시도 — 펜스/잡담/잡 중괄호를 한 경로로 처리.
            for (int i = 0; i < llmResponse.Length; i++)
            {
                if (llmResponse[i] != '{')
                {
                    continue;
                }
                int index = i;
                if (!MiniJson.TryParseValue(llmResponse, ref index, out object value))
                {
                    continue;
                }
                if (!(value is Dictionary<string, object> obj))
                {
                    continue;
                }
                if (!obj.TryGetValue("settings", out object settingsObj))
                {
                    continue; // settings 누락 → 이 오브젝트는 무효
                }
                if (!(settingsObj is Dictionary<string, object> rawSettings) || rawSettings.Count == 0)
                {
                    continue; // settings가 오브젝트가 아니거나 빈 오브젝트 → 무효
                }

                var settings = new Dictionary<string, string>();
                foreach (KeyValuePair<string, object> pair in rawSettings)
                {
                    string normalized = NormalizeValue(pair.Value);
                    if (normalized != null)
                    {
                        settings[pair.Key] = normalized;
                    }
                }
                if (settings.Count == 0)
                {
                    continue; // 표준화 가능한 값이 하나도 없음
                }

                string strategy = DefaultStrategy;
                if (obj.TryGetValue("strategy", out object strategyObj)
                    && strategyObj is string text && text.Length > 0)
                {
                    strategy = text;
                }

                hypothesis = new ToggleHypothesis(strategy, settings);
                return true;
            }
            return false;
        }

        // 설정 값 문자열 표준화: 숫자 → InvariantCulture, 불리언 → "true"/"false".
        // ponytail: 중첩 오브젝트/배열/null 값은 설정으로 표현 불가 → 건너뜀. 필요해지면 평탄화가 업그레이드 경로.
        private static string NormalizeValue(object value)
        {
            switch (value)
            {
                case string s:
                    return s;
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "true" : "false";
                default:
                    return null;
            }
        }
    }
}
