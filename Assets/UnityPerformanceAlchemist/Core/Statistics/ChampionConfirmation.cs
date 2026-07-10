using System;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    public sealed class ConfirmationResult
    {
        public bool Confirmed;                    // = Adoption.Outcome == Adopt
        public AdoptionDecision Adoption;         // freshBaseline vs freshChampion 재검정
        public ShippingLabel Shipping;            // freshChampion 기준
        public double P95DeltaMs;                 // champion − baseline (음수 = 개선)
        public double P99DeltaMs;
    }

    // ponytail: 확인 런도 단일 비교 α=0.05 — 누적 드리프트를 ≤α false-pass 한도로 축소할 뿐 0이 아님.
    // 업그레이드 경로: 채택별 챔피언 재검증 → α-spending.
    public static class ChampionConfirmation
    {
        // 두 인자 모두 루프 종료 시점 fresh ABAB 인터리브 측정분이어야 함(호출부 보장). t=0 저장 표본 오버로드 없음.
        public static ConfirmationResult Confirm(SampleSet freshBaseline, SampleSet freshChampion,
                                                 double tierBudgetMs)
        {
            if (freshBaseline == null)
            {
                throw new ArgumentNullException(nameof(freshBaseline));
            }
            if (freshChampion == null)
            {
                throw new ArgumentNullException(nameof(freshChampion));
            }

            AdoptionDecision adoption = AdoptionGate.Evaluate(freshBaseline, freshChampion);
            ShippingLabel shipping = ShippingGate.Evaluate(freshChampion, tierBudgetMs);
            DescriptiveStats baselineStats = DescriptiveStats.Compute(freshBaseline.RunMediansMs);
            DescriptiveStats championStats = DescriptiveStats.Compute(freshChampion.RunMediansMs);

            return new ConfirmationResult
            {
                Confirmed = adoption.Outcome == AdoptionOutcome.Adopt,
                Adoption = adoption,
                Shipping = shipping,
                P95DeltaMs = championStats.P95 - baselineStats.P95,
                P99DeltaMs = championStats.P99 - baselineStats.P99
            };
        }
    }
}
