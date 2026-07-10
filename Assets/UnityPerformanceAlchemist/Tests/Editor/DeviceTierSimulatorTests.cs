using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Experiments;
using UnityPerformanceAlchemist.Editor;

namespace UnityPerformanceAlchemist.Tests.Editor
{
    // EditMode 스모크: 플레이모드 불요 케이스만 (계약 §6).
    public class DeviceTierSimulatorTests
    {
        [Test]
        public void CaptureApplyRestore_RoundTrip_VerifyRestoredTrue()
        {
            DeviceTierSimulator.Snapshot snapshot = DeviceTierSimulator.Capture();
            try
            {
                IReadOnlyDictionary<string, string> lowProxy =
                    DeviceTierProfile.Defaults.First(t => t.TierId == "low").QualityProxy;

                DeviceTierSimulator.Apply(lowProxy);
                DeviceTierSimulator.Restore(snapshot);

                Assert.That(DeviceTierSimulator.VerifyRestored(snapshot), Is.True);
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
            }
        }

        [Test]
        public void Apply_AllNineTechniqueKeys_DoesNotThrowAndRestores()
        {
            DeviceTierSimulator.Snapshot snapshot = DeviceTierSimulator.Capture();
            try
            {
                // TechniqueRegistry 9키 전부 OffValue로 적용 — 매핑 누락 시 여기서 드러난다
                var allOff = TechniqueRegistry.All.ToDictionary(t => t.SettingKey, t => t.OffValue);

                Assert.That(() => DeviceTierSimulator.Apply(allOff), Throws.Nothing);

                DeviceTierSimulator.Restore(snapshot);
                Assert.That(DeviceTierSimulator.VerifyRestored(snapshot), Is.True);
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
            }
        }

        [Test]
        public void Apply_UnknownKey_IsIgnoredAndStateUnchanged()
        {
            DeviceTierSimulator.Snapshot snapshot = DeviceTierSimulator.Capture();
            try
            {
                var unknownOnly = new Dictionary<string, string>
                {
                    ["definitelyNotARealSettingKey"] = "42",
                };

                Assert.That(() => DeviceTierSimulator.Apply(unknownOnly), Throws.Nothing);

                // 알 수 없는 키는 아무 상태도 건드리지 않으므로 스냅샷과 그대로 일치해야 한다
                Assert.That(DeviceTierSimulator.VerifyRestored(snapshot), Is.True);
            }
            finally
            {
                DeviceTierSimulator.Restore(snapshot);
            }
        }
    }
}
