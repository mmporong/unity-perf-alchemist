using System;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Experiments
{
    public sealed class DerivedConfig
    {
        public string TierId;
        public IReadOnlyDictionary<string, string> Settings;
        public double MedianMs;
        public bool BudgetSatisfied;
        public bool ConfirmationFailed;
        public string MeasurementSource;   // "editor-proxy" | "on-device"
        public DerivedAgainst DerivedAgainst;
    }

    public sealed class ConfigDiffEntry
    {
        public string Key;
        public string PrevValue;   // prev에 키가 없으면 null
        public string NextValue;   // next에 키가 없으면 null
    }

    public sealed class ConfigDiff
    {
        public string TierId;
        public IReadOnlyList<ConfigDiffEntry> Changes;
        public double PrevMedianMs;
        public double NextMedianMs;
        public bool PrevBudgetSatisfied;
        public bool NextBudgetSatisfied;
    }

    public static class ConfigDerivation
    {
        // 결과 중 중앙값 최저 config 선택. 동률 → 변경 세팅 수 적은 쪽 → RunId 사전순(ordinal).
        // 변경 세팅 수 기준 런 = RunId 사전순 최소 런(정의 순서상 첫 런 = OFAT baseline / factorial 전체 Off).
        public static DerivedConfig Derive(string tierId, IReadOnlyList<ExperimentResult> results,
                                           double tierBudgetMs, string measurementSource, DerivedAgainst derivedAgainst)
        {
            if (results == null || results.Count == 0)
            {
                throw new ArgumentException("결과가 비어 있어 config를 도출할 수 없습니다.", nameof(results));
            }

            ExperimentResult reference = results[0];
            for (int i = 1; i < results.Count; i++)
            {
                if (string.CompareOrdinal(results[i].RunId, reference.RunId) < 0)
                {
                    reference = results[i];
                }
            }

            ExperimentResult best = results[0];
            int bestChanges = CountChanges(reference.Settings, best.Settings);
            for (int i = 1; i < results.Count; i++)
            {
                ExperimentResult candidate = results[i];
                int candidateChanges = CountChanges(reference.Settings, candidate.Settings);
                if (IsBetter(candidate, candidateChanges, best, bestChanges))
                {
                    best = candidate;
                    bestChanges = candidateChanges;
                }
            }

            return new DerivedConfig
            {
                TierId = tierId,
                Settings = best.Settings,
                MedianMs = best.Stats.Median,
                BudgetSatisfied = best.Stats.Median <= tierBudgetMs,
                ConfirmationFailed = false,   // 확인 런 결과는 호출부(러너)가 이후에 기록
                MeasurementSource = measurementSource,
                DerivedAgainst = derivedAgainst,
            };
        }

        // 키 합집합에서 값이 다른 항목만 (한쪽에 없는 키는 null과 비교). TierId 불일치 → ArgumentException.
        public static ConfigDiff Diff(DerivedConfig prev, DerivedConfig next)
        {
            if (prev.TierId != next.TierId)
            {
                throw new ArgumentException($"TierId 불일치: prev={prev.TierId}, next={next.TierId}");
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in prev.Settings.Keys)
            {
                keys.Add(key);
            }
            foreach (string key in next.Settings.Keys)
            {
                keys.Add(key);
            }

            var changes = new List<ConfigDiffEntry>();
            foreach (string key in keys)
            {
                prev.Settings.TryGetValue(key, out string prevValue);
                next.Settings.TryGetValue(key, out string nextValue);
                if (prevValue != nextValue)
                {
                    changes.Add(new ConfigDiffEntry { Key = key, PrevValue = prevValue, NextValue = nextValue });
                }
            }

            return new ConfigDiff
            {
                TierId = prev.TierId,
                Changes = changes,
                PrevMedianMs = prev.MedianMs,
                NextMedianMs = next.MedianMs,
                PrevBudgetSatisfied = prev.BudgetSatisfied,
                NextBudgetSatisfied = next.BudgetSatisfied,
            };
        }

        private static bool IsBetter(ExperimentResult candidate, int candidateChanges,
                                     ExperimentResult best, int bestChanges)
        {
            if (candidate.Stats.Median != best.Stats.Median)
            {
                return candidate.Stats.Median < best.Stats.Median;
            }
            if (candidateChanges != bestChanges)
            {
                return candidateChanges < bestChanges;
            }
            return string.CompareOrdinal(candidate.RunId, best.RunId) < 0;
        }

        private static int CountChanges(IReadOnlyDictionary<string, string> reference,
                                        IReadOnlyDictionary<string, string> settings)
        {
            var keys = new HashSet<string>(reference.Keys);
            keys.UnionWith(settings.Keys);
            int changed = 0;
            foreach (string key in keys)
            {
                reference.TryGetValue(key, out string referenceValue);
                settings.TryGetValue(key, out string value);
                if (referenceValue != value)
                {
                    changed++;
                }
            }
            return changed;
        }
    }
}
