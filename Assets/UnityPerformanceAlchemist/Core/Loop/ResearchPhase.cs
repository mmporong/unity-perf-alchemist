namespace UnityPerformanceAlchemist.Core.Loop
{
    /// <summary>
    /// 설정-레벨 연구 루프의 단계 (계약 §2).
    /// </summary>
    public enum ResearchPhase
    {
        Idle,
        Baseline,
        Hypothesis,
        ApplySettings,
        Measure,
        Decide,
        Confirmation,
        Done
    }
}
