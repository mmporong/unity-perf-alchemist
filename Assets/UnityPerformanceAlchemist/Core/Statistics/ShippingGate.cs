using System;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    public sealed class ShippingLabel
    {
        public bool BudgetSatisfied;
        public double MedianMs;
        public double BudgetMs;
    }

    /// <summary>티어 프레임 예산 대비 출하 가능 여부 라벨링. 스칼라 예산 — DeviceTierProfile 비의존.</summary>
    public static class ShippingGate
    {
        public static ShippingLabel Evaluate(SampleSet finalRuns, double tierBudgetMs)
        {
            if (finalRuns == null)
            {
                throw new ArgumentNullException(nameof(finalRuns));
            }

            double median = DescriptiveStats.Compute(finalRuns.RunMediansMs).Median;
            return new ShippingLabel
            {
                // 경계: 중앙값 == 예산 → satisfied (≤ 비교)
                BudgetSatisfied = median <= tierBudgetMs,
                MedianMs = median,
                BudgetMs = tierBudgetMs
            };
        }
    }
}
