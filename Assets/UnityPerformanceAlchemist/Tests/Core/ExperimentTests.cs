using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Experiments;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Tests.Core
{
    public class ExperimentTests
    {
        // ---------- OFAT ----------

        [Test]
        public void Ofat_BaselineAllOn_ProducesOnePlusKRuns_EachTogglesExactlyOneKey()
        {
            IReadOnlyList<OptimizationTechnique> techniques = TechniqueRegistry.All;
            Dictionary<string, string> baseline = techniques.ToDictionary(t => t.SettingKey, t => t.OnValue);

            IReadOnlyList<PlannedRun> runs = ExperimentPlan.Ofat(baseline, techniques, seed: 1);

            Assert.That(runs.Count, Is.EqualTo(1 + techniques.Count));

            PlannedRun baselineRun = runs.Single(r => CountDiffs(baseline, r.Settings) == 0);
            Assert.That(baselineRun.RunId, Is.EqualTo("run_0")); // 정의 순서 0 = baseline (10런 → 패딩 폭 1)

            var toggledKeys = new List<string>();
            foreach (PlannedRun run in runs)
            {
                if (ReferenceEquals(run, baselineRun))
                {
                    continue;
                }
                Assert.That(CountDiffs(baseline, run.Settings), Is.EqualTo(1), run.RunId);
                string toggledKey = baseline.Keys.Single(k => baseline[k] != run.Settings[k]);
                OptimizationTechnique technique = techniques.Single(t => t.SettingKey == toggledKey);
                Assert.That(run.Settings[toggledKey], Is.EqualTo(technique.OffValue)); // 전부 On baseline → Off로 뒤집힘
                toggledKeys.Add(toggledKey);
            }
            CollectionAssert.AreEquivalent(techniques.Select(t => t.SettingKey), toggledKeys); // 기법당 정확히 1회
        }

        [Test]
        public void Ofat_BaselineMissingKey_AppliesOnValue()
        {
            List<OptimizationTechnique> techniques = TechniqueRegistry.All.Take(2).ToList();
            var baseline = new Dictionary<string, string>();

            IReadOnlyList<PlannedRun> runs = ExperimentPlan.Ofat(baseline, techniques, seed: 7);

            Assert.That(runs.Count, Is.EqualTo(3));
            Assert.That(runs.Count(r => r.Settings.Count == 0), Is.EqualTo(1)); // baseline 런
            Assert.That(runs.Count(r => r.Settings.Count == 1), Is.EqualTo(2));
            foreach (PlannedRun run in runs.Where(r => r.Settings.Count == 1))
            {
                KeyValuePair<string, string> only = run.Settings.Single();
                OptimizationTechnique technique = techniques.Single(t => t.SettingKey == only.Key);
                Assert.That(only.Value, Is.EqualTo(technique.OnValue)); // 키 없음 → OnValue 적용
            }
        }

        // ---------- Factorial ----------

        [Test]
        public void Factorial_ThreeTechniques_ProducesEightUniqueCombinations()
        {
            List<OptimizationTechnique> techniques = TechniqueRegistry.All.Take(3).ToList();

            IReadOnlyList<PlannedRun> runs = ExperimentPlan.Factorial(techniques, seed: 3);

            Assert.That(runs.Count, Is.EqualTo(8));
            Assert.That(runs.Select(Signature).Distinct().Count(), Is.EqualTo(8)); // 중복 조합 없음
            foreach (PlannedRun run in runs)
            {
                Assert.That(run.Settings.Count, Is.EqualTo(3));
                foreach (OptimizationTechnique technique in techniques)
                {
                    Assert.That(run.Settings[technique.SettingKey],
                                Is.EqualTo(technique.OnValue).Or.EqualTo(technique.OffValue));
                }
            }
        }

        [Test]
        public void Factorial_SameSeed_ReproducesSameOrder()
        {
            List<OptimizationTechnique> techniques = TechniqueRegistry.All.Take(4).ToList();

            List<string> first = ExperimentPlan.Factorial(techniques, seed: 42).Select(r => r.RunId).ToList();
            List<string> second = ExperimentPlan.Factorial(techniques, seed: 42).Select(r => r.RunId).ToList();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Factorial_DifferentSeed_ProducesDifferentOrder()
        {
            // 16개 런 순열이 두 시드에서 우연히 일치할 확률은 무시 가능한 수준 — 완전 보장은 아님
            List<OptimizationTechnique> techniques = TechniqueRegistry.All.Take(4).ToList();

            List<string> a = ExperimentPlan.Factorial(techniques, seed: 12345).Select(r => r.RunId).ToList();
            List<string> b = ExperimentPlan.Factorial(techniques, seed: 54321).Select(r => r.RunId).ToList();

            CollectionAssert.AreEquivalent(a, b);   // 같은 런 집합
            Assert.That(b, Is.Not.EqualTo(a));      // 순서는 상이
        }

        [Test]
        public void Factorial_NineTechniques_ThrowsArgumentException()
        {
            Assert.That(TechniqueRegistry.All.Count, Is.EqualTo(9));
            Assert.Throws<ArgumentException>(() => ExperimentPlan.Factorial(TechniqueRegistry.All, seed: 1));
        }

        // ---------- ConfigDerivation.Derive ----------

        [Test]
        public void Derive_PicksLowestMedian_AndFillsMetadata()
        {
            var results = new List<ExperimentResult>
            {
                MakeResult("run_0", Settings(("shadows", "on"), ("msaa", "4")), 20.0),
                MakeResult("run_1", Settings(("shadows", "off"), ("msaa", "4")), 12.0),
                MakeResult("run_2", Settings(("shadows", "on"), ("msaa", "0")), 15.0),
            };

            DerivedConfig config = ConfigDerivation.Derive("mid", results, 16.67, "editor-proxy", Against());

            Assert.That(config.TierId, Is.EqualTo("mid"));
            Assert.That(config.MedianMs, Is.EqualTo(12.0));
            Assert.That(config.Settings["shadows"], Is.EqualTo("off"));
            Assert.That(config.BudgetSatisfied, Is.True);         // 12.0 <= 16.67
            Assert.That(config.ConfirmationFailed, Is.False);
            Assert.That(config.MeasurementSource, Is.EqualTo("editor-proxy"));
            Assert.That(config.DerivedAgainst.patchId, Is.EqualTo("patch-test"));

            DerivedConfig overBudget = ConfigDerivation.Derive("mid", results, 10.0, "editor-proxy", Against());
            Assert.That(overBudget.BudgetSatisfied, Is.False);    // 12.0 > 10.0
        }

        [Test]
        public void Derive_TieOnMedian_PrefersFewerChangedSettings()
        {
            var results = new List<ExperimentResult>
            {
                MakeResult("run_0", Settings(("a", "1"), ("b", "1")), 20.0),   // 기준(RunId 최소)
                MakeResult("run_2", Settings(("a", "2"), ("b", "2")), 10.0),   // 변경 2개
                MakeResult("run_1", Settings(("a", "2"), ("b", "1")), 10.0),   // 변경 1개 → 승리
            };

            DerivedConfig config = ConfigDerivation.Derive("low", results, 33.33, "editor-proxy", Against());

            Assert.That(config.Settings["a"], Is.EqualTo("2"));
            Assert.That(config.Settings["b"], Is.EqualTo("1"));
        }

        [Test]
        public void Derive_TieOnMedianAndChanges_PrefersLexicographicRunId()
        {
            var results = new List<ExperimentResult>
            {
                MakeResult("run_0", Settings(("a", "1"), ("b", "1")), 20.0),
                MakeResult("run_2", Settings(("a", "1"), ("b", "2")), 10.0),   // 변경 1개
                MakeResult("run_1", Settings(("a", "2"), ("b", "1")), 10.0),   // 변경 1개, RunId 사전순 앞 → 승리
            };

            DerivedConfig config = ConfigDerivation.Derive("low", results, 33.33, "editor-proxy", Against());

            Assert.That(config.Settings["a"], Is.EqualTo("2"));
            Assert.That(config.Settings["b"], Is.EqualTo("1"));
        }

        [Test]
        public void Derive_EmptyResults_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                ConfigDerivation.Derive("low", new List<ExperimentResult>(), 33.33, "editor-proxy", Against()));
        }

        // ---------- ConfigDerivation.Diff ----------

        [Test]
        public void Diff_ReturnsOnlyChangedKeys()
        {
            DerivedConfig prev = MakeConfig("mid", Settings(("a", "1"), ("b", "1")), 20.0, budgetSatisfied: false);
            DerivedConfig next = MakeConfig("mid", Settings(("a", "2"), ("b", "1"), ("c", "3")), 12.0, budgetSatisfied: true);

            ConfigDiff diff = ConfigDerivation.Diff(prev, next);

            Assert.That(diff.TierId, Is.EqualTo("mid"));
            Assert.That(diff.Changes.Count, Is.EqualTo(2));       // b는 불변 → 미포함

            ConfigDiffEntry entryA = diff.Changes.Single(e => e.Key == "a");
            Assert.That(entryA.PrevValue, Is.EqualTo("1"));
            Assert.That(entryA.NextValue, Is.EqualTo("2"));

            ConfigDiffEntry entryC = diff.Changes.Single(e => e.Key == "c");
            Assert.That(entryC.PrevValue, Is.Null);               // prev에 없던 키
            Assert.That(entryC.NextValue, Is.EqualTo("3"));

            Assert.That(diff.PrevMedianMs, Is.EqualTo(20.0));
            Assert.That(diff.NextMedianMs, Is.EqualTo(12.0));
            Assert.That(diff.PrevBudgetSatisfied, Is.False);
            Assert.That(diff.NextBudgetSatisfied, Is.True);
        }

        [Test]
        public void Diff_TierIdMismatch_ThrowsArgumentException()
        {
            DerivedConfig prev = MakeConfig("low", Settings(("a", "1")), 20.0, budgetSatisfied: true);
            DerivedConfig next = MakeConfig("mid", Settings(("a", "1")), 20.0, budgetSatisfied: true);

            Assert.Throws<ArgumentException>(() => ConfigDerivation.Diff(prev, next));
        }

        // ---------- DeviceTierProfile ----------

        [Test]
        public void DeviceTierDefaults_HasThreeTiersWithExpectedBudgets()
        {
            IReadOnlyList<DeviceTierProfile> defaults = DeviceTierProfile.Defaults;

            Assert.That(defaults.Select(d => d.TierId), Is.EqualTo(new[] { "low", "mid", "high" }));
            Assert.That(defaults[0].FrameBudgetMs, Is.EqualTo(33.33));
            Assert.That(defaults[1].FrameBudgetMs, Is.EqualTo(16.67));
            Assert.That(defaults[2].FrameBudgetMs, Is.EqualTo(16.67));
            Assert.That(defaults[2].QualityProxy["msaa"], Is.EqualTo("4"));
        }

        // ---------- 헬퍼 ----------

        private static Dictionary<string, string> Settings(params (string Key, string Value)[] pairs)
        {
            var settings = new Dictionary<string, string>();
            foreach ((string key, string value) in pairs)
            {
                settings[key] = value;
            }
            return settings;
        }

        private static ExperimentResult MakeResult(string runId, Dictionary<string, string> settings, double medianMs)
        {
            double[] medians = { medianMs, medianMs, medianMs };
            return new ExperimentResult
            {
                RunId = runId,
                Settings = settings,
                Samples = new SampleSet(medians, runId),
                Stats = DescriptiveStats.Compute(medians),
            };
        }

        private static DerivedConfig MakeConfig(string tierId, Dictionary<string, string> settings,
                                                double medianMs, bool budgetSatisfied)
        {
            return new DerivedConfig
            {
                TierId = tierId,
                Settings = settings,
                MedianMs = medianMs,
                BudgetSatisfied = budgetSatisfied,
                ConfirmationFailed = false,
                MeasurementSource = "editor-proxy",
                DerivedAgainst = Against(),
            };
        }

        private static DerivedAgainst Against()
        {
            return new DerivedAgainst
            {
                gameBuildVersion = "test-build",
                upaSchemaVersion = "0.2.0",
                patchId = "patch-test",
                derivedAtUtc = "2026-07-10T00:00:00Z",
            };
        }

        private static int CountDiffs(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        {
            var keys = new HashSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            int diffs = 0;
            foreach (string key in keys)
            {
                a.TryGetValue(key, out string valueA);
                b.TryGetValue(key, out string valueB);
                if (valueA != valueB)
                {
                    diffs++;
                }
            }
            return diffs;
        }

        private static string Signature(PlannedRun run)
        {
            return string.Join(";", run.Settings
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + "=" + p.Value));
        }
    }
}
