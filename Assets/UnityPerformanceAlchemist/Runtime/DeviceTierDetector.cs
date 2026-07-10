using UnityEngine;

namespace UnityPerformanceAlchemist.Runtime
{
    public static class DeviceTierDetector
    {
        // ponytail: 휴리스틱 근사 — 정확한 매핑은 기기 테이블/MPC 조회가 업그레이드 경로
        public static string DetectTierId()
        {
            int systemMemoryMb = SystemInfo.systemMemorySize;
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            int processorCount = SystemInfo.processorCount;

            int score = 0;

            if (systemMemoryMb >= 8192)
            {
                score += 2;
            }
            else if (systemMemoryMb >= 4096)
            {
                score += 1;
            }

            if (graphicsMemoryMb >= 4096)
            {
                score += 2;
            }
            else if (graphicsMemoryMb >= 2048)
            {
                score += 1;
            }

            if (processorCount >= 8)
            {
                score += 2;
            }
            else if (processorCount >= 4)
            {
                score += 1;
            }

            if (score >= 5)
            {
                return "high";
            }

            if (score >= 2)
            {
                return "mid";
            }

            return "low";
        }
    }
}
