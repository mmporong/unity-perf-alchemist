using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    /// <summary>독립 벤치마크 런들의 런 중앙값 표본 집합 — MWU 검정의 관측 단위.</summary>
    public sealed class SampleSet
    {
        public IReadOnlyList<double> RunMediansMs { get; }   // 독립 런당 중앙값 1개씩
        public string ConfigLabel { get; }
        public bool IsFresh { get; }         // 루프 종료 시점 fresh 재측정분 (확인 런용)
        public bool IsInterleaved { get; }   // ABAB 인터리브 측정분
        public int Count => RunMediansMs.Count;

        public SampleSet(IEnumerable<double> runMediansMs, string configLabel = "",
                         bool isFresh = false, bool isInterleaved = false)
        {
            if (runMediansMs == null)
            {
                throw new ArgumentNullException(nameof(runMediansMs));
            }

            RunMediansMs = runMediansMs.ToArray();
            ConfigLabel = configLabel ?? "";
            IsFresh = isFresh;
            IsInterleaved = isInterleaved;
        }

        // 프레임 시계열 → 워밍업 폐기 → 런 중앙값.
        // 폐기량 = max(warmupFrameCount, warmupSeconds를 덮는 데 필요한 프레임 수).
        public static double ComputeRunMedian(IReadOnlyList<double> frameTimesMs,
                                              int warmupFrameCount = 30, double warmupSeconds = 1.0)
        {
            if (frameTimesMs == null)
            {
                throw new ArgumentNullException(nameof(frameTimesMs));
            }

            // warmupSeconds를 덮는 프레임 수: 누적 프레임타임이 warmupSeconds*1000ms 이상이 되는 최소 프레임 수
            double targetMs = warmupSeconds * 1000.0;
            double cumulativeMs = 0.0;
            int framesCoveringWarmupSeconds = 0;
            while (framesCoveringWarmupSeconds < frameTimesMs.Count && cumulativeMs < targetMs)
            {
                cumulativeMs += frameTimesMs[framesCoveringWarmupSeconds];
                framesCoveringWarmupSeconds++;
            }

            int discardCount = Math.Max(warmupFrameCount, framesCoveringWarmupSeconds);
            if (discardCount < 0)
            {
                discardCount = 0;
            }

            int remaining = frameTimesMs.Count - discardCount;
            if (remaining <= 0)
            {
                throw new ArgumentException("워밍업 폐기 후 남는 프레임이 없습니다.", nameof(frameTimesMs));
            }

            double[] measured = new double[remaining];
            for (int i = 0; i < remaining; i++)
            {
                measured[i] = frameTimesMs[discardCount + i];
            }

            return DescriptiveStats.Compute(measured).Median;
        }
    }
}
