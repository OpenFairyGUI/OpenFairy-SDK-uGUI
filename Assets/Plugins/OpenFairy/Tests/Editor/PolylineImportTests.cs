using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class PolylineImportTests
    {
        [Test]
        public void Schema_and_real_shape_configuration_keep_open_points_stroke_and_round_endpoints()
        {
            var file = Path.GetTempFileName();
            var go = new GameObject("Line", typeof(RectTransform), typeof(Graph));
            try
            {
                File.WriteAllText(file, "<component size='100,100'><displayList><graph id='line' type='polyline' points='10,20,90,20,90,80' lineSize='8' lineColor='#8040c020' roundEdge='true'/></displayList></component>");
                var element = Schema.Component.Load(file).DisplayList[0];
                typeof(Migrate).GetMethod("ConfigureShape", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { go.GetComponent<Graph>(), element });
                var graph = go.GetComponent<Graph>();
                Assert.That(graph.kind, Is.EqualTo(Graph.Kind.Polyline));
                Assert.That(graph.roundEdge, Is.True);
                Assert.That(graph.lineSize, Is.EqualTo(8));
                Assert.That(graph.lineColor, Is.EqualTo(new Color(64 / 255f, 192 / 255f, 32 / 255f, 128 / 255f)));
                CollectionAssert.AreEqual(new[] { new Vector2(10, 20), new Vector2(90, 20), new Vector2(90, 80) }, graph.points);
            }
            finally { File.Delete(file); Object.DestroyImmediate(go); }
        }
    }
}
