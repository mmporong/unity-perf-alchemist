using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityPerformanceAlchemist.Core.Statistics;

namespace UnityPerformanceAlchemist.Editor
{
    public sealed class BenchmarkOptions
    {
        public int RunsPerCondition = 10;
        public int FramesPerRun = 120;
        public int WarmupFrames = 30;
        public double WarmupSeconds = 1.0;
    }

    /// <summary>
    /// 에디터 플레이모드 벤치마크 러너. 각 런 = 플레이모드 진입 → 프레임 시계열 수집 →
    /// 플레이모드 종료 → SampleSet.ComputeRunMedian(워밍업 폐기 포함).
    /// 설정-레벨 헤드라인 경로: 파일 쓰기 / AssetDatabase.Refresh / 도메인 리로드 유발 금지.
    /// </summary>
    public static class EditorBenchmarkRunner
    {
        // 런당 워치독: 초과 시 플레이모드 강제 종료 + 해당 런 폐기(기록만 남김)
        private const double RunWatchdogSeconds = 30.0;
        private const double PlayModeExitWaitSeconds = 10.0;

        // N회 반복: 각 런 = 플레이모드 진입 → 워밍업 → FramesPerRun 프레임 수집 → 종료 → 런 중앙값
        public static async Task<SampleSet> MeasureAsync(BenchmarkOptions options, string configLabel,
                                                         bool isFresh = false, bool isInterleaved = false,
                                                         Func<Task> applySettingsBeforePlay = null,
                                                         CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var runMedians = new List<double>(options.RunsPerCondition);
            using (new DomainReloadGuard())
            {
                for (int run = 0; run < options.RunsPerCondition; run++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (applySettingsBeforePlay != null)
                    {
                        await applySettingsBeforePlay();
                    }

                    double? median = await MeasureSingleRunAsync(options);
                    if (median.HasValue)
                    {
                        runMedians.Add(median.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"[Alchemist] 런 {run + 1}/{options.RunsPerCondition} 폐기 " +
                                         $"(워치독 {RunWatchdogSeconds}s 초과 또는 표본 부족): '{configLabel}'");
                    }
                }
            }

            return new SampleSet(runMedians, configLabel, isFresh, isInterleaved);
        }

        // ABAB 인터리브: baseline·champion 세팅을 DeviceTierSimulator.Apply로 교차 적용하며 각 N런.
        // 반환 두 SampleSet 모두 IsFresh=true, IsInterleaved=true (확인 런 전용).
        public static async Task<(SampleSet baseline, SampleSet champion)> MeasureInterleavedAsync(
            BenchmarkOptions options,
            IReadOnlyDictionary<string, string> baselineSettings,
            IReadOnlyDictionary<string, string> championSettings,
            CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (baselineSettings == null)
            {
                throw new ArgumentNullException(nameof(baselineSettings));
            }
            if (championSettings == null)
            {
                throw new ArgumentNullException(nameof(championSettings));
            }

            var baselineMedians = new List<double>(options.RunsPerCondition);
            var championMedians = new List<double>(options.RunsPerCondition);
            DeviceTierSimulator.Snapshot snapshot = DeviceTierSimulator.Capture();
            using (new DomainReloadGuard())
            try
            {
                for (int run = 0; run < options.RunsPerCondition; run++)
                {
                    ct.ThrowIfCancellationRequested();
                    // A: baseline
                    DeviceTierSimulator.Apply(baselineSettings);
                    double? baselineMedian = await MeasureSingleRunAsync(options);
                    if (baselineMedian.HasValue)
                    {
                        baselineMedians.Add(baselineMedian.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"[Alchemist] 인터리브 baseline 런 {run + 1} 폐기 (워치독/표본 부족)");
                    }

                    // B: champion
                    DeviceTierSimulator.Apply(championSettings);
                    double? championMedian = await MeasureSingleRunAsync(options);
                    if (championMedian.HasValue)
                    {
                        championMedians.Add(championMedian.Value);
                    }
                    else
                    {
                        Debug.LogWarning($"[Alchemist] 인터리브 champion 런 {run + 1} 폐기 (워치독/표본 부족)");
                    }
                }
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
            }

            return (new SampleSet(baselineMedians, "baseline", isFresh: true, isInterleaved: true),
                    new SampleSet(championMedians, "champion", isFresh: true, isInterleaved: true));
        }

