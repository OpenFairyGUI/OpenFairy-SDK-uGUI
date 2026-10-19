using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class RelationImportTests
    {
        [Test]
        public void Schema_retains_percent_canonicalizes_aliases_and_expands_size_in_order()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, @"<component size='100,100'><displayList>
<text id='caption'><relation target='' sidePair='center-center%,width-width,height_height%,size%,left-left'/></text>
</displayList></component>");
                var rules = Schema.Component.Load(file).DisplayList[0].Relations[0].Rules;
                Assert.AreEqual(4, rules.Length);
                Assert.AreEqual(RelationSide.CenterCenter, rules[0].side);
                Assert.IsTrue(rules[0].percent);
                Assert.AreEqual(RelationSide.Width, rules[1].side);
                Assert.IsFalse(rules[1].percent);
                Assert.AreEqual(RelationSide.Height, rules[2].side);
                Assert.IsTrue(rules[2].percent);
                Assert.AreEqual(RelationSide.LeftLeft, rules[3].side);
            }
            finally { File.Delete(file); }
        }

        [Test]
        public void Parent_anchor_bake_uses_affine_rules_and_rejects_order_dependent_percent_rules()
        {
            var method = typeof(Migrate).Assembly.GetType("OpenFairy.UGUI.Editor.RelationAnchorBaker")
                .GetMethod("TryBake", BindingFlags.NonPublic | BindingFlags.Static);
            var source = new Schema.Display { Position = new Vector2(10, 20),
                Relations = new[] { new Schema.Relation { Rules = new[] {
                    new RelationRule { side = RelationSide.Width, percent = true },
                    new RelationRule { side = RelationSide.CenterCenter, percent = true },
                } } } };
            object[] args = { source, new Vector2(100, 80), new Vector2(200, 160), Vector2.zero, Vector2.zero };
            Assert.IsTrue((bool)method.Invoke(null, args));
            Assert.That(((Vector2)args[3]).x, Is.EqualTo(.05f).Within(.0001f));
            Assert.That(((Vector2)args[4]).x, Is.EqualTo(.55f).Within(.0001f));
            source.Anchor = true;
            source.Pivot = new Vector2(.5f, .5f);
            args[0] = source;
            Assert.IsFalse((bool)method.Invoke(null, args));
        }
    }
}
