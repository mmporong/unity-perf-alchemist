using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Unity.Profiling;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            public string code; 
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
            try
            {
                isRunning = true;
                researchHistory.Clear();

                var state = new AlchemistFsmState
                {
                    phase = AlchemistFsmState.Phase.Baseline,
                    generation = 1,
                    maxGenerations = 10,
                    bestCode = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript)),
                    targetScriptPath = AssetDatabase.GetAssetPath(targetScript),
                    optimizationGoal = optimizationGoal,
                    llmProvider = selectedProvider.ToString(),
                    localEndpoint = localEndpoint,
                    localModel = localModel
                };
                state.apiKey = apiKey; // JsonIgnore이므로 직접 할당 (직렬화 제외)
                state.Save();

                await RunFsmAsync(state);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] StartResearchLoop 예외: {e}");
                isRunning = false;
                AlchemistFsmState.Delete();
            }
        }

        public async void ResumeAfterDomainReload(AlchemistFsmState state)
        {
            try
            {
                isRunning = true;
                initialFPS = state.initialFPS;
                bestFPS = state.bestFPS;
                bestCode = state.bestCode;
                optimizationGoal = state.optimizationGoal;
                // apiKey는 JsonIgnore — EditorPrefs에서 복원
                apiKey = EditorPrefs.GetString("Alchemist_API_Key", "");
                state.apiKey = apiKey;
                localEndpoint = state.localEndpoint;
                localModel = state.localModel;
                if (System.Enum.TryParse<LLMProvider>(state.llmProvider, out var provider))
                    selectedProvider = provider;

                researchHistory.Clear();
                foreach (var dto in state.history)
                    researchHistory.Add(new GenData { generation = dto.generation, fps = dto.fps, strategy = dto.strategy, isAccepted = dto.isAccepted, code = "" });

                Repaint();
                await RunFsmAsync(state);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] ResumeAfterDomainReload 예외: {e}");
                isRunning = false;
                AlchemistFsmState.Delete();
            }
        }

        private async Task RunFsmAsync(AlchemistFsmState state)
        {
            while (isRunning && state.phase != AlchemistFsmState.Phase.Done)
            {
                switch (state.phase)
                {
                    case AlchemistFsmState.Phase.Baseline:
                    {
                        Log("⏯️ 기준 성능 측정 중...");
                        float baseFPS = await RunBenchmark();
                        state.initialFPS = baseFPS;
                        state.bestFPS = baseFPS;
                        initialFPS = baseFPS;
                        bestFPS = baseFPS;
                        researchHistory.Add(new GenData { generation = 0, fps = baseFPS, strategy = "Initial Baseline", isAccepted = true, code = state.bestCode });
                        state.history.Add(new AlchemistFsmState.GenDataDto { generation = 0, fps = baseFPS, strategy = "Initial Baseline", isAccepted = true });
                        state.phase = AlchemistFsmState.Phase.Hypothesis;
                        state.Save();
                        await SyncToWeb();
                        break;
                    }

                    case AlchemistFsmState.Phase.Hypothesis:
                    {
                        if (state.generation > state.maxGenerations) { state.phase = AlchemistFsmState.Phase.Done; break; }

                        Log($"[Gen {state.generation}] AI 가설 수립 중...");
                        var (strategy, newCode) = await RequestHypothesis(state.bestCode, state.bestFPS);

                        if (string.IsNullOrEmpty(newCode)) { state.generation++; state.Save(); break; }

                        state.pendingStrategy = strategy;
                        state.pendingCode = newCode;
                        state.phase = AlchemistFsmState.Phase.WriteFile_Committed;
                        state.Save();

                        // TCS: 어셈블리에서 에러 발견 시에만 resolve
                        // 컴파일 성공 → 도메인 리로드 → continuation 소멸 → bootstrap이 재개
                        var compileTcs = new TaskCompletionSource<bool>();
                        void onCompile(string path, CompilerMessage[] msgs)
                        {
                            if (msgs.Any(m => m.type == CompilerMessageType.Error))
                            {
                                CompilationPipeline.assemblyCompilationFinished -= onCompile;
                                compileTcs.TrySetResult(true);
                            }
                            // 에러 없으면 resolve 안 함 → 도메인 리로드가 continuation을 소멸시킴
                        }
                        CompilationPipeline.assemblyCompilationFinished += onCompile;

                        File.WriteAllText(state.targetScriptPath, newCode);
                        AssetDatabase.Refresh();

                        // 에러 발생 시에만 아래로 도달 (성공 시 도메인 리로드로 소멸)
                        await compileTcs.Task;
                        CompilationPipeline.assemblyCompilationFinished -= onCompile;

                        Log($"[Gen {state.generation}] ⚠️ 컴파일 에러! 즉시 롤백합니다.");
                        File.WriteAllText(state.targetScriptPath, state.bestCode);
                        AssetDatabase.Refresh();
                        await Task.Delay(3000);

                        researchHistory.Add(new GenData { generation = state.generation, fps = 0, strategy = "FAILED: Compile Error", isAccepted = false, code = "REJECTED_COMPILE_ERROR" });
                        state.history.Add(new AlchemistFsmState.GenDataDto { generation = state.generation, fps = 0, strategy = "FAILED: Compile Error", isAccepted = false });
                        state.generation++;
                        state.phase = state.generation > state.maxGenerations ? AlchemistFsmState.Phase.Done : AlchemistFsmState.Phase.Hypothesis;
                        state.Save();
                        await SyncToWeb();
                        break;
                    }

                    case AlchemistFsmState.Phase.WriteFile_Committed:
                        await Task.Delay(500);
                        break;

                    case AlchemistFsmState.Phase.Benchmark:
                    {
                        Log($"[Gen {state.generation}] 성능 검증 중 (Benchmark)...");
                        float testFPS = await RunBenchmark();
                        state.currentTestFPS = testFPS;
                        state.phase = AlchemistFsmState.Phase.Decide;
                        state.Save();
                        break;
                    }

                    case AlchemistFsmState.Phase.Decide:
                    {
                        float testFPS = state.currentTestFPS;
                        bool accepted = testFPS > state.bestFPS + 0.5f;

                        if (accepted)
                        {
                            Log($"[Gen {state.generation}] ✨ 가설 채택! 성능 향상: {state.bestFPS:F1} → {testFPS:F1} FPS");
                            state.bestFPS = testFPS;
                            bestFPS = testFPS;
                            state.bestCode = state.pendingCode;
                            bestCode = state.pendingCode;
                        }
                        else
                        {
                            Log($"[Gen {state.generation}] ❌ 가설 기각. 성능: {testFPS:F1} FPS. 롤백합니다.");
                        }

                        researchHistory.Add(new GenData { generation = state.generation, fps = testFPS, strategy = state.pendingStrategy, isAccepted = accepted, code = accepted ? state.pendingCode : "Rejected." });
                        state.history.Add(new AlchemistFsmState.GenDataDto { generation = state.generation, fps = testFPS, strategy = state.pendingStrategy, isAccepted = accepted });

                        state.generation++;
                        state.phase = state.generation > state.maxGenerations ? AlchemistFsmState.Phase.Done : AlchemistFsmState.Phase.Hypothesis;
                        state.Save();

                        if (!accepted)
                        {
                            // 도메인 리로드 전 phase 저장 완료 → bootstrap이 올바른 phase로 재개
                            File.WriteAllText(state.targetScriptPath, state.bestCode);
                            AssetDatabase.Refresh();
                            await Task.Delay(3000);
                        }
                        await SyncToWeb();
                        break;
                    }
                }

                Repaint();
            }

            isRunning = false;
            AlchemistFsmState.Delete();
            await SyncToWeb();
            Log(state.phase == AlchemistFsmState.Phase.Done ? "🏁 Research Loop Finished." : "🛑 Research Loop Stopped.");
            Repaint();
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

            string json = JsonConvert.SerializeObject(payload);
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
            Log("⏯️ Running Benchmark (ProfilerRecorder, Watchdog Enabled)...");
            EditorApplication.isPlaying = true;

            float startTime = (float)EditorApplication.timeSinceStartup;
            float timeout = 15f;

            await Task.Delay(2000); // 씬 초기화 대기

            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            int frames = 60;
            for (int i = 0; i < frames; i++)
            {
                if ((float)EditorApplication.timeSinceStartup - startTime > timeout)
                {
                    Debug.LogError("[Alchemist] Benchmark Timeout! Rolling back.");
                    recorder.Dispose();
                    EditorApplication.isPlaying = false;
                    return 0.1f;
                }
                await Task.Yield();
            }

            int sampleCount = recorder.Count;
            float avgFrameMs;
            if (sampleCount > 0)
            {
                double totalNs = 0;
                for (int i = 0; i < sampleCount; i++)
                    totalNs += recorder.GetSample(i).Value;
                avgFrameMs = (float)(totalNs / sampleCount * 1e-6);
            }
            else
            {
                avgFrameMs = 16.67f;
            }
            recorder.Dispose();
            EditorApplication.isPlaying = false;

            float fps = avgFrameMs > 0 ? 1000f / avgFrameMs : 0.1f;
            SaveBenchmarkResults(fps, avgFrameMs);
            return fps;
        }

        private void SaveBenchmarkResults(float fps, float frameMs)
        {
            Directory.CreateDirectory("Artifacts");
            var result = new
            {
                timestamp = System.DateTime.UtcNow.ToString("o"),
                fps = fps,
                frameTimeMs = frameMs
            };
            File.WriteAllText(
                Path.Combine("Artifacts", "benchmark_results.json"),
                JsonConvert.SerializeObject(result, Formatting.Indented)
            );
        }

        private async Task<(string strategy, string code)> RequestHypothesis(string currentCode, float currentFPS)
        {
            // Two-part format: simple JSON for strategy, csharp block for code
            // Small models (llama3.2:1b) cannot reliably embed code inside JSON strings
            string prompt =
                $"Unity C# Performance Optimization\n" +
                $"Goal: {optimizationGoal}\nCurrent FPS: {currentFPS:F1}\n\n" +
                $"Propose ONE optimization for the code below.\n" +
                $"Reply in EXACTLY this format:\n\n" +
                $"STRATEGY:\n{{\"strategy\": \"one sentence description\"}}\n\n" +
                $"CODE:\n```csharp\n// complete optimized C# file here\n```\n\n" +
                $"Code to optimize:\n{currentCode}";

            string response = "";
            if (selectedProvider == LLMProvider.Gemini)
                response = await PostToAI($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}", prompt, true);
            else
                response = await PostToAI(localEndpoint, prompt, false);

            try
            {
                string strategy = "Optimization Step";
                string code = currentCode;

                // Extract strategy — try JSON first, fall back to first prose line
                int strategyIdx = response.IndexOf("STRATEGY:");
                if (strategyIdx >= 0)
                {
                    string afterStrategy = response.Substring(strategyIdx + 9).Trim();
                    int codeMarker = afterStrategy.IndexOf("CODE:");
                    string strategySection = codeMarker > 0 ? afterStrategy.Substring(0, codeMarker) : afterStrategy;
                    int bs = strategySection.IndexOf('{');
                    int be = strategySection.IndexOf('}');
                    if (bs >= 0 && be > bs)
                    {
                        try
                        {
                            var obj = JObject.Parse(strategySection.Substring(bs, be - bs + 1));
                            strategy = obj["strategy"]?.ToString() ?? strategy;
                        }
                        catch { /* fall through to plain text */ }
                    }
                    // Plain text fallback: first non-empty line of strategy section
                    if (strategy == "Optimization Step")
                    {
                        foreach (string line in strategySection.Split('\n'))
                        {
                            string trimmed = line.Trim().TrimStart('{', '"').TrimEnd('}', '"');
                            if (trimmed.Length > 10 && !trimmed.StartsWith("//"))
                            {
                                strategy = trimmed.Length > 120 ? trimmed.Substring(0, 120) : trimmed;
                                break;
                            }
                        }
                    }
                }

                // Extract code from ```csharp or ``` block
                if (response.Contains("```csharp"))
                {
                    int start = response.IndexOf("```csharp") + 9;
                    int end = response.IndexOf("```", start);
                    if (end > start) code = response.Substring(start, end - start).Trim();
                }
                else if (response.Contains("CODE:"))
                {
                    int codeIdx = response.IndexOf("CODE:") + 5;
                    string afterCode = response.Substring(codeIdx).Trim();
                    if (afterCode.Contains("```"))
                    {
                        int start = afterCode.IndexOf("```") + 3;
                        int end = afterCode.IndexOf("```", start);
                        if (end > start) code = afterCode.Substring(start, end - start).Trim();
                    }
                }

                return (strategy, string.IsNullOrEmpty(code) ? currentCode : code);
            }
            catch
            {
                return ("AI Proposal Failed", currentCode);
            }
        }

        private async Task<string> PostToAI(string url, string prompt, bool isGemini)
        {
            string payload;
            if (isGemini)
            {
                var geminiBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                payload = JsonConvert.SerializeObject(geminiBody);
            }
            else
            {
                var ollamaBody = new { model = localModel, messages = new[] { new { role = "user", content = prompt } }, stream = false };
                payload = JsonConvert.SerializeObject(ollamaBody);
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
                    var root = JObject.Parse(request.downloadHandler.text);
                    if (isGemini)
                        return root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "";
                    else
                        return root["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                }
                return "";
            }
        }

        private void Log(string msg) { Debug.Log("[Alchemist] " + msg); }
    }
}