        // 단일 런. 실패(워치독/표본 부족) 시 null = 런 폐기.
        private static async Task<double?> MeasureSingleRunAsync(BenchmarkOptions options)
        {
            double startTime = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;

            // 플레이모드 진입 대기 — 기존 코드 패턴(Task.Yield 폴링)
            while (!EditorApplication.isPlaying || !Application.isPlaying)
            {
                if (IsTimedOut(startTime))
                {
                    return await AbortRunAsync();
                }
                await Task.Yield();
            }

            // 워밍업 프레임도 함께 수집 — ComputeRunMedian이 max(WarmupFrames, WarmupSeconds 커버분)을 폐기한다
            int totalFrames = options.WarmupFrames + options.FramesPerRun;
            IReadOnlyList<double> frameTimesMs;
            using (ProfilerSampling sampling = ProfilerSampling.Start(totalFrames))
            {
                while (sampling.FrameCount < totalFrames)
                {
                    if (IsTimedOut(startTime))
                    {
                        return await AbortRunAsync();
                    }
                    await Task.Yield();
                }
                frameTimesMs = sampling.ReadFrameTimesMs();
            }

            EditorApplication.isPlaying = false;
            await WaitForPlayModeExitAsync();

            try
            {
                return SampleSet.ComputeRunMedian(frameTimesMs, options.WarmupFrames, options.WarmupSeconds);
            }
            catch (ArgumentException)
            {
                // 워밍업 폐기 후 남는 프레임 0개 → 런 폐기
                return null;
            }
        }

        private static bool IsTimedOut(double startTime)
        {
            return EditorApplication.timeSinceStartup - startTime > RunWatchdogSeconds;
        }

        // 워치독 초과: 플레이모드 강제 종료 + 런 폐기(null)
        private static async Task<double?> AbortRunAsync()
        {
            EditorApplication.isPlaying = false;
            await WaitForPlayModeExitAsync();
            return null;
        }

        private static async Task WaitForPlayModeExitAsync()
        {
            double exitStart = EditorApplication.timeSinceStartup;
            while (Application.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - exitStart > PlayModeExitWaitSeconds)
                {
                    Debug.LogWarning("[Alchemist] 플레이모드 종료 대기 시간 초과 — 계속 진행");
                    break;
                }
                await Task.Yield();
            }
        }

        /// <summary>
        /// 플레이모드 진입 시 도메인 리로드가 켜져 있으면 async continuation과 정적 상태가 파괴되어
        /// 측정 루프가 재개되지 못한다(에디터가 플레이모드에 갇힘). 측정 동안 Enter Play Mode Options로
        /// 도메인 리로드를 끄고, 종료 시 사용자 원래 설정으로 복원한다.
        /// </summary>
        private sealed class DomainReloadGuard : IDisposable
        {
            private readonly bool savedEnabled;
            private readonly EnterPlayModeOptions savedOptions;
            private readonly bool changed;

            public DomainReloadGuard()
            {
                savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
                savedOptions = EditorSettings.enterPlayModeOptions;

                bool reloadDisabled = savedEnabled &&
                    (savedOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
                if (!reloadDisabled)
                {
                    EditorSettings.enterPlayModeOptionsEnabled = true;
                    EditorSettings.enterPlayModeOptions = savedOptions | EnterPlayModeOptions.DisableDomainReload;
                    changed = true;
                    Debug.Log("[Alchemist] 측정 동안 Enter Play Mode 도메인 리로드를 비활성화합니다 (종료 시 복원).");
                }
            }

            public void Dispose()
            {
                if (changed)
                {
                    EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
                    EditorSettings.enterPlayModeOptions = savedOptions;
                }
            }
        }
    }
}
