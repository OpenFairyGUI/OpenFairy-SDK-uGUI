using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PopupSelfReentryTests
    {
        private GameObject _canvas;
        private Root _root;
        private PopupSelfReentryWindow _window;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Root.inst, Is.Null);
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _root = Root.Create((RectTransform)_canvas.transform);
            var window = new GameObject("Window", typeof(RectTransform));
            window.transform.SetParent(_canvas.transform, false);
            ((RectTransform)window.transform).sizeDelta = new Vector2(120, 80);
            _window = window.AddComponent<PopupSelfReentryWindow>();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _window.whenHidden = null;
            Object.Destroy(_canvas);
            yield return null;
        }
        private static bool Canceled(Task<bool> task)
        {
            Assert.That(task.IsCompleted, Is.True);
            return task.GetAwaiter().GetResult();
        }
        [UnityTest]
        public IEnumerator Showing_the_same_popup_during_OnHide_does_not_duplicate_its_next_hide_animation()
        {
            var first = _root.ShowPopup(_window.contentPane).SuppressCancellationThrow().AsTask();
            Task<bool> nested = null;
            _window.whenHidden = () => {
                _window.whenHidden = null;
                nested = _root.ShowPopup(_window.contentPane).SuppressCancellationThrow().AsTask();
            };
            var outer = _root.ShowPopup(_window.contentPane).SuppressCancellationThrow().AsTask();
            Assert.That(nested, Is.Not.Null, "The same-window OnHide callback must actually have run.");
            Assert.That(Canceled(first), Is.True);
            Assert.That(Canceled(nested), Is.True, "Preserve the existing outer replacement cancellation semantics.");
            Assert.That(outer.IsCompleted, Is.False);
            Assert.That(_window.gameObject.activeSelf, Is.True);
            _window.waitOnHide = true;
            int before = _window.hideAnimations;
            _root.HidePopup(_window.contentPane);
            Assert.That(_window.hideAnimations - before, Is.EqualTo(1), "One popup must invoke one hide animation, even when a previous OnHide reopened it.");
            Assert.That(_root.hasAnyPopup, Is.False);
            for (int i = 0; i < 4 && !outer.IsCompleted; i++) yield return null;
            Assert.That(Canceled(outer), Is.False);
            Assert.That(_window.gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator Showing_a_regular_window_after_OnHide_reopens_it_as_popup_does_not_leave_an_orphan_popup()
        {
            var first = _root.ShowPopup(_window.contentPane).SuppressCancellationThrow().AsTask();
            Task<bool> nested = null;
            _window.whenHidden = () => {
                _window.whenHidden = null;
                nested = _root.ShowPopup(_window.contentPane).SuppressCancellationThrow().AsTask();
            };
            _root.ShowWindow(_window);
            Assert.That(nested, Is.Not.Null);
            Assert.That(Canceled(first), Is.True);
            Assert.That(Canceled(nested), Is.True);
            Assert.That(_root.activeWindowCount, Is.EqualTo(1));
            Assert.That(_root.hasAnyPopup, Is.False, "The outer operation requested a regular Window, not a leftover popup session.");
            Assert.DoesNotThrow(() => _root.HidePopup());
            _window.HideImmediately();
            Assert.That(_root.activeWindowCount, Is.Zero);
            yield return null;
        }
    }
}
