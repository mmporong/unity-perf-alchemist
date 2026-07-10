using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityPerformanceAlchemist.Core.Loop;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// fenced 파일 재작성 연구 루프 — 기존 PerformanceAlchemist.cs의 FSM
    /// (ScanProject→Baseline→Hypothesis→WriteFile_Committed→Benchmark→Decide, TCS 컴파일 감시, 롤백) 이식.
    /// 기본 off: RunAsync(optIn: true) 명시로만 진입. AI가 소스 파일을 직접 재작성하고
    /// 컴파일/도메인 리로드를 유발하므로 헤드라인 설정-레벨 루프와 완전히 격리한다.
    /// </summary>
    public sealed class FileRewriteResearchRunner
    {
        public const string WarningLabel = "EXPERIMENTAL: AI가 소스 파일을 재작성합니다";

        private readonly ILlmProvider provider;
        private readonly BenchmarkOptions benchmark;

        public bool IsRunning { get; private set; }

        public FileRewriteResearchRunner(ILlmProvider provider, BenchmarkOptions benchmark)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            this.provider = provider;
            this.benchmark = benchmark ?? new BenchmarkOptions();
        }

        /// <summary>기본 off — optIn=true를 명시해야만 실행된다.</summary>
        public async Task RunAsync(string scanRoot, string optimizationGoal, int maxGenerations,
                                   bool optIn, IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            if (!optIn)
            {
                throw new InvalidOperationException($"{WarningLabel} — optIn=true 명시 없이는 실행할 수 없습니다.");
            }
            if (IsRunning)
            {
                throw new InvalidOperationException("[Alchemist] fenced 루프가 이미 실행 중입니다.");
            }

            Debug.LogWarning("[Alchemist] " + WarningLabel);
            var state = new FsmStatePersistence
            {
                phase = FsmStatePersistence.Phase.ScanProject,
                generation = 1,
                maxGenerations = maxGenerations,
                scanRoot = string.IsNullOrEmpty(scanRoot) ? "Assets" : scanRoot,
                optimizationGoal = optimizationGoal ?? "",
            };
            FillProviderInfo(state);
            state.runsPerCondition = benchmark.RunsPerCondition;
            state.framesPerRun = benchmark.FramesPerRun;
            state.warmupFrames = benchmark.WarmupFrames;
            state.warmupSeconds = benchmark.WarmupSeconds;
            state.Save();

            IsRunning = true;
            try
            {
                await RunFsmAsync(state, progress, ct);
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>도메인 리로드 후 RecoveryBootstrap 전용 재진입점 (상태 파일 존재 = 과거 opt-in 증거).</summary>
        public async Task ResumeAsync(FsmStatePersistence state, IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (IsRunning)
            {
                throw new InvalidOperationException("[Alchemist] fenced 루프가 이미 실행 중입니다.");
            }

            IsRunning = true;
            try
            {
                await RunFsmAsync(state, progress, ct);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void FillProviderInfo(FsmStatePersistence state)
        {
            // 재개(RecoveryBootstrap) 시 동일 프로바이더를 복원할 수 있도록 상태에 기록
            if (provider is OllamaProvider ollama)
            {
                state.llmProvider = "Ollama";
                state.localEndpoint = ollama.Endpoint;
                state.localModel = ollama.Model;
            }
            else if (provider is GeminiProvider gemini)
            {
                state.llmProvider = "Gemini";
                state.localModel = gemini.Model;   // Gemini 모델명도 localModel 슬롯에 실어 나른다
            }
        }

        private async Task RunFsmAsync(FsmStatePersistence state, IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            if (benchmark.RunsPerCondition < AdoptionGate.MinSamplesPerGroup)
            {
                // 정직한 한계 표시: 조건당 런 수가 게이트 최소 표본 미만이면 판정이 전부 Inconclusive(=기각)로 나온다.
                Debug.LogWarning($"[Alchemist] RunsPerCondition={benchmark.RunsPerCondition}은 게이트 최소 표본" +
                                 $"({AdoptionGate.MinSamplesPerGroup}) 미만 — 모든 판정이 Inconclusive로 기각됩니다. 런 수를 늘리세요.");
            }

            try
            {
                while (state.phase != FsmStatePersistence.Phase.Done)
                {
                    ct.ThrowIfCancellationRequested();

                    switch (state.phase)
                    {
                        case FsmStatePersistence.Phase.Idle:
                            state.phase = FsmStatePersistence.Phase.ScanProject;
                            break;

                        case FsmStatePersistence.Phase.ScanProject:
                            StepScanProject(state);
                            break;

                        case FsmStatePersistence.Phase.Baseline:
                            await StepBaselineAsync(state, progress);
                            break;

                        case FsmStatePersistence.Phase.Hypothesis:
                            await StepHypothesisAsync(state, progress, ct);
                            break;

                        case FsmStatePersistence.Phase.WriteFile_Committed:
                            // 컴파일 진행 대기 — 성공 시 도메인 리로드가 이 루프를 소멸시키고 RecoveryBootstrap이 재개
                            await Task.Delay(500, ct);
                            break;

                        case FsmStatePersistence.Phase.Benchmark:
                            await StepBenchmarkAsync(state);
                            break;

                        case FsmStatePersistence.Phase.Decide:
                            await StepDecideAsync(state, progress);
                            break;
                    }
                }

                FsmStatePersistence.Delete();
                Report(progress, state.generation, "done", 0.0, "Done", "fenced 연구 루프 종료");
            }
            catch (OperationCanceledException)
            {
                // 중단: 디스크에 pendingCode가 남아 있을 수 있으므로 bestCode로 복구 후 상태 정리 (오재개 방지)
                TryRollbackFile(state);
                FsmStatePersistence.Delete();
                Debug.Log("[Alchemist] fenced 연구 루프가 중단되었습니다. 파일과 상태를 정리했습니다.");
            }
        }

        private static void StepScanProject(FsmStatePersistence state)
        {
            Debug.Log($"[Alchemist] 프로젝트 스캔 중: {state.scanRoot}");
            List<string> scripts = ScanAndScore(state.scanRoot);
            if (scripts.Count == 0)
            {
                Debug.LogWarning("[Alchemist] 최적화 대상 스크립트를 찾을 수 없습니다.");
                state.phase = FsmStatePersistence.Phase.Done;
                return;
            }

            state.pendingScripts = scripts;
            state.currentScriptIndex = 0;
            state.targetScriptPath = scripts[0];
            state.bestCode = File.ReadAllText(scripts[0]);
            state.targetScriptSha256 = FsmStatePersistence.ComputeSha256(state.bestCode);
            Debug.Log($"[Alchemist] {scripts.Count}개 스크립트 발견. 첫 대상: {Path.GetFileName(scripts[0])}");
            state.phase = FsmStatePersistence.Phase.Baseline;
            state.Save();
        }

        private async Task StepBaselineAsync(FsmStatePersistence state, IProgress<ResearchProgress> progress)
        {
            Debug.Log($"[Alchemist] [{Path.GetFileName(state.targetScriptPath)}] 기준 성능 측정 중 ({benchmark.RunsPerCondition}런)...");
            SampleSet baseline = await EditorBenchmarkRunner.MeasureAsync(benchmark, "fenced-baseline");
            if (baseline.Count == 0)
            {
                // 베이스라인 전량 폐기(워치독 등) — 상태 파일을 남기면 다음 도메인 리로드에서 오재개되므로 안전 종료
                Debug.LogWarning("[Alchemist] 베이스라인 런 전량 폐기 — fenced 루프를 안전 종료합니다.");
                state.phase = FsmStatePersistence.Phase.Done;
                state.Save();
                Report(progress, 0, "baseline", 0.0, "Discarded", "베이스라인 표본 없음 — 종료");
                return;
            }
            state.bestRunMediansMs = baseline.RunMediansMs.ToList();

            double median = DescriptiveStats.Compute(baseline.RunMediansMs).Median;
            state.history.Add(new FsmStatePersistence.GenDataDto
            {
                generation = 0,
                medianMs = median,
                strategy = $"Baseline: {Path.GetFileName(state.targetScriptPath)}",
                isAccepted = true,
                scriptPath = state.targetScriptPath,
            });
            state.phase = FsmStatePersistence.Phase.Hypothesis;
            state.Save();
            Report(progress, 0, "baseline", median, "Baseline", $"기준선 {median:F2}ms");
        }

        private async Task StepHypothesisAsync(FsmStatePersistence state, IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            if (state.generation > state.maxGenerations)
            {
                state.phase = FsmStatePersistence.Phase.Done;
                return;
            }

            Debug.Log($"[Alchemist] [Gen {state.generation}] AI 가설 수립 중...");
            (string strategy, string newCode) = await RequestHypothesisAsync(state, ct);

            if (string.IsNullOrEmpty(newCode) || newCode == state.bestCode)
            {
                state.generation++;
                state.Save();
                return;
            }

            state.pendingStrategy = strategy;
            state.pendingCode = newCode;
            state.phase = FsmStatePersistence.Phase.WriteFile_Committed;
            state.targetScriptSha256 = FsmStatePersistence.ComputeSha256(newCode);
            state.Save();

            // TCS: 어셈블리에서 컴파일 에러 발견 시에만 resolve.
            // 컴파일 성공 → 도메인 리로드 → 이 continuation 소멸 → RecoveryBootstrap이 Benchmark로 재개.
            var compileTcs = new TaskCompletionSource<bool>();
            void OnCompile(string assemblyPath, CompilerMessage[] messages)
            {
                if (messages.Any(m => m.type == CompilerMessageType.Error))
                {
                    CompilationPipeline.assemblyCompilationFinished -= OnCompile;
                    compileTcs.TrySetResult(true);
                }
                // 에러 없으면 resolve 안 함 → 도메인 리로드가 continuation을 소멸시킴
            }
            CompilationPipeline.assemblyCompilationFinished += OnCompile;

            File.WriteAllText(state.targetScriptPath, newCode);
            AssetDatabase.Refresh();

            // 컴파일 에러 발생 시에만 아래로 도달 (성공 시 도메인 리로드로 소멸)
            await compileTcs.Task;
            CompilationPipeline.assemblyCompilationFinished -= OnCompile;

            Debug.LogWarning($"[Alchemist] [Gen {state.generation}] 컴파일 에러 — 즉시 롤백합니다.");
            state.history.Add(new FsmStatePersistence.GenDataDto
            {
                generation = state.generation,
                medianMs = 0.0,
                strategy = "FAILED: Compile Error",
                isAccepted = false,
                scriptPath = state.targetScriptPath,
            });
            state.generation++;
            state.phase = state.generation > state.maxGenerations
                ? FsmStatePersistence.Phase.Done
                : FsmStatePersistence.Phase.Hypothesis;
            // 롤백 쓰기가 리로드를 유발할 수 있으므로 phase/해시를 먼저 저장 → 재개 시 올바른 phase
            state.targetScriptSha256 = FsmStatePersistence.ComputeSha256(state.bestCode);
            state.Save();

            File.WriteAllText(state.targetScriptPath, state.bestCode);
            AssetDatabase.Refresh();
            await Task.Delay(3000, ct);
            Report(progress, state.generation - 1, "FAILED: Compile Error", 0.0, "Rollback", "컴파일 에러로 기각");
        }

        private async Task StepBenchmarkAsync(FsmStatePersistence state)
        {
            Debug.Log($"[Alchemist] [Gen {state.generation}] 성능 검증 중 ({benchmark.RunsPerCondition}런)...");
            SampleSet candidate = await EditorBenchmarkRunner.MeasureAsync(benchmark, $"fenced-gen{state.generation}");
            state.candidateRunMediansMs = candidate.RunMediansMs.ToList();
            state.phase = FsmStatePersistence.Phase.Decide;
            state.Save();
        }

        private async Task StepDecideAsync(FsmStatePersistence state, IProgress<ResearchProgress> progress)
        {
            var bestSet = new SampleSet(state.bestRunMediansMs, "fenced-best");
            var candidateSet = new SampleSet(state.candidateRunMediansMs, "fenced-candidate");
            // 기존 +0.5f FPS 휴리스틱 대신 AdoptionGate — Inconclusive는 채택하지 않는다.
            AdoptionDecision decision = AdoptionGate.Evaluate(bestSet, candidateSet);
            bool accepted = decision.Outcome == AdoptionOutcome.Adopt;

            if (decision.Outcome == AdoptionOutcome.Inconclusive)
            {
                Debug.LogWarning($"[Alchemist] [Gen {state.generation}] 게이트 Inconclusive — 채택하지 않음: {decision.Reason}");
            }

            if (accepted)
            {
                Debug.Log($"[Alchemist] [Gen {state.generation}] 가설 채택: {decision.Reason}");
                state.bestCode = state.pendingCode;
                state.bestRunMediansMs = new List<double>(state.candidateRunMediansMs);
            }
            else
            {
                Debug.Log($"[Alchemist] [Gen {state.generation}] 가설 기각({decision.Outcome}): {decision.Reason} — 롤백합니다.");
            }

            state.history.Add(new FsmStatePersistence.GenDataDto
            {
                generation = state.generation,
                medianMs = decision.CandidateMedianMs,
                strategy = state.pendingStrategy,
                isAccepted = accepted,
                scriptPath = state.targetScriptPath,
            });
            Report(progress, state.generation, state.pendingStrategy, decision.CandidateMedianMs,
                   decision.Outcome.ToString(), decision.Reason);

            // 롤백 대상은 '이번 세대의' 파일/코드 — 다음 스크립트로 넘어가기 전에 캡처
            // (기존 코드는 스크립트 전환 시 이전 파일 롤백이 누락되는 버그가 있었음)
            string rollbackPath = state.targetScriptPath;
            string rollbackCode = state.bestCode;

            state.generation++;
            if (state.generation > state.maxGenerations)
            {
                state.currentScriptIndex++;
                if (state.currentScriptIndex < state.pendingScripts.Count)
                {
                    string nextScript = state.pendingScripts[state.currentScriptIndex];
                    Debug.Log($"[Alchemist] 다음 스크립트 ({state.currentScriptIndex + 1}/{state.pendingScripts.Count}): {Path.GetFileName(nextScript)}");
                    state.targetScriptPath = nextScript;
                    state.bestCode = File.ReadAllText(nextScript);
                    state.generation = 1;
                    state.bestRunMediansMs = new List<double>();
                    state.phase = FsmStatePersistence.Phase.Baseline;
                }
                else
                {
                    state.phase = FsmStatePersistence.Phase.Done;
                }
            }
            else
            {
                state.phase = FsmStatePersistence.Phase.Hypothesis;
            }
            state.targetScriptSha256 = FsmStatePersistence.ComputeSha256(state.bestCode);
            state.Save();

            if (!accepted)
            {
                // 도메인 리로드 전 phase/해시 저장 완료 → RecoveryBootstrap이 올바른 phase로 재개
                File.WriteAllText(rollbackPath, rollbackCode);
                AssetDatabase.Refresh();
                await Task.Delay(3000);
            }
        }

        private async Task<(string strategy, string code)> RequestHypothesisAsync(FsmStatePersistence state, CancellationToken ct)
        {
            string currentCode = state.bestCode;
            double bestMedian = state.bestRunMediansMs.Count > 0
                ? DescriptiveStats.Compute(state.bestRunMediansMs).Median
                : 0.0;

            // Two-part format: 소형 모델(llama3.2:1b)은 JSON 문자열 안에 코드를 안정적으로 넣지 못함 (기존 로직 이식)
            string prompt =
                "Unity C# Performance Optimization\n" +
                $"Goal: {state.optimizationGoal}\nCurrent median frame time: {bestMedian:F2} ms (lower is better)\n\n" +
                "Propose ONE optimization for the code below.\n" +
                "Reply in EXACTLY this format:\n\n" +
                "STRATEGY:\n{\"strategy\": \"one sentence description\"}\n\n" +
                "CODE:\n```csharp\n// complete optimized C# file here\n```\n\n" +
                "Code to optimize:\n" +
                (currentCode.Length > 3000 ? currentCode.Substring(0, 3000) + "\n// ... (truncated)" : currentCode);

            string response = await provider.CompleteAsync(prompt, ct);
            if (string.IsNullOrEmpty(response))
            {
                return ("AI Proposal Failed", "");
            }

            string strategy = "Optimization Step";
            string code = "";

            // 전략 추출 — JSON 우선, 실패 시 첫 평문 줄 폴백 (기존 로직 이식)
            int strategyIdx = response.IndexOf("STRATEGY:", StringComparison.Ordinal);
            if (strategyIdx >= 0)
            {
                string afterStrategy = response.Substring(strategyIdx + 9).Trim();
                int codeMarker = afterStrategy.IndexOf("CODE:", StringComparison.Ordinal);
                string strategySection = codeMarker > 0 ? afterStrategy.Substring(0, codeMarker) : afterStrategy;
                int braceStart = strategySection.IndexOf('{');
                int braceEnd = strategySection.IndexOf('}');
                if (braceStart >= 0 && braceEnd > braceStart)
                {
                    try
                    {
                        JObject obj = JObject.Parse(strategySection.Substring(braceStart, braceEnd - braceStart + 1));
                        JToken token = obj["strategy"];
                        if (token != null)
                        {
                            strategy = token.ToString();
                        }
                    }
                    catch
                    {
                        // 평문 폴백으로 진행
                    }
                }
                if (strategy == "Optimization Step")
                {
                    foreach (string line in strategySection.Split('\n'))
                    {
                        string trimmed = line.Trim().TrimStart('{', '"').TrimEnd('}', '"');
                        if (trimmed.Length > 10 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                        {
                            strategy = trimmed.Length > 120 ? trimmed.Substring(0, 120) : trimmed;
                            break;
                        }
                    }
                }
            }

            // 코드 추출 — ```csharp 또는 CODE: 뒤 ``` 블록 (기존 로직 이식)
            if (response.Contains("```csharp"))
            {
                int start = response.IndexOf("```csharp", StringComparison.Ordinal) + 9;
                int end = response.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start)
                {
                    code = response.Substring(start, end - start).Trim();
                }
            }
            else if (response.Contains("CODE:"))
            {
                int codeIdx = response.IndexOf("CODE:", StringComparison.Ordinal) + 5;
                string afterCode = response.Substring(codeIdx).Trim();
                int fenceStart = afterCode.IndexOf("```", StringComparison.Ordinal);
                if (fenceStart >= 0)
                {
                    int start = fenceStart + 3;
                    int end = afterCode.IndexOf("```", start, StringComparison.Ordinal);
                    if (end > start)
                    {
                        code = afterCode.Substring(start, end - start).Trim();
                    }
                }
            }

            return (strategy, code);
        }

        private static List<string> ScanAndScore(string root)
        {
            var scored = new List<(string path, int score)>();
            if (!Directory.Exists(root))
            {
                return new List<string>();
            }

            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (ScriptScorer.IsExcludedPath(normalized))
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                int score = ScriptScorer.Score(content);
                if (score > 0)
                {
                    scored.Add((path, score));
                }
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));
            return scored.Select(s => s.path).ToList();
        }

        private static void TryRollbackFile(FsmStatePersistence state)
        {
            if (string.IsNullOrEmpty(state.targetScriptPath) || string.IsNullOrEmpty(state.bestCode))
            {
                return;
            }
            if (!File.Exists(state.targetScriptPath))
            {
                return;
            }
            try
            {
                if (File.ReadAllText(state.targetScriptPath) != state.bestCode)
                {
                    File.WriteAllText(state.targetScriptPath, state.bestCode);
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Alchemist] 중단 롤백 실패: {e.Message}");
            }
        }

        private static void Report(IProgress<ResearchProgress> progress, int generation, string strategy,
                                   double medianMs, string outcome, string message)
        {
            progress?.Report(new ResearchProgress
            {
                Generation = generation,
                Strategy = strategy,
                MedianMs = medianMs,
                Outcome = outcome,
                Message = message,
            });
        }
    }
}
