using System;
using System.Collections;
using System.Collections.Generic;
using NanamiUI.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NanamiUI.Tests
{
    public class MainNavigationTests
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
            _main = UnityEngine.Object.Instantiate(prefab, _rig.CanvasRt, false);
            var rt = (RectTransform)_main.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_main);
            _rig.Teardown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Real_pointer_clicks_open_demo_and_back_to_home()
        {
            var main = Comp("UI.Basics.Main");
            yield return Click(Field(main, "m_btn_Button"));
            Assert.IsNotNull(Comp("UI.Basics.Demo_Button"), "真实点击 Button 导航项应打开 Demo_Button");

            yield return Click(Field(main, "m_btn_Back"));
            yield return new WaitForSeconds(0.4f);

            Assert.IsFalse(((UnityEngine.Component)Comp("UI.Basics.Demo_Button")).gameObject.activeInHierarchy, "真实点击 Back 后 demo 容器应隐藏");
            Assert.IsFalse(((UnityEngine.Component)Field(main, "m_btn_Back")).gameObject.activeInHierarchy, "Back 后返回按钮应隐藏");
            Assert.IsTrue(((UnityEngine.Component)Field(main, "m_btn_Button")).gameObject.activeInHierarchy, "Back 后主页按钮应重新可点");
        }

        private object Comp(string fullName) =>
            Array.Find(_main.GetComponentsInChildren<NanamiUI.Component>(true), c => c.GetType().FullName == fullName);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);

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
            Assert.IsNotEmpty(hits, "按钮中心应能被 GraphicRaycaster 命中");
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(target, "命中对象或父节点应有 IPointerClickHandler");
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
