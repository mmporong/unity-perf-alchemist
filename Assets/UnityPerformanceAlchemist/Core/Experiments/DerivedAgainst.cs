namespace UnityPerformanceAlchemist.Core.Experiments
{
    // Unity SO 직렬화 호환을 위해 이 클래스만 public 필드 소문자 camelCase 허용 (계약 §3).
    [System.Serializable]
    public sealed class DerivedAgainst
    {
        public string gameBuildVersion;
        public string upaSchemaVersion;
        public string patchId;
        public string derivedAtUtc;   // ISO-8601, Editor 경계에서 주입 (Core 시계 접근 0)
    }
}
