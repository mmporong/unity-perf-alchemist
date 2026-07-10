using System;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Experiments
{
    public sealed class PlannedRun
    {
        public string RunId;
        public IReadOnlyDictionary<string, string> Settings;  // 이 런에 적용할 전체 세팅
    }

    public static class ExperimentPlan
    {
        // RunId 규칙: "run_" + 0패딩 인덱스. 인덱스는 랜덤화 전 정의 순서 기준,
        // 패딩 폭 = (총 런 수 - 1)의 십진 자릿수 (예: 10런 → run_0..run_9, 256런 → run_000..run_255).

        // OFAT: baseline 1런 + 기법별로 baseline에서 해당 키만 뒤집은 1런씩 = 1+k런.
        // 뒤집기 규칙: baseline 값이 OnValue면 OffValue, 그 외(다른 값·키 없음)는 OnValue.
        public static IReadOnlyList<PlannedRun> Ofat(IReadOnlyDictionary<string, string> baseline,
                                                     IReadOnlyList<OptimizationTechnique> techniques, int seed)
        {
            int total = 1 + techniques.Count;
            var runs = new List<PlannedRun>(total);
            runs.Add(new PlannedRun { RunId = MakeRunId(0, total), Settings = Copy(baseline) });

            for (int i = 0; i < techniques.Count; i++)
            {
                OptimizationTechnique technique = techniques[i];
                Dictionary<string, string> settings = Copy(baseline);
                bool isOn = settings.TryGetValue(technique.SettingKey, out string current)
                            && current == technique.OnValue;
                settings[technique.SettingKey] = isOn ? technique.OffValue : technique.OnValue;
                runs.Add(new PlannedRun { RunId = MakeRunId(i + 1, total), Settings = settings });
            }

            Shuffle(runs, seed);
            return runs;
        }

        // Full factorial: 2^k 런. 정의 순서 인덱스 i의 비트 j가 1이면 기법 j = OnValue, 0이면 OffValue.
        public static IReadOnlyList<PlannedRun> Factorial(IReadOnlyList<OptimizationTechnique> techniques, int seed)
        {
            if (techniques.Count > 8)
            {
                throw new ArgumentException(
                    $"Full factorial은 기법 8종 이하만 지원합니다 (요청 {techniques.Count}종 = 2^{techniques.Count}런). " +
                    "기법 수를 줄이거나 OFAT을 사용하세요.", nameof(techniques));
            }

            int total = 1 << techniques.Count;
            var runs = new List<PlannedRun>(total);
            for (int i = 0; i < total; i++)
            {
                var settings = new Dictionary<string, string>(techniques.Count);
                for (int bit = 0; bit < techniques.Count; bit++)
                {
                    OptimizationTechnique technique = techniques[bit];
                    settings[technique.SettingKey] = ((i >> bit) & 1) == 1 ? technique.OnValue : technique.OffValue;
                }
                runs.Add(new PlannedRun { RunId = MakeRunId(i, total), Settings = settings });
            }

            Shuffle(runs, seed);
            return runs;
        }

        private static string MakeRunId(int definitionIndex, int totalRuns)
        {
            int width = (totalRuns - 1).ToString().Length;
            return "run_" + definitionIndex.ToString().PadLeft(width, '0');
        }

        private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>();
            if (source != null)
            {
                foreach (KeyValuePair<string, string> pair in source)
                {
                    copy[pair.Key] = pair.Value;
                }
            }
            return copy;
        }

        // Fisher-Yates. System.Random(seed) 주입식이라 같은 시드는 같은 실행 순서를 재현한다.
        private static void Shuffle(List<PlannedRun> runs, int seed)
        {
            var random = new Random(seed);
            for (int i = runs.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                PlannedRun temp = runs[i];
                runs[i] = runs[j];
                runs[j] = temp;
            }
        }
    }
}
