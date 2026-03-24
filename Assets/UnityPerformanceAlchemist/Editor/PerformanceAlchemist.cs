using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace UnityPerformanceAlchemist.Editor
{
    public class PerformanceAlchemistWindow : EditorWindow
    {
        private enum LLMProvider { Gemini, Ollama_Local }
        private LLMProvider selectedProvider = LLMProvider.Ollama_Local;
        
        private string apiKey = "";
        private string localEndpoint = "http://localhost:11434/v1/chat/completions";
        private string localModel = "llama3.2:1b";
        
        private MonoScript targetScript;
        private string optimizationGoal = "Maximize FPS on Mobile Hardware";
        private bool isRunning = false;
        
        // --- AutoResearch Metrics ---
        [System.Serializable]
        private class GenData
        {
            public int generation;
            public float fps;
            public string strategy;
            public bool isAccepted;
            public string code; // 리팩토링된 코드 전문
        }
        private List<GenData> researchHistory = new List<GenData>();
        private float initialFPS = 0;
        private float bestFPS = 0;
        private string bestCode = "";

        [MenuItem("Window/Alchemist/Performance Researcher")]
        public static void ShowWindow()
        {
            var window = GetWindow<PerformanceAlchemistWindow>("Alchemist Dashboard 🧪");
            window.minSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            apiKey = EditorPrefs.GetString("Alchemist_API_Key", "");
            localEndpoint = EditorPrefs.GetString("Alchemist_Local_Endpoint", "http://localhost:11434/v1/chat/completions");
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            DrawSettings();
            DrawPerformanceGraph();
            EditorGUILayout.EndHorizontal();

            DrawResearchHistory();
            
            if (isRunning) Repaint();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("⚛️ AutoResearch Autonomous Engine", headerStyle);
            
            if (bestFPS > 0)
            {
                float improvement = ((bestFPS - initialFPS) / initialFPS) * 100f;
                var subHeaderStyle = new GUIStyle(EditorStyles.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
                string color = improvement > 0 ? "green" : "white";
                GUILayout.Label($"Current Improvement: <color={color}>+{improvement:F1}%</color> (Base: {initialFPS:F1} → Best: {bestFPS:F1} FPS)", new GUIStyle(subHeaderStyle) { richText = true });
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            GUILayout.Label("🛠️ Configuration", EditorStyles.boldLabel);
            
            selectedProvider = (LLMProvider)EditorGUILayout.EnumPopup("LLM Provider", selectedProvider);

            if (selectedProvider == LLMProvider.Gemini)
            {
                apiKey = EditorGUILayout.PasswordField("Gemini API Key", apiKey);
                if (GUI.changed) EditorPrefs.SetString("Alchemist_API_Key", apiKey);
            }
            else
            {
                localEndpoint = EditorGUILayout.TextField("Ollama Endpoint", localEndpoint);
                localModel = EditorGUILayout.TextField("Model Name", localModel);
                if (GUI.changed) EditorPrefs.SetString("Alchemist_Local_Endpoint", localEndpoint);
            }

            targetScript = (MonoScript)EditorGUILayout.ObjectField("Target Script", targetScript, typeof(MonoScript), false);
            optimizationGoal = EditorGUILayout.TextField("Goal", optimizationGoal);

            EditorGUILayout.Space();

            GUI.backgroundColor = isRunning ? Color.red : Color.green;
            if (GUILayout.Button(isRunning ? "🛑 STOP RESEARCH" : "🚀 START AUTORESEARCH", GUILayout.Height(40)))
            {
                if (isRunning) isRunning = false;
                else StartResearchLoop();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPerformanceGraph()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📈 Performance Trend (FPS)", EditorStyles.boldLabel);
            
            Rect graphRect = GUILayoutUtility.GetRect(200, 150);
            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

            if (researchHistory.Count > 1)
            {
                float maxFPS = researchHistory.Max(h => h.fps) * 1.2f;
                if (maxFPS < 60) maxFPS = 60;

                for (int i = 0; i < researchHistory.Count - 1; i++)
                {
                    Vector2 start = new Vector2(
                        graphRect.x + (i * (graphRect.width / (researchHistory.Count - 1))),
                        graphRect.y + graphRect.height - (researchHistory[i].fps / maxFPS * graphRect.height)
                    );
                    Vector2 end = new Vector2(
                        graphRect.x + ((i + 1) * (graphRect.width / (researchHistory.Count - 1))),
                        graphRect.y + graphRect.height - (researchHistory[i + 1].fps / maxFPS * graphRect.height)
                    );
                    Handles.color = researchHistory[i+1].isAccepted ? Color.green : Color.red;
                    Handles.DrawLine(start, end);
                    
                    EditorGUI.DrawRect(new Rect(start.x - 2, start.y - 2, 4, 4), Color.white);
                }
                Vector2 lastPoint = new Vector2(
                    graphRect.x + graphRect.width,
                    graphRect.y + graphRect.height - (researchHistory.Last().fps / maxFPS * graphRect.height)
                );
                EditorGUI.DrawRect(new Rect(lastPoint.x - 2, lastPoint.y - 2, 4, 4), Color.white);
            }
            else
            {
                GUI.Label(graphRect, "Not enough data to draw graph...", new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } });
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawResearchHistory()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📜 Research Change Log & Hypotheses", EditorStyles.boldLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var data in researchHistory)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.textArea);
                string statusIcon = data.isAccepted ? "✅" : "❌";
                string color = data.isAccepted ? "green" : "gray";
                
                GUILayout.Label($"Gen {data.generation}", GUILayout.Width(50));
                GUILayout.Label($"{statusIcon}", GUILayout.Width(20));
                GUILayout.Label($"<color={color}>{data.fps:F1} FPS</color>", new GUIStyle(EditorStyles.label) { richText = true, fontStyle = FontStyle.Bold }, GUILayout.Width(80));
                GUILayout.Label($"{data.strategy}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private Vector2 scrollPos;

        private async void StartResearchLoop()
        {
            if (targetScript == null) { Debug.LogError("Target script missing!"); return; }

            isRunning = true;
            researchHistory.Clear();
            bestCode = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
            
            float baseFPS = await RunBenchmark();
            initialFPS = baseFPS;
            bestFPS = baseFPS;
            researchHistory.Add(new GenData { generation = 0, fps = baseFPS, strategy = "Initial Baseline", isAccepted = true, code = bestCode });
            await SyncToWeb();

            for (int gen = 1; gen <= 10; gen++)
            {
                if (!isRunning) break;

                Log($"[Gen {gen}] AI 가설 수립 중...");
                var (strategy, newCode) = await RequestHypothesis(bestCode, bestFPS);
                
                if (string.IsNullOrEmpty(newCode)) continue;

                File.WriteAllText(AssetDatabase.GetAssetPath(targetScript), newCode);
                AssetDatabase.Refresh();
                await Task.Delay(5000); 

                Log($"[Gen {gen}] 성능 검증 중 (Benchmark)...");
                float testFPS = await RunBenchmark();
                
                bool accepted = testFPS > bestFPS + 0.5f; 
                if (accepted)
                {
                    Log($"[Gen {gen}] ✨ 가설 채택! 성능 향상 확인: {bestFPS:F1} → {testFPS:F1} FPS");
                    bestFPS = testFPS;
                    bestCode = newCode;
                }
                else
                {
                    Log($"[Gen {gen}] ❌ 가설 기각. 성능 저하 또는 정체: {testFPS:F1} FPS. 롤백합니다.");
                    File.WriteAllText(AssetDatabase.GetAssetPath(targetScript), bestCode);
                    AssetDatabase.Refresh();
                    await Task.Delay(3000);
                }

                researchHistory.Add(new GenData { generation = gen, fps = testFPS, strategy = strategy, isAccepted = accepted, code = (accepted ? newCode : "Rejected. Rolled back to previous best.") });
                await SyncToWeb(); 
            }

            isRunning = false;
            await SyncToWeb();
            Log("🏁 Research Loop Finished.");
        }

        private async Task SyncToWeb()
        {
            string url = "http://localhost:3848/api/update";
            float improvement = initialFPS > 0 ? ((bestFPS - initialFPS) / initialFPS) * 100f : 0;
            
            var payload = new
            {
                initialFPS = initialFPS,
                bestFPS = bestFPS,
                improvement = improvement,
                history = researchHistory,
                status = isRunning ? "Running" : "Finished"
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
            }
        }

        private async Task<float> RunBenchmark()
        {
            EditorApplication.isPlaying = true;
            await Task.Delay(2000); 

            float totalDelta = 0;
            int frames = 60;
            for (int i = 0; i < frames; i++)
            {
                totalDelta += Time.smoothDeltaTime;
                await Task.Yield();
            }

            EditorApplication.isPlaying = false;
            return frames / totalDelta;
        }

        private async Task<(string strategy, string code)> RequestHypothesis(string currentCode, float currentFPS)
        {
            string prompt = $"Unity Performance Alchemist Mission:\nGoal: {optimizationGoal}\nCurrent FPS: {currentFPS:F1}\n\nTask:\nAnalyze the following C# code and propose ONE specific architectural optimization.\nProvide your response in JSON format:\n{{\n  \"strategy\": \"Short description of what you are changing\",\n  \"code\": \"Full refactored C# code\"\n}}\n\nCode:\n{currentCode}";

            string responseJson = "";
            if (selectedProvider == LLMProvider.Gemini)
                responseJson = await PostToAI($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}", prompt, true);
            else
                responseJson = await PostToAI(localEndpoint, prompt, false);

            try
            {
                string cleanJson = responseJson;
                if (cleanJson.Contains("```json"))
                {
                    int start = cleanJson.IndexOf("```json") + 7;
                    int end = cleanJson.LastIndexOf("```");
                    cleanJson = cleanJson.Substring(start, end - start).Trim();
                }

                string strategy = "Optimization Step";
                string code = currentCode;

                if (cleanJson.Contains("\"strategy\":")) {
                    int sStart = cleanJson.IndexOf("\"strategy\":") + 12;
                    int sEnd = cleanJson.IndexOf("\"", sStart);
                    strategy = cleanJson.Substring(sStart, sEnd - sStart);
                }
                
                if (cleanJson.Contains("\"code\":")) {
                    int cStart = cleanJson.IndexOf("\"code\":") + 8;
                    int cEnd = cleanJson.LastIndexOf("\"");
                    code = cleanJson.Substring(cStart, cEnd - cStart).Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\t", "\t");
                }

                return (strategy, code);
            }
            catch
            {
                return ("AI Proposal Failed", currentCode);
            }
        }

        private async Task<string> PostToAI(string url, string prompt, bool isGemini)
        {
            string payload = "";
            if (isGemini) {
                payload = $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{prompt.Replace("\"", "\\\"").Replace("\n", "\\n")}\"}}]}}]}}";
            } else {
                payload = $"{{\"model\":\"{localModel}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{prompt.Replace("\"", "\\\"").Replace("\n", "\\n")}\"}}],\"stream\":false}}";
            }

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string text = request.downloadHandler.text;
                    if (isGemini) {
                        int start = text.IndexOf("\"text\": \"") + 9;
                        int end = text.IndexOf("\"", start);
                        return text.Substring(start, end - start).Replace("\\n", "\n").Replace("\\\"", "\"");
                    } else {
                        int start = text.IndexOf("\"content\":\"") + 11;
                        int end = text.IndexOf("\"", start);
                        return text.Substring(start, end - start).Replace("\\n", "\n").Replace("\\\"", "\"");
                    }
                }
                return "";
            }
        }

        private void Log(string msg) { Debug.Log("[Alchemist] " + msg); }
    }
}
