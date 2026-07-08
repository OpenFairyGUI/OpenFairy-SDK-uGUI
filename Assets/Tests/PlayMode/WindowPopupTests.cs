using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    // GRoot 覆盖层 + PopupMenu + Window 的运行时机制回归。定位/居中/尺寸是分析精确值；交互直接调 handler（不经 raycast，
    // 因 golden rig 是 WorldSpace + 禁用相机，raycast 命中不可靠）。
    public class WindowPopupTests
    {
        private const string PopupMenuPrefab = "Assets/UIProject/Assets/Basics/popupmenu/PopupMenu.prefab";
        private const string PopupItemPrefab = "Assets/UIProject/Assets/Basics/popupmenu/PopupMenuItem.prefab";
        private const string WindowAPrefab = "Assets/UIProject/Assets/Basics/WindowA.prefab";

        private NanamiPageRenderer _rig;
        private GRoot _gr;

        private static GameObject Load(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new NanamiPageRenderer();
            _rig.Setup();
            _rig.Configure(1136, 640);
            var design = new GameObject("Design", typeof(RectTransform));
            var drt = (RectTransform)design.transform;
            drt.SetParent(_rig.CanvasRt, false);
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0, 1);
            drt.sizeDelta = new Vector2(1136, 640);
            drt.anchoredPosition = Vector2.zero;
            _gr = GRoot.Create(drt);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig.Teardown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Center_places_object_in_middle()
        {
            var obj = new GameObject("obj", typeof(RectTransform));
            var rt = (RectTransform)obj.transform;
            rt.SetParent(_gr.rect, false);
            rt.sizeDelta = new Vector2(400, 300);
            _gr.Center(rt);
            yield return null;
            Assert.AreEqual(368f, rt.anchoredPosition.x, 1f);   // (1136-400)/2
            Assert.AreEqual(-170f, rt.anchoredPosition.y, 1f);  // -(640-300)/2
        }

        [UnityTest]
        public IEnumerator PopupMenu_positions_below_target_and_sizes_to_items()
        {
            var pm = new NanamiUI.PopupMenu(Load(PopupMenuPrefab), Load(PopupItemPrefab));
            var clicked = 0;
            pm.AddItem("Item 1", () => clicked++);
            pm.AddItem("Item 2", () => { });
            pm.AddItem("Item 3", () => { });
            pm.AddItem("Item 4", () => { });

            var target = new GameObject("target", typeof(RectTransform));
            var trt = (RectTransform)target.transform;
            trt.SetParent(_gr.rect, false);
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
            trt.sizeDelta = new Vector2(158, 48);
            trt.anchoredPosition = new Vector2(60, -48); // fairy (60,48)
            Canvas.ForceUpdateCanvases();

            pm.Show(trt, PopupDirection.Down);
            Canvas.ForceUpdateCanvases();

            // 期望：在 target 下方 xx=60,yy=48+48=96 → anchoredPosition (60,-96)；高度 = 60 + (4*22 - 56) = 92
            Assert.AreEqual(60f, pm.ContentPane.anchoredPosition.x, 1.5f);
            Assert.AreEqual(-96f, pm.ContentPane.anchoredPosition.y, 1.5f);
            Assert.AreEqual(92f, pm.ContentPane.rect.height, 1.5f);
            Assert.IsTrue(_gr.HasAnyPopup);

            // 点第一项：回调触发 + 菜单收起（hideOnClickItem）。直接调 handler。
            var item = pm.ContentPane.Find("list").GetChild(0);
            ((IPointerClickHandler)item.GetComponent<IPointerClickHandler>()).OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.AreEqual(1, clicked, "点菜单项应触发其回调");
            Assert.IsFalse(_gr.HasAnyPopup, "hideOnClickItem 应在点项后收起菜单");
        }

        [UnityTest]
        public IEnumerator Window_shows_centered_and_close_hides()
        {
            var win = new CenteringWindow { prefab = Load(WindowAPrefab) };
            win.Show();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsNotNull(win.Root);
            Assert.IsTrue(win.Root.gameObject.activeSelf);
            Assert.AreEqual(368f, win.Root.anchoredPosition.x, 1.5f);  // (1136-400)/2
            Assert.AreEqual(-120f, win.Root.anchoredPosition.y, 1.5f); // -(640-400)/2

            // 点关闭按钮 → Hide（默认无动效 → HideImmediately）
            ButtonBase close = null;
            foreach (var b in win.Root.GetComponentsInChildren<ButtonBase>(true))
                if (b.name == "closeButton") { close = b; break; }
            Assert.IsNotNull(close, "应找到 closeButton");
            close.onClick.Invoke();
            yield return null;
            Assert.IsFalse(win.Root.gameObject.activeSelf, "关闭后 window 应隐藏");
        }

        private sealed class CenteringWindow : NanamiUI.Window
        {
            protected override void OnInit()
            {
                base.OnInit();
                Center();
            }
        }
    }
}
