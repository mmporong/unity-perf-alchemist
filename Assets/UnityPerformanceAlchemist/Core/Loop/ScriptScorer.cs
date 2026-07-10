using System.Text.RegularExpressions;

namespace UnityPerformanceAlchemist.Core.Loop
{
    /// <summary>
    /// fenced 모드용 스크립트 점수화 — 기존 Editor/PerformanceAlchemist.cs의
    /// ScoreScript 가중치와 ScanAndScoreScripts 경로 제외 규칙을 그대로 이식.
    /// File IO는 Core에 두지 않음: 내용/경로 문자열만 받는다.
    /// </summary>
    public static class ScriptScorer
    {
        // 기존 regex 가중치 그대로 (Update 4, FixedUpdate 3, LateUpdate 2, for 2, foreach 2,
        // GetComponent 3, FindObjectOfType 4, Instantiate 3, Destroy 2, new 1).
        public static int Score(string csFileContent)
        {
            if (string.IsNullOrEmpty(csFileContent))
            {
                return 0;
            }

            int score = 0;
            score += Regex.Matches(csFileContent, @"\bvoid\s+Update\s*\(").Count * 4;
            score += Regex.Matches(csFileContent, @"\bvoid\s+FixedUpdate\s*\(").Count * 3;
            score += Regex.Matches(csFileContent, @"\bvoid\s+LateUpdate\s*\(").Count * 2;
            score += Regex.Matches(csFileContent, @"\bfor\s*\(").Count * 2;
            score += Regex.Matches(csFileContent, @"\bforeach\s*\(").Count * 2;
            score += Regex.Matches(csFileContent, @"\bGetComponent\s*[<(]").Count * 3;
            score += Regex.Matches(csFileContent, @"\bFindObjectOfType\b").Count * 4;
            score += Regex.Matches(csFileContent, @"\bInstantiate\s*\(").Count * 3;
            score += Regex.Matches(csFileContent, @"\bDestroy\s*\(").Count * 2;
            score += Regex.Matches(csFileContent, @"\bnew\s+\w").Count;
            return score;
        }

        // 기존 제외 규칙 그대로: Editor 전용, 테스트, 자동생성, Alchemist 자신, 서드파티.
        // normalizedPath는 '\\'→'/' 정규화된 경로여야 한다(호출부 책임).
        public static bool IsExcludedPath(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return true;
            }
            if (normalizedPath.Contains("/Editor/"))
            {
                return true;
            }
            if (normalizedPath.Contains("/Tests/") || normalizedPath.Contains("/Test/"))
            {
                return true;
            }
            if (normalizedPath.EndsWith(".g.cs") || normalizedPath.EndsWith("Designer.cs"))
            {
                return true;
            }
            if (normalizedPath.Contains("UnityPerformanceAlchemist"))
            {
                return true;
            }
            if (normalizedPath.Contains("/TextMesh Pro/"))
            {
                return true;
            }
            if (normalizedPath.Contains("/Plugins/"))
            {
                return true;
            }
            if (normalizedPath.Contains("/GoogleMobileAds/"))
            {
                return true;
            }
            if (normalizedPath.Contains("/PackageCache/"))
            {
                return true;
            }
            if (normalizedPath.Contains("/Examples"))
            {
                return true;
            }
            return false;
        }
    }
}
