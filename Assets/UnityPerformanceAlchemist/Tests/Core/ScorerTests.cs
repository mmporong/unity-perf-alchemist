using NUnit.Framework;
using UnityPerformanceAlchemist.Core.Loop;

namespace UnityPerformanceAlchemist.Tests.Core
{
    public class ScorerTests
    {
        // ---------------------------------------------------------------
        // Score — 기존 가중치로 손계산한 기대 점수
        // (Update 4, FixedUpdate 3, LateUpdate 2, for 2, foreach 2,
        //  GetComponent 3, FindObjectOfType 4, Instantiate 3, Destroy 2, new 1)
        // ---------------------------------------------------------------

        [Test]
        public void Score_Update_for_GetComponent()
        {
            // Update ×1(4) + for ×1(2) + GetComponent ×1(3) = 9
            string snippet =
                "public class Mover\n"
                + "{\n"
                + "    void Update()\n"
                + "    {\n"
                + "        for (int i = 0; i < 10; i++)\n"
                + "        {\n"
                + "            var t = GetComponent<Transform>();\n"
                + "        }\n"
                + "    }\n"
                + "}\n";
            Assert.That(ScriptScorer.Score(snippet), Is.EqualTo(9));
        }

        [Test]
        public void Score_FixedUpdate_foreach_Destroy()
        {
            // FixedUpdate ×1(3) + foreach ×1(2) + Destroy ×1(2) = 7
            // 주의: "FixedUpdate"가 Update 패턴(\bvoid\s+Update)에 이중 매칭되지 않아야 함
            string snippet =
                "void FixedUpdate()\n"
                + "{\n"
                + "    foreach (var e in entities)\n"
                + "    {\n"
                + "        Destroy(e);\n"
                + "    }\n"
                + "}\n";
            Assert.That(ScriptScorer.Score(snippet), Is.EqualTo(7));
        }

        [Test]
        public void Score_FindObjectOfType_Instantiate_new()
        {
            // FindObjectOfType ×1(4) + Instantiate ×1(3) + new ×1(1) = 8
            string snippet =
                "void Start()\n"
                + "{\n"
                + "    FindObjectOfType<Camera>();\n"
                + "    var go = Instantiate(prefab);\n"
                + "    var list = new List<int>();\n"
                + "}\n";
            Assert.That(ScriptScorer.Score(snippet), Is.EqualTo(8));
        }

        [Test]
        public void Score_LateUpdate만()
        {
            // LateUpdate ×1(2) = 2 — "LateUpdate"가 Update 패턴에 매칭되면 6이 되므로 회귀 가드
            Assert.That(ScriptScorer.Score("void LateUpdate()\n{\n}\n"), Is.EqualTo(2));
        }

        [Test]
        public void Score_키워드없는_내용은_0()
        {
            Assert.That(ScriptScorer.Score("// just a comment\n"), Is.EqualTo(0));
            Assert.That(ScriptScorer.Score(""), Is.EqualTo(0));
            Assert.That(ScriptScorer.Score(null), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------
        // IsExcludedPath — 기존 ScanAndScoreScripts 제외 규칙
        // ---------------------------------------------------------------

        [TestCase("Assets/Game/Editor/Tool.cs", true)]
        [TestCase("Assets/Game/Tests/FooTests.cs", true)]
        [TestCase("Assets/Game/Test/Foo.cs", true)]
        [TestCase("Assets/Game/Gen/Schema.g.cs", true)]
        [TestCase("Assets/Game/Forms/MyDesigner.cs", true)]
        [TestCase("Assets/UnityPerformanceAlchemist/Core/Loop/MiniJson.cs", true)]
        [TestCase("Assets/TextMesh Pro/Scripts/TMP_Text.cs", true)]
        [TestCase("Assets/Plugins/Lib.cs", true)]
        [TestCase("Assets/GoogleMobileAds/Api/Ad.cs", true)]
        [TestCase("Library/PackageCache/com.unity.x/Runtime/A.cs", true)]
        [TestCase("Assets/ThirdParty/Examples/Demo.cs", true)]
        [TestCase("Assets/Game/Player.cs", false)]
        [TestCase("Assets/Game/EditorConfigLoader.cs", false)] // "/Editor/" 폴더가 아니면 제외 아님
        public void IsExcludedPath_규칙(string normalizedPath, bool expected)
        {
            Assert.That(ScriptScorer.IsExcludedPath(normalizedPath), Is.EqualTo(expected));
        }

        [Test]
        public void IsExcludedPath_빈경로는_제외()
        {
            Assert.That(ScriptScorer.IsExcludedPath(""), Is.True);
            Assert.That(ScriptScorer.IsExcludedPath(null), Is.True);
        }
    }
}
