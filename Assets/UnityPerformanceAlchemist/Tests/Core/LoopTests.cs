using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Loop;

namespace UnityPerformanceAlchemist.Tests.Core
{
    public class LoopTests
    {
        // ---------------------------------------------------------------
        // ResearchTransitions — 계약 §2 전이표 전 조합 고정
        // ---------------------------------------------------------------

        // 전이표에 명시된 (phase, event) 쌍 — 이 밖의 조합은 현재 phase 유지여야 한다.
        private static readonly HashSet<(ResearchPhase, StepEvent)> DefinedPairs =
            new HashSet<(ResearchPhase, StepEvent)>
            {
                (ResearchPhase.Idle, StepEvent.Start),
                (ResearchPhase.Baseline, StepEvent.BaselineMeasured),
                (ResearchPhase.Hypothesis, StepEvent.HypothesisReady),
                (ResearchPhase.Hypothesis, StepEvent.HypothesisFailed),
                (ResearchPhase.ApplySettings, StepEvent.SettingsApplied),
                (ResearchPhase.Measure, StepEvent.Measured),
                (ResearchPhase.Decide, StepEvent.Adopted),
                (ResearchPhase.Decide, StepEvent.RolledBack),
                (ResearchPhase.Confirmation, StepEvent.ConfirmationDone),
            };

        [TestCase(ResearchPhase.Idle, StepEvent.Start, ResearchPhase.Baseline)]
        [TestCase(ResearchPhase.Baseline, StepEvent.BaselineMeasured, ResearchPhase.Hypothesis)]
        [TestCase(ResearchPhase.Hypothesis, StepEvent.HypothesisReady, ResearchPhase.ApplySettings)]
        [TestCase(ResearchPhase.ApplySettings, StepEvent.SettingsApplied, ResearchPhase.Measure)]
        [TestCase(ResearchPhase.Measure, StepEvent.Measured, ResearchPhase.Decide)]
        [TestCase(ResearchPhase.Confirmation, StepEvent.ConfirmationDone, ResearchPhase.Done)]
        public void Next_고정전이(ResearchPhase current, StepEvent evt, ResearchPhase expected)
        {
            // 세대 수와 무관한 전이 — 양쪽 세대 조건에서 동일해야 한다.
            Assert.That(ResearchTransitions.Next(current, evt, 1, 3), Is.EqualTo(expected));
            Assert.That(ResearchTransitions.Next(current, evt, 3, 3), Is.EqualTo(expected));
        }

        [TestCase(StepEvent.HypothesisFailed, ResearchPhase.Hypothesis)]
        [TestCase(StepEvent.Adopted, ResearchPhase.Decide)]
        [TestCase(StepEvent.RolledBack, ResearchPhase.Decide)]
        public void Next_세대의존전이(StepEvent evt, ResearchPhase current)
        {
            // gen < max → Hypothesis 로 계속, gen >= max → Confirmation 으로 종결
            Assert.That(ResearchTransitions.Next(current, evt, 1, 3), Is.EqualTo(ResearchPhase.Hypothesis));
            Assert.That(ResearchTransitions.Next(current, evt, 3, 3), Is.EqualTo(ResearchPhase.Confirmation));
            Assert.That(ResearchTransitions.Next(current, evt, 4, 3), Is.EqualTo(ResearchPhase.Confirmation));
        }

        [Test]
        public void Next_와일드카드_Stop은_모든_phase에서_Done()
        {
            foreach (ResearchPhase phase in (ResearchPhase[])Enum.GetValues(typeof(ResearchPhase)))
            {
                Assert.That(ResearchTransitions.Next(phase, StepEvent.Stop, 1, 3),
                            Is.EqualTo(ResearchPhase.Done), phase.ToString());
            }
        }

        [Test]
        public void Next_와일드카드_GenerationsExhausted는_모든_phase에서_Confirmation()
        {
            foreach (ResearchPhase phase in (ResearchPhase[])Enum.GetValues(typeof(ResearchPhase)))
            {
                Assert.That(ResearchTransitions.Next(phase, StepEvent.GenerationsExhausted, 1, 3),
                            Is.EqualTo(ResearchPhase.Confirmation), phase.ToString());
            }
        }

