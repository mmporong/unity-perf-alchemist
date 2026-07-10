using System;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    public enum MwuMethod
    {
        NormalApproximation,
        ExactNoTies,
        ExactPermutation
    }

    public sealed class MwuResult
    {
        public double U;                 // U = min(U_a, U_b)
        public double PValueTwoSided;
        public MwuMethod Method;
    }

    /// <summary>
    /// Mann-Whitney U (Wilcoxon rank-sum) 양측 검정.
    /// 두 군 모두 n ≥ 8 → 정규 근사(연속성 보정 0.5, 동점 보정 분산).
    /// 어느 한 군 n < 8 → 정확법: 동점 없으면 카운팅 DP로 정확 U 분포,
    /// 동점 있으면 mid-rank 기반 C(n_a+n_b, n_a) 조합 전수 열거 순열 검정.
    /// </summary>
    public static class MannWhitneyU
    {
        public const int MinSamplesPerGroupForNormalApprox = 8;

        private const double RankSumEpsilon = 1e-9;

        public static MwuResult Test(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }
            if (a.Count == 0 || b.Count == 0)
            {
                throw new ArgumentException("각 군의 표본 수는 1 이상이어야 합니다.");
            }

            int na = a.Count;
            int nb = b.Count;
            int n = na + nb;

            // 풀링 → 정렬 → mid-rank 부여
            double[] values = new double[n];
            for (int i = 0; i < na; i++)
            {
                values[i] = a[i];
            }
            for (int j = 0; j < nb; j++)
            {
                values[na + j] = b[j];
            }

            int[] order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (x, y) => values[x].CompareTo(values[y]));

            double[] ranks = new double[n];
            bool hasTies = false;
            double tieSum = 0.0;   // Σ(t³ − t), t = 각 동점 그룹 크기
            int pos = 0;
            while (pos < n)
            {
                int end = pos;
                while (end + 1 < n && values[order[end + 1]] == values[order[pos]])
                {
                    end++;
                }
                int t = end - pos + 1;
                double midRank = (pos + end) / 2.0 + 1.0;   // 1-based mid-rank
                for (int k = pos; k <= end; k++)
                {
                    ranks[order[k]] = midRank;
                }
                if (t > 1)
                {
                    hasTies = true;
                    tieSum += (double)t * t * t - t;
                }
                pos = end + 1;
            }

            double ra = 0.0;
            for (int i = 0; i < na; i++)
            {
                ra += ranks[i];
            }
            double ua = ra - na * (na + 1) / 2.0;
            double ub = (double)na * nb - ua;
            double u = Math.Min(ua, ub);

            if (na >= MinSamplesPerGroupForNormalApprox && nb >= MinSamplesPerGroupForNormalApprox)
            {
                return NormalApproximationTest(u, na, nb, tieSum);
            }
            if (!hasTies)
            {
                return ExactNoTiesTest(u, na, nb);
            }

            // 동점 + 소표본 → 순열 검정. 조합 수는 동일하므로 작은 군 기준으로 열거(재귀 깊이 최소화).
            double totalRankSum = n * (n + 1) / 2.0;   // mid-rank여도 전체 랭크 합은 불변
            return na <= nb
                ? ExactPermutationTest(u, ranks, na, ra)
                : ExactPermutationTest(u, ranks, nb, totalRankSum - ra);
        }

        private static MwuResult NormalApproximationTest(double u, int na, int nb, double tieSum)
        {
            double n = na + nb;
            double meanU = na * (double)nb / 2.0;
            // 동점 보정 분산: σ²_U = n_a·n_b/12 · [(N+1) − Σ(t³−t)/(N(N−1))]
            double variance = na * (double)nb / 12.0 * ((n + 1.0) - tieSum / (n * (n - 1.0)));
            double p;
            if (variance <= 0.0)
            {
                p = 1.0;   // 전 표본 동일값 → 분산 0 → 판별 불능
            }
            else
            {
                double numerator = Math.Abs(u - meanU) - 0.5;   // 연속성 보정 0.5 (0 방향으로)
                if (numerator < 0.0)
                {
                    numerator = 0.0;
                }
                double z = numerator / Math.Sqrt(variance);
                p = 2.0 * StandardNormalCdf(-z);
                if (p > 1.0)
                {
                    p = 1.0;
                }
            }
            return new MwuResult { U = u, PValueTwoSided = p, Method = MwuMethod.NormalApproximation };
        }

        // 동점 없는 정확 U 분포 — 카운팅 DP (Gaussian binomial 점화식):
        // N(v|i,j) = N(v−j|i−1,j) + N(v|i,j−1). 바깥 루프 j, 안쪽 i 오름차순으로 in-place 갱신
        // (dp[i−1]은 이미 j층, dp[i][v]의 기존 값은 j−1층).
        private static MwuResult ExactNoTiesTest(double u, int na, int nb)
        {
            int m = Math.Min(na, nb);
            int nBig = Math.Max(na, nb);
            int maxU = m * nBig;

            double[][] dp = new double[m + 1][];
            for (int i = 0; i <= m; i++)
            {
                dp[i] = new double[maxU + 1];
                dp[i][0] = 1.0;   // N(0|i,0) = 1
            }
            for (int j = 1; j <= nBig; j++)
            {
                for (int i = 1; i <= m; i++)
                {
                    int hi = Math.Min(maxU, i * j);
                    for (int v = j; v <= hi; v++)
                    {
                        dp[i][v] += dp[i - 1][v - j];
                    }
                }
            }

            int uObserved = (int)Math.Round(u);   // 동점 없음 → U는 정수
            double total = 0.0;
            double tail = 0.0;
            for (int v = 0; v <= maxU; v++)
            {
                total += dp[m][v];
                if (v <= uObserved)
                {
                    tail += dp[m][v];
                }
            }
            // U = min(U_a, U_b) ≤ n_a·n_b/2 이므로 P(U ≤ obs)가 작은 쪽 꼬리. 양측 p = 2·꼬리, 1.0 클램프.
            double p = 2.0 * tail / total;
            if (p > 1.0)
            {
                p = 1.0;
            }
            return new MwuResult { U = u, PValueTwoSided = p, Method = MwuMethod.ExactNoTies };
        }

        // 동점 + 소표본: mid-rank를 고정하고 군 배정 조합을 전수 열거하는 순열 검정.
        // 양측 p = 2·min(P(R ≤ obs), P(R ≥ obs)), 1.0 클램프 — 동점 시 rank-sum 분포가 비대칭일 수 있음.
        private static MwuResult ExactPermutationTest(double u, double[] pooledRanks,
                                                      int enumGroupSize, double observedRankSum)
        {
            long total = 0;
            long countLessOrEqual = 0;
            long countGreaterOrEqual = 0;
            // ponytail: C(N, n_small) 전수 열거 — 반대군이 크게 커지면 조합 폭발. rank-sum 분포 DP가 업그레이드 경로.
            EnumerateCombinations(pooledRanks, 0, enumGroupSize, 0.0, observedRankSum,
                                  ref total, ref countLessOrEqual, ref countGreaterOrEqual);

            double p = 2.0 * Math.Min(countLessOrEqual, countGreaterOrEqual) / total;
            if (p > 1.0)
            {
                p = 1.0;
            }
            return new MwuResult { U = u, PValueTwoSided = p, Method = MwuMethod.ExactPermutation };
        }

        private static void EnumerateCombinations(double[] ranks, int start, int remaining, double sum,
                                                  double target, ref long total,
                                                  ref long countLe, ref long countGe)
        {
            if (remaining == 0)
            {
                total++;
                if (sum <= target + RankSumEpsilon)
                {
                    countLe++;
                }
                if (sum >= target - RankSumEpsilon)
                {
                    countGe++;
                }
                return;
            }
            for (int i = start; i <= ranks.Length - remaining; i++)
            {
                EnumerateCombinations(ranks, i + 1, remaining - 1, sum + ranks[i], target,
                                      ref total, ref countLe, ref countGe);
            }
        }

        private static double StandardNormalCdf(double z)
        {
            return 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
        }

        // erf 근사 — Abramowitz & Stegun 7.1.26, |오차| ≤ 1.5e-7 (Math.Erf는 netstandard2.1에 없음)
        private static double Erf(double x)
        {
            double sign = x < 0.0 ? -1.0 : 1.0;
            double ax = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.3275911 * ax);
            double poly = ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t
                            - 0.284496736) * t + 0.254829592) * t;
            return sign * (1.0 - poly * Math.Exp(-ax * ax));
        }
    }
}
