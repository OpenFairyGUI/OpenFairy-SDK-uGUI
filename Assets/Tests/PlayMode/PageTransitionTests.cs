using System.Collections;
using System.IO;
using System.Text;
using OpenFairy.UGUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    // 翻页动效回归：Main 的 c1 从 0→1（进 Demo）时，btns 组本应"飞出去再消失"（gearDisplay 隐藏 + gearXY 缓动，
    // FairyGUI 用 display lock 延迟隐藏）。修复前 OpenFairy.UGUI 立即 SetActive(false) → 瞬间消失。这里断言 btns 在
    // 滑出途中保持 active，缓动结束后才隐藏。轨迹写 Temp/OpenFairy.UGUIPageTransition.txt 供人看。
    public class PageTransitionTests
    {
        private OpenFairyPageRenderer _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new OpenFairyPageRenderer();
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
        public IEnumerator Btns_flies_out_before_hiding()
        {
            _rig.LoadComponent("Basics", "Main"); // 烘焙态 = 页 0：btns active @x≈86，container inactive @x≈-1143
            Assert.IsNotNull(_rig.Instance);
            var btns = (RectTransform)_rig.Instance.transform.Find("btns");
            var container = (RectTransform)_rig.Instance.transform.Find("container");
            Assert.IsNotNull(btns, "btns group not found");
            Assert.IsNotNull(container, "container not found");

            InteractionDriver.DriveControllerPage(_rig.Instance, "c1", "_1"); // 进 Demo：btns 滑出+隐藏，container 滑入+显示

            var log = new StringBuilder();
            var sawBtnsActiveMidSlide = false;
            var sawContainerActiveMidSlide = false;
            for (var i = 0; i < 45; i++)
            {
                yield return null;
                var bx = btns.anchoredPosition.x;
                var cx = container.anchoredPosition.x;
                var ba = btns.gameObject.activeSelf;
                var ca = container.gameObject.activeSelf;
                log.AppendLine($"f{i,2}: btns active={ba} x={bx,7:F1} | container active={ca} x={cx,7:F1}");
                if (ba && bx > 120 && bx < 1120) sawBtnsActiveMidSlide = true;      // 滑出途中仍显示
                if (ca && cx > -1120 && cx < -20) sawContainerActiveMidSlide = true; // 滑入途中已显示
            }
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "OpenFairy.UGUIPageTransition.txt"), log.ToString());

            Assert.IsTrue(sawBtnsActiveMidSlide, "btns 应在滑出途中保持 active（飞出），而非立即消失。\n" + log);
            Assert.IsTrue(sawContainerActiveMidSlide, "container 应在滑入途中已显示。\n" + log);
            Assert.IsFalse(btns.gameObject.activeSelf, "btns 应在缓动结束后隐藏。\n" + log);
            Assert.IsTrue(container.gameObject.activeSelf, "container 应在缓动结束后显示。\n" + log);
            Assert.AreEqual(0f, container.anchoredPosition.x, 1.5f, "container 应停在 x=0。\n" + log);
        }
    }
}
