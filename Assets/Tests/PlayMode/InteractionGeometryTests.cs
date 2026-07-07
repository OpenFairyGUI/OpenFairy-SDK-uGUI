using System.Collections;
using System.Collections.Generic;
using System.IO;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    // 交互几何 parity：实例化烘焙态 prefab，经真实 handler 把目标驱动到某交互态，settle 掉 gear 缓动后，
    // 把目标子树的几何快照与 FairyGUI 参照（由 Generate Golden References 生成）比。
    // 静态渲染 parity 已单独保证"任意状态渲成像素=FairyGUI"，故这里比几何不比像素。
    public class InteractionGeometryTests
    {
        public static IEnumerable<ParityCatalog.InteractionCase> Cases => ParityCatalog.Interactions;

        // gear 切页缓动默认 0.3s；40 帧(@captureDeltaTime 1/60 ≈0.67s)足够收敛，省去对 DOTween 的直接引用。
        private const int SettleFrames = 40;

        private NanamiPageRenderer _rig;

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
        public IEnumerator Matches_FairyGUI_geometry([ValueSource(nameof(Cases))] ParityCatalog.InteractionCase c)
        {
            var geoPath = ParityCatalog.GeometryPath(c);
            if (!File.Exists(geoPath))
                Assert.Fail($"No geometry reference for {c.Name}. Run Tools/NanamiUI/Generate Golden References.");

            _rig.LoadComponent(c.Package, c.Component);
            Assert.IsNotNull(_rig.Instance, $"Prefab missing: {ParityCatalog.PrefabPath(c.Package, c.Component)}");
            _rig.PlaceCamera();

            var target = _rig.Instance.transform.Find(c.Target);
            Assert.IsNotNull(target, $"Target '{c.Target}' not found under {c.Component}");

            InteractionDriver.Drive(target.gameObject, c.Action, c.Param, _rig.Camera);
            for (var i = 0; i < SettleFrames; i++)
                yield return null;
            Canvas.ForceUpdateCanvases();

            var actual = GeometrySnapshot.FromNanami(target);
            var reference = GeometrySnapshot.Load(geoPath);
            var diff = actual.Compare(reference, c.Epsilon);
            Assert.IsNull(diff, $"{c.Name}: {diff}");
        }
    }
}
