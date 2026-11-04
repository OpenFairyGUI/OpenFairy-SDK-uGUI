using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests.PlayMode
{
    public enum ListTestState { up, down, over, selectedOver, disabled, selectedDisabled }
    public sealed class ListTestButton : NamedStateButton<ListTestState> { }

    public class VirtualListTests
    {
        private readonly System.Collections.Generic.List<GameObject> _objects = new();

        private ListSource MakeList(Vector2 viewSize, Vector2 itemSize, ListLayoutType layout)
        {
            var root = new GameObject("List", typeof(RectTransform));
            _objects.Add(root);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = viewSize;
            var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D));
            var view = (RectTransform)viewport.transform;
            view.SetParent(rect, false);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = view.offsetMax = Vector2.zero;
            var template = new GameObject("Template", typeof(RectTransform), typeof(ListTestButton));
            _objects.Add(template);
            ((RectTransform)template.transform).sizeDelta = itemSize;
            template.GetComponent<ListTestButton>().mode = ButtonMode.Check;
            template.SetActive(false);
            var source = root.AddComponent<ListSource>();
            source.itemPrefab = template;
            source.itemSize = itemSize;
            source.layout = layout;
            root.AddComponent<ListSelection>();
            return source;
        }

        [TestCase(ListLayoutType.SingleColumn, 0f, 16f)]
        [TestCase(ListLayoutType.SingleRow, 29f, 0f)]
        [TestCase(ListLayoutType.FlowHorizontal, 0f, 16f)]
        [TestCase(ListLayoutType.FlowVertical, 29f, 0f)]
        [TestCase(ListLayoutType.Pagination, 0f, 34f)]
        public void Fractional_virtual_items_use_ceiled_cells_and_truncated_auto_partitions(ListLayoutType mode, float x, float y)
        {
            var source = MakeList(new Vector2(103.5f, 67.5f), new Vector2(25.25f, 13.75f), mode);
            source.lineItemCount = 3;
            source.lineItemCount2 = 2;
            source.colGap = 3;
            source.lineGap = 2;
            source.SetVirtual();
            source.Fill(12, (_, _) => { });
            var index = mode is ListLayoutType.SingleColumn or ListLayoutType.SingleRow ? 1 : 3;
            var item = (RectTransform)source.GetItemAt(index).transform;
            Assert.That(item.anchoredPosition.x, Is.EqualTo(x).Within(.001f));
            Assert.That(-item.anchoredPosition.y, Is.EqualTo(y).Within(.001f));
            if (mode is ListLayoutType.SingleColumn or ListLayoutType.FlowHorizontal or ListLayoutType.Pagination)
                Assert.That(item.rect.width, Is.EqualTo(mode == ListLayoutType.SingleColumn ? 103 : 32));
            if (mode == ListLayoutType.Pagination)
            {
                Assert.That(item.rect.height, Is.EqualTo(32));
                Assert.That(((RectTransform)source.GetItemAt(6).transform).anchoredPosition.x, Is.EqualTo(103.5f));
                Assert.That(source.scrollPane.contentSize.x, Is.EqualTo(207));
            }
        }

        [Test]
        public void Fractional_measured_height_stays_on_the_object_but_uses_a_ceiled_virtual_cell()
        {
            var source = MakeList(new Vector2(100, 100), new Vector2(100, 13.25f), ListLayoutType.SingleColumn);
            source.lineGap = 2;
            source.SetVirtual();
            source.Fill(30, (_, _) => { }, measureHeight: (_, i) => 13.25f + i * .125f);
            Assert.That(((RectTransform)source.GetItemAt(0).transform).rect.height, Is.EqualTo(13.25f));
            Assert.That(((RectTransform)source.GetItemAt(1).transform).anchoredPosition.y, Is.EqualTo(-16));
        }

        [Test]
        public void Scroll_render_truncation_keeps_the_exact_logical_position()
        {
            var source = MakeList(new Vector2(100, 100), new Vector2(100, 20), ListLayoutType.SingleColumn);
            source.SetVirtual();
            source.Fill(30, (_, _) => { });
            var pane = source.scrollPane;
            pane.SetPosition(new Vector2(0, 9.75f));
            pane.Rebuild(CanvasUpdate.PostLayout);
            Assert.That(pane.content.anchoredPosition.y, Is.EqualTo(9));
            Assert.That(pane.position.y, Is.EqualTo(9.75f));
            pane.SetPosition(new Vector2(0, 9.750001f));
            Assert.That(pane.position.y, Is.EqualTo(9.750001f));
        }

        [UnityTest]
        public IEnumerator Ceiled_overlap_remains_reachable_with_a_fractional_viewport()
        {
            var source = MakeList(new Vector2(100, 50.5f), new Vector2(100, 20), ListLayoutType.SingleColumn);
            source.SetVirtual();
            source.Fill(5, (_, _) => { });
            var pane = source.scrollPane;
            pane.bounceEffect = false;
            Assert.That(pane.contentSize.y, Is.EqualTo(100));
            Assert.That(pane.maxScroll.y, Is.EqualTo(50));
            pane.SetPosition(new Vector2(0, 50));
            yield return null;
            yield return null;
            Assert.That(pane.position.y, Is.EqualTo(50));
            Assert.That(pane.contentSize.y, Is.EqualTo(100));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _objects)
                Object.Destroy(obj);
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Render_order_changes_drawing_but_keeps_indices_and_positions()
        {
            foreach (var (order, expected) in new[] { (ListRenderOrder.Descent, new[] { 4, 3, 2, 1, 0 }), (ListRenderOrder.Arch, new[] { 0, 1, 4, 3, 2 }) })
            {
                var source = MakeList(new Vector2(200, 300), new Vector2(200, 30), ListLayoutType.SingleColumn);
                source.renderOrder = order;
                source.apexIndex = 2;
                source.Fill(5, (item, index) => item.name = index.ToString());
                var drawn = source.content.Cast<Transform>().Select(item => int.Parse(item.name)).ToArray();
                CollectionAssert.AreEqual(expected, drawn, order.ToString());
                for (var i = 0; i < 5; i++)
                {
                    var item = source.GetItemAt(i);
                    Assert.That(source.GetItemIndex(item), Is.EqualTo(i));
                    Assert.That(((RectTransform)item.transform).anchoredPosition.y, Is.EqualTo(-30 * i).Within(.01f));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedRowsKeepSelectionAndRejectRecycledBindings()
        {
            var source = MakeList(new Vector2(200, 300), new Vector2(200, 30), ListLayoutType.SingleColumn);
            source.SetVirtual();
            source.Fill(10000, (item, index) => item.name = index.ToString());
            var pane = source.GetComponent<ScrollPane>();
            var selection = source.GetComponent<ListSelection>();
            yield return null;
            Assert.That(source.visibleItems.Count, Is.LessThanOrEqualTo(12));
            Assert.That(pane.contentSize.y, Is.EqualTo(300000));
            selection.selectedIndex = 9000;
            source.ScrollToIndex(9000);
            var selectedItem = source.visibleItems[9000];
            Assert.That(selectedItem.GetComponent<ListTestButton>().selected, Is.True);
            int click = -1;
            selection.onClickItem.AddListener(index => click = index);
            selectedItem.GetComponent<ListTestButton>().onClick.Invoke();
            Assert.That(click, Is.EqualTo(9000));
            int version = source.version;
            pane.SetPosition(Vector2.zero);
            Assert.That(source.IsBound(selectedItem, 9000, version), Is.False);
            Assert.That(selection.selectedIndex, Is.EqualTo(9000));
            source.ScrollToIndex(9000);
            Assert.That(source.visibleItems[9000].GetComponent<ListTestButton>().selected, Is.True);
            source.Fill(2, (item, index) => item.name = index.ToString());
            Assert.That(source.firstVisibleIndex, Is.InRange(0, 1));
            Assert.That(selection.selectedIndex, Is.EqualTo(-1));
            Assert.That(source.visibleItems.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MeasuredRowsPreserveViewportAnchorAndBottom()
        {
            var source = MakeList(new Vector2(200, 120), new Vector2(200, 30), ListLayoutType.SingleColumn);
            source.SetVirtual();
            var sizes = Enumerable.Repeat(30f, 1010).ToArray();
            source.Fill(1000, (item, index) => { }, measureHeight: (item, index) => sizes[index]);
            var pane = source.GetComponent<ScrollPane>();
            pane.SetPosition(new Vector2(0, 5000));
            yield return null;
            int anchor = source.firstVisibleIndex;
            var anchorRect = (RectTransform)source.visibleItems[anchor].transform;
            float screenTop = -anchorRect.anchoredPosition.y - pane.position.y;
            Assert.That(source.visibleItems.ContainsKey(anchor - 1), Is.True);
            sizes[anchor - 1] += 40;
            yield return null;
            Assert.That(source.firstVisibleIndex, Is.EqualTo(anchor));
            Assert.That(-anchorRect.anchoredPosition.y - pane.position.y, Is.EqualTo(screenTop).Within(0.01f));
            Assert.That(source.visibleItems.Count, Is.LessThanOrEqualTo(7));
            pane.SetPosition(new Vector2(0, pane.maxScroll.y));
            source.Fill(1010, (item, index) => { }, measureHeight: (item, index) => sizes[index]);
            Assert.That(pane.position.y, Is.EqualTo(pane.maxScroll.y).Within(0.01f));
            var ordered = source.visibleItems.OrderBy(pair => pair.Key).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                var previous = (RectTransform)ordered[i - 1].Value.transform;
                var current = (RectTransform)ordered[i].Value.transform;
                Assert.That(previous.anchoredPosition.y - current.anchoredPosition.y,
                    Is.EqualTo(previous.rect.height).Within(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator HorizontalLoopPreservesIdentityAndDragAcrossSeam()
        {
            var source = MakeList(new Vector2(300, 100), new Vector2(40, 100), ListLayoutType.SingleRow);
            source.colGap = 5;
            source.SetVirtual(true);
            source.Fill(2, (item, index) => item.name = index.ToString());
            var pane = source.GetComponent<ScrollPane>();
            var selection = source.GetComponent<ListSelection>();
            selection.selectedIndex = 1;
            yield return null;
            var original = source.visibleItems.Values.Select(item =>
                (item.name, ((RectTransform)item.transform).anchoredPosition.x - pane.position.x))
                .OrderBy(value => value.Item2).ToArray();
            pane.SetPosition(pane.position + new Vector2(90, 0));
            var wrapped = source.visibleItems.Values.Select(item =>
                (item.name, ((RectTransform)item.transform).anchoredPosition.x - pane.position.x))
                .OrderBy(value => value.Item2).ToArray();
            Assert.That(wrapped, Is.EqualTo(original));
            foreach (var pair in source.visibleItems)
            {
                Assert.That(pair.Value.name, Is.EqualTo((pair.Key % 2).ToString()));
                Assert.That(pair.Value.GetComponent<ListTestButton>().selected, Is.EqualTo(pair.Key % 2 == 1));
            }
            var events = new GameObject("Events", typeof(EventSystem));
            _objects.Add(events);
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { position = new Vector2(200, 100) };
            pane.OnBeginDrag(pointer);
            pointer.position = new Vector2(-70, 100);
            pane.OnDrag(pointer);
            float before = pane.position.x;
            pointer.position = new Vector2(-71, 100);
            pane.OnDrag(pointer);
            float delta = (pane.position.x - before + 90) % 90;
            Assert.That(delta, Is.EqualTo(1).Within(0.01f));
            Assert.That(source.visibleItems.Count, Is.LessThanOrEqualTo(10));
        }
    }
}
