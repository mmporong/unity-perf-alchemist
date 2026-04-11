using UnityEditor;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    [InitializeOnLoad]
    public static class AlchemistRecoveryBootstrap
    {
        static AlchemistRecoveryBootstrap()
        {
            EditorApplication.delayCall += TryRecover;
        }

        private static void TryRecover()
        {
            var state = AlchemistFsmState.Load();
            if (state == null) return;

            if (state.phase == AlchemistFsmState.Phase.Idle || state.phase == AlchemistFsmState.Phase.Done)
            {
                AlchemistFsmState.Delete();
                return;
            }

            Debug.Log($"[Alchemist] Domain reload recovery. Phase: {state.phase}, Gen: {state.generation}");

            if (state.phase == AlchemistFsmState.Phase.WriteFile_Committed)
            {
                state.phase = AlchemistFsmState.Phase.Benchmark;
                state.Save();
            }

            var window = EditorWindow.GetWindow<PerformanceAlchemistWindow>("Alchemist Dashboard ");
            window.ResumeAfterDomainReload(state);
        }
    }
}
