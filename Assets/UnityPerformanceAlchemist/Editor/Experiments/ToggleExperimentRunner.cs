using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityPerformanceAlchemist.Core.Experiments;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Editor
{
    public enum DoeMode
    {
        Ofat,
        Factorial
    }

    public sealed class DoeExperimentConfig
    {
        public string TierId = "mid";
        public DoeMode Mode = DoeMode.Ofat;
        public IReadOnlyList<OptimizationTechnique> Techniques;
        public BenchmarkOptions Benchmark = new BenchmarkOptions();
        public int Seed = 12345;
        public string GameBuildVersion = "";
        public string PatchId = "";
    }

    /// <summary>
    /// LLM 없는 순수 DOE 모드 — ExperimentPlan(OFAT/Factorial) 실행 → ExperimentResult 수집 →
    /// ConfigDerivation(+확인 런) → Artifacts JSON(계약 §5 스키마) → ReportExporter.
    /// 헤드라인 경로: .cs 파일 쓰기 / AssetDatabase.Refresh / 도메인 리로드 유발 금지.
    /// </summary>
    public sealed class ToggleExperimentRunner
    {
        private const string SchemaVersion = "0.2.0";
        private const string ArtifactsDir = "Artifacts";
        private const string ConfirmationNote =
            "루프 중 예산 내였던 챔피언이 확인 런에서 budgetSatisfied:false로 뒤집힐 수 있음 = 지속부하 스로틀 현실(버그 아님)";

        private readonly DoeExperimentConfig config;

        public ToggleExperimentRunner(DoeExperimentConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>실행 후 Artifacts JSON 경로 반환. 중간 취소 시에도 수집분으로 기록을 남긴다.</summary>
        public async Task<string> RunAsync(IProgress<ResearchProgress> progress, CancellationToken ct)
        {
            DeviceTierProfile tier = DeviceTierProfile.Defaults.FirstOrDefault(t => t.TierId == config.TierId)
                                     ?? DeviceTierProfile.Defaults[1];
            IReadOnlyList<OptimizationTechnique> techniques = config.Techniques ?? TechniqueRegistry.All;
            IReadOnlyList<PlannedRun> plan = config.Mode == DoeMode.Factorial
                ? ExperimentPlan.Factorial(techniques, config.Seed)
                : ExperimentPlan.Ofat(tier.QualityProxy, techniques, config.Seed);

            var results = new List<ExperimentResult>(plan.Count);
            DeviceTierSimulator.Snapshot snapshot = DeviceTierSimulator.Capture();
            bool wasCancelled = false;
            try
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        wasCancelled = true;
                        break;
                    }

                    PlannedRun run = plan[i];
                    DeviceTierSimulator.Apply(run.Settings);
                    SampleSet samples;
                    try
                    {
                        samples = await EditorBenchmarkRunner.MeasureAsync(config.Benchmark, run.RunId, ct: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // 측정 중 취소 — 지금까지의 결과로 부분 기록을 남긴다
                        wasCancelled = true;
                        DeviceTierSimulator.Restore(snapshot);
                        break;
                    }
                    DeviceTierSimulator.Restore(snapshot);

                    if (samples.Count == 0)
                    {
                        progress?.Report(new ResearchProgress
                        {
                            Generation = i + 1,
                            Strategy = run.RunId,
                            MedianMs = double.NaN,
                            Outcome = "Discarded",
                            Message = "모든 런이 폐기되어 결과에서 제외"
                        });
                        continue;
                    }

                    DescriptiveStats stats = DescriptiveStats.Compute(samples.RunMediansMs);
                    results.Add(new ExperimentResult
                    {
                        RunId = run.RunId,
                        Settings = run.Settings,
                        Samples = samples,
                        Stats = stats,
                    });
                    progress?.Report(new ResearchProgress
                    {
                        Generation = i + 1,
                        Strategy = run.RunId,
                        MedianMs = stats.Median,
                        Outcome = "Measured",
                        Message = $"{run.RunId}: 중앙값 {stats.Median:F2}ms ({i + 1}/{plan.Count})"
                    });
                }

                if (results.Count == 0)
                {
                    ct.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("측정 결과가 없어 config를 도출할 수 없습니다.");
                }

                var derivedAgainst = new DerivedAgainst
                {
                    gameBuildVersion = config.GameBuildVersion,
                    upaSchemaVersion = SchemaVersion,
                    patchId = config.PatchId,
                    derivedAtUtc = DateTime.UtcNow.ToString("o"),
                };
                DerivedConfig derived = ConfigDerivation.Derive(
                    tier.TierId, results, tier.FrameBudgetMs, "editor-proxy", derivedAgainst);

                ConfirmationResult confirmation = null;
                SampleSet freshBaseline = null;
                SampleSet freshChampion = null;
                try
                {
                    if (wasCancelled)
                    {
                        // 중단 — 확인 런 미수행: stale 부분 측정으로 출하 라벨을 내지 않는다
                        derived.ConfirmationFailed = true;
                        progress?.Report(new ResearchProgress
                        {
                            Generation = plan.Count,
                            Strategy = "confirmation",
                            MedianMs = double.NaN,
                            Outcome = "ConfirmationFailed",
                            Message = "사용자 중단 — 확인 런 미수행, 부분 기록만 남김"
                        });
                    }
                    else if (!SettingsEqual(tier.QualityProxy, derived.Settings))
                    {
                        (freshBaseline, freshChampion) = await EditorBenchmarkRunner.MeasureInterleavedAsync(
                            config.Benchmark, tier.QualityProxy, derived.Settings, ct);
                        if (freshBaseline.Count == 0 || freshChampion.Count == 0)
                        {
                            // 확인 런 전량 폐기(워치독 등) — 검정 불가는 확인 실패로 기록하고 기록은 남긴다
                            derived.ConfirmationFailed = true;
                            freshBaseline = null;
                            freshChampion = null;
                            progress?.Report(new ResearchProgress
                            {
                                Generation = plan.Count,
                                Strategy = "confirmation",
                                MedianMs = double.NaN,
                                Outcome = "ConfirmationFailed",
                                Message = "확인 런 표본 전량 폐기 — 검정 불가"
                            });
                        }
                        else
                        {
                            confirmation = ChampionConfirmation.Confirm(freshBaseline, freshChampion, tier.FrameBudgetMs);
                            derived.ConfirmationFailed = !confirmation.Confirmed;
                            // 확인 런의 fresh 재측정이 출하 라벨의 최신 진실 (지속부하 스로틀로 뒤집힐 수 있음 = 버그 아님)
                            derived.MedianMs = confirmation.Shipping.MedianMs;
                            derived.BudgetSatisfied = confirmation.Shipping.BudgetSatisfied;
                            progress?.Report(new ResearchProgress
                            {
                                Generation = plan.Count,
                                Strategy = "confirmation",
                                MedianMs = confirmation.Shipping.MedianMs,
                                Outcome = confirmation.Confirmed ? "Confirmed" : "ConfirmationFailed",
                                Message = confirmation.Adoption.Reason
                            });
                        }
                    }
                    else
                    {
                        // 챔피언==baseline: A-vs-A 재검정은 항상 미채택이라 생략하되,
                        // 출하 라벨(budgetSatisfied)은 stale 표본이 아닌 fresh N런으로 갱신한다.
                        DeviceTierSimulator.Apply(derived.Settings);
                        freshChampion = await EditorBenchmarkRunner.MeasureAsync(
                            config.Benchmark, "champion(baseline)", isFresh: true, ct: ct);
                        DeviceTierSimulator.Restore(snapshot);
                        if (freshChampion.Count == 0)
                        {
                            derived.ConfirmationFailed = true;
                            freshChampion = null;
                            progress?.Report(new ResearchProgress
                            {
                                Generation = plan.Count,
                                Strategy = "confirmation",
                                MedianMs = double.NaN,
                                Outcome = "ConfirmationFailed",
                                Message = "fresh 라벨 런 표본 전량 폐기 — 라벨 불가"
                            });
                        }
                        else
                        {
                            ShippingLabel label = ShippingGate.Evaluate(freshChampion, tier.FrameBudgetMs);
                            derived.MedianMs = label.MedianMs;
                            derived.BudgetSatisfied = label.BudgetSatisfied;
                            progress?.Report(new ResearchProgress
                            {
                                Generation = plan.Count,
                                Strategy = "confirmation",
                                MedianMs = label.MedianMs,
                                Outcome = "SkippedIdentical",
                                Message = "챔피언==baseline — A-vs-A 재검정 생략, fresh 출하 라벨만 산출"
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 확인 런 도중 취소 — 확인 미완 = 확인 실패로 기록
                    derived.ConfirmationFailed = true;
                    confirmation = null;
                    freshBaseline = null;
                    freshChampion = null;
                }

                string path = WriteArtifacts(tier, results, derived, confirmation, freshBaseline, freshChampion);
                ReportExporter.Export(ArtifactsDir);
                return path;
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
            }
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
                if (!b.TryGetValue(pair.Key, out string value) || value != pair.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private string WriteArtifacts(DeviceTierProfile tier, List<ExperimentResult> results,
                                      DerivedConfig derived, ConfirmationResult confirmation,
                                      SampleSet freshBaseline, SampleSet freshChampion)
        {
            // baseline = RunId 사전순 최소 런 (OFAT baseline / factorial 전체 Off) — ConfigDerivation과 동일 기준
            ExperimentResult baselineResult = results.OrderBy(r => r.RunId, StringComparer.Ordinal).First();

            var steps = new JArray();
            int generation = 0;
            foreach (ExperimentResult result in results)
            {
                if (ReferenceEquals(result, baselineResult))
                {
                    continue;
                }
                generation++;
                AdoptionDecision gate = AdoptionGate.Evaluate(baselineResult.Samples, result.Samples);
                steps.Add(new JObject
                {
                    ["generation"] = generation,
                    ["strategy"] = DescribeChanges(baselineResult.Settings, result.Settings, result.RunId),
                    ["settings"] = ToJObject(result.Settings),
                    ["runMediansMs"] = new JArray(result.Samples.RunMediansMs),
                    ["gate"] = new JObject
                    {
                        ["outcome"] = gate.Outcome.ToString(),
                        ["pValue"] = gate.PValue,
                        ["cliffsDelta"] = gate.CliffsDelta,
                        ["method"] = gate.Method.ToString(),
                    },
                });
            }

            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["tierId"] = tier.TierId,
                ["measurementSource"] = "editor-proxy",
                ["derivedAgainst"] = new JObject
                {
                    ["gameBuildVersion"] = derived.DerivedAgainst.gameBuildVersion,
                    ["upaSchemaVersion"] = derived.DerivedAgainst.upaSchemaVersion,
                    ["patchId"] = derived.DerivedAgainst.patchId,
                    ["derivedAtUtc"] = derived.DerivedAgainst.derivedAtUtc,
                },
                ["baseline"] = new JObject
                {
                    ["runMediansMs"] = new JArray(baselineResult.Samples.RunMediansMs),
                    ["stats"] = StatsToJObject(baselineResult.Stats),
                },
                ["steps"] = steps,
                ["derivedConfig"] = new JObject
                {
                    ["tierId"] = derived.TierId,
                    ["settings"] = ToJObject(derived.Settings),
                    ["medianMs"] = derived.MedianMs,
                    ["budgetSatisfied"] = derived.BudgetSatisfied,
                    ["confirmationFailed"] = derived.ConfirmationFailed,
                },
            };

            if (confirmation != null)
            {
                root["confirmation"] = new JObject
                {
                    ["freshBaseline"] = new JObject
                    {
                        ["runMediansMs"] = new JArray(freshBaseline.RunMediansMs),
                        ["isFresh"] = freshBaseline.IsFresh,
                        ["isInterleaved"] = freshBaseline.IsInterleaved,
                    },
                    ["freshChampion"] = new JObject
                    {
                        ["runMediansMs"] = new JArray(freshChampion.RunMediansMs),
                        ["isFresh"] = freshChampion.IsFresh,
                        ["isInterleaved"] = freshChampion.IsInterleaved,
                    },
                    ["confirmed"] = confirmation.Confirmed,
                    ["p95DeltaMs"] = confirmation.P95DeltaMs,
                    ["p99DeltaMs"] = confirmation.P99DeltaMs,
                    ["detail"] = confirmation.Adoption.Reason,
                    ["note"] = ConfirmationNote,
                };
            }

            ConfigDiff diff = TryDiffAgainstPrevious(derived);
            if (diff != null)
            {
                var changes = new JArray();
                foreach (ConfigDiffEntry entry in diff.Changes)
                {
                    changes.Add(new JObject
                    {
                        ["key"] = entry.Key,
                        ["prevValue"] = entry.PrevValue,
                        ["nextValue"] = entry.NextValue,
                    });
                }
                root["configDiff"] = new JObject
                {
                    ["tierId"] = diff.TierId,
                    ["changes"] = changes,
                    ["prevMedianMs"] = diff.PrevMedianMs,
                    ["nextMedianMs"] = diff.NextMedianMs,
                    ["prevBudgetSatisfied"] = diff.PrevBudgetSatisfied,
                    ["nextBudgetSatisfied"] = diff.NextBudgetSatisfied,
                };
            }

            Directory.CreateDirectory(ArtifactsDir);
            string path = Path.Combine(ArtifactsDir,
                $"research_run_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented));
            return path;
        }

        // 같은 tierId의 가장 최근 이전 Artifacts에서 derivedConfig를 읽어 Diff. 없으면 null.
        private static ConfigDiff TryDiffAgainstPrevious(DerivedConfig next)
        {
            if (!Directory.Exists(ArtifactsDir))
            {
                return null;
            }

            List<string> files = Directory.GetFiles(ArtifactsDir, "research_run_*.json")
                                          .OrderBy(f => f, StringComparer.Ordinal)
                                          .ToList();
            for (int i = files.Count - 1; i >= 0; i--)
            {
                try
                {
                    JObject root = JObject.Parse(File.ReadAllText(files[i]));
                    if (!(root["derivedConfig"] is JObject derivedConfig)
                        || (string)derivedConfig["tierId"] != next.TierId)
                    {
                        continue;
                    }

                    var settings = new Dictionary<string, string>();
                    if (derivedConfig["settings"] is JObject settingsObject)
                    {
                        foreach (JProperty property in settingsObject.Properties())
                        {
                            settings[property.Name] = (string)property.Value;
                        }
                    }

                    var prev = new DerivedConfig
                    {
                        TierId = next.TierId,
                        Settings = settings,
                        MedianMs = (double?)derivedConfig["medianMs"] ?? 0.0,
                        BudgetSatisfied = (bool?)derivedConfig["budgetSatisfied"] ?? false,
                    };
                    return ConfigDerivation.Diff(prev, next);
                }
                catch (Exception)
                {
                    // 손상/구버전 파일은 diff 후보에서 조용히 제외
                }
            }
            return null;
        }

        private static string DescribeChanges(IReadOnlyDictionary<string, string> baseline,
                                              IReadOnlyDictionary<string, string> settings, string runId)
        {
            var parts = new List<string>();
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in baseline.Keys)
            {
                keys.Add(key);
            }
            foreach (string key in settings.Keys)
            {
                keys.Add(key);
            }
            foreach (string key in keys)
            {
                baseline.TryGetValue(key, out string prev);
                settings.TryGetValue(key, out string next);
                if (prev != next)
                {
                    parts.Add($"{key}: {prev ?? "-"}→{next ?? "-"}");
                }
            }
            return parts.Count == 0 ? runId : $"{runId} ({string.Join(", ", parts)})";
        }

        private static JObject ToJObject(IReadOnlyDictionary<string, string> settings)
        {
            var result = new JObject();
            foreach (KeyValuePair<string, string> pair in settings)
            {
                result[pair.Key] = pair.Value;
            }
            return result;
        }

        private static JObject StatsToJObject(DescriptiveStats stats)
        {
            return new JObject
            {
                ["mean"] = stats.Mean,
                ["median"] = stats.Median,
                ["min"] = stats.Min,
                ["max"] = stats.Max,
                ["p90"] = stats.P90,
                ["p95"] = stats.P95,
                ["p99"] = stats.P99,
                ["mad"] = stats.Mad,
                ["count"] = stats.Count,
            };
        }
    }
}
