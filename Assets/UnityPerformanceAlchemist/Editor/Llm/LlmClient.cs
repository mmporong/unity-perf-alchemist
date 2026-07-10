using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// UnityWebRequest POST 공통 + JSON 응답 텍스트 추출 — LLM 프로바이더 공용 (기존 PostToAI 이식).
    /// 실패 시 빈 문자열 반환 + 경고 로그: 호출부(러너)는 빈 응답을 가설 실패로 처리한다.
    /// </summary>
    public static class LlmClient
    {
        public static async Task<string> PostJsonAsync(string url, string jsonPayload,
                                                       int timeoutSeconds, CancellationToken ct)
        {
            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = timeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        request.Abort();
                        ct.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Alchemist] LLM 요청 실패 ({url}): {request.error}");
                    return "";
                }
                return request.downloadHandler.text;
            }
        }

        /// <summary>JSON 응답에서 JSONPath 경로의 텍스트 추출. 없거나 파싱 실패 시 빈 문자열.</summary>
        public static string ExtractText(string responseJson, string tokenPath)
        {
            if (string.IsNullOrEmpty(responseJson))
            {
                return "";
            }
            try
            {
                JToken token = JObject.Parse(responseJson).SelectToken(tokenPath);
                return token != null ? token.ToString() : "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Alchemist] LLM 응답 파싱 실패: {e.Message}");
                return "";
            }
        }
    }
}
