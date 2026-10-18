using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class RightClickTests
    {
        private readonly InputTestFixture _input = new();
        private readonly List<Object> _owned = new();
        private Mouse _mouse, _previousMouse;
        private NativeLeakDetectionMode _leakMode;
        private EventSystem _previousEvents, _events;
        private RectTransform _canvas;
        private Root _root;
        private bool _background;
        private AudioClip _clip;

        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)Own(new GameObject(name, typeof(RectTransform))).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }
        private T Make<T>(Transform parent = null) where T : ButtonBase
        {
            var rect = Rect(typeof(T).Name, parent == null ? _canvas : parent, new Vector2(160, 80));
            rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button = rect.gameObject.AddComponent<T>();
            button.sound = _clip;
            return button;
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Root.inst, Is.Null);
            _previousMouse = Mouse.current;
            _previousEvents = EventSystem.current;
            _leakMode = NativeLeakDetection.Mode;
            _background = Application.runInBackground;
            Application.runInBackground = true;
            _input.Setup();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _mouse = InputSystem.AddDevice<Mouse>();
            _canvas = Rect("Canvas", null, new Vector2(800, 600));
            _canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();
            _root = Root.Create(_canvas);
            _root.soundSource.mute = true;
            _clip = Own(AudioClip.Create("Click", 44100, 1, 44100, false));
            var go = Own(new GameObject("Events")); go.SetActive(false);
            _events = go.AddComponent<EventSystem>();
            var module = go.AddComponent<PopupInputModule>();
            var actions = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var map = actions.AddActionMap("UI");
            module.point = Own(InputActionReference.Create(map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position")));
            module.leftClick = Own(InputActionReference.Create(map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton")));
            module.rightClick = Own(InputActionReference.Create(map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton")));
            module.middleClick = Own(InputActionReference.Create(map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton")));
            go.SetActive(true);
            EventSystem.current = _events;
            yield return null;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.Destroy(_owned[i]);
                _owned.Clear();
                yield return null;
                Assert.That(Root.inst, Is.Null);
            }
            finally
            {
                _input.TearDown();
                NativeLeakDetection.Mode = _leakMode;
                if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
                if (_previousEvents != null) EventSystem.current = _previousEvents;
                Application.runInBackground = _background;
            }
        }
        private IEnumerator Point(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            var hits = new List<RaycastResult>();
            _events.RaycastAll(new PointerEventData(_events) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "The test must hit an actual native Graphic.");
            Assert.That(hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target), Is.True);
        }
        private IEnumerator Click(MouseButton button)
        {
            var point = _mouse.position.ReadValue();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point }.WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }
        [UnityTest]
        public IEnumerator Native_right_click_is_independent_of_left_selection_changed_and_click_sound()
        {
            var button = Make<GrayedTestButton>(); button.mode = ButtonMode.Check;
            int right = 0, left = 0, changed = 0;
            button.onRightClick.AddListener(() => right++);
            button.onClick.AddListener(() => left++);
            button.onChanged.AddListener(() => changed++);
            yield return Point((RectTransform)button.transform);
            var page = button.controller.page;
            yield return Click(MouseButton.Right);
            Assert.That((right, left, changed), Is.EqualTo((1, 0, 0)));
            Assert.That(button.selected, Is.False);
            Assert.That(button.controller.page, Is.EqualTo(page));
            Assert.That(_root.soundSource.isPlaying, Is.False);
            yield return Click(MouseButton.Middle);
            Assert.That((right, left, changed), Is.EqualTo((1, 0, 0)));
            Assert.That(_root.soundSource.isPlaying, Is.False);
            yield return Click(MouseButton.Left);
            Assert.That((right, left, changed), Is.EqualTo((1, 1, 1)));
            Assert.That(button.selected, Is.True);
            Assert.That(_root.soundSource.isPlaying, Is.True, "Left-click audio is still the positive control.");
        }
        [UnityTest]
        public IEnumerator Combo_right_click_does_not_open_dropdown_or_change_its_selection()
        {
            var combo = Make<GrayedTestCombo>();
            combo.items = new[] { "First", "Second" }; combo.selectedIndex = 1;
            var popup = Rect("DropdownTemplate", _canvas, new Vector2(160, 82));
            var list = Rect("list", popup, new Vector2(160, 80));
            var source = list.gameObject.AddComponent<ListSource>();
            var item = Make<GrayedTestButton>();
            source.itemPrefab = item.gameObject; source.itemSize = new Vector2(160, 40); source.layout = ListLayoutType.SingleColumn;
            item.gameObject.SetActive(false);
            popup.gameObject.SetActive(false);
            combo.dropdownPrefab = popup.gameObject;
            int right = 0, left = 0, changed = 0;
            combo.onRightClick.AddListener(() => right++); combo.onClick.AddListener(() => left++); combo.onChanged.AddListener(() => changed++);
            yield return Point((RectTransform)combo.transform);
            yield return Click(MouseButton.Right);
            Assert.That((right, left, changed), Is.EqualTo((1, 0, 0)));
            Assert.That(combo.selectedIndex, Is.EqualTo(1)); Assert.That(combo.text, Is.EqualTo("Second"));
            Assert.That(_root.hasAnyPopup, Is.False); Assert.That(_root.soundSource.isPlaying, Is.False);
            yield return Click(MouseButton.Middle);
            Assert.That(right, Is.EqualTo(1)); Assert.That(_root.hasAnyPopup, Is.False);
            yield return Click(MouseButton.Left);
            Assert.That(_root.hasAnyPopup, Is.True, "The unchanged native left-click path still opens the baked dropdown.");
            Assert.That(_root.soundSource.isPlaying, Is.True);
            Assert.That((right, left, changed), Is.EqualTo((1, 0, 0)), "Opening a combo is not a choice change or its separate onClick event.");
        }
        [UnityTest]
        public IEnumerator Native_dispatch_uses_the_nearest_button_and_plain_components_do_not_swallow_or_redispatch()
        {
            var parent = Make<GrayedTestButton>();
            ((RectTransform)parent.transform).sizeDelta = new Vector2(260, 180);
            var child = Make<GrayedTestButton>(parent.transform);
            var visual = Rect("PlainComponent", child.transform, new Vector2(100, 40));
            visual.gameObject.AddComponent<Component>(); visual.gameObject.AddComponent<UnityEngine.UI.Image>();
            int parentRight = 0, childRight = 0;
            parent.onRightClick.AddListener(() => parentRight++); child.onRightClick.AddListener(() => childRight++);
            yield return Point(visual); yield return Click(MouseButton.Right);
            Assert.That(childRight, Is.EqualTo(1)); Assert.That(parentRight, Is.Zero, "Native click dispatch must not become global bubbling.");
            child.enabled = false; parent.enabled = false;
            yield return Click(MouseButton.Right);
            Assert.That(childRight, Is.EqualTo(1)); Assert.That(parentRight, Is.Zero);
            child.enabled = true; parent.enabled = true;
            yield return Click(MouseButton.Right);
            Assert.That(childRight, Is.EqualTo(2)); Assert.That(parentRight, Is.Zero);
        }
    }
}
