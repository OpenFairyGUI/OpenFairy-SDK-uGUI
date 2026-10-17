using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class PivotImportTests
    {
        private GameObject _parent;
        private RectTransform _child;

        private RectTransform Bake(Vector2 parentSize, Vector2 childSize, Vector2 position,
            Vector2 pivot, bool anchor, params RelationRule[] rules)
        {
            _parent = new GameObject("Parent", typeof(RectTransform));
            var parent = (RectTransform)_parent.transform;
            parent.pivot = new Vector2(.3f, .8f);
            parent.sizeDelta = parentSize;
            _child = (RectTransform)new GameObject("Child", typeof(RectTransform)).transform;
            _child.SetParent(parent, false);
            var element = new Schema.Display { Position = position, Pivot = pivot, Anchor = anchor };
            if (rules.Length > 0)
                element.Relations = new[] { new Schema.Relation { Rules = rules } };
            typeof(Migrate).GetMethod("SetRect", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { _child, element, childSize, parent });
            return parent;
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(_parent);

        private void Bounds(float left, float top, float width, float height)
        {
            // Read actual native corners relative to the parent's top-left, independent of anchoredPosition.
            var parent = (RectTransform)_parent.transform;
            var corners = new Vector3[4];
            _child.GetWorldCorners(corners);
            var upperLeft = parent.InverseTransformPoint(corners[1]);
            var lowerRight = parent.InverseTransformPoint(corners[3]);
            Assert.That(upperLeft.x - parent.rect.xMin, Is.EqualTo(left).Within(.001f));
            Assert.That(parent.rect.yMax - upperLeft.y, Is.EqualTo(top).Within(.001f));
            Assert.That(lowerRight.x - upperLeft.x, Is.EqualTo(width).Within(.001f));
            Assert.That(upperLeft.y - lowerRight.y, Is.EqualTo(height).Within(.001f));
        }

        private void Offsets(float left, float bottom, float right, float top)
        {
            Assert.That(_child.offsetMin.x, Is.EqualTo(left).Within(.001f));
            Assert.That(_child.offsetMin.y, Is.EqualTo(bottom).Within(.001f));
            Assert.That(_child.offsetMax.x, Is.EqualTo(right).Within(.001f));
            Assert.That(_child.offsetMax.y, Is.EqualTo(top).Within(.001f));
        }

        [TestCase(false, 230, 170)]
        [TestCase(true, 200, 110)]
        public void Point_anchor_preserves_authored_rectangle_with_either_position_convention(
            bool anchor, float left, float top)
        {
            var parent = Bake(new Vector2(600, 400), new Vector2(120, 80),
                new Vector2(230, 170), new Vector2(.25f, .75f), anchor);
            Bounds(left, top, 120, 80);
            Offsets(left, -top - 80, left + 120, -top);
            parent.sizeDelta = new Vector2(850, 550);
            Bounds(left, top, 120, 80);
            Offsets(left, -top - 80, left + 120, -top);
        }

        [Test]
        public void Full_stretch_preserves_zero_offsets_after_center_pivot_and_parent_resize()
        {
            var parent = Bake(new Vector2(160, 160), new Vector2(160, 160),
                new Vector2(80, 80), new Vector2(.5f, .5f), true,
                new RelationRule { side = RelationSide.Width, percent = true },
                new RelationRule { side = RelationSide.Height, percent = true });
            Bounds(0, 0, 160, 160);
            Offsets(0, 0, 0, 0);
            Assert.That(_child.anchoredPosition, Is.EqualTo(Vector2.zero));
            parent.sizeDelta = new Vector2(240, 220);
            Bounds(0, 0, 240, 220);
            Offsets(0, 0, 0, 0);
        }

        [Test]
        public void Stretch_with_overhanging_art_preserves_all_four_margins()
        {
            var parent = Bake(new Vector2(400, 90), new Vector2(459, 147),
                new Vector2(200, 45), new Vector2(.5f, .5f), true,
                new RelationRule { side = RelationSide.Width },
                new RelationRule { side = RelationSide.Height });
            Bounds(-29.5f, -28.5f, 459, 147);
            Offsets(-29.5f, -28.5f, 29.5f, 28.5f);
            Assert.That(_child.anchoredPosition, Is.EqualTo(Vector2.zero));
            parent.sizeDelta = new Vector2(600, 120);
            Bounds(-29.5f, -28.5f, 659, 177);
            Offsets(-29.5f, -28.5f, 29.5f, 28.5f);
        }

        [Test]
        public void One_axis_stretch_and_centered_axis_preserve_noncentral_pivot_geometry()
        {
            var parent = Bake(new Vector2(400, 300), new Vector2(300, 90),
                new Vector2(100, 200), new Vector2(.2f, .7f), true,
                new RelationRule { side = RelationSide.Width },
                new RelationRule { side = RelationSide.MiddleMiddle });
            Bounds(40, 137, 300, 90);
            Offsets(40, -77, -60, 13);
            parent.sizeDelta = new Vector2(500, 360);
            Bounds(40, 167, 400, 90);
            Offsets(40, -77, -60, 13);
        }
    }
}
