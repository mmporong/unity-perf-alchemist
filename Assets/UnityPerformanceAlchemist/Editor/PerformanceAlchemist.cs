using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace UnityPerformanceAlchemist.Editor
{
    public class PerformanceAlchemistWindow : EditorWindow
    {
        private string apiKey = "";
        private MonoScript targetScript;
        private string optimizationGoal = "Maximize FPS on Mobile Hardware";
        private bool isRunning = false;
        private string logOutput = "Ready to start research loop...";
        
        private float bestFrameTime = float.MaxValue;
        private string bestCode = "";

        [MenuItem("Window/Alchemist/Performance Researcher")]
        public static void ShowWindow()
        {
            GetWindow<PerformanceAlchemistWindow>("Alchemist 🧪");
        }

        private void OnEnable()
        {
            apiKey = EditorPrefs.GetString("Alchemist_API_Key", "");
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity Performance Alchemist", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            apiKey = EditorGUILayout.PasswordField("Gemini API Key", apiKey);
            if (GUI.changed) EditorPrefs.SetString("Alchemist_API_Key", apiKey);

            targetScript = (MonoScript)EditorGUILayout.ObjectField("Target C# Script", targetScript, typeof(MonoScript), false);
            optimizationGoal = EditorGUILayout.TextField("Optimization Goal", optimizationGoal);

            EditorGUILayout.Space();

            if (GUILayout.Button(isRunning ? "🛑 Stop Research" : "🚀 Start Autonomous Research", GUILayout.Height(40)))
            {
                if (isRunning) isRunning = false;
                else StartResearch();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Research Logs:", EditorStyles.miniLabel);
            EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
        }

        private async void StartResearch()
        {
            if (targetScript == null || string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[Alchemist] Target Script or API Key is missing!");
                return;
            }

            isRunning = true;
            bestCode = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
            bestFrameTime = float.MaxValue;

            Log("🔍 Research Loop Started...");

            for (int gen = 1; gen <= 10; gen++)
            {
                if (!isRunning) break;
                Log($"\n--- Generation {gen} ---");

                // 1. Benchmark current version
                float currentFPS = await RunBenchmark();
                float currentFrameTime = 1000f / currentFPS;
                Log($"📊 Current Perf: {currentFrameTime:F2}ms ({currentFPS:F1} FPS)");

                if (currentFrameTime < bestFrameTime)
                {
                    Log("✨ Improvement Found! Saving as baseline.");
                    bestFrameTime = currentFrameTime;
                    bestCode = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
                }
                else
                {
                    Log("📉 Regression. Rolling back to stable code.");
                    File.WriteAllText(AssetDatabase.GetAssetPath(targetScript), bestCode);
                    AssetDatabase.Refresh();
                    await Task.Delay(2000); // Wait for compilation
                }

                // 2. Consult AI for next hypothesis
                Log("🧠 Consulting Gemini for architectural refactor...");
                string newCode = await RequestGeminiRefactor(bestCode, bestFrameTime);
                
                if (!string.IsNullOrEmpty(newCode))
                {
                    File.WriteAllText(AssetDatabase.GetAssetPath(targetScript), newCode);
                    AssetDatabase.Refresh();
                    Log("📝 Applied AI-proposed refactor. Waiting for compilation...");
                    await Task.Delay(5000); // Wait for Unity to compile
                }
            }

            isRunning = false;
            Log("\n🏁 Research Loop Finished.");
        }

        private async Task<float> RunBenchmark()
        {
            Log("⏯️ Running Benchmark...");
            EditorApplication.isPlaying = true;
            await Task.Delay(2000); // Warm up

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

        private async Task<string> RequestGeminiRefactor(string code, float frameTime)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";
            
            var payload = new
            {
                contents = new[] {
                    new {
                        role = "user",
                        parts = new[] {
                            new { text = $"Unity Performance Task: {optimizationGoal}\nCurrent code runs at {frameTime:F2}ms frame time.\nRefactor this code for better performance (use Job System, Burst, or Pooling if applicable).\nONLY output raw C# code.\n\nCode:\n{code}" }
                        }
                    }
                }
            };

            string json = EditorJsonUtility.ToJson(payload);
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Basic parsing of Gemini response (In a production tool, use a proper JSON parser)
                    string responseText = request.downloadHandler.text;
                    int start = responseText.IndexOf("```csharp") + 9;
                    if (start < 9) start = responseText.IndexOf("```") + 3;
                    int end = responseText.LastIndexOf("```");
                    
                    if (start > 8 && end > start)
                    {
                        return responseText.Substring(start, end - start).Trim();
                    }
                    return responseText; // Fallback
                }
                else
                {
                    Debug.LogError($"[Alchemist] API Error: {request.error}");
                    return null;
                }
            }
        }

        private void Log(string msg)
        {
            logOutput += "\n" + msg;
            Debug.Log("[Alchemist] " + msg);
            Repaint();
        }
    }
}