        [Test]
        public void Next_전이표_밖_조합은_전부_현재_phase_유지()
        {
            // 전 조합(8 phase × 11 event × 세대 조건 2가지) 중 표에 없는 조합 전수 검사
            foreach (ResearchPhase phase in (ResearchPhase[])Enum.GetValues(typeof(ResearchPhase)))
            {
                foreach (StepEvent evt in (StepEvent[])Enum.GetValues(typeof(StepEvent)))
                {
                    if (evt == StepEvent.Stop || evt == StepEvent.GenerationsExhausted)
                    {
                        continue; // 와일드카드는 별도 테스트
                    }
                    if (DefinedPairs.Contains((phase, evt)))
                    {
                        continue; // 정의된 전이는 별도 테스트
                    }
                    foreach (int generation in new[] { 1, 3 })
                    {
                        Assert.That(ResearchTransitions.Next(phase, evt, generation, 3),
                                    Is.EqualTo(phase), $"{phase} + {evt} (gen={generation})");
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // ToggleHypothesisParser — fixture (계약 §2, §6)
        // ---------------------------------------------------------------

        [Test]
        public void 파서_마크다운펜스_응답()
        {
            string response = "```json\n{\"strategy\": \"disable shadows\", \"settings\": {\"shadows\": \"off\"}}\n```";
            Assert.That(ToggleHypothesisParser.TryParse(response, out ToggleHypothesis h), Is.True);
            Assert.That(h.Strategy, Is.EqualTo("disable shadows"));
            Assert.That(h.Settings["shadows"], Is.EqualTo("off"));
            Assert.That(h.Settings.Count, Is.EqualTo(1));
        }

        [Test]
        public void 파서_펜스없는_순수JSON_응답()
        {
            string response = "{\"strategy\": \"reduce msaa\", \"settings\": {\"msaa\": \"0\"}}";
            Assert.That(ToggleHypothesisParser.TryParse(response, out ToggleHypothesis h), Is.True);
            Assert.That(h.Strategy, Is.EqualTo("reduce msaa"));
            Assert.That(h.Settings["msaa"], Is.EqualTo("0"));
        }

        [Test]
        public void 파서_앞뒤잡담_잡중괄호_섞인_응답()
        {
            string response = "Sure! My plan {not valid json} is below.\n"
                + "{\"strategy\": \"lower msaa\", \"settings\": {\"msaa\": 0}}\n"
                + "Hope this helps!";
            Assert.That(ToggleHypothesisParser.TryParse(response, out ToggleHypothesis h), Is.True);
            Assert.That(h.Strategy, Is.EqualTo("lower msaa"));
            Assert.That(h.Settings["msaa"], Is.EqualTo("0"));
        }

        [Test]
        public void 파서_JSON없는_응답은_실패()
        {
            Assert.That(ToggleHypothesisParser.TryParse("I cannot help with that.", out _), Is.False);
            Assert.That(ToggleHypothesisParser.TryParse("", out _), Is.False);
            Assert.That(ToggleHypothesisParser.TryParse(null, out _), Is.False);
        }

        [Test]
        public void 파서_settings_누락_또는_빈오브젝트는_실패()
        {
            Assert.That(ToggleHypothesisParser.TryParse("{\"strategy\": \"x\"}", out _), Is.False);
            Assert.That(ToggleHypothesisParser.TryParse("{\"strategy\": \"x\", \"settings\": {}}", out _), Is.False);
        }

        [Test]
        public void 파서_잘린_부분JSON은_실패()
        {
            string response = "{\"strategy\": \"x\", \"settings\": {\"shadows\":";
            Assert.That(ToggleHypothesisParser.TryParse(response, out _), Is.False);
        }

        [Test]
        public void 파서_숫자_불리언_값은_문자열로_표준화()
        {
            string response = "{\"settings\": {\"msaa\": 0, \"vsync\": true, \"lodBias\": 0.7}}";
            Assert.That(ToggleHypothesisParser.TryParse(response, out ToggleHypothesis h), Is.True);
            Assert.That(h.Strategy, Is.EqualTo("(no strategy)")); // strategy 누락 → 기본값
            Assert.That(h.Settings["msaa"], Is.EqualTo("0"));
            Assert.That(h.Settings["vsync"], Is.EqualTo("true"));
            Assert.That(h.Settings["lodBias"], Is.EqualTo("0.7"));
        }

        [Test]
        public void 파서_중첩값이_섞인_오브젝트에서도_settings_추출()
        {
            string response = "{\"strategy\": \"toggle shadows\", \"meta\": {\"tried\": [1, 2]}, "
                + "\"settings\": {\"shadows\": \"off\", \"shadowDistance\": 15}}";
            Assert.That(ToggleHypothesisParser.TryParse(response, out ToggleHypothesis h), Is.True);
            Assert.That(h.Strategy, Is.EqualTo("toggle shadows"));
            Assert.That(h.Settings["shadows"], Is.EqualTo("off"));
            Assert.That(h.Settings["shadowDistance"], Is.EqualTo("15"));
            Assert.That(h.Settings.Count, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------
        // MiniJson 단위 케이스 (InternalsVisibleTo("UPA.Tests.Core"))
        // ---------------------------------------------------------------

        [Test]
        public void MiniJson_중첩_오브젝트_배열()
        {
            Assert.That(MiniJson.TryParse("{\"a\": [1, 2, {\"b\": null}], \"c\": true}", out object v), Is.True);
            var obj = (Dictionary<string, object>)v;
            var list = (List<object>)obj["a"];
            Assert.That(list.Count, Is.EqualTo(3));
            Assert.That(list[0], Is.EqualTo(1.0));
            Assert.That(list[1], Is.EqualTo(2.0));
            Assert.That(((Dictionary<string, object>)list[2])["b"], Is.Null);
            Assert.That(obj["c"], Is.EqualTo(true));
        }

        [Test]
        public void MiniJson_유니코드_이스케이프()
        {
            Assert.That(MiniJson.TryParse("\"\\ud55c\\uAE00\"", out object v), Is.True);
            Assert.That(v, Is.EqualTo("한글"));
        }

        [Test]
        public void MiniJson_문자열_이스케이프()
        {
            Assert.That(MiniJson.TryParse("\"a\\nb\\t\\\"q\\\"\\\\\"", out object v), Is.True);
            Assert.That(v, Is.EqualTo("a\nb\t\"q\"\\"));
        }

        [Test]
        public void MiniJson_숫자_지수표기_음수()
        {
            Assert.That(MiniJson.TryParse("-1.5e2", out object v), Is.True);
            Assert.That((double)v, Is.EqualTo(-150.0));
            Assert.That(MiniJson.TryParse("0.147", out object w), Is.True);
            Assert.That((double)w, Is.EqualTo(0.147));
        }

        [Test]
        public void MiniJson_리터럴()
        {
            Assert.That(MiniJson.TryParse("true", out object t), Is.True);
            Assert.That(t, Is.EqualTo(true));
            Assert.That(MiniJson.TryParse("false", out object f), Is.True);
            Assert.That(f, Is.EqualTo(false));
            Assert.That(MiniJson.TryParse("null", out object n), Is.True);
            Assert.That(n, Is.Null);
        }

        [Test]
        public void MiniJson_비정상_입력은_실패()
        {
            Assert.That(MiniJson.TryParse("{", out _), Is.False);
            Assert.That(MiniJson.TryParse("{\"a\":}", out _), Is.False);
            Assert.That(MiniJson.TryParse("[1,]", out _), Is.False);
            Assert.That(MiniJson.TryParse("{} trailing", out _), Is.False); // 전체 파싱은 꼬리 잡문 불허
            Assert.That(MiniJson.TryParse("\"unterminated", out _), Is.False);
            Assert.That(MiniJson.TryParse(null, out _), Is.False);
        }
    }
}
