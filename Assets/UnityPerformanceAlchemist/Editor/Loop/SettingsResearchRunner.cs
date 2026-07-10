using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityPerformanceAlchemist.Core.Experiments;
using UnityPerformanceAlchemist.Core.Loop;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>헤드라인 설정 연구 루프 실행 설정 (EDITOR_PIN 시그니처).</summary>
    public sealed class ResearchRunConfig
    {
        public string TierId = "mid";
        public int MaxGenerations = 5;
        public BenchmarkOptions Benchmark = new BenchmarkOptions();
        public string GameBuildVersion = "";
        public string PatchId = "";
        public ILlmProvider Provider;
        public IReadOnlyDictionary<string, string> BaselineSettings;
    }

    /// <summary>러너 진행 상황 리포트 단위 (SettingsResearchRunner/ToggleExperimentRunner/FileRewriteResearchRunner 공용).</summary>
    public sealed class ResearchProgress
    {
        public int Generation;
        public string Strategy;
        public double MedianMs;
        public string Outcome;
        public string Message;
    }

    /// <summary>
    /// 헤드라인 in-process 설정 연구 루프 (계약 §5).
    /// 흐름: 스냅샷 → 티어 QualityProxy 적용 → baseline N런 → (가설→적용→N런→AdoptionGate)×maxGenerations
    ///       → fresh ABAB 확인 런 → ChampionConfirmation → ShippingGate → DerivedConfig → Artifacts JSON → 스냅샷 복원.
    /// 이 경로에서는 .cs 파일 쓰기/AssetDatabase.Refresh/도메인 리로드 금지 —
    /// FsmStatePersistence/RecoveryBootstrap 참조 0 (정적 수용 기준).
    /// </summary>
    public sealed class SettingsResearchRunner
    {
        private const string SchemaVersion = "0.2.0";
        private const string MeasurementSource = "editor-proxy";
        private const string ConfirmationNote =
            "루프 중 예산 내였던 챔피언이 확인 런에서 budgetSatisfied:false로 뒤집힐 수 있음 = 지속부하 스로틀 현실(버그 아님)";

        private readonly ResearchRunConfig config;

        public bool IsRunning { get; private set; }

        public SettingsResearchRunner(ResearchRunConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            this.config = config;
        }

        /// <summary>반환: Artifacts JSON 절대 경로. confirmationFailed/중단 시에도 기록은 남기고 경로를 반환한다.</summary>
        public async Task<string> RunAsync(IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("[Alchemist] 연구 루프가 이미 실행 중입니다.");
            }
            if (config.Provider == null)
            {
                throw new InvalidOperationException("[Alchemist] ResearchRunConfig.Provider가 설정되어야 합니다.");
            }

            IsRunning = true;
            DeviceTierProfile tier = FindTier(config.TierId);
            var snapshot = DeviceTierSimulator.Capture();
            try
            {
                return await RunCoreAsync(tier, progress, ct);
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
                if (!DeviceTierSimulator.VerifyRestored(snapshot))
                {
                    Debug.LogWarning("[Alchemist] 품질 설정 스냅샷 복원 검증 실패 — QualitySettings를 수동 확인하세요.");
                }
                IsRunning = false;
            }
        }

        private async Task<string> RunCoreAsync(DeviceTierProfile tier,
                                                IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            var baselineSettings = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in config.BaselineSettings ?? tier.QualityProxy)
            {
                baselineSettings[pair.Key] = pair.Value;
            }

            var steps = new List<StepRecord>();
            SampleSet baseline = null;
            SampleSet best = null;
            var bestSettings = new Dictionary<string, string>(baselineSettings);
            SampleSet freshBaseline = null;
            SampleSet freshChampion = null;
            ConfirmationResult confirmation = null;
            ShippingLabel shipping = null;
            bool confirmationFailed = false;
            string confirmationDetail = null;
            bool cancelled = false;

            try
            {
                DeviceTierSimulator.Apply(baselineSettings);
                baseline = await EditorBenchmarkRunner.MeasureAsync(config.Benchmark, "baseline", ct: ct);
                best = baseline;
                Report(progress, 0, "baseline", MedianOf(baseline), "", $"기준선 측정 완료 ({baseline.Count}런)");

                var knownKeys = new HashSet<string>(TechniqueRegistry.All.Select(t => t.SettingKey));

                for (int generation = 1; generation <= config.MaxGenerations; generation++)
                {
                    ct.ThrowIfCancellationRequested();

                    string prompt = BuildPrompt(bestSettings, steps);
                    string response = await config.Provider.CompleteAsync(prompt, ct);

                    if (!ToggleHypothesisParser.TryParse(response, out ToggleHypothesis hypothesis))
                    {
                        steps.Add(StepRecord.Skipped(generation, "(hypothesis parse failed)"));
                        Report(progress, generation, "(hypothesis parse failed)", MedianOf(best),
                               "Inconclusive", "LLM 응답에서 유효한 가설 JSON을 찾지 못함 — 세대 스킵");
                        continue;
                    }

                    // 알려진 기법 키만 수용 — LLM이 지어낸 키가 best 설정/derivedConfig로 흘러들지 않게 필터링.
                    var proposed = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, string> pair in hypothesis.Settings)
                    {
                        if (knownKeys.Contains(pair.Key))
                        {
                            proposed[pair.Key] = pair.Value;
                        }
                    }
                    if (proposed.Count == 0)
                    {
                        steps.Add(StepRecord.Skipped(generation, hypothesis.Strategy));
                        Report(progress, generation, hypothesis.Strategy, MedianOf(best),
                               "Inconclusive", "미지의 설정 키만 제안됨 — 세대 스킵");
                        continue;
                    }

                    var candidateSettings = new Dictionary<string, string>(bestSettings);
                    foreach (KeyValuePair<string, string> pair in proposed)
                    {
                        candidateSettings[pair.Key] = pair.Value;
                    }

                    DeviceTierSimulator.Apply(candidateSettings);
                    SampleSet candidate = await EditorBenchmarkRunner.MeasureAsync(config.Benchmark, $"gen{generation}", ct: ct);
                    AdoptionDecision decision = AdoptionGate.Evaluate(best, candidate);

                    steps.Add(new StepRecord
                    {
                        Generation = generation,
                        Strategy = hypothesis.Strategy,
                        Settings = proposed,
                        RunMediansMs = candidate.RunMediansMs.ToList(),
                        Outcome = decision.Outcome.ToString(),
                        PValue = decision.PValue,
                        CliffsDelta = decision.CliffsDelta,
                        Method = decision.Method.ToString(),
                    });

                    if (decision.Outcome == AdoptionOutcome.Adopt)
                    {
                        best = candidate;
                        bestSettings = candidateSettings;
                    }
                    else
                    {
                        // 채택 실패 → 후보 설정을 best로 복원
                        DeviceTierSimulator.Apply(bestSettings);
                    }
                    Report(progress, generation, hypothesis.Strategy, decision.CandidateMedianMs,
                           decision.Outcome.ToString(), decision.Reason);
                }

                // 확인 런: 루프 종료 시점 fresh ABAB 인터리브 재측정 → 챔피언 확인 + 출하 라벨
                if (!SettingsEqual(bestSettings, baselineSettings))
                {
                    (freshBaseline, freshChampion) = await EditorBenchmarkRunner.MeasureInterleavedAsync(
                        config.Benchmark, baselineSettings, bestSettings, ct);
                    if (freshBaseline.Count == 0 || freshChampion.Count == 0)
                    {
                        // 워치독 등으로 확인 런 표본 전량 폐기 — 확인 불가 = 루프 신뢰 철회
                        confirmationFailed = true;
                        confirmationDetail = "확인 런 표본 전량 폐기(워치독/표본 부족) — 확인 불가";
                        Report(progress, config.MaxGenerations, "confirmation", 0.0,
                               "NotConfirmed", confirmationDetail);
                    }
                    else
                    {
                        confirmation = ChampionConfirmation.Confirm(freshBaseline, freshChampion, tier.FrameBudgetMs);
                        shipping = confirmation.Shipping;
                        confirmationFailed = !confirmation.Confirmed;
                        confirmationDetail = confirmation.Adoption.Reason;
                        Report(progress, config.MaxGenerations, "confirmation", confirmation.Shipping.MedianMs,
                               confirmation.Confirmed ? "Confirmed" : "NotConfirmed", confirmation.Adoption.Reason);
                    }
                }
                else
                {
                    // 한 세대도 채택되지 않음(챔피언==baseline) — A-vs-A 재검정은 항상 비유의라 생략하고,
                    // 출하 라벨만 fresh N런으로 산출한다 (stale 표본으로 라벨 금지, confirmationFailed 아님).
                    DeviceTierSimulator.Apply(bestSettings);
                    freshChampion = await EditorBenchmarkRunner.MeasureAsync(
                        config.Benchmark, "champion(baseline)", isFresh: true, ct: ct);
                    if (freshChampion.Count == 0)
                    {
                        confirmationFailed = true;
                        confirmationDetail = "fresh 라벨 런 표본 전량 폐기(워치독/표본 부족) — 라벨 불가";
                        Report(progress, config.MaxGenerations, "confirmation", 0.0,
                               "NotConfirmed", confirmationDetail);
                    }
                    else
                    {
                        shipping = ShippingGate.Evaluate(freshChampion, tier.FrameBudgetMs);
                        confirmationDetail = "채택된 세대 없음 — 챔피언==baseline, A-vs-A 재검정 생략(fresh 출하 라벨만 산출)";
                        Report(progress, config.MaxGenerations, "confirmation", shipping.MedianMs,
                               "SkippedIdentical", confirmationDetail);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 중단 시에도 기록은 남긴다 — 확인 런을 못 마쳤으므로 confirmationFailed = true
                cancelled = true;
                confirmationFailed = true;
                confirmationDetail = confirmationDetail ?? "사용자 중단 — 확인 런 미수행";
                Report(progress, 0, "cancelled", best != null ? MedianOf(best) : 0.0,
                       "Cancelled", "사용자 중단 — 지금까지의 기록을 Artifacts JSON으로 남김");
            }

            return WriteArtifacts(tier, baseline, best, steps, freshBaseline, freshChampion,
                                  confirmation, shipping, confirmationFailed, confirmationDetail,
                                  bestSettings, cancelled);
        }

        private static bool SettingsEqual(IReadOnlyDictionary<string, string> a,
                                          IReadOnlyDictionary<string, string> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            foreach (KeyValuePair<string, string> pair in a)
            {
                if (!b.TryGetValue(pair.Key, out string other) || other != pair.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private string WriteArtifacts(DeviceTierProfile tier, SampleSet baseline, SampleSet best,
                                      List<StepRecord> steps, SampleSet freshBaseline, SampleSet freshChampion,
                                      ConfirmationResult confirmationResult, ShippingLabel shipping,
                                      bool confirmationFailed, string confirmationDetail,
                                      Dictionary<string, string> bestSettings, bool cancelled)
        {
            // confirmationFailed=true → 신규 출하 없음: 소비자는 직전 config 유지, 콜드 스타트면 무출하.
            // (이 JSON의 derivedConfig는 기록용이며 confirmationFailed 표시가 진실이다.)
            // 출하 라벨(budgetSatisfied)은 fresh 재측정 shipping에서만 나온다 — stale 표본 라벨 금지.
            double derivedMedianMs = shipping != null
                ? shipping.MedianMs
                : (best != null ? MedianOf(best) : 0.0);
            bool budgetSatisfied = shipping != null && shipping.BudgetSatisfied;

            var derivedAgainst = new DerivedAgainst
            {
                gameBuildVersion = config.GameBuildVersion,
                upaSchemaVersion = SchemaVersion,
                patchId = config.PatchId,
                derivedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            var derivedConfig = new DerivedConfig
            {
                TierId = tier.TierId,
                Settings = bestSettings,
                MedianMs = derivedMedianMs,
                BudgetSatisfied = budgetSatisfied,
                ConfirmationFailed = confirmationFailed,
                MeasurementSource = MeasurementSource,
                DerivedAgainst = derivedAgainst,
            };

            var artifact = new
            {
                schemaVersion = SchemaVersion,
                tierId = tier.TierId,
                measurementSource = MeasurementSource,
                derivedAgainst = derivedAgainst,
                baseline = baseline == null ? null : new
                {
                    runMediansMs = baseline.RunMediansMs,
                    stats = StatsJson(baseline.RunMediansMs),
                },
                steps = steps.Select(s => new
                {
                    generation = s.Generation,
                    strategy = s.Strategy,
                    settings = s.Settings,
                    runMediansMs = s.RunMediansMs,
                    gate = new
                    {
                        outcome = s.Outcome,
                        pValue = s.PValue,
                        cliffsDelta = s.CliffsDelta,
                        method = s.Method,
                    },
                }).ToList(),
                confirmation = freshChampion == null ? null : new
                {
                    freshBaseline = freshBaseline == null ? null : new
                    {
                        runMediansMs = freshBaseline.RunMediansMs,
                        isFresh = freshBaseline.IsFresh,
                        isInterleaved = freshBaseline.IsInterleaved,
                    },
                    freshChampion = new
                    {
                        runMediansMs = freshChampion.RunMediansMs,
                        isFresh = freshChampion.IsFresh,
                        isInterleaved = freshChampion.IsInterleaved,
                    },
                    confirmed = confirmationResult != null && confirmationResult.Confirmed,
                    p95DeltaMs = confirmationResult != null ? confirmationResult.P95DeltaMs : (double?)null,
                    p99DeltaMs = confirmationResult != null ? confirmationResult.P99DeltaMs : (double?)null,
                    detail = confirmationDetail,
                    note = ConfirmationNote,
                },
                derivedConfig = new
                {
                    tierId = derivedConfig.TierId,
                    settings = derivedConfig.Settings,
                    medianMs = derivedConfig.MedianMs,
                    budgetSatisfied = derivedConfig.BudgetSatisfied,
                    confirmationFailed = derivedConfig.ConfirmationFailed,
                },
            };

            Directory.CreateDirectory("Artifacts");
            string path = Path.Combine("Artifacts", $"research_run_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(artifact, Formatting.Indented));
            return Path.GetFullPath(path);
        }

        private static string BuildPrompt(IReadOnlyDictionary<string, string> currentSettings, List<StepRecord> steps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are optimizing Unity quality settings. Lower frame time (ms) is better.");
            sb.AppendLine("Current settings:");
            foreach (KeyValuePair<string, string> pair in currentSettings.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append("- ").Append(pair.Key).Append(" = ").Append(pair.Value).AppendLine();
            }
            sb.AppendLine("Available settings (two levels each):");
            foreach (OptimizationTechnique technique in TechniqueRegistry.All)
            {
                sb.Append("- ").Append(technique.SettingKey)
                  .Append(": \"").Append(technique.OffValue).Append("\" (cheap) / \"")
                  .Append(technique.OnValue).Append("\" (expensive)").AppendLine();
            }
            if (steps.Count > 0)
            {
                sb.AppendLine("Recent gate results:");
                for (int i = Math.Max(0, steps.Count - 3); i < steps.Count; i++)
                {
                    StepRecord step = steps[i];
                    sb.Append("- Gen ").Append(step.Generation).Append(": ").Append(step.Strategy)
                      .Append(" -> ").Append(step.Outcome).AppendLine();
                }
            }
            sb.AppendLine("Propose exactly ONE setting change to try next.");
            // 1B 모델 대응: 코드 블록 요구 금지 — JSON 오브젝트 하나만 출력하도록 강제.
            sb.Append("JSON만 출력: {\"strategy\":\"...\",\"settings\":{...}}");
            return sb.ToString();
        }

        private static DeviceTierProfile FindTier(string tierId)
        {
            foreach (DeviceTierProfile tier in DeviceTierProfile.Defaults)
            {
                if (tier.TierId == tierId)
                {
                    return tier;
                }
            }
            Debug.LogWarning($"[Alchemist] 알 수 없는 티어 '{tierId}' — mid로 대체합니다.");
            return DeviceTierProfile.Defaults.First(t => t.TierId == "mid");
        }

        private static double MedianOf(SampleSet samples)
        {
            return samples.Count > 0 ? DescriptiveStats.Compute(samples.RunMediansMs).Median : 0.0;
        }

        // Artifacts JSON용 camelCase 통계 직렬화 — ToggleExperimentRunner와 동일 키 스키마.
        private static object StatsJson(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return null;
            }
            DescriptiveStats stats = DescriptiveStats.Compute(values);
            return new
            {
                count = stats.Count,
                mean = stats.Mean,
                median = stats.Median,
                min = stats.Min,
                max = stats.Max,
                p90 = stats.P90,
                p95 = stats.P95,
                p99 = stats.P99,
                mad = stats.Mad,
            };
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

        /// <summary>Artifacts steps 항목의 내부 기록 단위 (계약 §5 스키마와 1:1).</summary>
        private sealed class StepRecord
        {
            public int Generation;
            public string Strategy = "";
            public Dictionary<string, string> Settings = new Dictionary<string, string>();
            public List<double> RunMediansMs = new List<double>();
            public string Outcome = "";
            public double PValue = 1.0;
            public double CliffsDelta;
            public string Method = "";

            public static StepRecord Skipped(int generation, string strategy)
            {
                return new StepRecord
                {
                    Generation = generation,
                    Strategy = strategy,
                    Outcome = AdoptionOutcome.Inconclusive.ToString(),
                };
            }
        }
    }
}
