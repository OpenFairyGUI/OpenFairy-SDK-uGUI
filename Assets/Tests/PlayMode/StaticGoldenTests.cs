using System.Collections;
using System.Collections.Generic;
using System.IO;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    // 静态 golden 回归：逐页渲 NanamiUI 末帧，与提交的 FairyGUI golden PNG 比，差异占比超每页阈值即失败。
    // golden 由 Tools/NanamiUI/Promote FairyGUI Captures to Golden 生成（缺图的页跳过）。
    public class StaticGoldenTests
    {
        public static IEnumerable<ParityPage> Pages => ParityCatalog.StaticPages;

        private NanamiPageRenderer _rig;

        // 用 [UnitySetUp]/[UnityTearDown]（协程、在 play mode 内跑）建/拆渲染台，
        // 不用 [OneTimeSetUp]——它在进入 play mode 之前执行，那时 SceneManager 等 play-only API 不可用。
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new NanamiPageRenderer();
            _rig.Setup();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig.Unload();
            _rig.Teardown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Matches_FairyGUI_golden([ValueSource(nameof(Pages))] ParityPage page)
        {
            var goldenPath = ParityCatalog.GoldenPath(page);
            if (!File.Exists(goldenPath))
                Assert.Ignore($"No golden for {page.Name}. Run Tools/NanamiUI/Promote FairyGUI Captures to Golden.");

            _rig.LoadPage(page);
            for (var i = 0; i < ParityCatalog.SettleFrames; i++)
                yield return null;

            var actual = _rig.Capture();
            var golden = GoldenImage.Load(goldenPath);
            var ratio = GoldenImage.DiffRatio(actual, golden);
            Object.DestroyImmediate(actual);
            Object.DestroyImmediate(golden);

            Assert.LessOrEqual(ratio, page.Threshold,
                $"{page.Name}: diff {ratio:P2} exceeds threshold {page.Threshold:P2}");
        }
    }
}
