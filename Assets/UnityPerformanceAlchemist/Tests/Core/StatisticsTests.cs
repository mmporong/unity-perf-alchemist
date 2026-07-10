using System;
using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Tests.Core
{
    public class StatisticsTests
    {
        // ---------- MWU: 정확법 (동점 없음, 카운팅 DP) ----------

        [Test]
        public void Mwu_ExactNoTies_CompleteSeparation_MinimumP()
        {
            // 유도: a={1,2,3,4}, b={5,6,7,8}. 모든 a < b → #{a>b} = 0 → U_a = 0, U_b = 16, U = 0.
            // 정확 분포: 총 배열 수 C(8,4) = 70, N(U=0) = 1 (a가 전부 앞에 오는 배열 1가지).
            // 양측 p = 2·P(U ≤ 0) = 2·(1/70) = 2/70 ≈ 0.0285714  — n=4,4 완전 분리 시 최소 p.
            var result = MannWhitneyU.Test(new double[] { 1, 2, 3, 4 }, new double[] { 5, 6, 7, 8 });

            Assert.That(result.Method, Is.EqualTo(MwuMethod.ExactNoTies));
            Assert.That(result.U, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(result.PValueTwoSided, Is.EqualTo(2.0 / 70.0).Within(1e-12));
        }

        [Test]
        public void Mwu_ExactNoTies_SingleInversion()
        {
            // 유도: a={1,2,3,5}, b={4,6,7,8}. a>b 쌍은 (5,4) 하나 → U_a = 1, U = 1.
            // N(U=0) = 1, N(U=1) = 1 → P(U ≤ 1) = 2/70 → 양측 p = 4/70 ≈ 0.0571429.
            var result = MannWhitneyU.Test(new double[] { 1, 2, 3, 5 }, new double[] { 4, 6, 7, 8 });

            Assert.That(result.Method, Is.EqualTo(MwuMethod.ExactNoTies));
            Assert.That(result.U, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(result.PValueTwoSided, Is.EqualTo(4.0 / 70.0).Within(1e-12));
        }

        [Test]
        public void Mwu_ExactNoTies_UnequalGroups_CompleteSeparation()
        {
            // 유도: a={1,2,3}, b={4..13} (n=3,10 — 한 군 n<8 → 정확법). 완전 분리 → U = 0.
            // 총 배열 수 C(13,3) = 286, N(U=0) = 1 → 양측 p = 2/286 ≈ 0.0069930.
            double[] b = new double[10];
            for (int i = 0; i < 10; i++)
            {
                b[i] = 4 + i;
            }
            var result = MannWhitneyU.Test(new double[] { 1, 2, 3 }, b);

            Assert.That(result.Method, Is.EqualTo(MwuMethod.ExactNoTies));
            Assert.That(result.U, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(result.PValueTwoSided, Is.EqualTo(2.0 / 286.0).Within(1e-12));
        }

        // ---------- MWU: 정규 근사 (양군 n ≥ 8) ----------

        [Test]
        public void Mwu_NormalApproximation_CompleteSeparation_N10()
        {
            // 유도: a=1..10, b=11..20 (동점 없음, 양군 n=10 ≥ 8 → 정규 근사). U = 0.
            // mean_U = 10·10/2 = 50. σ²_U = 10·10·(20+1)/12 = 175 (동점 보정항 0).
            // 연속성 보정: |0 − 50| − 0.5 = 49.5 → z = 49.5/√175 = 3.741848…  (z² = 49.5²/175 = 14.0014286)
            // p = 2·Φ(−3.741848) ≈ 1.8268e-4 (표준정규표 보간: Φ(−3.74)=9.201e-5, Φ(−3.75)=8.842e-5).
            double[] a = new double[10];
            double[] b = new double[10];
            for (int i = 0; i < 10; i++)
            {
                a[i] = 1 + i;
                b[i] = 11 + i;
            }
            var result = MannWhitneyU.Test(a, b);

            Assert.That(result.Method, Is.EqualTo(MwuMethod.NormalApproximation));
            Assert.That(result.U, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(result.PValueTwoSided, Is.EqualTo(1.8268e-4).Within(5e-6));
        }

        [Test]
        public void Mwu_NormalApproximation_IdenticalArrays_PIsOne()
        {
            // A/A: 동일 배열 (n=10,10 → 정규 근사, 전 값이 군 간 동점).
            // R_a = R_b → U_a = U_b = 50 = mean_U → |U − mean| = 0 → 보정 후 분자 0 → z = 0 → p = 2·Φ(0) = 1.0.
            double[] a = new double[10];
            double[] b = new double[10];
            for (int i = 0; i < 10; i++)
            {
                a[i] = 10 + i;
                b[i] = 10 + i;
            }
            var result = MannWhitneyU.Test(a, b);

            Assert.That(result.Method, Is.EqualTo(MwuMethod.NormalApproximation));
            Assert.That(result.PValueTwoSided, Is.EqualTo(1.0).Within(1e-12));
        }

        // ---------- MWU: 정확 순열 검정 (동점 + 소표본) ----------

        [Test]
        public void Mwu_ExactPermutation_TiesHandCase()
        {
            // 유도: a={1,2,2}, b={2,3,4} (n=3,3, 값 2가 군 간 동점 → 순열 검정).
            // 풀링 정렬: 1,2,2,2,3,4 → mid-rank: 1은 1, 세 개의 2는 (2+3+4)/3=3, 3은 5, 4는 6.
            // 랭크 멀티셋 {1,3,3,3,5,6}, a의 랭크 {1,3,3} → R_obs = 7. U_a = 7 − 3·4/2 = 1, U_b = 8 → U = 1.
            // C(6,3)=20개 조합의 rank-sum 분포(손으로 전수 열거):
            //   {1,3,3}×3 → 7 | {1,3,5}×3 → 9 | {3,3,3}×1 → 9 | {1,3,6}×3 → 10
            //   {3,3,5}×3 → 11 | {1,5,6}×1 → 12 | {3,3,6}×3 → 12 | {3,5,6}×3 → 14
            //   → 합7:3, 합9:4, 합10:3, 합11:3, 합12:4, 합14:3 (총 20 ✓)
            // P(R ≤ 7) = 3/20, P(R ≥ 7) = 20/20 → 양측 p = 2·min(3,20)/20 = 6/20 = 0.3.
            var result = MannWhitneyU.Test(new double[] { 1, 2, 2 }, new double[] { 2, 3, 4 });

            Assert.That(result.Method, Is.EqualTo(MwuMethod.ExactPermutation));
            Assert.That(result.U, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(result.PValueTwoSided, Is.EqualTo(0.3).Within(1e-12));
        }

        [Test]
        public void Mwu_ExactPermutation_IdenticalSmallArrays_PIsOne()
        {
            // A/A 소표본: a=b={1,2,3} (동점 → 순열 검정). mid-rank 멀티셋 {1.5,1.5,3.5,3.5,5.5,5.5}.
            // R_obs = 1.5+3.5+5.5 = 10.5 = 분포 중심. 손 열거: 합6.5:2, 합8.5:4, 합10.5:8, 합12.5:4, 합14.5:2 (총 20).
            // P(R ≤ 10.5) = 14/20, P(R ≥ 10.5) = 14/20 → p = min(1, 2·14/20) = 1.0.
            var result = MannWhitneyU.Test(new double[] { 1, 2, 3 }, new double[] { 1, 2, 3 });

            Assert.That(result.Method, Is.EqualTo(MwuMethod.ExactPermutation));
            Assert.That(result.PValueTwoSided, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void Mwu_EmptyGroup_Throws()
        {
            Assert.That(() => MannWhitneyU.Test(new double[0], new double[] { 1.0 }),
                        Throws.ArgumentException);
            Assert.That(() => MannWhitneyU.Test(new double[] { 1.0 }, new double[0]),
                        Throws.ArgumentException);
        }

        // ---------- Cliff's delta ----------

        [Test]
        public void CliffsDelta_CompleteSeparation_IsPlusMinusOne()
        {
            // a={1,2}, b={3,4}: #{a>b}=0, #{a<b}=4 → δ = (0−4)/4 = −1. 반대 방향은 +1.
            Assert.That(CliffsDelta.Compute(new double[] { 1, 2 }, new double[] { 3, 4 }),
                        Is.EqualTo(-1.0).Within(1e-12));
            Assert.That(CliffsDelta.Compute(new double[] { 3, 4 }, new double[] { 1, 2 }),
                        Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void CliffsDelta_IdenticalArrays_IsZero()
        {
            // a=b={1,2,3}: #{a>b} = (2>1),(3>1),(3>2) = 3, #{a<b} = (1<2),(1<3),(2<3) = 3 → δ = 0.
            Assert.That(CliffsDelta.Compute(new double[] { 1, 2, 3 }, new double[] { 1, 2, 3 }),
                        Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void CliffsDelta_MixedHandCase()
        {
            // a={1,3}, b={2,4}: #{a>b} = (3>2) = 1, #{a<b} = (1<2),(1<4),(3<4) = 3 → δ = (1−3)/4 = −0.5.
            Assert.That(CliffsDelta.Compute(new double[] { 1, 3 }, new double[] { 2, 4 }),
                        Is.EqualTo(-0.5).Within(1e-12));
        }

        [Test]
        public void CliffsDelta_EmptyGroup_Throws()
        {
            Assert.That(() => CliffsDelta.Compute(new double[0], new double[] { 1.0 }),
                        Throws.ArgumentException);
        }

        // ---------- DescriptiveStats ----------

        [Test]
        public void DescriptiveStats_FiveValues_AllFields()
        {
            // 유도: {1,2,3,4,5}. Mean=3, Median=3, Min=1, Max=5.
            // R-7 퍼센타일: rank = (5−1)·p.
            //   P90: rank=3.6 → v[3]+0.6·(v[4]−v[3]) = 4+0.6 = 4.6
            //   P95: rank=3.8 → 4.8 | P99: rank=3.96 → 4.96
            // MAD: |x−3| = {2,1,0,1,2} → 정렬 {0,1,1,2,2} → 중앙값 1.
            var stats = DescriptiveStats.Compute(new double[] { 1, 2, 3, 4, 5 });

            Assert.That(stats.Mean, Is.EqualTo(3.0).Within(1e-12));
            Assert.That(stats.Median, Is.EqualTo(3.0).Within(1e-12));
            Assert.That(stats.Min, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(stats.Max, Is.EqualTo(5.0).Within(1e-12));
            Assert.That(stats.P90, Is.EqualTo(4.6).Within(1e-12));
            Assert.That(stats.P95, Is.EqualTo(4.8).Within(1e-12));
            Assert.That(stats.P99, Is.EqualTo(4.96).Within(1e-12));
            Assert.That(stats.Mad, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(stats.Count, Is.EqualTo(5));
        }

        [Test]
        public void DescriptiveStats_EvenCount_MedianInterpolated()
        {
            // {1,2,3,4}: rank = 3·0.5 = 1.5 → v[1]+0.5·(v[2]−v[1]) = 2.5 (두 중앙 원소 평균).
            var stats = DescriptiveStats.Compute(new double[] { 1, 2, 3, 4 });

            Assert.That(stats.Median, Is.EqualTo(2.5).Within(1e-12));
        }

        [Test]
        public void DescriptiveStats_Empty_Throws()
        {
            Assert.That(() => DescriptiveStats.Compute(new double[0]), Throws.ArgumentException);
        }

        // ---------- SampleSet ----------

        [Test]
        public void SampleSet_Constructor_StoresProperties()
        {
            var set = new SampleSet(new double[] { 16.7, 17.0 }, "shadows=off",
                                    isFresh: true, isInterleaved: true);

            Assert.That(set.Count, Is.EqualTo(2));
            Assert.That(set.RunMediansMs[0], Is.EqualTo(16.7).Within(1e-12));
            Assert.That(set.ConfigLabel, Is.EqualTo("shadows=off"));
            Assert.That(set.IsFresh, Is.True);
            Assert.That(set.IsInterleaved, Is.True);
        }

        [Test]
        public void ComputeRunMedian_WarmupSecondsDominant()
        {
            // 프레임 {500,600,10,20,30}ms, warmupFrameCount=1, warmupSeconds=1.0:
            // 누적 500 < 1000 → 500+600 = 1100 ≥ 1000 → 1초를 덮는 프레임 수 = 2.
            // 폐기 = max(1, 2) = 2 → 남는 {10,20,30} → 중앙값 20.
            double median = SampleSet.ComputeRunMedian(new double[] { 500, 600, 10, 20, 30 },
                                                       warmupFrameCount: 1, warmupSeconds: 1.0);

            Assert.That(median, Is.EqualTo(20.0).Within(1e-12));
        }

        [Test]
        public void ComputeRunMedian_WarmupFrameCountDominant()
        {
            // 프레임 {500,600,10,20,30}ms, warmupFrameCount=4, warmupSeconds=0.0:
            // 0초 커버 프레임 수 = 0 → 폐기 = max(4, 0) = 4 → 남는 {30} → 중앙값 30.
            double median = SampleSet.ComputeRunMedian(new double[] { 500, 600, 10, 20, 30 },
                                                       warmupFrameCount: 4, warmupSeconds: 0.0);

            Assert.That(median, Is.EqualTo(30.0).Within(1e-12));
        }

        [Test]
        public void ComputeRunMedian_DefaultWarmup()
        {
            // 100ms × 40프레임, 기본값(warmupFrameCount=30, warmupSeconds=1.0):
            // 1초 커버 = 10프레임 → 폐기 = max(30, 10) = 30 → 남는 10프레임(전부 100) → 중앙값 100.
            double[] frames = new double[40];
            for (int i = 0; i < 40; i++)
            {
                frames[i] = 100.0;
            }
            double median = SampleSet.ComputeRunMedian(frames);

            Assert.That(median, Is.EqualTo(100.0).Within(1e-12));
        }

        [Test]
        public void ComputeRunMedian_AllFramesConsumed_Throws()
        {
            // 폐기 = max(2, 0) = 2 = 전체 프레임 수 → 남는 프레임 0 → ArgumentException.
            Assert.That(() => SampleSet.ComputeRunMedian(new double[] { 10, 20 },
                                                         warmupFrameCount: 2, warmupSeconds: 0.0),
                        Throws.ArgumentException);
        }
    }
}
