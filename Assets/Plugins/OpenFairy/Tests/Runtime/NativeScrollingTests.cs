using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Page = OpenFairy.UGUI.Tests.ControllerActionTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class NativeScrollingTests
    {
        private readonly List<GameObject> _objects = new();

        private RectTransform Rect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _objects.Add(go);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private ListSource List(bool horizontal = false)
        {
            var root = Rect("List", null, new Vector2(200, 120), Vector2.zero);
            var viewport = Rect("viewport", root, root.rect.size, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var template = Rect("Template", null, horizontal ? new Vector2(40, 120) : new Vector2(200, 30), Vector2.zero);
            template.gameObject.SetActive(false);
            var source = root.gameObject.AddComponent<ListSource>();
            source.itemPrefab = template.gameObject;
            source.itemSize = template.rect.size;
            source.layout = horizontal ? ListLayoutType.SingleRow : ListLayoutType.SingleColumn;
            return source;
        }

        private EventSystem Events()
        {
            var go = new GameObject("Events", typeof(EventSystem));
            _objects.Add(go);
            return go.GetComponent<EventSystem>();
        }

        private static PointerEventData Pointer(EventSystem events, RectTransform rect, Vector2 local)
        {
            var position = (Vector2)rect.TransformPoint(local);
            return new PointerEventData(events) { position = position,
                pointerPressRaycast = new RaycastResult { gameObject = rect.gameObject, screenPosition = position } };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.Destroy(_objects[i]);
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Native_drag_wheel_and_programmatic_events_respect_input_modes()
        {
            var source = List();
            source.SetVirtual();
            source.Fill(20, (_, _) => { });
            var pane = source.scrollPane;
            Assert.IsInstanceOf<ScrollRect>(pane);
            pane.bounceEffect = false;
            pane.inertia = false;
            yield return null;
            var pointer = Pointer(Events(), pane.viewport, new Vector2(100, -60));
            pane.OnBeginDrag(pointer);
            pointer.position += new Vector2(0, 20);
            pane.OnDrag(pointer);
            pane.OnEndDrag(pointer);
            Assert.That(pane.position.y, Is.EqualTo(20).Within(.01f));
            pointer.scrollDelta = new Vector2(0, -1);
            pane.OnScroll(pointer);
            Assert.That(pane.position.y, Is.EqualTo(70).Within(.01f));
            pane.touchEffect = false;
            pane.OnBeginDrag(pointer);
            pointer.position += new Vector2(0, 50);
            pane.OnDrag(pointer);
            pane.OnEndDrag(pointer);
            Assert.That(pane.position.y, Is.EqualTo(70).Within(.01f));
            pane.mouseWheelEnabled = false;
            pane.OnScroll(pointer);
            Assert.That(pane.position.y, Is.EqualTo(70).Within(.01f));
            var events = 0;
            pane.onScroll.AddListener(() => events++);
            pane.SetPosition(new Vector2(0, 200));
            Assert.That(events, Is.EqualTo(1));
            yield return null;
            Assert.That(events, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Native_drag_origin_survives_loop_rebasing()
        {
            var source = List(true);
            source.colGap = 5;
            source.SetVirtual(true);
            source.Fill(2, (_, _) => { });
            var pane = source.scrollPane;
            pane.inertia = false;
            yield return null;
            var pointer = Pointer(Events(), pane.viewport, new Vector2(100, -60));
            pane.OnBeginDrag(pointer);
            pointer.position -= new Vector2(270, 0);
            pane.OnDrag(pointer);
            yield return null;
            var before = pane.position.x;
            pointer.position -= new Vector2(1, 0);
            pane.OnDrag(pointer);
            Assert.That(pane.position.x - before, Is.EqualTo(1).Within(.01f));
            pane.OnEndDrag(pointer);
            Assert.That(source.visibleItems.Count, Is.LessThanOrEqualTo(8));
        }

        [UnityTest]
        public IEnumerator Measured_resize_during_native_drag_keeps_the_pointer_origin_and_velocity_baseline()
        {
            var source = List();
            source.SetVirtual();
            var heights = new float[1000];
            for (var i = 0; i < heights.Length; i++) heights[i] = 30;
            source.Fill(1000, (_, _) => { }, measureHeight: (_, index) => heights[index]);
            var pane = source.scrollPane;
            pane.bounceEffect = false;
            yield return null;
            var pointer = Pointer(Events(), pane.viewport, new Vector2(100, -60));
            pane.OnBeginDrag(pointer);
            pointer.position += new Vector2(0, 50);
            pane.OnDrag(pointer);
            yield return null;
            var anchor = source.firstVisibleIndex;
            Assert.Greater(anchor, 0);
            heights[anchor - 1] += 40;
            pane.velocity = new Vector2(0, 140);
            yield return null;
            Assert.That(Mathf.Abs(pane.velocity.y), Is.LessThan(1000));
            var before = pane.position.y;
            pointer.position += new Vector2(0, 1);
            pane.OnDrag(pointer);
            Assert.That(pane.position.y - before, Is.EqualTo(1).Within(.01f));
            pane.OnEndDrag(pointer);
        }

        [UnityTest]
        public IEnumerator Native_scrollbar_preserves_grip_offset_and_authored_track_step()
        {
            var source = List();
            source.SetVirtual();
            source.Fill(20, (_, _) => { });
            var pane = source.scrollPane;
            var outer = Rect("Scrollbar", (RectTransform)source.transform, new Vector2(20, 120), new Vector2(210, 0));
            var area = Rect("Track", outer, new Vector2(20, 100), new Vector2(0, -10));
            var grip = Rect("Grip", area, new Vector2(20, 20), Vector2.zero);
            var bar = outer.gameObject.AddComponent<AuthoredScrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = grip;
            grip.offsetMin = grip.offsetMax = Vector2.zero;
            bar.pane = pane;
            bar.horizontal = false;
            pane.verticalScrollbar = bar;
            pane.SetPosition(Vector2.zero);
            yield return null;
            Assert.That(bar.size, Is.EqualTo(.2f).Within(.001f));
            var pointer = Pointer(Events(), grip, new Vector2(10, -5));
            bar.OnBeginDrag(pointer);
            pointer.position -= new Vector2(0, 20);
            bar.OnDrag(pointer);
            Assert.That(pane.position.y, Is.EqualTo(120).Within(.1f));
            bar.OnEndDrag(pointer);
            pointer.position = area.TransformPoint(new Vector2(10, -95));
            bar.OnPointerDown(pointer);
            Assert.That(pane.position.y, Is.EqualTo(220).Within(.1f));
        }

        [UnityTest]
        public IEnumerator Every_scrollbar_direction_hides_zero_and_full_grips_and_restores_overflow()
        {
            foreach (var direction in new[] { Scrollbar.Direction.LeftToRight, Scrollbar.Direction.RightToLeft, Scrollbar.Direction.BottomToTop, Scrollbar.Direction.TopToBottom })
            {
                var root = Rect("Scrollbar", null, new Vector2(200, 200), Vector2.zero);
                var grip = Rect("Grip", root, new Vector2(20, 20), Vector2.zero);
                var bar = root.gameObject.AddComponent<AuthoredScrollbar>();
                bar.handleRect = grip;
                bar.direction = direction;
                foreach (var size in new[] { 0f, .5f, 1f, .25f })
                {
                    bar.size = size;
                    yield return null;
                    Assert.That(grip.gameObject.activeSelf, Is.EqualTo(size is > 0 and < 1), $"{direction}, size={size}");
                    Assert.That(root.gameObject.activeSelf, Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator Non_list_content_size_changes_refresh_native_bounds_and_grips()
        {
            var root = Rect("Scroll", null, new Vector2(200, 120), Vector2.zero);
            var viewport = Rect("viewport", root, root.rect.size, Vector2.zero);
            var content = Rect("content", viewport, new Vector2(200, 300), Vector2.zero);
            var child = Rect("Text", content, new Vector2(200, 300), Vector2.zero);
            var pane = root.gameObject.AddComponent<ScrollPane>();
            pane.viewport = viewport;
            pane.content = content;
            pane.horizontal = false;
            var track = Rect("Scrollbar", root, new Vector2(20, 120), new Vector2(200, 0));
            var grip = Rect("Grip", track, new Vector2(20, 20), Vector2.zero);
            var bar = track.gameObject.AddComponent<AuthoredScrollbar>();
            bar.pane = pane;
            bar.handleRect = grip;
            pane.verticalScrollbar = bar;
            foreach (var height in new[] { 300f, 0f, 240f })
            {
                child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                yield return null;
                yield return null;
                Assert.That(pane.contentSize.y, Is.EqualTo(height).Within(.01f));
                Assert.That(grip.gameObject.activeSelf, Is.EqualTo(height > 120));
            }
        }

        [UnityTest]
        public IEnumerator Animated_scroll_uses_the_unscaled_engine_clock_with_manual_defaults()
        {
            DG.Tweening.DOTween.Init();
            var update = DG.Tweening.DOTween.defaultUpdateType;
            var timeScale = Time.timeScale;
            var source = List();
            source.SetVirtual();
            source.Fill(20, (_, _) => { });
            try
            {
                DG.Tweening.DOTween.defaultUpdateType = DG.Tweening.UpdateType.Manual;
                Time.timeScale = 0;
                source.scrollPane.SetPosition(new Vector2(0, 200), true);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(source.scrollPane.position.y, Is.EqualTo(200).Within(.01f));
                Assert.That(DG.Tweening.DOTween.defaultUpdateType, Is.EqualTo(DG.Tweening.UpdateType.Manual));
            }
            finally
            {
                source.scrollPane.StopMovement();
                DG.Tweening.DOTween.defaultUpdateType = update;
                Time.timeScale = timeScale;
            }
        }

        [UnityTest]
        public IEnumerator Page_controller_and_native_position_update_in_both_directions_without_feedback()
        {
            var source = List();
            source.SetVirtual();
            source.Fill(12, (_, _) => { });
            var pane = source.scrollPane;
            var panel = source.gameObject.AddComponent<ControllerActionTestPanel>();
            var gear = new GearScrollPage<Page> { target = source.gameObject, pane = pane,
                pages = new[] { Page.First, Page.Second, Page.Third } };
            panel.first.gears = new Gear<Page>[] { gear };
            pane.pageOwner = panel;
            pane.pageController = 0;
            pane.pageCount = 3;
            pane.pageMode = true;
            pane.SetPage(2);
            Assert.That(panel.first.page, Is.EqualTo(Page.Third));
            Assert.That(pane.position.y, Is.EqualTo(240).Within(.01f));
            gear.Apply(Page.Second);
            Assert.That(panel.first.page, Is.EqualTo(Page.Second));
            Assert.That(pane.position.y, Is.EqualTo(120).Within(.01f));
            panel.first.page = Page.Third;
            yield return null;
            Assert.That(panel.first.page, Is.EqualTo(Page.Third));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(panel.first.page, Is.EqualTo(Page.Third));
            Assert.That(pane.position.y, Is.EqualTo(240).Within(.1f));
        }
    }
}
