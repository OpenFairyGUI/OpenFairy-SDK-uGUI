using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class TooltipImportTests
    {
        private static object Call(string name, params object[] arguments) => typeof(Migrate)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);

        [Test]
        public void Schema_preserves_tooltip_text_touchability_and_component_root_relations()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component size='100,60'><displayList><component id='child' tooltips='A &amp; B&#10;C' touchable='false'/></displayList><relation target='child' sidePair='width-width,height-height'/></component>");
                var component = Schema.Component.Load(file);
                Assert.That(component.DisplayList[0].Tooltips, Is.EqualTo("A & B\nC"));
                Assert.That(component.DisplayList[0].Touchable, Is.False);
                Assert.That(component.Relations.Length, Is.EqualTo(1));
                Assert.That(component.Relations[0].Target, Is.EqualTo("child"));
                Assert.That(component.Relations[0].Rules[1].side, Is.EqualTo(RelationSide.Height));
            }
            finally { File.Delete(file); }
        }

        [TestCase(Schema.TextAutoSize.Both, 2)]
        [TestCase(Schema.TextAutoSize.Height, 4)]
        [TestCase(Schema.TextAutoSize.None, 6)]
        [TestCase(Schema.TextAutoSize.Shrink, 6)]
        public void Content_owned_axes_do_not_receive_parent_size_constraints(Schema.TextAutoSize size, int count)
        {
            var element = new Schema.Display { Kind = Schema.DisplayKind.Text, AutoSize = size,
                Relations = new[] { new Schema.Relation { Rules = new[] {
                    new RelationRule { side = RelationSide.Width }, new RelationRule { side = RelationSide.Height },
                    new RelationRule { side = RelationSide.RightExtRight }, new RelationRule { side = RelationSide.BottomExtBottom },
                    new RelationRule { side = RelationSide.CenterCenter }, new RelationRule { side = RelationSide.MiddleMiddle },
                } } } };
            Call("PrepareTextRelations", element);
            Assert.That(element.Relations[0].Rules.Length, Is.EqualTo(count));
            Assert.That(element.Relations[0].Rules[count - 1].side, Is.EqualTo(RelationSide.MiddleMiddle));
        }

        [TestCase(Schema.TextAutoSize.Both, true, true)]
        [TestCase(Schema.TextAutoSize.Height, true, false)]
        [TestCase(Schema.TextAutoSize.None, false, false)]
        [TestCase(Schema.TextAutoSize.Shrink, false, false)]
        [TestCase(Schema.TextAutoSize.Ellipsis, false, false)]
        public void Text_auto_size_bakes_only_the_native_layout_axes_it_owns(Schema.TextAutoSize size, bool exists, bool horizontal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            try
            {
                var element = new Schema.Display { Kind = Schema.DisplayKind.Text, AutoSize = size };
                Call("ConfigureTextLayout", go, element);
                var fitter = go.GetComponent<ContentSizeFitter>();
                Assert.That(fitter != null, Is.EqualTo(exists));
                if (!exists) return;
                Assert.That(fitter.verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                Assert.That(fitter.horizontalFit, Is.EqualTo(horizontal ? ContentSizeFitter.FitMode.PreferredSize
                    : ContentSizeFitter.FitMode.Unconstrained));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Input_text_keeps_its_authored_rectangle_and_size_relations()
        {
            var go = new GameObject("Input", typeof(RectTransform));
            try
            {
                var element = new Schema.Display { Kind = Schema.DisplayKind.InputText, AutoSize = Schema.TextAutoSize.Both,
                    Relations = new[] { new Schema.Relation { Rules = new[] { new RelationRule { side = RelationSide.Width } } } } };
                Call("PrepareTextRelations", element);
                Call("ConfigureTextLayout", go, element);
                Assert.That(element.Relations[0].Rules.Length, Is.EqualTo(1));
                Assert.That(go.GetComponent<ContentSizeFitter>(), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Component_root_relation_bakes_a_direct_child_reference()
        {
            var root = new GameObject("Label", typeof(RectTransform));
            var child = new GameObject("Title", typeof(RectTransform));
            try
            {
                child.transform.SetParent(root.transform, false);
                var rt = (RectTransform)root.transform;
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(200, 60);
                var childRect = (RectTransform)child.transform;
                childRect.sizeDelta = new Vector2(180, 40);
                var title = new Schema.Display { Id = "title", Kind = Schema.DisplayKind.Text };
                var source = new Schema.Display { Relations = new[] { new Schema.Relation { Target = "title",
                    Rules = new[] { new RelationRule { side = RelationSide.Height } } } } };
                var children = new Dictionary<string, (Schema.Display, GameObject)> { ["title"] = (title, child) };
                Call("BuildRelations", rt, rt, source, root, children);
                var relation = root.GetComponent<Relation>();
                Assert.That(relation.bindings[0].target, Is.SameAs(childRect));
                Assert.That(relation.coordinateSpace, Is.SameAs(rt));
                childRect.sizeDelta = new Vector2(180, 100);
                relation.Sync();
                Assert.That(rt.rect.height, Is.EqualTo(120));
                relation.Sync();
                Assert.That(rt.rect.height, Is.EqualTo(120));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
