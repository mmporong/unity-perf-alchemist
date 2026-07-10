using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Experiments
{
    public sealed class OptimizationTechnique
    {
        public string Id;              // 예: "resolutionScale"
        public string DisplayName;
        public string SettingKey;      // 설정 dict 키 (Id와 동일 가능)
        public string OffValue;        // 토글 실험의 두 수준 (문자열 표준화)
        public string OnValue;
    }

    public static class TechniqueRegistry
    {
        // 계약 §3 고정 9종 — Editor DeviceTierSimulator가 전부 매핑해야 함. OnValue = 고품질, OffValue = 저비용.
        private static readonly OptimizationTechnique[] AllTechniques =
        {
            Make("resolutionScale", "Resolution Scale", offValue: "0.75", onValue: "1.0"),
            Make("shadows", "Shadows", offValue: "off", onValue: "on"),
            Make("shadowDistance", "Shadow Distance", offValue: "15", onValue: "40"),
            Make("msaa", "MSAA", offValue: "0", onValue: "4"),
            Make("pixelLightCount", "Pixel Light Count", offValue: "0", onValue: "2"),
            Make("textureQuality", "Texture Quality", offValue: "half", onValue: "full"),
            Make("lodBias", "LOD Bias", offValue: "0.7", onValue: "1.0"),
            Make("softParticles", "Soft Particles", offValue: "off", onValue: "on"),
            Make("vsync", "VSync", offValue: "off", onValue: "on"),
        };

        public static IReadOnlyList<OptimizationTechnique> All
        {
            get { return AllTechniques; }
        }

        private static OptimizationTechnique Make(string id, string displayName, string offValue, string onValue)
        {
            return new OptimizationTechnique
            {
                Id = id,
                DisplayName = displayName,
                SettingKey = id,
                OffValue = offValue,
                OnValue = onValue,
            };
        }
    }
}
