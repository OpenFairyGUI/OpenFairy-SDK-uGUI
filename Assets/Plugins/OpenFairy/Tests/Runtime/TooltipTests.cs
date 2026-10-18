using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class TooltipTests
    {
        private enum Page { Wide }
        private GameObject _canvas;
        private GameObject _eventSystem;
        private Label _template;
        private Root _root;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = (RectTransform)_canvas.transform;
            var designRoot = Rect("Design", canvasRect, new Vector2(640, 480));
            _root = Root.Create(designRoot);
            _eventSystem = new GameObject("Events", typeof(EventSystem));
            var templateRect = Rect("Template", canvasRect, new Vector2(220, 50));
            _template = templateRect.gameObject.AddComponent<Label>();
            var title = Rect("Title", templateRect, new Vector2(200, 30));
            title.anchorMin = title.anchorMax = new Vector2(.5f, .5f);
            title.pivot = new Vector2(.5f, .5f);
            title.anchoredPosition = Vector2.zero;
            var text = title.gameObject.AddComponent<TextComponent>();
#if FAIRYGUI_TMPRO
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "Configure a TMP default font before running layout tests.");
            text.font = TMP_Settings.defaultFontAsset;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = Vector4.zero;
#else
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
#endif
            text.fontSize = 20;
            text.text = "Short";
            _template.titleText = text;
            var fitter = title.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var relation = templateRect.gameObject.AddComponent<Relation>();
            relation.reference = relation.coordinateSpace = templateRect;
            relation.bindings = new[] { new RelationBinding { target = title, pivotAsAnchor = true,
                rules = new[] { new RelationRule { side = RelationSide.Height } } } };
            relation.Record();
            _template.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = size;
            return rt;
        }

        private Tooltip Trigger(string text, float delay = 0)
        {
            var rt = Rect("Trigger", _canvas.transform, new Vector2(80, 40));
            rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            var tooltip = rt.gameObject.AddComponent<Tooltip>();
            tooltip.template = _template;
            tooltip.text = text;
            tooltip.delay = delay;
            return tooltip;
        }

        private PointerEventData Pointer() => new PointerEventData(_eventSystem.GetComponent<EventSystem>())
            { position = new Vector2(100, 100) };

        private Label View => _root.GetComponentInChildren<Label>(true);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_canvas);
            Object.Destroy(_eventSystem);
            yield return null;
            Assert.That(Root.inst, Is.Null);
        }

        [UnityTest]
        public IEnumerator Hover_waits_for_delay_and_exit_or_disable_cancels_pending_work()
        {
            var trigger = Trigger("Delayed", .1f);
            var pointer = Pointer();
            trigger.OnPointerEnter(pointer);
            Assert.That(View.gameObject.activeSelf, Is.False);
            trigger.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(View.gameObject.activeSelf, Is.False);
            trigger.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(View.gameObject.activeSelf, Is.True);
            Assert.That(View.title, Is.EqualTo("Delayed"));
            trigger.gameObject.SetActive(false);
            Assert.That(View.gameObject.activeSelf, Is.False);
            trigger.gameObject.SetActive(true);
            trigger.OnPointerEnter(pointer);
            trigger.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(View.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Stale_exit_does_not_hide_the_new_owner_and_press_does_not_steal_parent_events()
        {
            var first = Trigger("First");
            var second = Trigger("Second");
            var parent = Rect("Button", _canvas.transform, new Vector2(80, 40));
            parent.gameObject.AddComponent<UnityEngine.UI.Button>();
            second.transform.SetParent(parent, false);
            var pointer = Pointer();
            first.OnPointerEnter(pointer);
            var view = View;
            second.OnPointerEnter(pointer);
            first.OnPointerExit(pointer);
            Assert.That(View, Is.SameAs(view));
            Assert.That(view.title, Is.EqualTo("Second"));
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That((object)second is IPointerDownHandler, Is.False);
            Assert.That((object)second is IBeginDragHandler, Is.False);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(second.gameObject), Is.SameAs(parent.gameObject));
            pointer.pointerPress = second.gameObject;
            yield return null;
            Assert.That(view.gameObject.activeSelf, Is.False);
            pointer.pointerPress = null;
            second.OnPointerEnter(pointer);
            pointer.eligibleForClick = true;
            yield return null;
            Assert.That(view.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Long_text_resizes_root_once_preserves_margins_and_never_intercepts_rays()
        {
            const string longText = "This longer message wraps into several lines while keeping the same authored width and outer margins.";
            var authoredMargin = ((RectTransform)_template.transform).rect.height - _template.titleText.rectTransform.rect.height;
            _root.ShowTooltip(_template, "Short", new Vector2(630, 470), 0);
            var view = View;
            var rt = (RectTransform)view.transform;
            var oldHeight = rt.rect.height;
            var shortPreferred = view.titleText.preferredHeight;
            Assert.That(view.titleText.rectTransform.rect.height, Is.EqualTo(shortPreferred).Within(.01f));
            Assert.That(oldHeight, Is.EqualTo(authoredMargin + shortPreferred).Within(.01f));
            _root.ShowTooltip(_template, longText, new Vector2(630, 470), 0);
            Assert.That(View, Is.SameAs(view));
            var longPreferred = view.titleText.preferredHeight;
            Assert.That(longPreferred, Is.GreaterThan(shortPreferred), "The fixture must actually wrap with the configured font.");
            Assert.That(view.titleText.rectTransform.rect.height, Is.EqualTo(longPreferred).Within(.01f));
            Assert.That(rt.rect.height, Is.GreaterThan(oldHeight));
            Assert.That(rt.rect.width, Is.EqualTo(220));
            Assert.That(rt.rect.height, Is.EqualTo(authoredMargin + longPreferred).Within(.01f));
            var stableSize = rt.rect.size;
            var stablePosition = rt.anchoredPosition;
            for (var i = 0; i < 8; i++)
            {
                yield return null;
                AssertSize(rt, stableSize);
                Assert.That(rt.anchoredPosition, Is.EqualTo(stablePosition));
            }
            Assert.That(rt.anchoredPosition.x, Is.GreaterThanOrEqualTo(0));
            Assert.That(rt.anchoredPosition.x + rt.rect.width, Is.LessThanOrEqualTo(_root.size.x));
            Assert.That(-rt.anchoredPosition.y + rt.rect.height, Is.LessThanOrEqualTo(_root.size.y));
            Assert.That(view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            foreach (var graphic in view.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
            _root.ShowTooltip(_template, "Short", Vector2.zero, 0);
            Assert.That(rt.rect.height, Is.EqualTo(oldHeight).Within(.01f));
        }

        [UnityTest]
        public IEnumerator Native_layout_keeps_content_owned_axes_when_size_gear_is_applied()
        {
            var text = _template.titleText;
            _template.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
            var height = ((RectTransform)text.transform).rect.height;
            Assert.That(height, Is.EqualTo(text.preferredHeight).Within(.01f));
            var gear = new GearSize<Page> { target = text.gameObject, pages = new[] { Page.Wide },
                sizes = new[] { new Vector2(300, 400) }, scales = new[] { Vector2.one } };
            gear.Apply(Page.Wide);
            Assert.That(((RectTransform)text.transform).rect.width, Is.EqualTo(300));
            Assert.That(((RectTransform)text.transform).rect.height, Is.EqualTo(height));
            yield return null;
            Assert.That(text.rectTransform.rect.height, Is.EqualTo(height).Within(.01f));
            text.text = "A longer caption must wrap after the nonautomatic width is narrowed.";
            gear.sizes[0] = new Vector2(80, 400);
            gear.Apply(Page.Wide);
            LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
            Assert.That(text.rectTransform.rect.width, Is.EqualTo(80));
            Assert.That(text.rectTransform.rect.height, Is.EqualTo(text.preferredHeight).Within(.01f));
            Assert.That(text.rectTransform.rect.height, Is.GreaterThan(height));
            Assert.That(text.rectTransform.rect.height, Is.Not.EqualTo(400));
        }

        [UnityTest]
        public IEnumerator Parent_following_an_automatic_child_stabilizes_without_size_feedback()
        {
            var text = _template.titleText;
#if FAIRYGUI_TMPRO
            text.textWrappingMode = TextWrappingModes.NoWrap;
#else
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
#endif
            text.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var relation = _template.GetComponent<Relation>();
            relation.bindings[0].rules = new[] { new RelationRule { side = RelationSide.RightExtRight },
                new RelationRule { side = RelationSide.Height } };
            relation.Record();
            var authoredRoot = ((RectTransform)_template.transform).rect.size;
            var authoredTitle = text.rectTransform.rect.size;
            _root.ShowTooltip(_template, "Wider caption", Vector2.zero, 0);
            var rt = (RectTransform)View.transform;
            var measured = new Vector2(View.titleText.preferredWidth, View.titleText.preferredHeight);
            // Parent RightExtRight follows the child's right edge: delta width * (1 - pivot).
            var expected = new Vector2(authoredRoot.x + (measured.x - authoredTitle.x) * (1 - text.rectTransform.pivot.x),
                authoredRoot.y + measured.y - authoredTitle.y);
            AssertSize(View.titleText.rectTransform, measured);
            AssertSize(rt, expected);
            var stable = rt.rect.size;
            for (var i = 0; i < 8; i++)
            {
                yield return null;
                AssertSize(rt, stable);
            }
        }

        [UnityTest]
        public IEnumerator Untouchable_sources_and_tooltip_presentations_are_excluded_from_native_raycasting()
        {
            var trigger = Trigger("Visible");
            var pointer = Pointer();
            var rt = (RectTransform)trigger.transform;
            yield return null; // Give the new overlay canvas its native rect, batch and Graphic.depth.
            Canvas.ForceUpdateCanvases();
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var graphic = trigger.GetComponent<Graphic>();
            Assert.That(graphic.depth, Is.GreaterThanOrEqualTo(0),
                $"Native canvas batch required: canvas={_canvas.GetComponent<Canvas>().pixelRect}, point={pointer.position}, rect={rt.rect}, culled={graphic.canvasRenderer.cull}.");
            Assert.That(_canvas.GetComponent<Canvas>().pixelRect.Contains(pointer.position), Is.True);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rt, pointer.position, null), Is.True);
            var hits = new List<RaycastResult>();
            _eventSystem.GetComponent<EventSystem>().RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == trigger.gameObject), Is.True);
            trigger.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            hits.Clear();
            _eventSystem.GetComponent<EventSystem>().RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == trigger.gameObject), Is.False);
            _root.ShowTooltip(_template, "Visible", Vector2.zero, 0);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var title = View.titleText.rectTransform;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, title.TransformPoint(title.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(title, pointer.position, null), Is.True);
            hits.Clear();
            _eventSystem.GetComponent<EventSystem>().RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject.transform.IsChildOf(View.transform)), Is.False);
        }

        private static void AssertSize(RectTransform rect, Vector2 expected)
        {
            Assert.That(rect.rect.width, Is.EqualTo(expected.x).Within(.01f));
            Assert.That(rect.rect.height, Is.EqualTo(expected.y).Within(.01f));
        }
    }
}
