using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// 도메인 리로드 후 fenced 파일 재작성 루프 재개 — 기존 AlchemistRecoveryBootstrap 이식 + 해시 가드.
    /// fenced 러너 상태 파일이 있을 때만 동작한다
    /// (설정-레벨 루프 SettingsResearchRunner는 리로드 무발생 — 여기서 참조 0 유지).
    /// </summary>
    [InitializeOnLoad]
    public static class RecoveryBootstrap
    {
        static RecoveryBootstrap()
        {
            EditorApplication.delayCall += TryRecover;
        }

        private static void TryRecover()
        {
            var state = FsmStatePersistence.Load();
            if (state == null)
            {
                return;
            }

            if (state.phase == FsmStatePersistence.Phase.Idle || state.phase == FsmStatePersistence.Phase.Done)
            {
                FsmStatePersistence.Delete();
                return;
            }

            // 해시 가드: 재개 전 대상 스크립트의 SHA256을 상태 파일 기록과 비교.
            // 불일치(외부 수정/무관한 리로드) → 재개 대신 상태 정리 + 로그.
            // ponytail: 해시 가드는 무관한 리로드 오발동만 방지 — 완전한 세션 지문은 업그레이드 경로.
            if (!string.IsNullOrEmpty(state.targetScriptPath))
            {
                if (!File.Exists(state.targetScriptPath))
                {
                    Debug.LogWarning($"[Alchemist] 재개 중단: 대상 스크립트가 없습니다 ({state.targetScriptPath}). 상태를 정리합니다.");
                    FsmStatePersistence.Delete();
                    return;
                }
                if (!string.IsNullOrEmpty(state.targetScriptSha256))
                {
                    string actual = FsmStatePersistence.ComputeSha256(File.ReadAllText(state.targetScriptPath));
                    if (actual != state.targetScriptSha256)
                    {
                        Debug.LogWarning($"[Alchemist] 재개 중단: 대상 스크립트 해시 불일치 ({state.targetScriptPath}) — " +
                                         "외부 수정 또는 무관한 도메인 리로드로 판단, 상태를 정리합니다.");
                        FsmStatePersistence.Delete();
                        return;
                    }
                }
            }

            Debug.Log($"[Alchemist] 도메인 리로드 복구. Phase: {state.phase}, Gen: {state.generation}");

            if (state.phase == FsmStatePersistence.Phase.WriteFile_Committed)
            {
                // 컴파일 성공으로 리로드된 것 → 후보 코드 측정 단계로 진행
                state.phase = FsmStatePersistence.Phase.Benchmark;
                state.Save();
            }

            var options = new BenchmarkOptions
            {
                RunsPerCondition = state.runsPerCondition,
                FramesPerRun = state.framesPerRun,
                WarmupFrames = state.warmupFrames,
                WarmupSeconds = state.warmupSeconds,
            };
            var runner = new FileRewriteResearchRunner(CreateProvider(state), options);
            ResumeFireAndForget(runner, state);
        }

        private static ILlmProvider CreateProvider(FsmStatePersistence state)
        {
            if (state.llmProvider == "Gemini")
            {
                var gemini = new GeminiProvider();
                if (!string.IsNullOrEmpty(state.localModel))
                {
                    gemini.Model = state.localModel;
                }
                return gemini;
            }
            return new OllamaProvider { Endpoint = state.localEndpoint, Model = state.localModel };
        }

        private static async void ResumeFireAndForget(FileRewriteResearchRunner runner, FsmStatePersistence state)
        {
            try
            {
                await runner.ResumeAsync(state, null, CancellationToken.None);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Alchemist] 재개 실패: {e}");
                FsmStatePersistence.Delete();
            }
        }
    }
}
