using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Experiments
{
    public sealed class DeviceTierProfile
    {
        public string TierId;              // "low" | "mid" | "high" (확장 가능)
        public string DisplayName;
        public double FrameBudgetMs;       // 예: low 33.33(30fps), mid 16.67(60fps), high 16.67
        public IReadOnlyDictionary<string, string> QualityProxy;   // 티어 근사용 기본 세팅

        // 계약 §3 기본 3티어. 값은 TechniqueRegistry와 동일한 문자열 표준화.
        public static IReadOnlyList<DeviceTierProfile> Defaults { get; } = new[]
        {
            new DeviceTierProfile
            {
                TierId = "low",
                DisplayName = "Low (30fps)",
                FrameBudgetMs = 33.33,
                QualityProxy = new Dictionary<string, string>
                {
                    ["resolutionScale"] = "0.75",
                    ["shadows"] = "off",
                    ["msaa"] = "0",
                    ["textureQuality"] = "half",
                    ["pixelLightCount"] = "0",
                },
            },
            new DeviceTierProfile
            {
                TierId = "mid",
                DisplayName = "Mid (60fps)",
                FrameBudgetMs = 16.67,
                QualityProxy = new Dictionary<string, string>
                {
                    ["resolutionScale"] = "1.0",
                    ["shadows"] = "on",
                    ["shadowDistance"] = "15",
                    ["msaa"] = "0",
                    ["pixelLightCount"] = "1",
                },
            },
            new DeviceTierProfile
            {
                TierId = "high",
                DisplayName = "High (60fps)",
                FrameBudgetMs = 16.67,
                QualityProxy = new Dictionary<string, string>
                {
                    // 전부 고품질 = TechniqueRegistry 9종의 OnValue
                    ["resolutionScale"] = "1.0",
                    ["shadows"] = "on",
                    ["shadowDistance"] = "40",
                    ["msaa"] = "4",
                    ["pixelLightCount"] = "2",
                    ["textureQuality"] = "full",
                    ["lodBias"] = "1.0",
                    ["softParticles"] = "on",
                    ["vsync"] = "on",
                },
            },
        };
    }
}
