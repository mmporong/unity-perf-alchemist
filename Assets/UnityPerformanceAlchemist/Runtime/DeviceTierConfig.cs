using System.Collections.Generic;
using UnityEngine;
using UnityPerformanceAlchemist.Core.Experiments;

namespace UnityPerformanceAlchemist.Runtime
{
    // 티어별 도출 설정의 직렬화 컨테이너 — 데이터만 담는다.
    // 도출 결과를 채우는 것은 Editor 쪽 러너의 책임 (계약 §4: SO에 정적 도출 헬퍼 금지).
    [CreateAssetMenu(menuName = "Alchemist/Device Tier Config", fileName = "DeviceTierConfig")]
    public sealed class DeviceTierConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class SettingEntry
        {
            public string key;
            public string value;
        }

        [System.Serializable]
        public sealed class TierEntry
        {
            public string tierId;                                    // "low" | "mid" | "high"
            public double budgetMs;                                  // 티어 프레임 예산(ms)
            public List<SettingEntry> settings = new List<SettingEntry>();
            public string measurementSource;                         // "editor-proxy" | "on-device"
            public bool budgetSatisfied;
            public bool confirmationFailed;
            public DerivedAgainst derivedAgainst;                    // Core 타입 직접 직렬화 ([Serializable])
        }

        public List<TierEntry> tiers = new List<TierEntry>();
    }
}
