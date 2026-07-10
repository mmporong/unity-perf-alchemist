using System.Collections.Generic;

namespace UnityPerformanceAlchemist.Core.Loop
{
    /// <summary>
    /// 연구 루프의 순수 데이터 모델 (계약 §2 — 필드 고정, 사이드이펙트 없음).
    /// </summary>
    public sealed class ResearchStateModel
    {
        public ResearchPhase Phase;
        public int Generation;
        public int MaxGenerations;
        public IReadOnlyDictionary<string, string> BestSettings;      // 설정 키 → 값(문자열 표준화)
        public IReadOnlyDictionary<string, string> PendingSettings;
        public string TierId;
    }

    /// <summary>
    /// 루프 전이를 일으키는 이벤트 (계약 §2).
    /// </summary>
    public enum StepEvent
    {
        Start,
        BaselineMeasured,
        HypothesisReady,
        HypothesisFailed,
        SettingsApplied,
        Measured,
        Adopted,
        RolledBack,
        GenerationsExhausted,
        ConfirmationDone,
        Stop
    }

    /// <summary>
    /// 순수 전이 함수 — 전이표는 계약 §2에 고정되어 있고 LoopTests가 전 조합을 검증한다.
    /// </summary>
    public static class ResearchTransitions
    {
        // 순수 함수: (phase, event, generation, maxGenerations) → next phase. 사이드이펙트 0.
        // 전이표: Idle+Start→Baseline; Baseline+BaselineMeasured→Hypothesis;
        //         Hypothesis+HypothesisReady→ApplySettings;
        //         Hypothesis+HypothesisFailed→(gen<max? Hypothesis : Confirmation);
        //         ApplySettings+SettingsApplied→Measure; Measure+Measured→Decide;
        //         Decide+(Adopted|RolledBack)→(gen<max? Hypothesis : Confirmation);
        //         *+GenerationsExhausted→Confirmation; Confirmation+ConfirmationDone→Done; *+Stop→Done.
        // 전이표에 없는 조합은 현재 phase 유지(무시).
        public static ResearchPhase Next(ResearchPhase current, StepEvent evt, int generation, int maxGenerations)
        {
            if (evt == StepEvent.Stop)
            {
                return ResearchPhase.Done;
            }
            if (evt == StepEvent.GenerationsExhausted)
            {
                return ResearchPhase.Confirmation;
            }

            switch (current)
            {
                case ResearchPhase.Idle:
                    if (evt == StepEvent.Start)
                    {
                        return ResearchPhase.Baseline;
                    }
                    return current;

                case ResearchPhase.Baseline:
                    if (evt == StepEvent.BaselineMeasured)
                    {
                        return ResearchPhase.Hypothesis;
                    }
                    return current;

                case ResearchPhase.Hypothesis:
                    if (evt == StepEvent.HypothesisReady)
                    {
                        return ResearchPhase.ApplySettings;
                    }
                    if (evt == StepEvent.HypothesisFailed)
                    {
                        return generation < maxGenerations ? ResearchPhase.Hypothesis : ResearchPhase.Confirmation;
                    }
                    return current;

                case ResearchPhase.ApplySettings:
                    if (evt == StepEvent.SettingsApplied)
                    {
                        return ResearchPhase.Measure;
                    }
                    return current;

                case ResearchPhase.Measure:
                    if (evt == StepEvent.Measured)
                    {
                        return ResearchPhase.Decide;
                    }
                    return current;

                case ResearchPhase.Decide:
                    if (evt == StepEvent.Adopted || evt == StepEvent.RolledBack)
                    {
                        return generation < maxGenerations ? ResearchPhase.Hypothesis : ResearchPhase.Confirmation;
                    }
                    return current;

                case ResearchPhase.Confirmation:
                    if (evt == StepEvent.ConfirmationDone)
                    {
                        return ResearchPhase.Done;
                    }
                    return current;

                default:
                    return current;
            }
        }
    }
}
