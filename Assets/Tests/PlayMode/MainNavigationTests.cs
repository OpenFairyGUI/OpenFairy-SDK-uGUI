using System;
using System.Collections;
using System.Collections.Generic;
using OpenFairy.UGUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public class MainNavigationTests
    {
        private const int TransitionFrames = 45;

        public readonly struct MainEntry
        {
            public readonly string Name;
            public readonly string ButtonField;
            public readonly string ComponentName;

            public MainEntry(string name, string buttonField, string componentName)
            {
                Name = name;
                ButtonField = buttonField;
                ComponentName = componentName;
            }

            public override string ToString() => Name;
        }

        public static readonly MainEntry[] Entries =
        {
            new("Button", "m_btn_Button", "UI.Basics.Demo_Button"),
            new("Image", "m_btn_Image", "UI.Basics.Demo_Image"),
            new("Graph", "m_btn_Graph", "UI.Basics.Demo_Graph"),
            new("MovieClip", "m_btn_MovieClip", "UI.Basics.Demo_MovieClip"),
            new("Depth", "m_btn_Depth", "UI.Basics.Demo_Depth"),
            new("Loader", "m_btn_Loader", "UI.Basics.Demo_Loader"),
            new("List", "m_btn_List", "UI.Basics.Demo_List"),
            new("ProgressBar", "m_btn_ProgressBar", "UI.Basics.Demo_ProgressBar"),
            new("Slider", "m_btn_Slider", "UI.Basics.Demo_Slider"),
            new("ComboBox", "m_btn_ComboBox", "UI.Basics.Demo_ComboBox"),
            new("Clip&Scroll", "m_btn_Clip_Scroll", "UI.Basics.Demo_Clip_Scroll"),
            new("Controller", "m_btn_Controller", "UI.Basics.Demo_Controller"),
            new("Relation", "m_btn_Relation", "UI.Basics.Demo_Relation"),
            new("Label", "m_btn_Label", "UI.Basics.Demo_Label"),
            new("Popup", "m_btn_Popup", "UI.Basics.Demo_Popup"),
            new("Window", "m_btn_Window", "UI.Basics.Demo_Window"),
            new("Drag&Drop", "m_btn_Drag_Drop", "UI.Basics.Demo_Drag_Drop"),
            new("Component", "m_btn_Component", "UI.Basics.Demo_Component"),
            new("Grid", "m_btn_Grid", "UI.Basics.Demo_Grid"),
            new("Text", "m_btn_Text", "UI.Basics.Demo_Text"),
        };

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

        [UnityTest]
        public IEnumerator Main_starts_on_home_with_all_entries()
        {
            var main = Comp("UI.Basics.Main");
            Assert.IsNotNull(main, "Main component should exist");
            Assert.IsTrue(Rt("btns").gameObject.activeInHierarchy, "Main button group should be visible");
            Assert.IsFalse(Rt("container").gameObject.activeSelf, "Demo container should start hidden");
            Assert.IsFalse(((UnityEngine.Component)Field(main, "m_btn_Back")).gameObject.activeInHierarchy, "Back should start hidden");

            foreach (var entry in Entries)
                Assert.IsTrue(((UnityEngine.Component)Field(main, entry.ButtonField)).gameObject.activeInHierarchy, $"{entry.Name} entry should be visible");
            yield break;
        }

        [UnityTest]
        public IEnumerator Real_pointer_clicks_open_demo_and_back_to_home([ValueSource(nameof(Entries))] MainEntry entry)
        {
            var main = Comp("UI.Basics.Main");
            var homeX = Rt("btns").anchoredPosition.x;
            yield return Click(Field(main, entry.ButtonField));
            Assert.IsNotNull(Comp(entry.ComponentName), $"{entry.Name} should instantiate {entry.ComponentName}");
            Assert.IsTrue(((UnityEngine.Component)Field(main, "m_btn_Back")).gameObject.activeInHierarchy, $"{entry.Name}: Back should be visible after entering a demo");
            yield return AssertEnterAnimation(entry.Name);

            yield return Click(Field(main, "m_btn_Back"));
            Assert.IsFalse(((UnityEngine.Component)Field(main, "m_btn_Back")).gameObject.activeInHierarchy, $"{entry.Name}: Back should hide immediately after returning");
            yield return AssertBackAnimation(entry.Name, homeX);

            Assert.IsFalse(((UnityEngine.Component)Comp(entry.ComponentName)).gameObject.activeInHierarchy, $"{entry.Name}: demo container should be hidden after Back");
            Assert.IsTrue(((UnityEngine.Component)Field(main, entry.ButtonField)).gameObject.activeInHierarchy, $"{entry.Name}: entry button should be visible again after Back");
        }

        private object Comp(string fullName) =>
            Array.Find(_main.GetComponentsInChildren<OpenFairy.UGUI.Component>(true), c => c.GetType().FullName == fullName);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);

        private RectTransform Rt(string name) => (RectTransform)_main.transform.Find(name);

        private IEnumerator AssertEnterAnimation(string name)
        {
            var btns = Rt("btns");
            var container = Rt("container");
            var sawBtnsMidSlide = false;
            var sawContainerMidSlide = false;
            for (var i = 0; i < TransitionFrames; i++)
            {
                yield return null;
                var bx = btns.anchoredPosition.x;
                var cx = container.anchoredPosition.x;
                if (btns.gameObject.activeSelf && bx > 120 && bx < 1120)
                    sawBtnsMidSlide = true;
                if (container.gameObject.activeSelf && cx > -1120 && cx < -20)
                    sawContainerMidSlide = true;
            }

            Assert.IsTrue(sawBtnsMidSlide, $"{name}: Main buttons should stay visible while sliding out");
            Assert.IsTrue(sawContainerMidSlide, $"{name}: Demo container should stay visible while sliding in");
            Assert.IsFalse(btns.gameObject.activeSelf, $"{name}: Main buttons should hide after entering");
            Assert.IsTrue(container.gameObject.activeSelf, $"{name}: Demo container should remain visible after entering");
            Assert.AreEqual(0f, container.anchoredPosition.x, 1.5f, $"{name}: Demo container should settle at x=0");
        }

        private IEnumerator AssertBackAnimation(string name, float homeX)
        {
            var btns = Rt("btns");
            var container = Rt("container");
            var sawBtnsMidSlide = false;
            var sawContainerMidSlide = false;
            for (var i = 0; i < TransitionFrames; i++)
            {
                yield return null;
                var bx = btns.anchoredPosition.x;
                var cx = container.anchoredPosition.x;
                if (btns.gameObject.activeSelf && bx > 120 && bx < 1120)
                    sawBtnsMidSlide = true;
                if (container.gameObject.activeSelf && cx > -1120 && cx < -20)
                    sawContainerMidSlide = true;
            }

            Assert.IsTrue(sawBtnsMidSlide, $"{name}: Main buttons should stay visible while sliding back in");
            Assert.IsTrue(sawContainerMidSlide, $"{name}: Demo container should stay visible while sliding out");
            Assert.IsTrue(btns.gameObject.activeSelf, $"{name}: Main buttons should remain visible after Back");
            Assert.IsFalse(container.gameObject.activeSelf, $"{name}: Demo container should hide after Back");
            Assert.AreEqual(homeX, btns.anchoredPosition.x, 1.5f, $"{name}: Main buttons should settle at home x");
        }

        private IEnumerator Click(object button)
        {
            var rt = (RectTransform)((UnityEngine.Component)button).transform;
            var screen = RectTransformUtility.WorldToScreenPoint(_rig.Camera, rt.TransformPoint(rt.rect.center));
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screen,
                pointerPressRaycast = new RaycastResult { module = _rig.Raycaster }
            };
            var hits = new List<RaycastResult>();
            _rig.Raycaster.Raycast(eventData, hits);
            Assert.IsNotEmpty(hits, "Button center should be hit by GraphicRaycaster");
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(target, "Hit object or parent should have IPointerClickHandler");
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
