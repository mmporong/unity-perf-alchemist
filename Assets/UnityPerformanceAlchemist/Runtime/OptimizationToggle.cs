using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnityPerformanceAlchemist.Runtime
{
    // 표준화된 설정 문자열(dict)을 QualitySettings/Screen에 적용한다 (계약 §4).
    // 알 수 없는 키는 무시하고 경고만 남긴다.
    public static class OptimizationToggle
    {
        public static void Apply(IReadOnlyDictionary<string, string> settings)
        {
            if (settings == null)
            {
                return;
            }

            foreach (var pair in settings)
            {
                ApplyOne(pair.Key, pair.Value);
            }
        }

        private static void ApplyOne(string key, string value)
        {
            switch (key)
            {
                case "resolutionScale":
                {
                    // ponytail: URP RenderScale 대신 Screen.SetResolution 스케일 — 파이프라인 비의존.
                    // 반복 적용 시 누적 축소를 막기 위해 항상 디스플레이 원본 해상도 기준으로 계산.
                    if (TryParseDouble(value, out double scale) && scale > 0.0)
                    {
                        int width = (int)System.Math.Round(Display.main.systemWidth * scale);
                        int height = (int)System.Math.Round(Display.main.systemHeight * scale);
                        if (width > 0 && height > 0)
                        {
                            Screen.SetResolution(width, height, Screen.fullScreenMode);
                        }
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "shadows":
                {
                    if (TryParseOnOff(value, out bool shadowsOn))
                    {
                        QualitySettings.shadows = shadowsOn ? ShadowQuality.All : ShadowQuality.Disable;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "shadowDistance":
                {
                    if (TryParseDouble(value, out double distance))
                    {
                        QualitySettings.shadowDistance = (float)distance;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "msaa":
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int msaa))
                    {
                        QualitySettings.antiAliasing = msaa;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "pixelLightCount":
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lights))
                    {
                        QualitySettings.pixelLightCount = lights;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "textureQuality":
                {
                    // "full" = 원본 밉 레벨(제한 0), "half" = 1레벨 낮춤
                    if (value == "full")
                    {
                        QualitySettings.globalTextureMipmapLimit = 0;
                    }
                    else if (value == "half")
                    {
                        QualitySettings.globalTextureMipmapLimit = 1;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "lodBias":
                {
                    if (TryParseDouble(value, out double lodBias))
                    {
                        QualitySettings.lodBias = (float)lodBias;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "softParticles":
                {
                    if (TryParseOnOff(value, out bool soft))
                    {
                        QualitySettings.softParticles = soft;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                case "vsync":
                {
                    if (TryParseOnOff(value, out bool vsyncOn))
                    {
                        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;
                    }
                    else
                    {
                        WarnValue(key, value);
                    }
                    break;
                }

                default:
                {
                    Debug.LogWarning($"[Alchemist] 알 수 없는 설정 키 무시: '{key}' (value: '{value}')");
                    break;
                }
            }
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseOnOff(string value, out bool isOn)
        {
            if (value == "on")
            {
                isOn = true;
                return true;
            }

            if (value == "off")
            {
                isOn = false;
                return true;
            }

            isOn = false;
            return false;
        }

        private static void WarnValue(string key, string value)
        {
            Debug.LogWarning($"[Alchemist] 설정 값 파싱 실패로 무시: '{key}' = '{value}'");
        }
    }
}
