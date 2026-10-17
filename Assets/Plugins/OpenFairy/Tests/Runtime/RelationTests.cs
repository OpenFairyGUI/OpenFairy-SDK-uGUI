using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public class RelationTests
    {
#pragma warning disable CS0649 // JsonUtility populates the fixture records.
        [Serializable] private sealed class FixtureSet { public Fixture[] cases; }
        [Serializable] private sealed class Rule { public string side; public bool percent; }
        [Serializable] private sealed class Frame
        {
            public float x, y, width, height, pivotX, pivotY;
            public bool anchor;
        }
        [Serializable] private sealed class Fixture
        {
            public Rule[] rules;
            public Frame owner, previous, target;
            public bool parentTarget, ownerIsTargetParent;
            public float[] expected, anchors;
        }
#pragma warning restore CS0649

        private readonly List<GameObject> _objects = new();

        private RectTransform Rect(string name, RectTransform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _objects.Add(go);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            return rt;
        }

        private static void Size(RectTransform rt, float width, float height)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static void SetFrame(RectTransform rt, Frame frame, RectTransform space)
        {
            rt.pivot = new Vector2(frame.pivotX, 1 - frame.pivotY);
            Size(rt, frame.width, frame.height);
            rt.position = space.TransformPoint(new Vector3(
                space.rect.xMin + frame.x + frame.pivotX * frame.width,
                space.rect.yMax - frame.y - frame.pivotY * frame.height));
        }

        private static float[] Read(RectTransform rt, RectTransform space)
        {
            var point = space.InverseTransformPoint(rt.position);
            return new[] { point.x - space.rect.xMin - rt.pivot.x * rt.rect.width,
                space.rect.yMax - point.y - (1 - rt.pivot.y) * rt.rect.height,
                rt.rect.width, rt.rect.height };
        }

        private static Relation Attach(RectTransform owner, RectTransform root, RectTransform target,
            bool ownerAnchor, bool targetAnchor, RelationRule[] rules, bool nativeAnchors = false)
        {
            var relation = owner.gameObject.AddComponent<Relation>();
            relation.reference = root;
            relation.coordinateSpace = root;
            relation.pivotAsAnchor = ownerAnchor;
            relation.bindings = new[] { new RelationBinding {
                target = target, rules = rules, pivotAsAnchor = targetAnchor, nativeAnchors = nativeAnchors,
            } };
            relation.Record();
            return relation;
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void Rect_transforms_match_reference_resize_fixtures()
        {
            var fixtures = JsonUtility.FromJson<FixtureSet>(Resources.Load<TextAsset>("RelationResizeFixtures").text).cases;
            for (var i = 0; i < fixtures.Length; i++)
            {
                var fixture = fixtures[i];
                var root = Rect("Root");
                Size(root, fixture.parentTarget ? fixture.previous.width : 2000,
                    fixture.parentTarget ? fixture.previous.height : 1600);
                var owner = Rect("Owner", root);
                if (fixture.anchors != null && fixture.anchors.Length == 4)
                {
                    owner.anchorMin = new Vector2(fixture.anchors[0], fixture.anchors[1]);
                    owner.anchorMax = new Vector2(fixture.anchors[2], fixture.anchors[3]);
                }
                SetFrame(owner, fixture.owner, root);
                var target = fixture.parentTarget ? root : Rect("Target", fixture.ownerIsTargetParent ? owner : root);
                var targetSpace = fixture.ownerIsTargetParent ? owner : root;
                if (!fixture.parentTarget) SetFrame(target, fixture.previous, targetSpace);
                var rules = new RelationRule[fixture.rules.Length];
                for (var j = 0; j < rules.Length; j++)
                    rules[j] = new RelationRule { side = (RelationSide)Enum.Parse(typeof(RelationSide), fixture.rules[j].side),
                        percent = fixture.rules[j].percent };
                var relation = Attach(owner, root, target, fixture.owner.anchor, fixture.previous.anchor,
                    rules, fixture.anchors != null && fixture.anchors.Length == 4);
                if (fixture.parentTarget) Size(target, fixture.target.width, fixture.target.height);
                else SetFrame(target, fixture.target, targetSpace);
                relation.Sync();
                var actual = Read(owner, root);
                for (var j = 0; j < actual.Length; j++)
                    Assert.That(actual[j], Is.EqualTo(fixture.expected[j]).Within(.003f), $"fixture {i}, field {j}");
                TearDown();
            }
        }

        [Test]
        public void Owner_resize_preserves_its_right_and_bottom_alignment()
        {
            var root = Rect("Root");
            Size(root, 1000, 800);
            var target = Rect("Target", root);
            var owner = Rect("Owner", root);
            SetFrame(owner, new Frame { x = 10, y = 20, width = 100, height = 80, pivotX = .5f, pivotY = .5f }, root);
            Attach(owner, root, target, false, false, new[] {
                new RelationRule { side = RelationSide.RightRight },
                new RelationRule { side = RelationSide.BottomBottom },
            });
            Size(owner, 140, 100);
            var actual = Read(owner, root);
            Assert.That(actual[0], Is.EqualTo(-30).Within(.001f));
            Assert.That(actual[1], Is.EqualTo(0).Within(.001f));
        }

        [Test]
        public void Reparented_owner_keeps_the_authored_reference_space()
        {
            var root = Rect("Root");
            Size(root, 1000, 800);
            var mask = Rect("Mask", root);
            SetFrame(mask, new Frame { x = 200, y = 150, width = 300, height = 200 }, root);
            var target = Rect("Target", root);
            SetFrame(target, new Frame { x = 60, y = 50, width = 200, height = 160 }, root);
            var owner = Rect("Owner", mask);
            SetFrame(owner, new Frame { x = 10, y = 20, width = 100, height = 80 }, root);
            var relation = Attach(owner, root, target, false, false,
                new[] { new RelationRule { side = RelationSide.LeftLeft, percent = true } });
            SetFrame(target, new Frame { x = 60, y = 50, width = 400, height = 160 }, root);
            relation.Sync();
            Assert.That(Read(owner, root)[0], Is.EqualTo(-40).Within(.001f));
        }

        [Test]
        public void Scroll_margin_is_not_scaled_by_the_parent_percent_relation()
        {
            var root = Rect("Root");
            Size(root, 200, 160);
            var viewport = Rect("Viewport", root);
            SetFrame(viewport, new Frame { x = 10, y = 20, width = 180, height = 140 }, root);
            var owner = Rect("Owner", viewport);
            SetFrame(owner, new Frame { x = 10, y = 20, width = 100, height = 80 }, viewport);
            var relation = Attach(owner, root, root, false, false,
                new[] { new RelationRule { side = RelationSide.LeftLeft, percent = true } });
            relation.coordinateSpace = viewport;
            relation.Record();
            Size(root, 400, 160);
            relation.Sync();
            Assert.That(Read(owner, viewport)[0], Is.EqualTo(20).Within(.001f));
            Assert.That(Read(owner, root)[0], Is.EqualTo(30).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Subsystem_reset_allows_a_new_polling_loop()
        {
            var reset = typeof(Relation).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            var root = Rect("Root");
            Size(root, 1000, 800);
            var target = Rect("Target", root);
            var owner = Rect("Owner", root);
            SetFrame(owner, new Frame { x = 10, y = 20, width = 100, height = 80 }, root);
            Attach(owner, root, target, false, false,
                new[] { new RelationRule { side = RelationSide.LeftLeft } });
            TearDown();
            reset.Invoke(null, null);
            root = Rect("Root");
            Size(root, 1000, 800);
            target = Rect("Target", root);
            owner = Rect("Owner", root);
            SetFrame(owner, new Frame { x = 10, y = 20, width = 100, height = 80 }, root);
            Attach(owner, root, target, false, false,
                new[] { new RelationRule { side = RelationSide.LeftLeft } });
            target.anchoredPosition += new Vector2(25, 0);
            yield return null;
            yield return null;
            Assert.That(Read(owner, root)[0], Is.EqualTo(35).Within(.001f));
        }
    }
}
