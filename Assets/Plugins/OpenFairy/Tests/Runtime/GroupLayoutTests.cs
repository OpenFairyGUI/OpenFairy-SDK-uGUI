using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class GroupLayoutTests
    {
        private readonly List<GameObject> _objects = new();

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 topLeft)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            return rect;
        }

        private RectTransform Canvas()
        {
            var canvas = Rect("Canvas", null, new Vector2(800, 200), Vector2.zero);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _objects.Add(canvas.gameObject);
            return canvas;
        }

        private static GroupLayout Group(RectTransform parent, GroupLayoutAxis axis, float gap)
        {
            var rect = Rect("Group", parent, new Vector2(50, 30), Vector2.zero);
            var group = rect.gameObject.AddComponent<GroupLayout>();
            group.axis = axis;
            group.spacing = gap;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        private static Vector2 Position(RectTransform rect, RectTransform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var topLeft = parent.InverseTransformPoint(corners[1]);
            return new Vector2(topLeft.x - parent.rect.xMin, parent.rect.yMax - topLeft.y);
        }

        private static IEnumerator Settle()
        {
            for (var i = 0; i < 5; i++) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator Horizontal_flow_preserves_member_heights_and_cross_axis_offsets()
        {
            var group = Group(Canvas(), GroupLayoutAxis.Horizontal, 5);
            var root = (RectTransform)group.transform;
            var caption = Rect("Caption", root, new Vector2(40, 14), Vector2.zero);
            var close = Rect("Close", root, new Vector2(0, 12), new Vector2(99, 7));
            yield return Settle();
            Assert.That(Position(caption, root), Is.EqualTo(Vector2.zero));
            Assert.That(Position(close, root), Is.EqualTo(new Vector2(45, 7)));
            Assert.That(root.rect.size, Is.EqualTo(new Vector2(45, 19)));
            close.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 25);
            yield return Settle();
            Assert.That(root.rect.width, Is.EqualTo(70));
            Assert.That(Position(close, root).y, Is.EqualTo(7));
            Assert.That(close.rect.height, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator Vertical_flow_keeps_horizontal_offsets_and_allows_overlapping_gaps()
        {
            var group = Group(Canvas(), GroupLayoutAxis.Vertical, -3);
            var root = (RectTransform)group.transform;
            var first = Rect("First", root, new Vector2(30, 20), Vector2.zero);
            var second = Rect("Second", root, new Vector2(12, 10), new Vector2(8, 80));
            yield return Settle();
            Assert.That(Position(first, root), Is.EqualTo(Vector2.zero));
            Assert.That(Position(second, root), Is.EqualTo(new Vector2(8, 17)));
            Assert.That(root.rect.size, Is.EqualTo(new Vector2(30, 27)));
        }

        [UnityTest]
        public IEnumerator Hidden_members_only_leave_layout_when_explicitly_excluded()
        {
            var group = Group(Canvas(), GroupLayoutAxis.Horizontal, 4);
            var root = (RectTransform)group.transform;
            Rect("First", root, new Vector2(20, 10), Vector2.zero);
            var hidden = Rect("Hidden", root, new Vector2(30, 10), Vector2.zero);
            var last = Rect("Last", root, new Vector2(10, 10), Vector2.zero);
            hidden.gameObject.SetActive(false);
            yield return Settle();
            Assert.That(root.rect.width, Is.EqualTo(68));
            Assert.That(Position(last, root).x, Is.EqualTo(58));
            group.excludeInvisibles = true;
            yield return Settle();
            Assert.That(root.rect.width, Is.EqualTo(34));
            Assert.That(Position(last, root).x, Is.EqualTo(24));
            hidden.gameObject.SetActive(true);
            yield return Settle();
            Assert.That(root.rect.width, Is.EqualTo(68));
        }

        [UnityTest]
        public IEnumerator Zero_sized_members_do_not_insert_an_extra_gap()
        {
            var group = Group(Canvas(), GroupLayoutAxis.Horizontal, 5);
            var root = (RectTransform)group.transform;
            Rect("First", root, new Vector2(20, 10), Vector2.zero);
            var zero = Rect("Zero", root, new Vector2(0, 10), Vector2.zero);
            var last = Rect("Last", root, new Vector2(10, 10), Vector2.zero);
            yield return Settle();
            Assert.That(Position(zero, root).x, Is.EqualTo(25));
            Assert.That(Position(last, root).x, Is.EqualTo(25));
            Assert.That(root.rect.width, Is.EqualTo(35));
        }

#if FAIRYGUI_TMPRO
        [UnityTest]
        public IEnumerator Fitted_captions_propagate_through_group_and_parent_tabs_without_overlap()
        {
            var canvas = Canvas();
            var row = Rect("Row", canvas, new Vector2(400, 40), Vector2.zero);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 3;
            var tabs = new RectTransform[3];
            var groups = new RectTransform[3];
            var captions = new TextMeshProUGUI[3];
            var closes = new RectTransform[3];
            for (var i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i] = Rect("Tab", row, new Vector2(68, 40), Vector2.zero);
                var group = Group(tab, GroupLayoutAxis.Horizontal, 5);
                groups[i] = (RectTransform)group.transform;
                groups[i].sizeDelta = new Vector2(50, 30);
                var caption = Rect("Caption", group.transform, new Vector2(45, 20), Vector2.zero);
                captions[i] = caption.gameObject.AddComponent<TextMeshProUGUI>();
                captions[i].font = TMP_Settings.defaultFontAsset;
                captions[i].fontSize = 20;
                captions[i].textWrappingMode = TextWrappingModes.NoWrap;
                captions[i].text = "W";
                var fitter = caption.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                closes[i] = Rect("Close", group.transform, new Vector2(0, 14), new Vector2(50, 7));
                var relation = tab.gameObject.AddComponent<Relation>();
                relation.reference = tab;
                relation.coordinateSpace = tab;
                relation.bindings = new[] { new RelationBinding { target = groups[i], rules = new[] { new RelationRule { side = RelationSide.Width } } } };
                relation.Record();
            }
            foreach (var value in new[] { "A", "WWWWWWW", "AB" })
            {
                captions[1].text = value;
                closes[1].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value.Length == 1 ? 0 : 25);
                yield return Settle();
                for (var i = 0; i < tabs.Length; i++)
                {
                    Assert.That(groups[i].rect.width, Is.EqualTo(captions[i].preferredWidth + 5 + closes[i].rect.width).Within(.02f));
                    Assert.That(tabs[i].rect.width, Is.EqualTo(groups[i].rect.width + 18).Within(.02f));
                    Assert.That(Position(closes[i], groups[i]).y, Is.EqualTo(7).Within(.02f));
                    if (i > 0)
                        Assert.That(Position(tabs[i], row).x, Is.EqualTo(Position(tabs[i - 1], row).x + tabs[i - 1].rect.width + 3).Within(.02f));
                }
            }
            row.gameObject.SetActive(false);
            captions[1].text = "WWW";
            row.gameObject.SetActive(true);
            yield return Settle();
            Assert.That(tabs[1].rect.width, Is.EqualTo(captions[1].preferredWidth + 5 + 25 + 18).Within(.02f));
        }
#endif
    }
}
