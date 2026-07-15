using System;
using System.Collections;
using System.Collections.Generic;
using OpenFairy.UGUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    // 端到端交互回归：经真实 GraphicRaycaster 命中 + ExecuteEvents 驱动页内二级交互（开窗/弹菜单/下拉、MovieClip 自播、
    // 输入框可编辑）。区别于 DemoSmokeTest（直接 Invoke onClick / 直接写 .text），这里走真实点击路径，能抓到
    // "射线打不到目标 / 覆盖层挡住 / 输入框不可编辑" 这类只在真跑时暴露的问题。
    public class InteractionRuntimeTests
    {
        private OpenFairyPageRenderer _rig;
        private GameObject _main;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new OpenFairyPageRenderer();
            _rig.Setup();
            _rig.Configure(1136, 640);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UIProject/Assets/Basics/Main.prefab");
#else
            GameObject prefab = null;
#endif
            _main = UnityEngine.Object.Instantiate(prefab, _rig.CanvasRt, false);
            var rt = (RectTransform)_main.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(_main);
            _main = null;
            _rig.Teardown();
            yield return null;
        }

        private object Comp(string fullName) =>
            Array.Find(_main.GetComponentsInChildren<OpenFairy.UGUI.Component>(true), c => c.GetType().FullName == fullName);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);

        // 真实点击：经目标所在画布的 GraphicRaycaster 命中（页面元素用页面画布，popup/window 用 Root 覆盖层画布），
        // 再 ExecuteEvents 派发 down/up/click。命中目标本身或其祖先的 IPointerClickHandler，验证目标确实可点。
        private bool ClickWorld(RectTransform rt)
        {
            var raycaster = rt.GetComponentInParent<GraphicRaycaster>();
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(rt.rect.center));
            var eventData = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(eventData, hits);
            if (hits.Count == 0)
                return false;
            eventData.pointerPressRaycast = hits[0];
            eventData.pointerCurrentRaycast = hits[0];
            // 命中的最上层必须落在目标子树内（否则说明目标被别的东西遮挡 = 真跑时点不到）。
            var hitTf = hits[0].gameObject.transform;
            if (hitTf != rt && !hitTf.IsChildOf(rt))
                return false;
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (target == null)
                return false;
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private IEnumerator Enter(string buttonField)
        {
            ClickWorld((RectTransform)((UnityEngine.Component)Field(Comp("UI.Basics.Main"), buttonField)).transform);
            // 进场动效 + 胶水接线要几帧
            for (var i = 0; i < 50; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator MovieClip_demo_actually_animates()
        {
            yield return Enter("m_btn_MovieClip");
            var demo = (UnityEngine.Component)Comp("UI.Basics.Demo_MovieClip");
            Assert.IsNotNull(demo, "MovieClip demo 应实例化");
            var mc = demo.GetComponentInChildren<OpenFairy.UGUI.MovieClip>(true);
            Assert.IsNotNull(mc, "MovieClip demo 应含 MovieClip 组件");
            Assert.IsNotNull(mc.frames, "MovieClip 应有帧数组");
            Assert.Greater(mc.frames.Length, 1, "MovieClip 应有多帧");
            foreach (var f in mc.frames)
                Assert.IsNotNull(f, "每帧 sprite 都应烘焙成功（非 null）");
            Assert.IsNotNull(mc.sprite, "MovieClip 当前应显示某帧 sprite（渲染非空）");
            Assert.IsTrue(mc.playing, "MovieClip demo 应自播");

            var before = mc.sprite;
            for (var i = 0; i < 40; i++)
                yield return null;
            Assert.AreNotSame(before, mc.sprite, "自播若干帧后当前 sprite 应变化（帧在推进）");
        }

        [UnityTest]
        public IEnumerator Window_opens_and_closes_via_real_click()
        {
            yield return Enter("m_btn_Window");
            var demo = Comp("UI.Basics.Demo_Window");
            Assert.IsNotNull(demo, "Window demo 应实例化");

            var openBtn = (RectTransform)((UnityEngine.Component)Field(demo, "m_n0")).transform;
            Assert.IsTrue(ClickWorld(openBtn), "开窗按钮 n0 应被真实射线命中并派发点击（按钮整块须可点，不能被背景穿透）");
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.AreEqual(1, OpenFairy.UGUI.Root.inst.activeWindowCount, "真实点击 n0 应开出一个 window");

            // 找到窗口关闭按钮并真实点击关闭。
            RectTransform close = null;
            foreach (var b in OpenFairy.UGUI.Root.inst.rect.GetComponentsInChildren<OpenFairy.UGUI.ButtonBase>(true))
                if (b.name == "closeButton") { close = (RectTransform)b.transform; break; }
            Assert.IsNotNull(close, "窗口应有 closeButton");
            Assert.IsTrue(ClickWorld(close), "closeButton 应被射线命中（经 Root 覆盖层画布）并派发点击");
            for (var i = 0; i < 10; i++)
                yield return null;
            Assert.AreEqual(0, OpenFairy.UGUI.Root.inst.activeWindowCount, "点关闭按钮后 window 应关闭");
        }

        [UnityTest]
        public IEnumerator Popup_opens_via_real_click_and_item_closes_it()
        {
            yield return Enter("m_btn_Popup");
            var demo = Comp("UI.Basics.Demo_Popup");
            Assert.IsNotNull(demo, "Popup demo 应实例化");

            var openBtn = (RectTransform)((UnityEngine.Component)Field(demo, "m_n0")).transform;
            Assert.IsTrue(ClickWorld(openBtn), "弹菜单按钮 n0 应被射线命中");
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.IsTrue(OpenFairy.UGUI.Root.inst.hasAnyPopup, "真实点击 n0 应弹出菜单");

            // 真实点击第一项：菜单应收起（hideOnClickItem）。
            var list = FindDeep(OpenFairy.UGUI.Root.inst.rect, "list");
            Assert.IsNotNull(list, "弹出的菜单应含 list");
            Assert.Greater(list.childCount, 0, "菜单应有项");
            Assert.IsTrue(ClickWorld((RectTransform)list.GetChild(0)), "菜单项应被射线命中");
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.IsFalse(OpenFairy.UGUI.Root.inst.hasAnyPopup, "点菜单项后菜单应收起");
        }

        [UnityTest]
        public IEnumerator TextInput_field_is_editable_and_readable()
        {
            yield return Enter("m_btn_Text");
            var demo = Comp("UI.Basics.Demo_Text");
            Assert.IsNotNull(demo, "Text demo 应实例化");
            var input = (OpenFairy.UGUI.TextInput)Field(demo, "m_n22");
            Assert.IsNotNull(input, "n22 应是输入框");
            Assert.IsNotNull(input.field, "TextInput 应绑定 uGUI InputField");
            Assert.IsFalse(input.field.readOnly, "输入框不应只读");
            Assert.IsTrue(input.field.interactable, "输入框应可交互（可聚焦编辑）");
            Assert.IsNotNull(input.field.textComponent, "InputField 应挂 textComponent 以显示输入");

            // 关键回归：输入框中心必须被真实射线命中并解析到 InputField（否则点不到 = 无法聚焦/输入）。
            // 之前的 bug：display 用 OpenFairy.UGUI.TextField 作 targetGraphic，其 OnEnable 把 raycastTarget 清零 → 命中落空。
            var rt = (RectTransform)input.transform;
            var raycaster = rt.GetComponentInParent<GraphicRaycaster>();
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(rt.rect.center));
            var ped = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(ped, hits);
            Assert.Greater(hits.Count, 0, "输入框中心应被真实射线命中（targetGraphic 须常驻可点）");
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreSame(input.field.gameObject, handler, "命中应解析到该 InputField 的对象（不能被背景穿透或 raycastTarget 被清）");

            // 真实点击 → 聚焦。
            ped.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(input.field.gameObject, ped, ExecuteEvents.pointerClickHandler);
            input.field.ActivateInputField();
            yield return null;
            Assert.IsTrue(input.field.isFocused, "真实点击后输入框应聚焦（可开始输入）");

            // 读写通路：设文本可读回。
            input.field.text = "Bob";
            yield return null;
            Assert.AreEqual("Bob", input.text, "输入框文本应可读回");
        }

        [UnityTest]
        public IEnumerator ComboBox_opens_dropdown_via_real_click()
        {
            yield return Enter("m_btn_ComboBox");
            var demo = Comp("UI.Basics.Demo_ComboBox");
            Assert.IsNotNull(demo, "ComboBox demo 应实例化");
            var combo = (RectTransform)((UnityEngine.Component)Field(demo, "m_n1")).transform;
            Assert.IsTrue(ClickWorld(combo), "ComboBox 应被真实射线命中（整块可点，不被背景穿透）");
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.IsTrue(OpenFairy.UGUI.Root.inst.hasAnyPopup, "真实点击 ComboBox 应弹出下拉");
        }

        [UnityTest]
        public IEnumerator ComboBox_selecting_dropdown_item_updates_title_and_closes()
        {
            yield return Enter("m_btn_ComboBox");
            var demo = Comp("UI.Basics.Demo_ComboBox");
            var comboComp = (UnityEngine.Component)Field(demo, "m_n1");
            Assert.IsTrue(ClickWorld((RectTransform)comboComp.transform), "ComboBox 应被命中");
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.IsTrue(OpenFairy.UGUI.Root.inst.hasAnyPopup, "应弹出下拉");

            // 下拉项都在 Root 覆盖层里（combo 本体在页面画布），取第 2 项真实点击。
            var items = OpenFairy.UGUI.Root.inst.rect.GetComponentsInChildren<OpenFairy.UGUI.ButtonBase>(true);
            Assert.Greater(items.Length, 1, "下拉应有多项");
            var combo = (OpenFairy.UGUI.IComboBox)comboComp; // 非泛型面，免反射
            var options = combo.items;
            Assert.IsTrue(ClickWorld((RectTransform)items[1].transform), "下拉第 2 项应被真实射线命中");
            for (var i = 0; i < 5; i++)
                yield return null;
            Assert.IsFalse(OpenFairy.UGUI.Root.inst.hasAnyPopup, "点选项后下拉应收起");
            Assert.AreEqual(options[1], combo.text, "选第 2 项后 ComboBox 当前文本应为该项");
        }

        private static bool IsComboBox(Type t)
        {
            for (; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(OpenFairy.UGUI.ComboBox<>))
                    return true;
            return false;
        }

        [UnityTest]
        public IEnumerator Every_combobox_on_the_page_opens_a_dropdown()
        {
            yield return Enter("m_btn_ComboBox");
            var demo = (UnityEngine.Component)Comp("UI.Basics.Demo_ComboBox");
            Assert.IsNotNull(demo, "ComboBox demo 应实例化");
            // 逐个真实点击页内每个 combobox（含 Dropdown 变体 n4/n5），断言各自都能弹下拉——而不是抽查一个 n1 就断言"ComboBox 可用"。
            var combos = new List<OpenFairy.UGUI.ButtonBase>();
            foreach (var b in demo.GetComponentsInChildren<OpenFairy.UGUI.ButtonBase>(true))
                if (IsComboBox(b.GetType()))
                    combos.Add(b);
            Assert.Greater(combos.Count, 1, "ComboBox demo 应有多个下拉（含 Dropdown 变体，不能只有 n1/n6）");
            foreach (var combo in combos)
            {
                OpenFairy.UGUI.Root.inst.HidePopup();
                yield return null;
                Assert.IsTrue(ClickWorld((RectTransform)combo.transform), $"{combo.name} 应被真实射线命中");
                for (var i = 0; i < 5; i++)
                    yield return null;
                Assert.IsTrue(OpenFairy.UGUI.Root.inst.hasAnyPopup, $"点 {combo.name} 应弹出下拉（每个 combobox 都要能开，不只抽查一个）");
            }
            OpenFairy.UGUI.Root.inst.HidePopup();
        }

        [UnityTest]
        public IEnumerator Slider_is_reachable_by_real_click_and_changes_value()
        {
            yield return Enter("m_btn_Slider");
            OpenFairy.UGUI.Slider slider = null;
            foreach (var s in _main.GetComponentsInChildren<OpenFairy.UGUI.Slider>(true))
                if (s.bar != null) { slider = s; break; } // 取横向 slider（有 bar），x 方向点击可跳值
            Assert.IsNotNull(slider, "Slider demo 应含横向 Slider 组件");
            slider.value = 0;
            slider.Apply();
            var rt = (RectTransform)slider.transform;
            // 真实点击 slider 约 70% 处：须被射线命中（否则拖动条区域点不到 = 真跑不可用），并跳到该值。
            var local = new Vector2(rt.rect.xMin + rt.rect.width * 0.7f, rt.rect.center.y);
            Assert.IsTrue(ClickWorldAt(rt, local), "Slider 轨道区域应被真实射线命中");
            for (var i = 0; i < 3; i++)
                yield return null;
            Assert.Greater(slider.value, 5f, "真实点击 slider 70% 处应把 value 跳上去（changeOnClick 生效）");
        }

        // 真实点击目标内的某个局部点（用于 slider 等需按位置点的目标）。
        private bool ClickWorldAt(RectTransform rt, Vector2 localPoint)
        {
            var raycaster = rt.GetComponentInParent<GraphicRaycaster>();
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(localPoint));
            var eventData = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(eventData, hits);
            if (hits.Count == 0)
                return false;
            var hitTf = hits[0].gameObject.transform;
            if (hitTf != rt && !hitTf.IsChildOf(rt))
                return false;
            eventData.pointerPressRaycast = hits[0];
            eventData.pointerCurrentRaycast = hits[0];
            var target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            if (target == null)
                return false;
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
            return true;
        }

        private static RectTransform FindDeep(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!c.gameObject.activeSelf)
                    continue;
                if (c.name == name)
                    return (RectTransform)c;
                if (FindDeep(c, name) is { } found)
                    return found;
            }
            return null;
        }
    }
}
