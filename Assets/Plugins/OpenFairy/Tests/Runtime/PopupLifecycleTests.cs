using System.Collections;
using Cysharp.Threading.Tasks;
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
    public class PopupLifecycleTests
    {
        private readonly InputTestFixture _input = new();
        private NativeLeakDetectionMode _leakMode;
        private GameObject _canvas, _events;
        private Root _root;
        private PopupInputModule _module;
        private Mouse _mouse, _previousMouse;
        private EventSystem _previousEvents;
        private InputActionAsset _actions;
        private bool _background;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Root.inst, Is.Null, "Run popup tests in an empty scene.");
            _background = Application.runInBackground;
            Application.runInBackground = true;
            _previousMouse = Mouse.current;
            _leakMode = NativeLeakDetection.Mode;
            _input.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _previousEvents = EventSystem.current;
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _root = Root.Create((RectTransform)_canvas.transform);
            _events = new GameObject("Events");
            _events.SetActive(false);
            var events = _events.AddComponent<EventSystem>();
            _module = _events.AddComponent<PopupInputModule>();
            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = _actions.AddActionMap("UI");
            _module.point = InputActionReference.Create(map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position"));
            _module.leftClick = InputActionReference.Create(map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton"));
            _module.rightClick = InputActionReference.Create(map.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton"));
            _module.middleClick = InputActionReference.Create(map.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton"));
            _events.SetActive(true);
            EventSystem.current = events;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                var actions = new[] { _module.point, _module.leftClick, _module.rightClick, _module.middleClick };
                Object.Destroy(_canvas);
                Object.Destroy(_events);
                yield return null;
                foreach (var action in actions)
                    Object.Destroy(action);
                Object.Destroy(_actions);
                InputSystem.RemoveDevice(_mouse);
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

        private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Popup(Vector2 position)
        {
            var popup = Rect("Popup", _root.rect, position, new Vector2(120, 80));
            popup.gameObject.AddComponent<UnityEngine.UI.Image>();
            popup.gameObject.SetActive(false);
            return popup;
        }

        private PopupTestWindow Window(bool animated = true)
        {
            var rect = Popup(new Vector2(30, 30));
            var window = rect.gameObject.AddComponent<PopupTestWindow>();
            if (animated)
            {
                window.showTransition = Animation(rect, 0, 1);
                window.hideTransition = Animation(rect, 1, 0);
            }
            return window;
        }

        private Transition Animation(RectTransform target, float from, float to)
        {
            var transition = target.gameObject.AddComponent<Transition>();
            transition.items = new[] { new TransitionItem { type = TransitionItemType.Alpha, target = target,
                tween = true, duration = .12f, start = new[] { from }, end = new[] { to } } };
            return transition;
        }

        private UnityEngine.UI.Button Button(Transform parent, Vector2 position)
        {
            var rect = Rect("Button", parent, position, new Vector2(100, 50));
            rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            return rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        }

        private static bool Canceled(System.Threading.Tasks.Task<bool> task)
        {
            Assert.That(task.IsCompleted, Is.True, "Popup did not finish; do not block the player loop.");
            return task.GetAwaiter().GetResult();
        }

        private IEnumerator Click(RectTransform target, MouseButton button = MouseButton.Left)
        {
            Assert.That(_mouse.enabled, Is.True);
            Assert.That(Mouse.current, Is.SameAs(_mouse));
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Animated_popup_waits_for_hide_and_leaves_logical_stack_before_the_animation_finishes()
        {
            var window = Window();
            var closed = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            Assert.That(window.initialized, Is.EqualTo(1));
            Assert.That(window.shown, Is.Zero);
            yield return new WaitForSeconds(.2f);
            Assert.That(window.shown, Is.EqualTo(1));
            _root.HidePopup(window.contentPane);
            Assert.That(_root.hasAnyPopup, Is.False);
            Assert.That(window.gameObject.activeSelf, Is.True);
            Assert.That(closed.IsCompleted, Is.False);
            Assert.That(window.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            yield return new WaitForSeconds(.2f);
            Assert.That(window.gameObject.activeSelf, Is.False);
            Assert.That(window.hidden, Is.EqualTo(1));
            Assert.That(closed.IsCompleted, Is.True);
            Assert.That(Canceled(closed), Is.False);
        }

        [UnityTest]
        public IEnumerator Reopening_cancels_the_old_wait_and_old_animation_cannot_hide_the_new_window()
        {
            var window = Window();
            var first = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            _root.HidePopup(window.contentPane);
            yield return null;
            var second = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            Assert.That(first.IsCompleted, Is.True);
            Assert.That(Canceled(first), Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(window.gameObject.activeSelf, Is.True);
            Assert.That(window.hidden, Is.Zero);
            Assert.That(second.IsCompleted, Is.False);
            _root.HidePopupImmediately(window.contentPane);
            Assert.That(Canceled(second), Is.False);
            Assert.That(window.hidden, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Lower_popup_receives_the_same_native_click_and_only_higher_popup_closes()
        {
            var lower = Popup(new Vector2(20, 20));
            var upper = Popup(new Vector2(220, 20));
            var button = Button(lower, new Vector2(10, 10));
            var clicks = 0;
            button.onClick.AddListener(() => ++clicks);
            var lowerClosed = _root.ShowPopup(lower).SuppressCancellationThrow().AsTask();
            var upperClosed = _root.ShowPopup(upper).SuppressCancellationThrow().AsTask();
            yield return Click((RectTransform)button.transform);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(lowerClosed.IsCompleted, Is.False);
            Assert.That(Canceled(upperClosed), Is.False);
            Assert.That(lower.gameObject.activeSelf, Is.True);
            Assert.That(upper.gameObject.activeSelf, Is.False);
            _root.HidePopup(lower);
            Assert.That(Canceled(lowerClosed), Is.False);
        }

        [UnityTest]
        public IEnumerator Page_click_is_not_consumed_and_toggle_does_not_reopen_until_the_next_press()
        {
            var popup = Popup(new Vector2(220, 20));
            var button = Button(_canvas.transform, new Vector2(20, 160));
            var clicks = 0;
            button.onClick.AddListener(() => { ++clicks; _root.TogglePopup(popup).SuppressCancellationThrow().Forget(); });
            _root.ShowPopup(popup).SuppressCancellationThrow().Forget();
            yield return Click((RectTransform)button.transform);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(_root.hasAnyPopup, Is.False);
            yield return Click((RectTransform)button.transform);
            Assert.That(clicks, Is.EqualTo(2));
            Assert.That(_root.hasAnyPopup, Is.True);
        }

        [UnityTest]
        public IEnumerator External_disable_and_root_disable_cancel_waiters_and_reenable_starts_clean()
        {
            var window = Window();
            var first = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            window.gameObject.SetActive(false);
            Assert.That(Canceled(first), Is.True);
            Assert.That(_root.hasAnyPopup, Is.False);
            var second = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            _root.enabled = false;
            Assert.That(Canceled(second), Is.True);
            Assert.That(window.gameObject.activeSelf, Is.False);
            _root.enabled = true;
            var third = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            yield return new WaitForSeconds(.25f);
            Assert.That(window.shown, Is.EqualTo(1));
            _root.HidePopupImmediately(window.contentPane);
            Assert.That(Canceled(third), Is.False);
            Assert.That(window.initialized, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Closing_callbacks_can_open_a_new_popup_without_the_old_close_range_removing_it()
        {
            var first = Window(false);
            var second = Window(false);
            var reopened = false;
            var firstClosed = _root.ShowPopup(first.contentPane).SuppressCancellationThrow().AsTask();
            _root.ShowPopup(second.contentPane).SuppressCancellationThrow().Forget();
            second.whenHidden = () =>
            {
                if (reopened) return;
                reopened = true;
                _root.ShowPopup(first.contentPane).SuppressCancellationThrow().Forget();
            };
            _root.HidePopup();
            Assert.That(Canceled(firstClosed), Is.True);
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(_root.hasAnyPopup, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Modal_layer_stays_below_the_top_modal_and_regular_windows_stay_below_it()
        {
            var modal = Window(false);
            modal.modal = true;
            modal.Show();
            var regular = Window(false);
            regular.Show();
            Assert.That(_root.hasModalWindow, Is.True);
            Assert.That(regular.contentPane.GetSiblingIndex(), Is.LessThan(modal.contentPane.GetSiblingIndex()));
            _root.BringToFront(regular);
            Assert.That(regular.contentPane.GetSiblingIndex(), Is.LessThan(modal.contentPane.GetSiblingIndex()));
            modal.Hide();
            Assert.That(_root.hasModalWindow, Is.False);
            Assert.That(_root.activeWindowCount, Is.EqualTo(1));
            regular.HideImmediately();
            Assert.That(_root.activeWindowCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Disabled_input_module_does_not_close_popups_and_reenable_restores_native_clicks()
        {
            var popup = Popup(new Vector2(220, 20));
            var button = Button(_canvas.transform, new Vector2(20, 160));
            var closed = _root.ShowPopup(popup).SuppressCancellationThrow().AsTask();
            _module.enabled = false;
            yield return Click((RectTransform)button.transform);
            Assert.That(closed.IsCompleted, Is.False);
            _module.enabled = true;
            yield return null;
            yield return Click((RectTransform)button.transform, MouseButton.Right);
            Assert.That(Canceled(closed), Is.False);
        }

        [UnityTest]
        public IEnumerator Prebound_close_button_uses_the_window_hide_animation()
        {
            var window = Window();
            var close = Rect("Close", window.transform, new Vector2(10, 10), new Vector2(40, 40));
            close.gameObject.AddComponent<UnityEngine.UI.Image>();
            window.closeButton = close.gameObject.AddComponent<ButtonAliasTestPanel>();
            var closed = _root.ShowPopup(window.contentPane).SuppressCancellationThrow().AsTask();
            yield return new WaitForSeconds(.2f);
            yield return Click(close);
            yield return new WaitForSeconds(.2f);
            Assert.That(Canceled(closed), Is.False);
            Assert.That(window.hidden, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Prebound_drag_area_uses_native_pointer_drag_and_preserves_design_coordinates()
        {
            var window = Window(false);
            var header = Rect("Header", window.transform, Vector2.zero, new Vector2(120, 40));
            header.gameObject.AddComponent<UnityEngine.UI.Image>();
            header.gameObject.AddComponent<WindowDragArea>().window = window;
            window.Show();
            Canvas.ForceUpdateCanvases();
            var initial = window.contentPane.anchoredPosition;
            var point = RectTransformUtility.WorldToScreenPoint(null, header.TransformPoint(header.rect.center));
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point + new Vector2(10, 0) }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point + new Vector2(50, 30) }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point + new Vector2(50, 30) });
            yield return null;
            Assert.That(window.contentPane.anchoredPosition.x, Is.EqualTo(initial.x + 40).Within(.1f));
            Assert.That(window.contentPane.anchoredPosition.y, Is.EqualTo(initial.y + 30).Within(.1f));
            window.HideImmediately();
        }
    }
}
