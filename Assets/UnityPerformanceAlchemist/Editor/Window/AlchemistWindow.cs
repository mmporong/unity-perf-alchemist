using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityPerformanceAlchemist.Core.Experiments;

namespace UnityPerformanceAlchemist.Editor
{
    // 헤드라인 설정-레벨 autoresearch UI (계약 §5 Window/).
    // 파일 재작성(fenced) 모드는 접힌 Experimental 섹션 뒤로 격리 — 기본 경로는 설정 토글만 다룬다.
    public sealed class AlchemistWindow : EditorWindow
    {
        private enum LlmProviderKind { Ollama_Local, Gemini }

        private LlmProviderKind selectedProvider = LlmProviderKind.Ollama_Local;
        private string apiKey = "";
        private string localEndpoint = "http://localhost:11434/v1/chat/completions";
        private string localModel = "llama3.2:1b";

        private int tierIndex = 1;  // 기본 "mid"
        private int maxGenerations = 5;
        private int runsPerCondition = 10;
        private DoeMode doeMode = DoeMode.Ofat;

        private bool isRunning;
        private CancellationTokenSource cts;

        private bool experimentalFoldout;          // 기본 접힘
        private bool fileRewriteAcknowledged;

        private readonly List<ResearchProgress> history = new List<ResearchProgress>();
        private readonly ResearchHistoryView historyView = new ResearchHistoryView();
        private readonly PerformanceGraphView graphView = new PerformanceGraphView();

        [MenuItem("Window/Alchemist/Performance Researcher")]
        public static void ShowWindow()
        {
            var window = GetWindow<AlchemistWindow>("Alchemist Researcher 🧪");
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
            graphView.Draw(history);
            EditorGUILayout.EndHorizontal();

            historyView.Draw(history);
            DrawExperimentalSection();

            if (isRunning)
            {
                Repaint();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("⚛️ Settings AutoResearch Engine", headerStyle);

            double baselineMs = 0.0;
            double bestMs = double.MaxValue;
            if (history.Count > 0)
            {
                baselineMs = history[0].MedianMs;
                foreach (var entry in history)
                {
                    if (entry.Outcome == "Adopt" && entry.MedianMs < bestMs)
                    {
                        bestMs = entry.MedianMs;
                    }
                }
            }

            if (baselineMs > 0.0 && bestMs < double.MaxValue)
            {
                double improvement = (baselineMs - bestMs) / baselineMs * 100.0;
                string color = improvement > 0.0 ? "green" : "white";
                var subHeaderStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Italic,
                    richText = true,
                };
                GUILayout.Label(
                    $"Median Frame Time: <color={color}>−{improvement:F1}%</color> (Base: {baselineMs:F2} ms → Best: {bestMs:F2} ms)",
                    subHeaderStyle);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            GUILayout.Label("🛠️ Configuration", EditorStyles.boldLabel);

            var tiers = DeviceTierProfile.Defaults;
            var tierLabels = new string[tiers.Count];
            for (int i = 0; i < tiers.Count; i++)
            {
                tierLabels[i] = tiers[i].DisplayName;
            }
            tierIndex = EditorGUILayout.Popup("Device Tier", tierIndex, tierLabels);

            selectedProvider = (LlmProviderKind)EditorGUILayout.EnumPopup("LLM Provider", selectedProvider);

            if (selectedProvider == LlmProviderKind.Gemini)
            {
                apiKey = EditorGUILayout.PasswordField("Gemini API Key", apiKey);
                if (GUI.changed)
                {
                    EditorPrefs.SetString("Alchemist_API_Key", apiKey);
                }
            }
            else
            {
                localEndpoint = EditorGUILayout.TextField("Ollama Endpoint", localEndpoint);
                localModel = EditorGUILayout.TextField("Model Name", localModel);
                if (GUI.changed)
                {
                    EditorPrefs.SetString("Alchemist_Local_Endpoint", localEndpoint);
                }
            }

            maxGenerations = Mathf.Max(1, EditorGUILayout.IntField("Max Generations", maxGenerations));
            runsPerCondition = Mathf.Max(1, EditorGUILayout.IntField("Runs Per Condition (N)", runsPerCondition));
            doeMode = (DoeMode)EditorGUILayout.EnumPopup("DOE Mode", doeMode);

            if (isRunning && history.Count > 0)
            {
                EditorGUILayout.Space(4);
                int lastGeneration = history[history.Count - 1].Generation;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 18),
                    Mathf.Clamp01((float)lastGeneration / maxGenerations),
                    $"Gen {lastGeneration} / {maxGenerations}");
            }

            EditorGUILayout.Space();

