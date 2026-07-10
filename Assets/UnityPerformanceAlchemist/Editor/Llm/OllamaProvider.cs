using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// 로컬 Ollama(OpenAI 호환 chat/completions) 프로바이더 — 기존 PostToAI 로직 이식.
    /// endpoint/model은 필드로 설정 가능.
    /// </summary>
    public sealed class OllamaProvider : ILlmProvider
    {
        public string Endpoint = "http://localhost:11434/v1/chat/completions";
        public string Model = "llama3.2:1b";

        private const int MaxTokens = 600;
        private const int TimeoutSeconds = 120;   // 소형 모델 긴 응답 대비

        public string DisplayName
        {
            get { return $"Ollama ({Model})"; }
        }

        public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
        {
            var body = new
            {
                model = Model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = MaxTokens,
                stream = false
            };
            string response = await LlmClient.PostJsonAsync(
                Endpoint, JsonConvert.SerializeObject(body), TimeoutSeconds, ct);
            return LlmClient.ExtractText(response, "choices[0].message.content");
        }
    }
}
