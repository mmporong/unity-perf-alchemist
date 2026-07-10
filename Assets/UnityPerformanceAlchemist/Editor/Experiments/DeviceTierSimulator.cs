using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// 에디터에서 기기 티어를 QualitySettings/Screen으로 근사하는 시뮬레이터.
    /// TechniqueRegistry 9키(resolutionScale, shadows, shadowDistance, msaa, pixelLightCount,
    /// textureQuality, lodBias, softParticles, vsync) 전부 매핑.
    /// </summary>
    public static class DeviceTierSimulator
    {
        /// <summary>QualitySettings + Screen 해상도 전체 스냅샷 (plain class).</summary>
        public sealed class Snapshot
        {
            public int PixelLightCount;
            public int GlobalTextureMipmapLimit;   // Unity 2022.3: masterTextureLimit의 후속 API
            public ShadowQuality Shadows;
            public float ShadowDistance;
            public int AntiAliasing;
            public float LodBias;
            public bool SoftParticles;
            public int VSyncCount;
            public int ScreenWidth;
            public int ScreenHeight;
        }

        // resolutionScale 반복 적용 시 배율이 중첩되지 않도록 최초 Apply 시점의 기준 해상도를 고정
        private static int baseWidth;
        private static int baseHeight;
        private static bool hasBaseResolution;

        public static Snapshot Capture()
        {
            return new Snapshot
            {
                PixelLightCount = QualitySettings.pixelLightCount,
                GlobalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit,
                Shadows = QualitySettings.shadows,
                ShadowDistance = QualitySettings.shadowDistance,
                AntiAliasing = QualitySettings.antiAliasing,
                LodBias = QualitySettings.lodBias,
                SoftParticles = QualitySettings.softParticles,
                VSyncCount = QualitySettings.vSyncCount,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
            };
        }

        /// <summary>설정 dict 적용. 알 수 없는 키는 무시(계약: 시뮬레이터는 조용히 무시).</summary>
        public static void Apply(IReadOnlyDictionary<string, string> settings)
        {
            if (settings == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in settings)
            {
                string value = pair.Value;
                switch (pair.Key)
                {
                    case "resolutionScale":
                        if (TryParseDouble(value, out double scale) && scale > 0.0)
                        {
                            if (!hasBaseResolution)
                            {
                                baseWidth = Screen.width;
                                baseHeight = Screen.height;
                                hasBaseResolution = true;
                            }
                            // ponytail: 에디터 게임 뷰에서 SetResolution은 사실상 무효(플레이어 전용) —
                            // 실기 해상도 스케일은 on-device 측정이 업그레이드 경로.
                            Screen.SetResolution(
                                Math.Max(1, (int)Math.Round(baseWidth * scale)),
                                Math.Max(1, (int)Math.Round(baseHeight * scale)),
                                Screen.fullScreenMode);
                        }
                        break;
                    case "shadows":
                        QualitySettings.shadows = value == "on" ? ShadowQuality.All : ShadowQuality.Disable;
                        break;
                    case "shadowDistance":
                        if (TryParseDouble(value, out double shadowDistance))
                        {
                            QualitySettings.shadowDistance = (float)shadowDistance;
                        }
                        break;
                    case "msaa":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int msaa))
                        {
                            QualitySettings.antiAliasing = msaa;
                        }
                        break;
                    case "pixelLightCount":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lights))
                        {
                            QualitySettings.pixelLightCount = lights;
                        }
                        break;
                    case "textureQuality":
                        // full = 밉맵 제한 0, half = 1단계 낮춤
                        QualitySettings.globalTextureMipmapLimit = value == "full" ? 0 : 1;
                        break;
                    case "lodBias":
                        if (TryParseDouble(value, out double lodBias))
                        {
                            QualitySettings.lodBias = (float)lodBias;
                        }
                        break;
                    case "softParticles":
                        QualitySettings.softParticles = value == "on";
                        break;
                    case "vsync":
                        QualitySettings.vSyncCount = value == "on" ? 1 : 0;
                        break;
                    default:
                        // 알 수 없는 키 무시 — 시뮬레이터가 다루지 않는 세팅은 상태를 건드리지 않는다
                        break;
                }
            }
        }

        public static void Restore(Snapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            QualitySettings.pixelLightCount = snapshot.PixelLightCount;
            QualitySettings.globalTextureMipmapLimit = snapshot.GlobalTextureMipmapLimit;
            QualitySettings.shadows = snapshot.Shadows;
            QualitySettings.shadowDistance = snapshot.ShadowDistance;
            QualitySettings.antiAliasing = snapshot.AntiAliasing;
            QualitySettings.lodBias = snapshot.LodBias;
            QualitySettings.softParticles = snapshot.SoftParticles;
            QualitySettings.vSyncCount = snapshot.VSyncCount;
            Screen.SetResolution(snapshot.ScreenWidth, snapshot.ScreenHeight, Screen.fullScreenMode);
        }

        /// <summary>스냅샷 전 필드와 현재 상태 비교.</summary>
        public static bool VerifyRestored(Snapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            bool qualityRestored =
                QualitySettings.pixelLightCount == snapshot.PixelLightCount
                && QualitySettings.globalTextureMipmapLimit == snapshot.GlobalTextureMipmapLimit
                && QualitySettings.shadows == snapshot.Shadows
                && Mathf.Approximately(QualitySettings.shadowDistance, snapshot.ShadowDistance)
                && QualitySettings.antiAliasing == snapshot.AntiAliasing
                && Mathf.Approximately(QualitySettings.lodBias, snapshot.LodBias)
                && QualitySettings.softParticles == snapshot.SoftParticles
                && QualitySettings.vSyncCount == snapshot.VSyncCount;

            // ponytail: 편집 모드에서는 SetResolution이 무효라 해상도는 플레이 모드에서만 비교 —
            // 편집 모드 해상도 검증은 게임 뷰 크기 제어 API 도입이 업그레이드 경로.
            bool resolutionRestored = !Application.isPlaying
                || (Screen.width == snapshot.ScreenWidth && Screen.height == snapshot.ScreenHeight);

            return qualityRestored && resolutionRestored;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
