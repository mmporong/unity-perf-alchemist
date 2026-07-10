using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// Google Gemini(v1beta generateContent) 프로바이더.
    /// apiKey는 EditorPrefs "Alchemist_API_Key"에서 매 호출 시 로드 — 파일/상태에 평문 저장 금지.
    /// </summary>
    public sealed class GeminiProvider : ILlmProvider
    {
        public const string ApiKeyPrefName = "Alchemist_API_Key";

        public string Model = "gemini-2.5-flash";

        private const int TimeoutSeconds = 120;

        public string DisplayName
        {
            get { return $"Gemini ({Model})"; }
        }

        public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
        {
            string apiKey = EditorPrefs.GetString(ApiKeyPrefName, "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning($"[Alchemist] Gemini API 키가 없습니다 (EditorPrefs \"{ApiKeyPrefName}\").");
                return "";
            }

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={apiKey}";
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };
            string response = await LlmClient.PostJsonAsync(
                url, JsonConvert.SerializeObject(body), TimeoutSeconds, ct);
            return LlmClient.ExtractText(response, "candidates[0].content.parts[0].text");
        }
    }
}