            if (isRunning)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("🛑 CANCEL", GUILayout.Height(40)))
                {
                    cts?.Cancel();
                }
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("🚀 START SETTINGS RESEARCH", GUILayout.Height(40)))
                {
                    StartSettingsResearch();
                }
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button($"🧪 RUN DOE EXPERIMENT ({doeMode})", GUILayout.Height(28)))
                {
                    StartDoeExperiment();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawExperimentalSection()
        {
            experimentalFoldout = EditorGUILayout.Foldout(experimentalFoldout, "⚠ Experimental: File Rewrite Mode", true);
            if (!experimentalFoldout)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "이 모드는 프로젝트의 .cs 파일을 LLM이 직접 재작성합니다. 컴파일 실패·도메인 리로드·코드 유실 위험이 있으므로 " +
                "반드시 VCS에 커밋된 깨끗한 작업 트리에서만 사용하세요. 헤드라인 설정-레벨 루프와 달리 실험적(fenced) 기능입니다.",
                MessageType.Warning);
            fileRewriteAcknowledged = EditorGUILayout.ToggleLeft(
                "위 위험을 이해했으며 파일 재작성 모드를 실행합니다", fileRewriteAcknowledged);

            using (new EditorGUI.DisabledScope(!fileRewriteAcknowledged || isRunning))
            {
                if (GUILayout.Button("START FILE REWRITE RESEARCH (Experimental)"))
                {
                    StartFileRewriteResearch();
                }
            }
        }

        private async void StartFileRewriteResearch()
        {
            if (isRunning)
            {
                return;
            }

            history.Clear();
            isRunning = true;
            cts = new CancellationTokenSource();
            var progress = new System.Progress<ResearchProgress>(OnProgress);

            try
            {
                var runner = new FileRewriteResearchRunner(
                    CreateProvider(),
                    new BenchmarkOptions { RunsPerCondition = runsPerCondition });
                // optIn=true는 UI 체크박스(fileRewriteAcknowledged) 통과가 곧 명시적 동의.
                await runner.RunAsync("Assets", "Maximize FPS on Mobile Hardware", maxGenerations,
                                      fileRewriteAcknowledged, progress, cts.Token);
                Debug.Log("[Alchemist] File rewrite research 종료 (Experimental).");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Alchemist] File rewrite research 취소됨.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] File rewrite research 실패: {e}");
            }
            finally
            {
                FinishRun();
            }
        }

        private async void StartSettingsResearch()
        {
            if (isRunning)
            {
                return;
            }

            history.Clear();
            isRunning = true;
            cts = new CancellationTokenSource();
            var progress = new System.Progress<ResearchProgress>(OnProgress);

            try
            {
                var tier = DeviceTierProfile.Defaults[tierIndex];
                var config = new ResearchRunConfig
                {
                    TierId = tier.TierId,
                    MaxGenerations = maxGenerations,
                    Benchmark = new BenchmarkOptions { RunsPerCondition = runsPerCondition },
                    GameBuildVersion = PlayerSettings.bundleVersion,
                    PatchId = "",
                    Provider = CreateProvider(),
                    BaselineSettings = tier.QualityProxy,
                };
                var runner = new SettingsResearchRunner(config);
                string artifactPath = await runner.RunAsync(progress, cts.Token);
                Debug.Log($"[Alchemist] Settings research 완료 — Artifacts: {artifactPath}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Alchemist] Settings research 취소됨.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] Settings research 실패: {e}");
            }
            finally
            {
                FinishRun();
            }
        }

        private async void StartDoeExperiment()
        {
            if (isRunning)
            {
                return;
            }

            history.Clear();
            isRunning = true;
            cts = new CancellationTokenSource();
            var progress = new System.Progress<ResearchProgress>(OnProgress);

            try
            {
                var tier = DeviceTierProfile.Defaults[tierIndex];
                IReadOnlyList<OptimizationTechnique> techniques = TechniqueRegistry.All;
                if (doeMode == DoeMode.Factorial && techniques.Count > 8)
                {
                    // ponytail: Factorial 2^k 가드(k ≤ 8) — 레지스트리 9종 중 앞 8종만 사용.
                    // 기법 다중 선택 UI가 업그레이드 경로.
                    techniques = techniques.Take(8).ToList();
                }

                var config = new DoeExperimentConfig
                {
                    TierId = tier.TierId,
                    Mode = doeMode,
                    Techniques = techniques,
                    Benchmark = new BenchmarkOptions { RunsPerCondition = runsPerCondition },
                    GameBuildVersion = PlayerSettings.bundleVersion,
                    PatchId = "",
                };
                var runner = new ToggleExperimentRunner(config);
                string artifactPath = await runner.RunAsync(progress, cts.Token);
                Debug.Log($"[Alchemist] DOE experiment 완료 — Artifacts: {artifactPath}");
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Alchemist] DOE experiment 취소됨.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] DOE experiment 실패: {e}");
            }
            finally
            {
                FinishRun();
            }
        }

        private ILlmProvider CreateProvider()
        {
            if (selectedProvider == LlmProviderKind.Gemini)
            {
                // apiKey는 GeminiProvider가 EditorPrefs("Alchemist_API_Key")에서 로드 (계약 §5).
                return new GeminiProvider();
            }
            return new OllamaProvider { Endpoint = localEndpoint, Model = localModel };
        }

        private void OnProgress(ResearchProgress entry)
        {
            history.Add(entry);
            Repaint();
        }

        private void FinishRun()
        {
            isRunning = false;
            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }
            Repaint();
        }
    }
}
