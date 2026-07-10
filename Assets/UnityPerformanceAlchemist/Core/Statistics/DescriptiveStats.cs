using System;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    /// <summary>기술 통계 요약. 퍼센타일은 선형 보간(R-7/Excel 방식) — rank = (n-1)*p, floor/ceil 사이 보간.</summary>
    public sealed class DescriptiveStats
    {
        public double Mean;
        public double Median;
        public double Min;
        public double Max;
        public double P90;
        public double P95;
        public double P99;
        public double Mad;   // median absolute deviation
        public int Count;

        private DescriptiveStats()
        {
        }

        public static DescriptiveStats Compute(IReadOnlyList<double> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            if (values.Count == 0)
            {
                throw new ArgumentException("빈 입력으로는 통계를 계산할 수 없습니다.", nameof(values));
            }

            int n = values.Count;
            double[] sorted = new double[n];
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                sorted[i] = values[i];
                sum += values[i];
            }
            Array.Sort(sorted);

            double median = PercentileSorted(sorted, 0.5);

            double[] deviations = new double[n];
            for (int i = 0; i < n; i++)
            {
                deviations[i] = Math.Abs(sorted[i] - median);
            }
            Array.Sort(deviations);

            return new DescriptiveStats
            {
                Mean = sum / n,
                Median = median,
                Min = sorted[0],
                Max = sorted[n - 1],
                P90 = PercentileSorted(sorted, 0.90),
                P95 = PercentileSorted(sorted, 0.95),
                P99 = PercentileSorted(sorted, 0.99),
                Mad = PercentileSorted(deviations, 0.5),
                Count = n
            };
        }

        // R-7 (Excel PERCENTILE.INC) 방식: rank = (n-1)*p, 이웃 두 원소 선형 보간
        private static double PercentileSorted(double[] sorted, double p)
        {
            double rank = (sorted.Length - 1) * p;
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi)
            {
                return sorted[lo];
            }
            return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
        }
    }
}
