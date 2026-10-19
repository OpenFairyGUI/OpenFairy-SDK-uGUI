using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public enum GrayedTestPage { up, down, over, selectedOver, disabled, selectedDisabled }
    public enum GrayedBasicTestPage { up, down, over, selectedOver }
    public sealed class GrayedTestButton : NamedStateButton<GrayedTestPage> { }
    public sealed class GrayedBasicTestButton : NamedStateButton<GrayedBasicTestPage> { }
    public sealed class GrayedTestCombo : NamedStateCombo<GrayedTestPage> { }

    public class ButtonGrayedTests
    {
        private readonly List<GameObject> objects = new();

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            objects.Add(go);
            go.transform.SetParent(parent, false);
            return go;
        }

        private PointerEventData Pointer() => new PointerEventData(Make("Events").AddComponent<EventSystem>())
            { button = PointerEventData.InputButton.Left };

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Common_grayed_buttons_keep_disabled_visuals_and_dispatch_clicks()
        {
            var button = Make("Button").AddComponent<GrayedTestButton>();
            button.mode = ButtonMode.Common;
            var pointer = Pointer();
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.grayed = true;
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.disabled));
            button.OnPointerEnter(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.disabled));
            button.OnPointerDown(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.selectedDisabled));
            button.OnPointerUp(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.disabled));
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
            pointer.button = PointerEventData.InputButton.Right;
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grayed_check_and_radio_buttons_still_update_selection_and_changed_events()
        {
            var button = Make("Check", Make("Panel").transform).AddComponent<GrayedTestButton>();
            var pointer = Pointer();
            var changed = 0;
            button.onChanged.AddListener(() => changed++);
            button.mode = ButtonMode.Check;
            button.grayed = true;
            button.OnPointerClick(pointer);
            Assert.That(button.selected, Is.True);
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.selectedDisabled));
            button.OnPointerClick(pointer);
            Assert.That(button.selected, Is.False);
            Assert.That(button.controller.page, Is.EqualTo(GrayedTestPage.disabled));
            Assert.That(changed, Is.EqualTo(2));
            button.mode = ButtonMode.Radio;
            button.OnPointerClick(pointer);
            button.OnPointerClick(pointer);
            Assert.That(button.selected, Is.True);
            Assert.That(changed, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grayed_buttons_without_disabled_pages_keep_normal_pointer_states()
        {
            var button = Make("Button").AddComponent<GrayedBasicTestButton>();
            button.grayed = true;
            var pointer = Pointer();
            button.OnPointerEnter(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.over));
            button.OnPointerDown(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.down));
            button.OnPointerUp(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.over));
            button.OnPointerExit(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.up));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Releasing_outside_a_check_button_leaves_its_hover_page()
        {
            var button = Make("Check").AddComponent<GrayedBasicTestButton>();
            button.mode = ButtonMode.Check;
            var pointer = Pointer();
            button.OnPointerEnter(pointer);
            button.OnPointerDown(pointer);
            button.OnPointerExit(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.over));
            button.OnPointerUp(pointer);
            Assert.That(button.controller.page, Is.EqualTo(GrayedBasicTestPage.up));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scale_down_effect_shrinks_around_the_center_while_pressed()
        {
            var canvas = Make("Canvas");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = Make("Button", canvas.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(100, 40);
            var button = go.AddComponent<GrayedBasicTestButton>();
            button.downEffect = ButtonDownEffect.Scale;
            button.downEffectValue = .5f;
            var center = rt.TransformPoint(rt.rect.center);
            var pointer = Pointer();
            button.OnPointerDown(pointer);
            Assert.That(rt.localScale.x, Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(Vector3.Distance(rt.TransformPoint(rt.rect.center), center), Is.LessThan(1e-3f));
            button.OnPointerUp(pointer);
            Assert.That(rt.localScale.x, Is.EqualTo(1).Within(1e-5f));
            Assert.That(Vector3.Distance(rt.TransformPoint(rt.rect.center), center), Is.LessThan(1e-3f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Touchable_is_a_native_raycast_channel_separate_from_visual_graying()
        {
            var canvas = Make("Canvas");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<GraphicRaycaster>();
            var go = Make("Button", canvas.transform);
            go.AddComponent<UnityEngine.UI.Image>();
            var button = go.AddComponent<GrayedTestButton>();
            button.grayed = true;
            var group = go.AddComponent<CanvasGroup>();
            var events = Make("Events").AddComponent<EventSystem>();
            var pointer = new PointerEventData(events);
            yield return null; // New graphics have depth -1 until the native canvas has rendered them.
            Canvas.ForceUpdateCanvases();
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, go.transform.position);
            Assert.That(go.GetComponent<Graphic>().depth, Is.GreaterThanOrEqualTo(0),
                $"Native canvas batch required: canvas={canvas.GetComponent<Canvas>().pixelRect}, point={pointer.position}, rect={((RectTransform)go.transform).rect}.");
            Assert.That(canvas.GetComponent<Canvas>().pixelRect.Contains(pointer.position), Is.True);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)go.transform, pointer.position, null), Is.True);
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == go), Is.True);
            group.blocksRaycasts = false;
            hits.Clear();
            events.RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == go), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Native_selectable_interactable_false_blocks_its_click_without_becoming_a_gray_flag()
        {
            var button = Make("NativeButton").AddComponent<UnityEngine.UI.Button>();
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);
            var pointer = Pointer();
            button.interactable = false;
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.Zero);
            button.interactable = true;
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grayed_combo_boxes_keep_their_dropdown_behavior()
        {
            var canvas = Make("Canvas");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = Make("Combo", canvas.transform);
            var combo = go.AddComponent<GrayedTestCombo>();
            var dropdown = Make("Dropdown");
            var list = Make("list", dropdown.transform);
            var source = list.AddComponent<ListSource>();
            var item = Make("Item");
            item.AddComponent<GrayedTestButton>();
            source.itemPrefab = item;
            source.itemSize = new Vector2(100, 30);
            combo.dropdownPrefab = dropdown;
            combo.items = new[] { "First", "Second" };
            combo.grayed = true;
            combo.OnPointerDown(Pointer());
            Assert.That(Root.inst.hasAnyPopup, Is.True);
            Root.inst.HidePopup();
            yield return null;
        }
    }
}
