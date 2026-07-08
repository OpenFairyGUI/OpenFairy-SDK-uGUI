using System;
using System.Collections;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    // 集成 smoke：实例化 Main、让 BasicsMain.Awake 跑，经反射驱动 Window/Popup/Depth/Drag&Drop demo，
    // 验证胶水（反射字段名、GRoot/Window/PopupMenu 接线）端到端不抛且能开窗/弹菜单。
    public class DemoSmokeTest
    {
        private NanamiPageRenderer _rig;
        private GameObject _main;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new NanamiPageRenderer();
            _rig.Setup();
            _rig.Configure(1136, 640);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UIProject/Assets/Basics/Main.prefab");
#else
            GameObject prefab = null;
#endif
            _main = UnityEngine.Object.Instantiate(prefab, _rig.CanvasRt, false); // 触发 BasicsMain.Awake（GRoot.Create + Back）
            var rt = (RectTransform)_main.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_main);
            _rig.Teardown();
            yield return null;
        }

        private object Comp(string fullName) =>
            Array.Find(_main.GetComponentsInChildren<NanamiUI.Component>(true), c => c.GetType().FullName == fullName);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);

        private static void Click(object button) =>
            ((UnityEvent)button.GetType().GetField("onClick").GetValue(button)).Invoke();

        [UnityTest]
        public IEnumerator Window_demo_opens_window()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_Window")); // → PlayWindow 接线 n0/n1
            yield return null;
            var demo = Comp("UI.Basics.Demo_Window");
            Assert.IsNotNull(demo, "Demo_Window 应已实例化");
            Click(Field(demo, "m_n0")); // 开 Window A
            yield return null;
            Assert.AreEqual(1, NanamiUI.GRoot.inst.ActiveWindowCount, "点 n0 应开一个 window");
        }

        [UnityTest]
        public IEnumerator Popup_demo_shows_menu()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_Popup"));
            yield return null;
            var demo = Comp("UI.Basics.Demo_Popup");
            Assert.IsNotNull(demo);
            Click(Field(demo, "m_n0")); // 弹菜单
            yield return null;
            Assert.IsTrue(NanamiUI.GRoot.inst.HasAnyPopup, "点 n0 应弹出菜单");
        }

        [UnityTest]
        public IEnumerator ProgressBar_demo_cycles_values()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_ProgressBar"));
            yield return null;
            var demo = (UnityEngine.Component)Comp("UI.Basics.Demo_ProgressBar");
            Assert.IsNotNull(demo);
            var bar = demo.GetComponentInChildren<NanamiUI.ProgressBar>();
            var v0 = bar.value;
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.AreNotEqual(v0, bar.value, "ProgressBar value 应随时间变化");
        }

        [UnityTest]
        public IEnumerator Text_demo_copies_text()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_Text"));
            yield return null;
            var demo = Comp("UI.Basics.Demo_Text");
            Assert.IsNotNull(demo);
            var n22 = (NanamiUI.Text)Field(demo, "m_n22");
            var n24 = (NanamiUI.Text)Field(demo, "m_n24");
            Click(Field(demo, "m_n25")); // 拷 n22 → n24
            yield return null;
            Assert.AreEqual(n22.text, n24.text, "点 n25 应把 n22 文本拷到 n24");
        }

        [UnityTest]
        public IEnumerator List_demo_scrolls()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_List"));
            yield return null;
            var pane = _main.GetComponentInChildren<NanamiUI.ScrollPane>();
            Assert.IsNotNull(pane, "List demo 应挂上 ScrollPane");
            var content = (RectTransform)pane.transform.Find("viewport/content");
            Assert.IsNotNull(content, "应有 content 容器");
            var vp = (RectTransform)pane.transform.Find("viewport");
            var y0 = content.anchoredPosition.y;

            Vector2 Screen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(_rig.Camera, vp.TransformPoint(local));
            PointerEventData At(Vector2 s) => new(EventSystem.current) { position = s, pointerPressRaycast = new RaycastResult { module = _rig.Raycaster } };
            var center = (Vector3)vp.rect.center;
            pane.OnBeginDrag(At(Screen(center)));
            pane.OnDrag(At(Screen(center + new Vector3(0, 60, 0)))); // 向上拖露出下方内容
            yield return null;
            Assert.GreaterOrEqual(content.anchoredPosition.y, y0, "向上拖后 content.y 应 >= 起始（可滚动则增大，clamp 有效）");
        }

        [UnityTest]
        public IEnumerator ComboBox_demo_opens_dropdown()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_ComboBox"));
            yield return null;
            var n1 = (UnityEngine.Component)Field(Comp("UI.Basics.Demo_ComboBox"), "m_n1");
            var handler = (IPointerClickHandler)n1.GetComponent(typeof(IPointerClickHandler));
            Assert.IsNotNull(handler, "ComboBox 应挂上点击中继");
            handler.OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.IsTrue(NanamiUI.GRoot.inst.HasAnyPopup, "点 ComboBox 应弹出下拉");
        }

        [UnityTest]
        public IEnumerator Grid_demo_fills_lists()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_Grid"));
            yield return null;
            var demo = Comp("UI.Basics.Demo_Grid");
            Assert.IsNotNull(demo);
            var list1 = (RectTransform)((UnityEngine.Component)Field(demo, "m_list1")).transform;
            var content = list1.Find("viewport/content");
            Assert.IsNotNull(content, "list1 应有 content 容器");
            Assert.Greater(content.childCount, 2, "list1 应被填入多个项");
        }

        [UnityTest]
        public IEnumerator Depth_and_dragdrop_demos_wire_without_error()
        {
            Click(Field(Comp("UI.Basics.Main"), "m_btn_Depth"));
            yield return null;
            Assert.IsNotNull(Comp("UI.Basics.Demo_Depth"), "Depth demo 应实例化且 PlayDepth 不抛");

            Click(Field(Comp("UI.Basics.Main"), "m_btn_Drag_Drop"));
            yield return null;
            Assert.IsNotNull(Comp("UI.Basics.Demo_Drag_Drop"), "Drag&Drop demo 应实例化且 PlayDragDrop 不抛");
            yield return null;
        }
    }
}
