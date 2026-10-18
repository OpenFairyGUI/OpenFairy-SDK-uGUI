using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class NativeRowLayoutTests
    {
        private readonly List<GameObject> objects = new();
        private ListSource Row(bool autoItemSize, bool scrolling = true)
        {
            var root = new GameObject("Row", typeof(RectTransform)); objects.Add(root);
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(200, 60);
            if (scrolling)
            {
                var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D));
                var view = (RectTransform)viewport.transform; view.SetParent(rect, false);
                view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = view.offsetMax = Vector2.zero;
            }
            var item = new GameObject("Item", typeof(RectTransform), typeof(LayoutElement), typeof(ContentSizeFitter)); objects.Add(item);
            ((RectTransform)item.transform).sizeDelta = new Vector2(40, 20);
            item.GetComponent<LayoutElement>().preferredWidth = 40;
            item.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            item.SetActive(false);
            var source = root.AddComponent<ListSource>();
            source.itemPrefab = item; source.itemSize = new Vector2(40, 20); source.colGap = 5;
            source.layout = ListLayoutType.SingleRow; source.autoItemSize = autoItemSize;
            if (scrolling) source.scrollPane = ScrollPane.Attach(rect);
            source.content = scrolling ? source.scrollPane.content : rect;
            source.rowLayout = source.content.gameObject.AddComponent<HorizontalLayoutGroup>();
            source.rowLayout.childControlWidth = source.rowLayout.childControlHeight = false;
            source.rowLayout.childForceExpandWidth = source.rowLayout.childForceExpandHeight = false;
            source.rowLayout.childAlignment = TextAnchor.UpperLeft; source.rowLayout.spacing = 5;
            source.rowSize = source.content.gameObject.AddComponent<ContentSizeFitter>();
            source.rowSize.horizontalFit = source.rowSize.verticalFit = scrolling
                ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            source.rowMinimum = source.content.gameObject.AddComponent<LayoutElement>();
            return source;
        }
        private static RectTransform[] Items(ListSource source) => System.Array.FindAll(
            source.content.GetComponentsInChildren<RectTransform>(true), rect => rect.parent == source.content && rect.gameObject.activeSelf);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear(); yield return null;
        }

        [UnityTest]
        public IEnumerator ActualWidthsAndLaterContentChangesUseOneNativeLayoutOwner()
        {
            var source = Row(true);
            float[] widths = { 30, 130, 50 };
            source.Fill(3, (go, index) => go.GetComponent<LayoutElement>().preferredWidth = widths[index], false);
            yield return null; yield return null;
            var items = Items(source);
            Assert.That(items[0].rect.height, Is.EqualTo(60).Within(.01));
            Assert.That(items[1].anchoredPosition.x, Is.EqualTo(35).Within(.01));
            Assert.That(items[2].anchoredPosition.x, Is.EqualTo(170).Within(.01));
            Assert.That(source.content.rect.width, Is.EqualTo(220).Within(.01));
            items[0].GetComponent<LayoutElement>().preferredWidth = 80;
            yield return null; yield return null;
            Assert.That(items[1].anchoredPosition.x, Is.EqualTo(85).Within(.01));
            Assert.That(items[2].anchoredPosition.x, Is.EqualTo(220).Within(.01));
            Assert.That(source.content.rect.width, Is.EqualTo(270).Within(.01));
            source.scrollPane.RefreshContent();
            Assert.That(source.content.rect.width, Is.EqualTo(270).Within(.01), "ScrollRect refresh must not overwrite the native fitter's extent");
            Assert.That(source.rowLayout.enabled && source.rowSize.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ExplicitAutoItemSizeFalseKeepsHeightAndVariableWidths()
        {
            var source = Row(false);
            source.Fill(2, (go, index) =>
            {
                go.GetComponent<LayoutElement>().preferredWidth = index == 0 ? 70 : 90;
                ((RectTransform)go.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20 + index * 10);
            }, false);
            yield return null; yield return null;
            var items = Items(source);
            Assert.That(items[0].rect.height, Is.EqualTo(20).Within(.01));
            Assert.That(items[1].rect.height, Is.EqualTo(30).Within(.01));
            Assert.That(items[1].anchoredPosition.x, Is.EqualTo(75).Within(.01));
        }

        [UnityTest]
        public IEnumerator ViewportResizeUpdatesCrossAxisAndNativeMinimumAlignment()
        {
            var source = Row(true);
            source.rowLayout.childAlignment = TextAnchor.MiddleCenter;
            source.Fill(2, (go, _) => go.GetComponent<LayoutElement>().preferredWidth = 40, false);
            yield return null; yield return null;
            Assert.That(Items(source)[0].anchoredPosition.x, Is.EqualTo(57.5f).Within(.01));
            ((RectTransform)source.transform).sizeDelta = new Vector2(300, 90);
            yield return null; yield return null;
            Assert.That(Items(source)[0].anchoredPosition.x, Is.EqualTo(107.5f).Within(.01));
            Assert.That(Items(source)[0].rect.height, Is.EqualTo(90).Within(.01));
        }

        [UnityTest]
        public IEnumerator NativeRowScrollingAndSnappingUseTheActualItemBounds()
        {
            var source = Row(true);
            float[] widths = { 30, 130, 50, 300 };
            source.Fill(widths.Length, (go, index) => go.GetComponent<LayoutElement>().preferredWidth = widths[index], false);
            yield return null; yield return null;
            // Actual starts: 0, 35, 170, 225. The fixed template step is 45.
            source.ScrollToIndex(2);
            Assert.That(source.scrollPane.position.x, Is.EqualTo(20).Within(.01), "Reveal the third item's right edge at 220.");
            Assert.That(source.firstVisibleIndex, Is.Zero, "A partially visible first item still intersects the viewport.");
            Assert.That(source.centeredIndex, Is.EqualTo(1));
            source.scrollPane.SetPosition(new Vector2(30, 0));
            Assert.That(source.firstVisibleIndex, Is.EqualTo(1), "An item ending exactly at the viewport edge is no longer visible.");
            source.ScrollToIndex(3);
            Assert.That(source.scrollPane.position.x, Is.EqualTo(225).Within(.01), "An item wider than the viewport starts at its actual left edge.");
            Assert.That(source.firstVisibleIndex, Is.EqualTo(3));
            Assert.That(source.centeredIndex, Is.EqualTo(3));
            source.ScrollToIndex(2, center: true);
            Assert.That(source.scrollPane.position.x, Is.EqualTo(95).Within(.01), "Center the actual 170..220 bounds in a 200-wide viewport.");
            Assert.That(source.firstVisibleIndex, Is.EqualTo(1));
            Assert.That(source.centeredIndex, Is.EqualTo(2));
            var snap = source.SnapPosition(new Vector2(155, 11));
            Assert.That(snap.x, Is.EqualTo(170).Within(.01));
            Assert.That(snap.y, Is.EqualTo(11));
            source.ScrollToIndex(0);
            Assert.That(source.scrollPane.position.x, Is.Zero.Within(.01));
            source.ScrollToIndex(2, center: true, animated: true);
            var deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (Mathf.Abs(source.scrollPane.position.x - 95) > .01f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(source.scrollPane.position.x, Is.EqualTo(95).Within(.01));
            source.ScrollToIndex(0);
            source.ScrollToIndex(2, animated: true);
            deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (Mathf.Abs(source.scrollPane.position.x - 20) > .01f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(source.scrollPane.position.x, Is.EqualTo(20).Within(.01), "The existing animated argument retains the native-bound target.");
        }

        [UnityTest]
        public IEnumerator NativeRowQueriesWithoutAScrollPaneUseTheListViewport()
        {
            var source = Row(true, scrolling: false);
            source.Fill(2, (go, index) => go.GetComponent<LayoutElement>().preferredWidth = index == 0 ? 30 : 70, false);
            yield return null; yield return null;
            Assert.That(source.scrollPane, Is.Null);
            Assert.That(source.firstVisibleIndex, Is.Zero);
            Assert.That(source.centeredIndex, Is.EqualTo(1));
            source.Fill(0, (_, _) => { }, false);
            Assert.That(source.firstVisibleIndex, Is.EqualTo(-1));
            Assert.That(source.centeredIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator SwitchingToVirtualFixedRowsDisablesTheBakedNativeDrivers()
        {
            var source = Row(true);
            source.SetVirtual();
            source.Fill(100, (go, _) => go.GetComponent<LayoutElement>().preferredWidth = 40, false);
            yield return null;
            Assert.That(source.rowLayout.enabled || source.rowSize.enabled || source.rowMinimum.enabled, Is.False);
            Assert.That(source.content.rect.width, Is.EqualTo(4495).Within(.01));
            Assert.That(source.visibleItems.Count, Is.LessThan(10));
            source.ScrollToIndex(50, center: true);
            Assert.That(source.scrollPane.position.x, Is.EqualTo(2170).Within(.01));
            Assert.That(source.firstVisibleIndex, Is.EqualTo(48));
            Assert.That(source.centeredIndex, Is.EqualTo(48), "The non-loop virtual query behavior remains unchanged.");
            Assert.That(source.SnapPosition(new Vector2(176, 0)).x, Is.EqualTo(180).Within(.01));
        }
    }
}
