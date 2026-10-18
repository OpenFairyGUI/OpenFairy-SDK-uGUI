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
    public class RelationFittedTargetTests
    {
        private readonly List<GameObject> _objects = new();

        private sealed class Fixture
        {
            public RectTransform root, first, second, firstTitle, secondTitle, firstSubtitle, secondSubtitle;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pivot)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Attach(RectTransform owner, RectTransform reference, RectTransform space,
            RectTransform target, RelationSide side, bool native = false)
        {
            var relation = owner.gameObject.AddComponent<Relation>();
            relation.reference = reference;
            relation.coordinateSpace = space;
            relation.pivotAsAnchor = true;
            relation.bindings = new[] { new RelationBinding { target = target, pivotAsAnchor = true,
                nativeAnchors = native, rules = new[] { new RelationRule { side = side } } } };
        }

        private static RectTransform Item(RectTransform root, float pivot, float right)
        {
            var item = Rect("Item", root, new Vector2(240, 60), new Vector2(pivot, 1));
            item.anchoredPosition = new Vector2(right - (1 - pivot) * 240, -20);
            var title = Rect("Title", item, new Vector2(140, 24), new Vector2(0, .5f));
            title.anchoredPosition = new Vector2(44, -20);
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            title.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var subtitle = Rect("Subtitle", item, new Vector2(60, 20), new Vector2(.5f, .5f));
            subtitle.anchoredPosition = new Vector2(114, -44);
            Attach(item, item, item, title, RelationSide.RightExtRight);
            Attach(subtitle, item, item, title, RelationSide.CenterCenter);
            return item;
        }

        private Fixture Create(float pivot, bool zeroDrivenSize)
        {
            var source = Rect("Canvas", null, new Vector2(1200, 200), new Vector2(0, 1));
            _objects.Add(source.gameObject);
            source.gameObject.SetActive(false);
            source.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var first = Item(source, pivot, 908);
            var second = Item(source, pivot, 1164);
            second.anchorMin = second.anchorMax = new Vector2(1, 1);
            second.anchoredPosition -= new Vector2(1200, 0);
            Attach(first, source, source, second, RelationSide.RightLeft);
            Attach(second, source, source, source, RelationSide.RightRight, true);
            foreach (var relation in source.GetComponentsInChildren<Relation>(true)) relation.Record();
            if (zeroDrivenSize)
                foreach (var fitter in source.GetComponentsInChildren<ContentSizeFitter>(true))
                    ((RectTransform)fitter.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            // Cloning preserves serialized baselines/references but resets each relation's nonserialized lifecycle.
            var root = Object.Instantiate(source);
            _objects.Add(root.gameObject);
            var a = (RectTransform)root.GetChild(0);
            var b = (RectTransform)root.GetChild(1);
            return new Fixture { root = root, first = a, second = b,
                firstTitle = (RectTransform)a.GetChild(0), secondTitle = (RectTransform)b.GetChild(0),
                firstSubtitle = (RectTransform)a.GetChild(1), secondSubtitle = (RectTransform)b.GetChild(1) };
        }

        private static Vector2 Edges(RectTransform rect, RectTransform root)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Vector2(root.InverseTransformPoint(corners[1]).x, root.InverseTransformPoint(corners[2]).x);
        }

        private static void AssertGeometry(Fixture fixture)
        {
            var first = Edges(fixture.first, fixture.root);
            var second = Edges(fixture.second, fixture.root);
            Assert.That(fixture.root.rect.xMax - second.y, Is.EqualTo(36).Within(.02f), "Outer right margin");
            Assert.That(second.x - first.y, Is.EqualTo(16).Within(.02f), "Chained sibling margin");
            foreach (var pair in new[] { (fixture.first, fixture.firstTitle, fixture.firstSubtitle),
                (fixture.second, fixture.secondTitle, fixture.secondSubtitle) })
            {
                Assert.That(pair.Item1.rect.width - pair.Item2.rect.width, Is.EqualTo(100).Within(.02f), "Authored child padding");
                var title = Edges(pair.Item2, pair.Item1);
                var subtitle = Edges(pair.Item3, pair.Item1);
                Assert.That((title.x + title.y) / 2, Is.EqualTo((subtitle.x + subtitle.y) / 2).Within(.02f), "Nested title alignment");
            }
        }

        private static IEnumerator Stable(Fixture fixture)
        {
            for (int frame = 0; frame < 4; frame++) yield return null;
            for (int frame = 0; frame < 4; frame++) { AssertGeometry(fixture); yield return null; }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator Unrelated_fitted_targets_keep_the_baked_baseline_before_first_layout()
        {
            var fixture = Create(1, true);
            Assert.That(fixture.firstTitle.rect.width, Is.Zero);
            fixture.root.gameObject.SetActive(true);
            var inner = fixture.first.GetComponent<Relation>();
            Assert.That(inner.bindings[0].previous.size.x, Is.EqualTo(140), "A target without its own Relation must not overwrite the saved baseline with zero.");
            yield return Stable(fixture);
        }

        [UnityTest]
        public IEnumerator Self_space_resizing_keeps_outer_alignment_and_sibling_chains_for_every_pivot()
        {
            foreach (float pivot in new[] { 0f, .5f, 1f })
            {
                var fixture = Create(pivot, false);
                fixture.root.gameObject.SetActive(true);
                yield return Stable(fixture);
                foreach (float width in new[] { 330f, 24f, 210f })
                {
                    fixture.secondTitle.GetComponent<LayoutElement>().preferredWidth = width;
                    fixture.firstTitle.GetComponent<LayoutElement>().preferredWidth = width / 2;
                    yield return Stable(fixture);
                }
                fixture.root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1500);
                yield return Stable(fixture);
                fixture.root.gameObject.SetActive(false);
                fixture.firstTitle.GetComponent<LayoutElement>().preferredWidth = 70;
                fixture.secondTitle.GetComponent<LayoutElement>().preferredWidth = 280;
                fixture.root.gameObject.SetActive(true);
                yield return Stable(fixture);
            }
        }

        [UnityTest]
        public IEnumerator Active_instantiation_restores_targets_before_child_behaviours_are_enabled()
        {
            var template = Create(1, true);
            var inactive = new GameObject("Inactive");
            _objects.Add(inactive);
            inactive.SetActive(false);
            template.root.SetParent(inactive.transform, false);
            template.root.gameObject.SetActive(true);
            var root = Object.Instantiate(template.root, null, false);
            _objects.Add(root.gameObject);
            var first = (RectTransform)root.GetChild(0);
            var second = (RectTransform)root.GetChild(1);
            var fixture = new Fixture { root = root, first = first, second = second,
                firstTitle = (RectTransform)first.GetChild(0), secondTitle = (RectTransform)second.GetChild(0),
                firstSubtitle = (RectTransform)first.GetChild(1), secondSubtitle = (RectTransform)second.GetChild(1) };
            Assert.That(first.GetComponent<Relation>().bindings[0].previous.size.x, Is.EqualTo(140));
            yield return Stable(fixture);
        }

        [UnityTest]
        public IEnumerator A_parent_can_follow_a_fitted_horizontal_group_without_accumulating_position_error()
        {
            var fixture = Create(.5f, true);
            var group = fixture.secondTitle;
            group.GetComponent<LayoutElement>().enabled = false;
            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 8;
            Rect("Icon", group, new Vector2(24, 24), new Vector2(0, 1));
            var label = Rect("Label", group, new Vector2(108, 24), new Vector2(0, 1));
            fixture.root.gameObject.SetActive(true);
            yield return Stable(fixture);
            foreach (float width in new[] { 250f, 30f, 108f })
            {
                label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                LayoutRebuilder.MarkLayoutForRebuild(group);
                yield return Stable(fixture);
                Assert.That(group.rect.width, Is.EqualTo(24 + 8 + width).Within(.02f), "Native group preferred width");
            }
        }

#if FAIRYGUI_TMPRO
        [UnityTest]
        public IEnumerator Native_text_long_short_changes_and_reenable_preserve_the_authored_relations()
        {
            var fixture = Create(1, true);
            fixture.root.gameObject.SetActive(true);
            yield return Stable(fixture);
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            var text = fixture.secondTitle.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            fixture.secondTitle.GetComponent<LayoutElement>().enabled = false;
            foreach (string value in new[] { "123456789012", "12", "1234567" })
            {
                text.text = value;
                yield return Stable(fixture);
                Assert.That(fixture.secondTitle.rect.width, Is.EqualTo(text.preferredWidth).Within(.02f), "Actual native text metrics");
            }
            fixture.root.gameObject.SetActive(false);
            text.text = "123";
            fixture.root.gameObject.SetActive(true);
            yield return Stable(fixture);
        }
#endif
    }
}
