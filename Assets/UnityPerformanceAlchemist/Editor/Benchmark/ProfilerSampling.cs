using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>
    /// ProfilerRecorder 래핑 — 프레임타임(ms) 수집만 공개 노출.
    /// GC 카운터는 가용성(Valid) 확인 후에만 노출한다. 허구 지표 금지:
    /// 수집 불가 환경에서 0이나 추정치를 지어내지 않고 조용히 미노출한다.
    /// </summary>
    public sealed class ProfilerSampling : IDisposable
    {
        private ProfilerRecorder frameTimeRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private bool isDisposed;

        private ProfilerSampling(int frameCapacity)
        {
            // "Main Thread" (ProfilerCategory.Internal) = 프레임당 메인 스레드 시간(ns)
            frameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", frameCapacity);

            // GC 카운터 가용성 확인 — 에디터/플랫폼에 따라 미지원일 수 있음
            try
            {
                gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            }
            catch (Exception)
            {
                gcAllocRecorder = default;
            }
        }

        /// <summary>수집 시작. frameCapacity = 보관할 최대 프레임 표본 수(링 버퍼).</summary>
        public static ProfilerSampling Start(int frameCapacity)
        {
            return new ProfilerSampling(frameCapacity);
        }

        /// <summary>지금까지 수집된 프레임 표본 수.</summary>
        public int FrameCount
        {
            get { return frameTimeRecorder.Valid ? frameTimeRecorder.Count : 0; }
        }

        /// <summary>GC 카운터 가용 여부. false면 GC 지표는 존재하지 않는 것으로 취급한다.</summary>
        public bool IsGcAvailable
        {
            get { return gcAllocRecorder.Valid; }
        }

        /// <summary>수집된 프레임타임 시계열(ms). ns → ms 변환.</summary>
        public IReadOnlyList<double> ReadFrameTimesMs()
        {
            int count = FrameCount;
            var result = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(frameTimeRecorder.GetSample(i).Value * 1e-6);
            }
            return result;
        }

        /// <summary>직전 프레임 GC 할당 바이트. 카운터 미가용 시 false — 값을 지어내지 않는다.</summary>
        public bool TryReadGcAllocatedBytesLastFrame(out long bytes)
        {
            if (gcAllocRecorder.Valid && gcAllocRecorder.Count > 0)
            {
                bytes = gcAllocRecorder.LastValue;
                return true;
            }
            bytes = 0;
            return false;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            if (frameTimeRecorder.Valid)
            {
                frameTimeRecorder.Dispose();
            }
            if (gcAllocRecorder.Valid)
            {
                gcAllocRecorder.Dispose();
            }
        }
    }
}
