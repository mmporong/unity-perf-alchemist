using System;
using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Statistics
{
    /// <summary>Cliff's delta 효과 크기.</summary>
    public static class CliffsDelta
    {
        // δ = (#{a_i > b_j} − #{a_i < b_j}) / (n_a · n_b). 범위 [-1, 1].
        public static double Compute(IReadOnlyList<double> a, IReadOnlyList<double> b)
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

            long greater = 0;
            long less = 0;
            for (int i = 0; i < a.Count; i++)
            {
                for (int j = 0; j < b.Count; j++)
                {
                    if (a[i] > b[j])
                    {
                        greater++;
                    }
                    else if (a[i] < b[j])
                    {
                        less++;
                    }
                }
            }
            return (greater - less) / ((double)a.Count * b.Count);
        }
    }
}
