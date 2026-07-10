using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    // Root 覆盖层 + PopupMenu + Window 的运行时机制回归。定位/居中/尺寸是分析精确值；交互直接调 handler（不经 raycast，
    // 因 golden rig 是 WorldSpace + 禁用相机，raycast 命中不可靠）。
    public class WindowPopupTests
    {
        private const string PopupMenuPrefab = "Assets/UIProject/Assets/Basics/popupmenu/PopupMenu.prefab";
        private const string PopupItemPrefab = "Assets/UIProject/Assets/Basics/popupmenu/PopupMenuItem.prefab";
        private const string WindowAPrefab = "Assets/UIProject/Assets/Basics/WindowA.prefab";

        private NanamiPageRenderer _rig;
        private Root _gr;

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
            _gr = Root.Create(drt);
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
            Assert.IsTrue(_gr.hasAnyPopup);

            // 点第一项：回调触发 + 菜单收起（hideOnClickItem）。直接调 handler。
            var item = pm.ContentPane.Find("list").GetChild(0);
            ((IPointerClickHandler)item.GetComponent<IPointerClickHandler>()).OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.AreEqual(1, clicked, "点菜单项应触发其回调");
            Assert.IsFalse(_gr.hasAnyPopup, "hideOnClickItem 应在点项后收起菜单");
        }

        [UnityTest]
        public IEnumerator PopupMenu_clear_items_reuses_items_and_clears_callbacks()
        {
            var pm = new NanamiUI.PopupMenu(Load(PopupMenuPrefab), Load(PopupItemPrefab));
            var oldCallback = 0;
            var freshCallback = 0;
            var first = pm.AddItem("Old", () => oldCallback++);
            var firstGo = ((UnityEngine.Component)first).gameObject;

            pm.ClearItems();
            Assert.AreEqual(0, pm.ContentPane.Find("list").childCount, "ClearItems 应把菜单项移出 active list");

            var second = pm.AddItem("Fresh", () => freshCallback++);
            var secondGo = ((UnityEngine.Component)second).gameObject;
            Assert.AreSame(firstGo, secondGo, "PopupMenu 应复用已清空的菜单项，而不是销毁重建");

            secondGo.GetComponent<IPointerClickHandler>().OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.AreEqual(0, oldCallback, "复用菜单项时旧 callback 应被清掉");
            Assert.AreEqual(1, freshCallback, "复用菜单项应只触发本轮 callback");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Window_shows_centered_and_close_hides()
        {
            var win = new CenteringWindow { prefab = Load(WindowAPrefab) };
            win.Show();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsNotNull(win.contentPane);
            Assert.IsTrue(win.contentPane.gameObject.activeSelf);
            Assert.AreEqual(368f, win.contentPane.anchoredPosition.x, 1.5f);  // (1136-400)/2
            Assert.AreEqual(-120f, win.contentPane.anchoredPosition.y, 1.5f); // -(640-400)/2

            // 点关闭按钮 → Hide（默认无动效 → HideImmediately）
            ButtonBase close = null;
            foreach (var b in win.contentPane.GetComponentsInChildren<ButtonBase>(true))
                if (b.name == "closeButton") { close = b; break; }
            Assert.IsNotNull(close, "应找到 closeButton");
            close.onClick.Invoke();
            yield return null;
            Assert.IsFalse(win.contentPane.gameObject.activeSelf, "关闭后 window 应隐藏");
        }

        [UnityTest]
        public IEnumerator Modal_window_shows_dim_layer_below_window_and_hides_on_close()
        {
            var win = new CenteringWindow { prefab = Load(WindowAPrefab), modal = true };
            win.Show();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(_gr.hasModalWindow, "模态窗打开时应报告有模态窗");
            var layer = FindDeep(_gr.rect, "ModalLayer");
            Assert.IsNotNull(layer, "应创建模态层");
            Assert.IsTrue(layer.gameObject.activeSelf, "模态层应激活");
            Assert.Less(layer.GetSiblingIndex(), win.contentPane.GetSiblingIndex(), "模态层应铺在窗口之下");

            win.HideImmediately();
            yield return null;
            Assert.IsFalse(_gr.hasModalWindow, "关窗后无模态窗");
            Assert.IsFalse(layer.gameObject.activeSelf, "关窗后模态层应隐藏");
        }

        private const string ComboPopupPrefab = "Assets/UIProject/Assets/Basics/components/ComboBoxPopup.prefab";
        private enum ComboPage { up, down, over, selectedOver }
        private sealed class TestCombo : NanamiUI.ComboBox<ComboPage> { }

        [UnityTest]
        public IEnumerator ComboBox_dropdown_clips_to_visibleItemCount_and_scrolls()
        {
            var popup = Load(ComboPopupPrefab);
            Assert.IsNotNull(popup, "需要已烘焙的 ComboBoxPopup 下拉");

            // 长列表（15 项，可见 5）：下拉裁到 5 项高并挂 ScrollPane。
            var combo = MakeCombo(15, 5);
            combo.OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            var list = FindDeep(_gr.rect, "list");
            Assert.IsNotNull(list, "应实例化下拉 list");
            var source = list.GetComponent<ListSource>();
            Assert.IsFalse(list.GetComponent<ListSelection>()?.enabled ?? false, "ComboBox 下拉应禁用普通 ListSelection，由 ComboBox 自己发 onChanged");
            Assert.IsFalse(list.GetComponent<ScrollPaneHost>()?.enabled ?? false, "ComboBox 下拉应禁用 prefab 自带 ScrollPaneHost，只在超出可见项时手动 Attach");
            Assert.AreEqual(5 * source.itemSize.y, list.rect.height, 1f, "list 高应裁到 visibleItemCount*itemH");
            Assert.IsNotNull(list.GetComponent<ScrollPane>(), "项数超可见数应挂 ScrollPane 以滚动");
            _gr.HidePopup();
            yield return null;

            // 短列表（3 项，可见 5）：撑开显示全部，不挂 ScrollPane（与旧行为一致，golden 不受影响）。
            var shortCombo = MakeCombo(3, 5);
            shortCombo.OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            var list2 = FindDeep(_gr.rect, "list");
            var source2 = list2.GetComponent<ListSource>();
            Assert.AreEqual(3 * source2.itemSize.y, list2.rect.height, 1f, "短列表 list 高 = 项数*itemH");
            Assert.IsNull(list2.GetComponent<ScrollPane>(), "未超可见数不应挂 ScrollPane");
        }

        private TestCombo MakeCombo(int itemCount, int visible)
        {
            var go = new GameObject("combo", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_gr.rect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(150, 30);
            rt.anchoredPosition = new Vector2(20, -20);
            var combo = go.AddComponent<TestCombo>();
            combo.dropdownPrefab = Load(ComboPopupPrefab);
            combo.visibleItemCount = visible;
            combo.items = new string[itemCount];
            for (var i = 0; i < itemCount; i++)
                combo.items[i] = "Item " + i;
            return combo;
        }

        private static RectTransform FindDeep(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!c.gameObject.activeSelf) // 跳过已收起的旧下拉，只找当前激活的
                    continue;
                if (c.name == name)
                    return (RectTransform)c;
                if (FindDeep(c, name) is { } found)
                    return found;
            }
            return null;
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
