using System.Collections.Generic;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Core.Experiments
{
    public sealed class ExperimentResult
    {
        public string RunId;
        public IReadOnlyDictionary<string, string> Settings;
        public SampleSet Samples;
        public DescriptiveStats Stats;
    }
}
