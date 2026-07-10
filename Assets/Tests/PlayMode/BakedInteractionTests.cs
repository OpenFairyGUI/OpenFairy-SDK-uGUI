using System;
using System.Collections;
using System.Collections.Generic;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NanamiUI.Tests
{
    // 直接实例化转换产物 prefab（不经 Main 页/业务胶水），用真实指针驱动，证明"转换器已把交互烘焙进通用工程"：
    // 关联控制器换页(tab/radio)、overflow=scroll 自挂 ScrollPane、输入框 Enter 提交、slider 连续拖动。
    // 这是"交互不正确但测不出来"的直接防线——通用工程拿到的就是这些 prefab，不含 demo 胶水。
    public class BakedInteractionTests
    {
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

        private GameObject Load(string component)
        {
            _rig.LoadComponent("Basics", component);
            _rig.PlaceCamera();
            return _rig.Instance;
        }

        private static object Comp(GameObject go, string fullName) =>
            Array.Find(go.GetComponentsInChildren<NanamiUI.Component>(true), c => c.GetType().FullName == fullName);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);
        private static bool Selected(object button) => (bool)button.GetType().GetField("selected").GetValue(button);

        private static string ControllerPage(object owner, string field)
        {
            var controller = owner.GetType().GetField(field).GetValue(owner);
            return controller.GetType().GetProperty("page").GetValue(controller).ToString();
        }

        private static RectTransform Rt(object comp) => (RectTransform)((UnityEngine.Component)comp).transform;

        // 真实点击：经 GraphicRaycaster 命中 + ExecuteEvents 派发 down/up/click（命中须落在目标子树内）。
        private bool ClickWorld(RectTransform rt)
        {
            var raycaster = rt.GetComponentInParent<GraphicRaycaster>();
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(rt.rect.center));
            var e = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(e, hits);
            if (hits.Count == 0)
                return false;
            var hitTf = hits[0].gameObject.transform;
            if (hitTf != rt && !hitTf.IsChildOf(rt))
                return false;
            e.pointerPressRaycast = e.pointerCurrentRaycast = hits[0];
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (target == null)
                return false;
            ExecuteEvents.Execute(target, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, e, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, e, ExecuteEvents.pointerClickHandler);
            return true;
        }

        // 真实拖动：从 fromLocal 拖到 toLocal（目标坐标系），经命中的 IDragHandler 派发 down/begin/drag/end。
        private void DragWorld(RectTransform rt, Vector2 fromLocal, Vector2 toLocal)
        {
            var raycaster = rt.GetComponentInParent<GraphicRaycaster>();
            var e = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(fromLocal)),
            };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(e, hits);
            Assert.Greater(hits.Count, 0, "拖动起点应被射线命中");
            e.pointerPressRaycast = e.pointerCurrentRaycast = hits[0];
            var down = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            var drag = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
            ExecuteEvents.Execute(down, e, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(drag, e, ExecuteEvents.beginDragHandler);
            e.position = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(toLocal));
            ExecuteEvents.Execute(drag, e, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(drag, e, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(down, e, ExecuteEvents.pointerUpHandler);
        }

        [UnityTest]
        public IEnumerator Baked_tab_button_switches_controller_page_without_glue()
        {
            var go = Load("Demo_Button");
            Assert.IsNotNull(go, "Demo_Button prefab 应存在（先跑 Migrate）");
            var demo = Comp(go, "UI.Basics.Demo_Button");
            yield return null;
            Assert.AreEqual("_0", ControllerPage(demo, "m_tab"), "初始 tab 在第 0 页");
            Assert.IsTrue(ClickWorld(Rt(Field(demo, "m_n24"))), "Tab2 应被真实射线命中");
            yield return null;
            // 关键：没有任何业务胶水，仅靠烘焙的 relatedController，点 Tab2 就把 tab 控制器切到第 1 页。
            Assert.AreEqual("_1", ControllerPage(demo, "m_tab"), "点 Tab2 应把 tab 控制器切到第 1 页");
            Assert.IsTrue(Selected(Field(demo, "m_n24")), "Tab2 应显示为选中");
            Assert.IsFalse(Selected(Field(demo, "m_n23")), "Tab1 应取消选中");
        }

        [UnityTest]
        public IEnumerator Baked_radio_group_is_mutually_exclusive_without_glue()
        {
            var go = Load("Demo_Button");
            var demo = Comp(go, "UI.Basics.Demo_Button");
            yield return null;
            Assert.IsTrue(ClickWorld(Rt(Field(demo, "m_n19"))), "Option2 应被命中");
            yield return null;
            Assert.AreEqual("_1", ControllerPage(demo, "m_RadioGroup"), "点 Option2 应把 RadioGroup 切到第 1 页");
            Assert.IsTrue(Selected(Field(demo, "m_n19")), "Option2 应选中");
            Assert.IsFalse(Selected(Field(demo, "m_n18")), "Option1 应取消选中（组互斥）");
            Assert.IsFalse(Selected(Field(demo, "m_n20")), "Option3 应取消选中");
        }

        [UnityTest]
        public IEnumerator Baked_scroll_component_auto_attaches_scrollpane_without_glue()
        {
            var go = Load("Demo_Clip&Scroll");
            Assert.IsNotNull(go, "Demo_Clip&Scroll prefab 应存在");
            // 转换器为 overflow=scroll 的组件烘焙了 ScrollPaneHost（无需 demo 胶水调用 Attach）。
            Assert.Greater(go.GetComponentsInChildren<ScrollPaneHost>(true).Length, 0, "滚动组件应烘焙 ScrollPaneHost");
            for (var i = 0; i < 5; i++)
                yield return null; // 等 ScrollPaneHost.Start 自挂 ScrollPane
            Assert.Greater(go.GetComponentsInChildren<ScrollPane>(true).Length, 0, "运行几帧后应自动挂上 ScrollPane");
        }

        [UnityTest]
        public IEnumerator Baked_input_has_submit_relay_and_is_editable()
        {
            var go = Load("Demo_Text");
            var demo = Comp(go, "UI.Basics.Demo_Text");
            var input = (NanamiUI.TextInput)Field(demo, "m_n22");
            yield return null;
            Assert.IsNotNull(input.field, "输入框应绑定 InputField");
            Assert.IsNotNull(input.submit, "输入框应烘焙 InputSubmit（Enter 提交中继）");
            Assert.IsFalse(input.field.readOnly, "默认可编辑");
            input.text = "abc";
            Assert.AreEqual("abc", input.text, "text 读写应经 field");
        }

        [UnityTest]
        public IEnumerator Baked_slider_continuous_drag_changes_value()
        {
            var go = Load("Demo_Slider");
            NanamiUI.Slider slider = null;
            foreach (var s in go.GetComponentsInChildren<NanamiUI.Slider>(true))
                if (s.bar != null) { slider = s; break; } // 横向 slider
            Assert.IsNotNull(slider, "应有横向 Slider");
            slider.value = 0;
            slider.Apply();
            yield return null;

            var rt = (RectTransform)slider.transform;
            // 真实连续拖动（OnDrag）从 grip 起拖到 80% 处——轨道点按只跳一次不跟踪（复刻 GSlider），连续拖动必须按住 grip。
            Assert.IsNotNull(slider.grip, "横向 Slider 应烘焙 grip");
            var gripCenter = (Vector2)rt.InverseTransformPoint(slider.grip.TransformPoint(slider.grip.rect.center));
            DragWorld(rt, gripCenter, new Vector2(rt.rect.xMin + rt.rect.width * 0.8f, rt.rect.center.y));
            yield return null;
            Assert.Greater(slider.value, 40f, "按住 grip 拖到 80% 处应把 value 拖上去（OnDrag 生效）");
        }

        [UnityTest]
        public IEnumerator Baked_combobox_opens_dropdown_without_root_glue()
        {
            // 通用工程场景：只实例化转换产物、不跑任何 NanamiUI 胶水（无 Root.Create）。
            // 点 ComboBox 应自建覆盖层并弹下拉（复刻 GRoot.inst 惰性自建），而不是 NullReferenceException。
            var go = Load("Demo_ComboBox");
            Assert.IsNotNull(go, "Demo_ComboBox prefab 应存在");
            Assert.IsTrue(NanamiUI.Root.inst == null, "前置：无业务胶水时不应已有 Root");
            // 经非泛型面找烘焙的 ComboBox（测试程序集访问不到生成的 UI.{包} 类型）。
            ButtonBase combo = null;
            foreach (var b in go.GetComponentsInChildren<ButtonBase>(true))
            {
                for (var t = b.GetType(); combo == null && t != null; t = t.BaseType)
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ComboBox<>))
                        combo = b;
                if (combo != null)
                    break;
            }
            Assert.IsNotNull(combo, "页面应有烘焙的 ComboBox");
            yield return null;

            ((IPointerClickHandler)combo).OnPointerClick(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.IsTrue(NanamiUI.Root.inst != null, "点下拉应自建 Root 覆盖层");
            Assert.IsTrue(NanamiUI.Root.inst.hasAnyPopup, "下拉应作为 popup 打开");
        }
    }
}
