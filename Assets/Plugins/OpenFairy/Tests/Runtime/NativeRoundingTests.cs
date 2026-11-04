using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public class NativeRoundingTests
    {
        // Captured from the unmodified FairyGUI SDK; no OpenFairy formula generates these values.
        private static IEnumerable NativeLists()
        {
            yield return new TestCaseData(ListLayoutType.SingleColumn, false, 0, new float[] { 29.0f, 0.0f, 25.25f, 13.75f, 29.0f, 0.0f, 28.375f, 15.875f, 29.0f, 16.0f, 31.5f, 18.0f, 29.0f, 36.0f, 34.625f, 20.125f, 29.0f, 59.0f, 37.75f, 22.25f, 29.0f, 84.0f, 40.875f, 24.375f, 29.0f, 111.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleColumn, false, 2, new float[] { 29.0f, 0.0f, 25.25f, 13.75f, 29.0f, 0.0f, 28.375f, 15.875f, 29.0f, 16.0f, 31.5f, 18.0f, 29.0f, 36.0f, 34.625f, 20.125f, 29.0f, 59.0f, 37.75f, 22.25f, 29.0f, 84.0f, 40.875f, 24.375f, 29.0f, 111.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleColumn, false, 3, new float[] { 29.0f, 0.0f, 25.25f, 13.75f, 29.0f, 0.0f, 28.375f, 15.875f, 29.0f, 16.0f, 31.5f, 18.0f, 29.0f, 36.0f, 34.625f, 20.125f, 29.0f, 59.0f, 37.75f, 22.25f, 29.0f, 84.0f, 40.875f, 24.375f, 29.0f, 111.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleColumn, true, 0, new float[] { 0.0f, 0.0f, 103.5f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 0.0f, 16.0f, 103.5f, 18.0f, 0.0f, 36.0f, 103.5f, 20.125f, 0.0f, 59.0f, 103.5f, 22.25f, 0.0f, 84.0f, 103.5f, 24.375f, 0.0f, 111.0f, 103.5f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleColumn, true, 2, new float[] { 0.0f, 0.0f, 103.5f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 0.0f, 16.0f, 103.5f, 18.0f, 0.0f, 36.0f, 103.5f, 20.125f, 0.0f, 59.0f, 103.5f, 22.25f, 0.0f, 84.0f, 103.5f, 24.375f, 0.0f, 111.0f, 103.5f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleColumn, true, 3, new float[] { 0.0f, 0.0f, 103.5f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 0.0f, 16.0f, 103.5f, 18.0f, 0.0f, 36.0f, 103.5f, 20.125f, 0.0f, 59.0f, 103.5f, 22.25f, 0.0f, 84.0f, 103.5f, 24.375f, 0.0f, 111.0f, 103.5f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, false, 0, new float[] { 0.0f, 20.0f, 25.25f, 13.75f, 0.0f, 20.0f, 28.375f, 15.875f, 29.0f, 20.0f, 31.5f, 18.0f, 64.0f, 20.0f, 34.625f, 20.125f, 102.0f, 20.0f, 37.75f, 22.25f, 143.0f, 20.0f, 40.875f, 24.375f, 187.0f, 20.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, false, 2, new float[] { 0.0f, 20.0f, 25.25f, 13.75f, 0.0f, 20.0f, 28.375f, 15.875f, 29.0f, 20.0f, 31.5f, 18.0f, 64.0f, 20.0f, 34.625f, 20.125f, 102.0f, 20.0f, 37.75f, 22.25f, 143.0f, 20.0f, 40.875f, 24.375f, 187.0f, 20.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, false, 3, new float[] { 0.0f, 20.0f, 25.25f, 13.75f, 0.0f, 20.0f, 28.375f, 15.875f, 29.0f, 20.0f, 31.5f, 18.0f, 64.0f, 20.0f, 34.625f, 20.125f, 102.0f, 20.0f, 37.75f, 22.25f, 143.0f, 20.0f, 40.875f, 24.375f, 187.0f, 20.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, true, 0, new float[] { 0.0f, 0.0f, 25.25f, 67.5f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 67.5f, 64.0f, 0.0f, 34.625f, 67.5f, 102.0f, 0.0f, 37.75f, 67.5f, 143.0f, 0.0f, 40.875f, 67.5f, 187.0f, 0.0f, 44.0f, 67.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, true, 2, new float[] { 0.0f, 0.0f, 25.25f, 67.5f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 67.5f, 64.0f, 0.0f, 34.625f, 67.5f, 102.0f, 0.0f, 37.75f, 67.5f, 143.0f, 0.0f, 40.875f, 67.5f, 187.0f, 0.0f, 44.0f, 67.5f });
            yield return new TestCaseData(ListLayoutType.SingleRow, true, 3, new float[] { 0.0f, 0.0f, 25.25f, 67.5f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 67.5f, 64.0f, 0.0f, 34.625f, 67.5f, 102.0f, 0.0f, 37.75f, 67.5f, 143.0f, 0.0f, 40.875f, 67.5f, 187.0f, 0.0f, 44.0f, 67.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, false, 0, new float[] { 2.0f, 0.0f, 25.25f, 13.75f, 2.0f, 0.0f, 28.375f, 15.875f, 31.0f, 0.0f, 31.5f, 18.0f, 66.0f, 0.0f, 34.625f, 20.125f, 2.0f, 23.0f, 37.75f, 22.25f, 43.0f, 23.0f, 40.875f, 24.375f, 2.0f, 50.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, false, 2, new float[] { 7.0f, 0.0f, 25.25f, 13.75f, 7.0f, 0.0f, 28.375f, 15.875f, 36.0f, 0.0f, 31.5f, 18.0f, 7.0f, 20.0f, 34.625f, 20.125f, 45.0f, 20.0f, 37.75f, 22.25f, 7.0f, 45.0f, 40.875f, 24.375f, 51.0f, 45.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, false, 3, new float[] { 0.0f, 8.0f, 25.25f, 13.75f, 0.0f, 8.0f, 28.375f, 15.875f, 29.0f, 8.0f, 31.5f, 18.0f, 64.0f, 8.0f, 34.625f, 20.125f, 0.0f, 31.0f, 37.75f, 22.25f, 41.0f, 31.0f, 40.875f, 24.375f, 85.0f, 31.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, true, 0, new float[] { 2.0f, 0.0f, 25.25f, 13.75f, 2.0f, 0.0f, 28.375f, 15.875f, 31.0f, 0.0f, 31.5f, 18.0f, 66.0f, 0.0f, 34.625f, 20.125f, 2.0f, 23.0f, 37.75f, 22.25f, 43.0f, 23.0f, 40.875f, 24.375f, 2.0f, 50.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, true, 2, new float[] { 0.0f, 0.0f, 45.0f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 48.0f, 0.0f, 55.0f, 18.0f, 0.0f, 20.0f, 48.0f, 20.125f, 51.0f, 20.0f, 52.0f, 22.25f, 0.0f, 45.0f, 48.0f, 24.375f, 51.0f, 45.0f, 53.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowHorizontal, true, 3, new float[] { 0.0f, 7.0f, 27.0f, 13.75f, 0.0f, 7.0f, 28.375f, 15.875f, 30.0f, 7.0f, 34.0f, 18.0f, 67.0f, 7.0f, 37.0f, 20.125f, 0.0f, 30.0f, 30.0f, 22.25f, 33.0f, 30.0f, 33.0f, 24.375f, 69.0f, 30.0f, 34.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, false, 0, new float[] { 0.0f, 5.0f, 25.25f, 13.75f, 0.0f, 5.0f, 28.375f, 15.875f, 0.0f, 20.75f, 31.5f, 18.0f, 0.0f, 40.75f, 34.625f, 20.125f, 38.0f, 5.0f, 37.75f, 22.25f, 38.0f, 29.25f, 40.875f, 24.375f, 82.0f, 5.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, false, 2, new float[] { 0.0f, 7.0f, 25.25f, 13.75f, 0.0f, 7.0f, 28.375f, 15.875f, 0.0f, 22.75f, 31.5f, 18.0f, 35.0f, 7.0f, 34.625f, 20.125f, 35.0f, 29.125f, 37.75f, 22.25f, 76.0f, 7.0f, 40.875f, 24.375f, 76.0f, 33.375f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, false, 3, new float[] { 10.0f, 0.0f, 25.25f, 13.75f, 10.0f, 0.0f, 28.375f, 15.875f, 10.0f, 15.75f, 31.5f, 18.0f, 10.0f, 35.75f, 34.625f, 20.125f, 48.0f, 0.0f, 37.75f, 22.25f, 48.0f, 24.25f, 40.875f, 24.375f, 48.0f, 50.625f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, true, 0, new float[] { 0.0f, 5.0f, 25.25f, 13.75f, 0.0f, 5.0f, 28.375f, 15.875f, 0.0f, 20.75f, 31.5f, 18.0f, 0.0f, 40.75f, 34.625f, 20.125f, 38.0f, 5.0f, 37.75f, 22.25f, 38.0f, 29.25f, 40.875f, 24.375f, 82.0f, 5.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, true, 2, new float[] { 0.0f, 0.0f, 25.25f, 28.0f, 0.0f, 0.0f, 28.375f, 15.875f, 0.0f, 30.0f, 31.5f, 38.0f, 35.0f, 0.0f, 34.625f, 31.0f, 35.0f, 33.0f, 37.75f, 34.0f, 76.0f, 0.0f, 40.875f, 31.0f, 76.0f, 33.0f, 44.0f, 34.0f });
            yield return new TestCaseData(ListLayoutType.FlowVertical, true, 3, new float[] { 9.0f, 0.0f, 25.25f, 17.0f, 9.0f, 0.0f, 28.375f, 15.875f, 9.0f, 19.0f, 31.5f, 22.0f, 9.0f, 43.0f, 34.625f, 24.0f, 47.0f, 0.0f, 37.75f, 19.0f, 47.0f, 21.0f, 40.875f, 21.0f, 47.0f, 44.0f, 44.0f, 24.0f });
            yield return new TestCaseData(ListLayoutType.Pagination, false, 0, new float[] { 0.0f, 0.0f, 25.25f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 18.0f, 64.0f, 0.0f, 34.625f, 20.125f, 0.0f, 22.125f, 37.75f, 22.25f, 41.0f, 22.125f, 40.875f, 24.375f, 103.5f, 0.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.Pagination, false, 2, new float[] { 0.0f, 0.0f, 25.25f, 13.75f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 18.0f, 0.0f, 20.0f, 34.625f, 20.125f, 38.0f, 20.0f, 37.75f, 22.25f, 103.5f, 0.0f, 40.875f, 24.375f, 147.5f, 0.0f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.Pagination, false, 3, new float[] { 0.0f, 9.0f, 25.25f, 13.75f, 0.0f, 9.0f, 28.375f, 15.875f, 29.0f, 9.0f, 31.5f, 18.0f, 64.0f, 9.0f, 34.625f, 20.125f, 0.0f, 31.125f, 37.75f, 22.25f, 41.0f, 31.125f, 40.875f, 24.375f, 85.0f, 31.125f, 44.0f, 26.5f });
            yield return new TestCaseData(ListLayoutType.Pagination, true, 0, new float[] { 0.0f, 0.0f, 25.25f, 32.0f, 0.0f, 0.0f, 28.375f, 15.875f, 29.0f, 0.0f, 31.5f, 32.0f, 64.0f, 0.0f, 34.625f, 32.0f, 0.0f, 34.0f, 37.75f, 32.0f, 41.0f, 34.0f, 40.875f, 32.0f, 103.5f, 0.0f, 44.0f, 32.0f });
            yield return new TestCaseData(ListLayoutType.Pagination, true, 2, new float[] { 0.0f, 0.0f, 45.0f, 32.0f, 0.0f, 0.0f, 28.375f, 15.875f, 48.0f, 0.0f, 55.0f, 32.0f, 0.0f, 34.0f, 48.0f, 32.0f, 51.0f, 34.0f, 52.0f, 32.0f, 103.5f, 0.0f, 48.0f, 32.0f, 154.5f, 0.0f, 53.0f, 32.0f });
            yield return new TestCaseData(ListLayoutType.Pagination, true, 3, new float[] { 0.0f, 0.0f, 27.0f, 32.0f, 0.0f, 0.0f, 28.375f, 15.875f, 30.0f, 0.0f, 34.0f, 32.0f, 67.0f, 0.0f, 37.0f, 32.0f, 0.0f, 34.0f, 30.0f, 32.0f, 33.0f, 34.0f, 33.0f, 32.0f, 69.0f, 34.0f, 34.0f, 32.0f });
        }

        [TestCaseSource(nameof(NativeLists))]
        public void ListsMatchNativeFractionalLayout(ListLayoutType mode, bool auto, int lanes, float[] expected)
        {
            var root = new GameObject("List", typeof(RectTransform), typeof(ListSource));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(103.5f, 67.5f);
                var source = root.GetComponent<ListSource>();
                source.layout = mode;
                source.autoItemSize = auto;
                source.lineItemCount = lanes;
                source.lineItemCount2 = mode == ListLayoutType.Pagination ? 2 : 0;
                source.colGap = 3;
                source.lineGap = 2;
                source.alignment = TextAnchor.MiddleCenter;
                source.foldInvisibleItems = true;
                var items = new List<RectTransform>();
                var sizes = new List<Vector2>();
                for (var i = 0; i < 7; i++)
                {
                    var item = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
                    item.SetParent(rect, false);
                    item.anchorMin = item.anchorMax = item.pivot = new Vector2(0, 1);
                    item.sizeDelta = new Vector2(25.25f + i * 3.125f, 13.75f + i * 2.125f);
                    item.gameObject.SetActive(i != 1);
                    items.Add(item);
                    // Native sourceWidth/sourceHeight are ints, independently of the current fractional size.
                    sizes.Add(new Vector2(25.9f + i * 3, 13.9f + i * 2));
                }
                ListLayout.Arrange(source, items, rect.rect.size, sizes);
                for (var i = 0; i < items.Count; i++)
                {
                    if (!items[i].gameObject.activeSelf) continue;
                    var item = items[i];
                    Assert.That(item.localPosition.x - rect.rect.xMin, Is.EqualTo(expected[i * 4]).Within(.001f), $"item {i} x");
                    Assert.That(rect.rect.yMax - item.localPosition.y, Is.EqualTo(expected[i * 4 + 1]).Within(.001f), $"item {i} y");
                    Assert.That(item.rect.width, Is.EqualTo(expected[i * 4 + 2]).Within(.001f), $"item {i} width");
                    Assert.That(item.rect.height, Is.EqualTo(expected[i * 4 + 3]).Within(.001f), $"item {i} height");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static IEnumerable NativeGroups()
        {
            yield return new TestCaseData(false, -1, 65.5f, new float[] { 15.0f, 20.0f, 25.0f });
            yield return new TestCaseData(false, -1, 103.5f, new float[] { 24.0f, 33.0f, 41.0f });
            yield return new TestCaseData(false, -1, 150.75f, new float[] { 36.0f, 48.0f, 61.0f });
            yield return new TestCaseData(false, 0, 65.5f, new float[] { 27.0f, 14.0f, 18.0f });
            yield return new TestCaseData(false, 0, 103.5f, new float[] { 44.0f, 24.0f, 30.0f });
            yield return new TestCaseData(false, 0, 150.75f, new float[] { 82.875f, 27.0f, 35.0f });
            yield return new TestCaseData(false, 1, 65.5f, new float[] { 12.0f, 28.0f, 19.0f });
            yield return new TestCaseData(false, 1, 103.5f, new float[] { 19.0f, 46.0f, 32.0f });
            yield return new TestCaseData(false, 1, 150.75f, new float[] { 20.0f, 90.0f, 35.0f });
            yield return new TestCaseData(true, -1, 65.5f, new float[] { 15.0f, 20.0f, 25.0f });
            yield return new TestCaseData(true, -1, 103.5f, new float[] { 24.0f, 33.0f, 41.0f });
            yield return new TestCaseData(true, -1, 150.75f, new float[] { 36.0f, 48.0f, 61.0f });
            yield return new TestCaseData(true, 0, 65.5f, new float[] { 27.0f, 14.0f, 18.0f });
            yield return new TestCaseData(true, 0, 103.5f, new float[] { 44.0f, 24.0f, 30.0f });
            yield return new TestCaseData(true, 0, 150.75f, new float[] { 82.875f, 27.0f, 35.0f });
            yield return new TestCaseData(true, 1, 65.5f, new float[] { 12.0f, 28.0f, 19.0f });
            yield return new TestCaseData(true, 1, 103.5f, new float[] { 19.0f, 46.0f, 32.0f });
            yield return new TestCaseData(true, 1, 150.75f, new float[] { 20.0f, 90.0f, 35.0f });
        }

        [TestCaseSource(nameof(NativeGroups))]
        public void GroupResizeMatchesNativeRemainingSpaceDistribution(bool vertical, int main, float size, float[] expected)
        {
            var root = new GameObject("Group", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(88.125f, 88.125f);
                for (var i = 0; i < 3; i++)
                {
                    var child = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
                    child.SetParent(rect, false);
                    child.sizeDelta = Vector2.one * (20.25f + i * 7.125f);
                }
                var group = root.AddComponent<GroupLayout>();
                group.axis = vertical ? GroupLayoutAxis.Vertical : GroupLayoutAxis.Horizontal;
                group.spacing = 3;
                group.mainGridIndex = main;
                var axis = vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal;
                rect.SetSizeWithCurrentAnchors(axis, size);
                for (var i = 0; i < 3; i++)
                    Assert.That(((RectTransform)rect.GetChild(i)).rect.size[(int)axis], Is.EqualTo(expected[i]).Within(.001f));
                group.autoSizeDisabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                for (var i = 0; i < 3; i++)
                    Assert.That(((RectTransform)rect.GetChild(i)).rect.size[(int)axis], Is.EqualTo(expected[i]).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Group_too_small_for_its_gaps_clamps_rounded_children_to_zero(bool vertical)
        {
            var root = new GameObject("Group", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(100, 100);
                for (var i = 0; i < 3; i++)
                {
                    var child = new GameObject("Item", typeof(RectTransform)).GetComponent<RectTransform>();
                    child.SetParent(rect, false);
                    child.sizeDelta = Vector2.one * 30;
                }
                var group = root.AddComponent<GroupLayout>();
                group.axis = vertical ? GroupLayoutAxis.Vertical : GroupLayoutAxis.Horizontal;
                group.spacing = 3;
                var axis = vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal;
                rect.SetSizeWithCurrentAnchors(axis, 0);
                for (var i = 0; i < 3; i++) Assert.That(((RectTransform)rect.GetChild(i)).rect.size[(int)axis], Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(50f, 0)]
        [TestCase(50.001f, 1)]
        [TestCase(150f, 1)]
        [TestCase(150.001f, 2)]
        public void PageMidpointsBelongToTheEarlierPage(float position, int expected)
            => Assert.That(ScrollPane.PageIndex(position, 100), Is.EqualTo(expected));

        [UnityTest]
        public IEnumerator Scrollbar_visibility_changes_do_not_unregister_graphics_inside_the_canvas_rebuild()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(AuthoredScrollbar));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(200, 200);
                var grip = new GameObject("Grip", typeof(RectTransform), typeof(Image), typeof(PixelAlignment)).GetComponent<RectTransform>();
                grip.SetParent(rect, false);
                var bar = root.GetComponent<AuthoredScrollbar>();
                bar.handleRect = grip;
                bar.size = .25f;
                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(grip.gameObject.activeSelf);
                bar.size = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.IsFalse(grip.gameObject.activeSelf);
                bar.size = .25f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.IsTrue(grip.gameObject.activeSelf);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(Scrollbar.Direction.LeftToRight, .333f, .5f, 36f)]
        [TestCase(Scrollbar.Direction.BottomToTop, .333f, .5f, 36f)]
        [TestCase(Scrollbar.Direction.LeftToRight, .5f, .5f, 27f)]
        public void ScrollbarFloorsLengthAndRoundsInTheAuthoredCoordinateSpace(Scrollbar.Direction direction,
            float fraction, float position, float expectedPosition)
        {
            var root = new GameObject("Bar", typeof(RectTransform), typeof(AuthoredScrollbar));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(200, 200);
                var track = new GameObject("Track", typeof(RectTransform)).GetComponent<RectTransform>();
                track.SetParent(rect, false);
                track.anchorMin = track.anchorMax = track.pivot = new Vector2(0, 1);
                track.anchoredPosition = new Vector2(1.5f, -1.5f);
                track.sizeDelta = new Vector2(101.5f, 101.5f);
                var grip = new GameObject("Grip", typeof(RectTransform)).GetComponent<RectTransform>();
                grip.SetParent(track, false);
                var bar = root.GetComponent<AuthoredScrollbar>();
                bar.direction = direction;
                bar.handleRect = grip;
                bar.size = fraction;
                bar.value = position;
                bar.Rebuild(CanvasUpdate.PostLayout);
                var alongX = direction == Scrollbar.Direction.LeftToRight;
                Assert.That(alongX ? grip.rect.width : grip.rect.height, Is.EqualTo(Mathf.Floor(101.5f * fraction)));
                var point = PixelAlignment.LocalPoint(grip, new Vector3(grip.rect.xMin, grip.rect.yMax), rect);
                Assert.That(alongX ? point.x - rect.rect.xMin : rect.rect.yMax - point.y, Is.EqualTo(expectedPosition).Within(.001f));
                bar.fixedGripSize = true;
                bar.authoredGripSize = 17.25f;
                bar.Rebuild(CanvasUpdate.PostLayout);
                Assert.That(alongX ? grip.rect.width : grip.rect.height, Is.EqualTo(17.25f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
