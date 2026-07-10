using System;
using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Tests.Core
{
    public class GateTests
    {
        // start, start+1, ... start+count−1
        private static double[] Sequence(double start, int count)
        {
            double[] result = new double[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = start + i;
            }
            return result;
        }

        // ---------- AdoptionGate 진리표 ----------

        [Test]
        public void AdoptionGate_SignificantLargeImprovement_Adopts()
        {
            // best=20..29, candidate=10..19 (각 n=10, 완전 분리).
            // p ≈ 1.83e-4 < 0.05, δ(candidate vs best) = −1 (|δ| ≥ 0.147), 중앙값 14.5 < 24.5 → Adopt.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(20, 10)),
                                                 new SampleSet(Sequence(10, 10)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Adopt));
            Assert.That(decision.PValue, Is.LessThan(AdoptionGate.Alpha));
            Assert.That(decision.CliffsDelta, Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(decision.BestMedianMs, Is.EqualTo(24.5).Within(1e-12));
            Assert.That(decision.CandidateMedianMs, Is.EqualTo(14.5).Within(1e-12));
        }

        [Test]
        public void AdoptionGate_SignificantWorsening_RollsBack()
        {
            // best=10..19, candidate=20..29: 유의(완전 분리)하지만 candidate 중앙값 24.5 > 14.5, δ=+1 → Rollback.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(10, 10)),
                                                 new SampleSet(Sequence(20, 10)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Rollback));
            Assert.That(decision.PValue, Is.LessThan(AdoptionGate.Alpha));
            Assert.That(decision.CliffsDelta, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void AdoptionGate_IdenticalDistributions_Inconclusive()
        {
            // A/A 동일 배열 → p = 1.0 (유의하지 않음) → Inconclusive.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(10, 10)),
                                                 new SampleSet(Sequence(10, 10)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Inconclusive));
            Assert.That(decision.PValue, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void AdoptionGate_SignificantButSmallEffect_Inconclusive()
        {
            // 유의하지만 효과 크기 미달 셀 (n=200 필요 — 소표본에서는 p<0.05와 |δ|<0.147이 양립 불가).
            // best_j = 100+j, candidate_i = 88+i (i,j = 0..199, 12 시프트).
            // #{cand>best}: 88+i > 100+j ⇔ j ≤ i−13 → Σ_{i=13..199}(i−12) = Σ_{k=1..187}k = 17578.
            // 동점: i−j = 12 → 188쌍. #{cand<best} = 40000 − 17578 − 188 = 22234.
            // δ = (17578 − 22234)/40000 = −4656/40000 = −0.1164 (|δ| < 0.147).
            // U_cand = 17578 + 188/2 = 17672, mean = 20000, σ ≈ 1156.13 (동점 보정 미미)
            // → z = (2328 − 0.5)/1156.13 ≈ 2.013 → p ≈ 0.044 < 0.05.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(100, 200)),
                                                 new SampleSet(Sequence(88, 200)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Inconclusive));
            Assert.That(decision.PValue, Is.LessThan(AdoptionGate.Alpha));               // 유의 셀 확인
            Assert.That(decision.CliffsDelta, Is.EqualTo(-0.1164).Within(1e-9));         // 효과 크기 미달 셀 확인
            Assert.That(Math.Abs(decision.CliffsDelta), Is.LessThan(AdoptionGate.DeltaThreshold));
        }

        [Test]
        public void AdoptionGate_SignificantSmallEffectWorsening_RollsBack()
        {
            // 위 케이스의 방향 반전: 유의 + 악화면 효과 크기와 무관하게 Rollback.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(88, 200)),
                                                 new SampleSet(Sequence(100, 200)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Rollback));
            Assert.That(decision.PValue, Is.LessThan(AdoptionGate.Alpha));
            Assert.That(Math.Abs(decision.CliffsDelta), Is.LessThan(AdoptionGate.DeltaThreshold));
        }

        [Test]
        public void AdoptionGate_BestBelowMinSamples_Inconclusive()
        {
            // best n=4 < MinSamplesPerGroup(5) → 데이터가 완전 분리라도 Inconclusive.
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(20, 4)),
                                                 new SampleSet(Sequence(10, 10)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Inconclusive));
        }

        [Test]
        public void AdoptionGate_CandidateBelowMinSamples_Inconclusive()
        {
            var decision = AdoptionGate.Evaluate(new SampleSet(Sequence(20, 10)),
                                                 new SampleSet(Sequence(10, 4)));

            Assert.That(decision.Outcome, Is.EqualTo(AdoptionOutcome.Inconclusive));
        }

        // ---------- ShippingGate 경계 ----------

        [Test]
        public void ShippingGate_MedianEqualsBudget_Satisfied()
        {
            // 중앙값 {16,17} → 16.5 == 예산 16.5 → satisfied (≤ 경계 포함).
            var label = ShippingGate.Evaluate(new SampleSet(new double[] { 16, 17 }), 16.5);

            Assert.That(label.BudgetSatisfied, Is.True);
            Assert.That(label.MedianMs, Is.EqualTo(16.5).Within(1e-12));
            Assert.That(label.BudgetMs, Is.EqualTo(16.5).Within(1e-12));
        }

        [Test]
        public void ShippingGate_MedianAboveBudget_NotSatisfied()
        {
            var label = ShippingGate.Evaluate(new SampleSet(new double[] { 16, 17 }), 16.49);

            Assert.That(label.BudgetSatisfied, Is.False);
        }

        [Test]
        public void ShippingGate_MedianBelowBudget_Satisfied()
        {
            var label = ShippingGate.Evaluate(new SampleSet(new double[] { 16, 17 }), 33.33);

            Assert.That(label.BudgetSatisfied, Is.True);
        }

        // ---------- ChampionConfirmation 진리표 (확인 성공/실패 × 예산 충족/미충족) ----------

        [Test]
        public void ChampionConfirmation_ConfirmedAndBudgetSatisfied()
        {
            // baseline=20..29, champion=10..19 → Adopt(확인 성공). champion 중앙값 14.5 ≤ 예산 20 → 충족.
            // P95(R-7, n=10): rank = 9·0.95 = 8.55 → baseline 28+0.55 = 28.55, champion 18.55 → Δ = −10.
            // P99: rank = 8.91 → baseline 28.91, champion 18.91 → Δ = −10.
            var result = ChampionConfirmation.Confirm(new SampleSet(Sequence(20, 10)),
                                                      new SampleSet(Sequence(10, 10)), 20.0);

            Assert.That(result.Confirmed, Is.True);
            Assert.That(result.Adoption.Outcome, Is.EqualTo(AdoptionOutcome.Adopt));
            Assert.That(result.Shipping.BudgetSatisfied, Is.True);
            Assert.That(result.P95DeltaMs, Is.EqualTo(-10.0).Within(1e-9));
            Assert.That(result.P99DeltaMs, Is.EqualTo(-10.0).Within(1e-9));
        }

        [Test]
        public void ChampionConfirmation_ConfirmedButOverBudget()
        {
            // 확인은 성공하지만 champion 중앙값 14.5 > 예산 12 → 예산 미충족.
            var result = ChampionConfirmation.Confirm(new SampleSet(Sequence(20, 10)),
                                                      new SampleSet(Sequence(10, 10)), 12.0);

            Assert.That(result.Confirmed, Is.True);
            Assert.That(result.Shipping.BudgetSatisfied, Is.False);
        }

        [Test]
        public void ChampionConfirmation_NotConfirmed_BudgetSatisfied()
        {
            // 동일 분포 → Inconclusive → 확인 실패. 중앙값 24.5 ≤ 예산 100 → 예산은 충족.
            var result = ChampionConfirmation.Confirm(new SampleSet(Sequence(20, 10)),
                                                      new SampleSet(Sequence(20, 10)), 100.0);

            Assert.That(result.Confirmed, Is.False);
            Assert.That(result.Shipping.BudgetSatisfied, Is.True);
        }

        [Test]
        public void ChampionConfirmation_NotConfirmed_OverBudget()
        {
            var result = ChampionConfirmation.Confirm(new SampleSet(Sequence(20, 10)),
                                                      new SampleSet(Sequence(20, 10)), 5.0);

            Assert.That(result.Confirmed, Is.False);
            Assert.That(result.Shipping.BudgetSatisfied, Is.False);
        }

        // ---------- A/A 널 테스트 ----------

        [Test]
        public void AdoptionGate_AaNull_FalseAdoptRateWithinBound()
        {
            // 귀무가설 참: 동일 분포(정규 μ=16.7ms, σ=1.5, Box-Muller)에서 두 군 n=10씩 × 200배치.
            // Adopt(거짓 채택) 비율은 α=0.05 근처 이하 — 방향·효과 크기 조건이 있어 실제로는 더 낮다.
            // 상한 0.08 = α + 시드 요동 여유.
            var rng = new Random(12345);
            const int batches = 200;
            int adoptCount = 0;
            for (int batch = 0; batch < batches; batch++)
            {
                double[] a = new double[10];
                double[] b = new double[10];
                for (int i = 0; i < 10; i++)
                {
                    a[i] = NextGaussian(rng, 16.7, 1.5);
                    b[i] = NextGaussian(rng, 16.7, 1.5);
                }
                var decision = AdoptionGate.Evaluate(new SampleSet(a), new SampleSet(b));
                if (decision.Outcome == AdoptionOutcome.Adopt)
                {
                    adoptCount++;
                }
            }

            Assert.That(adoptCount / (double)batches, Is.LessThanOrEqualTo(0.08));
        }

        // Box-Muller 정규 근사 표본. u1 ∈ (0,1] 보장을 위해 1 − NextDouble().
        private static double NextGaussian(Random rng, double mean, double stdDev)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
