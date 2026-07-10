using System;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    public enum AdoptionOutcome
    {
        Adopt,
        Rollback,
        Inconclusive
    }

    public sealed class AdoptionDecision
    {
        public AdoptionOutcome Outcome;
        public double PValue;
        public double CliffsDelta;
        public double BestMedianMs;
        public double CandidateMedianMs;
        public MwuMethod Method;
        public string Reason;   // 사람이 읽는 근거 한 줄
    }

    // ponytail: 게이트는 p50(런 중앙값) 단일 지표. p95/p99 꼬리 악화는 게이트하지 않음(기록만) —
    // 스터터가 중요해지면 p95 회귀 가드 추가가 업그레이드 경로.
    public static class AdoptionGate
    {
        public const double Alpha = 0.05;
        public const double DeltaThreshold = 0.147;
        public const int MinSamplesPerGroup = 5;   // 미만이면 Inconclusive

        // currentBest vs candidate. 개선 = candidate 중앙값이 낮음.
        public static AdoptionDecision Evaluate(SampleSet currentBest, SampleSet candidate)
        {
            if (currentBest == null)
            {
                throw new ArgumentNullException(nameof(currentBest));
            }
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            double bestMedian = currentBest.Count > 0
                ? DescriptiveStats.Compute(currentBest.RunMediansMs).Median
                : double.NaN;
            double candidateMedian = candidate.Count > 0
                ? DescriptiveStats.Compute(candidate.RunMediansMs).Median
                : double.NaN;

            if (currentBest.Count < MinSamplesPerGroup || candidate.Count < MinSamplesPerGroup)
            {
                // 표본 부족 → 검정 미수행 (Method는 무의미한 기본값, Reason이 진실)
                return new AdoptionDecision
                {
                    Outcome = AdoptionOutcome.Inconclusive,
                    PValue = 1.0,
                    CliffsDelta = 0.0,
                    BestMedianMs = bestMedian,
                    CandidateMedianMs = candidateMedian,
                    Method = MwuMethod.NormalApproximation,
                    Reason = $"표본 부족: best={currentBest.Count}, candidate={candidate.Count} (군당 최소 {MinSamplesPerGroup})"
                };
            }

            MwuResult mwu = MannWhitneyU.Test(currentBest.RunMediansMs, candidate.RunMediansMs);
            // delta < 0 ⇔ candidate 값이 낮음(빠름) = 개선 방향
            double delta = Statistics.CliffsDelta.Compute(candidate.RunMediansMs, currentBest.RunMediansMs);

            bool isSignificant = mwu.PValueTwoSided < Alpha;
            bool isLargeEffect = Math.Abs(delta) >= DeltaThreshold;
            bool isImprovedDirection = candidateMedian < bestMedian && delta < 0.0;
            bool isWorsenedDirection = candidateMedian > bestMedian && delta > 0.0;

            AdoptionOutcome outcome;
            string reason;
            if (isSignificant && isLargeEffect && isImprovedDirection)
            {
                outcome = AdoptionOutcome.Adopt;
                reason = $"유의(p={mwu.PValueTwoSided:F4}) + 효과(|δ|={Math.Abs(delta):F3}) + 개선({candidateMedian:F2}ms < {bestMedian:F2}ms)";
            }
            else if (isSignificant && isWorsenedDirection)
            {
                outcome = AdoptionOutcome.Rollback;
                reason = $"유의(p={mwu.PValueTwoSided:F4})하지만 악화({candidateMedian:F2}ms > {bestMedian:F2}ms)";
            }
            else if (isSignificant)
            {
                outcome = AdoptionOutcome.Inconclusive;
                reason = isLargeEffect
                    ? $"유의하나 방향 판정 불가(δ={delta:F3}, 중앙값 차 {candidateMedian - bestMedian:F2}ms)"
                    : $"유의하나 효과 크기 미달(|δ|={Math.Abs(delta):F3} < {DeltaThreshold})";
            }
            else
            {
                outcome = AdoptionOutcome.Inconclusive;
                reason = $"유의하지 않음(p={mwu.PValueTwoSided:F4} ≥ {Alpha})";
            }

            return new AdoptionDecision
            {
                Outcome = outcome,
                PValue = mwu.PValueTwoSided,
                CliffsDelta = delta,
                BestMedianMs = bestMedian,
                CandidateMedianMs = candidateMedian,
                Method = mwu.Method,
                Reason = reason
            };
        }
    }
}
