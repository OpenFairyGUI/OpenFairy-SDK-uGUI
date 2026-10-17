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
    public class NativeScrollingImportTests
    {
        [Test]
        public void Auto_scrollbar_without_a_canvas_group_bakes_hidden_and_noninteractive()
        {
            var paneObject = new GameObject("Pane", typeof(RectTransform), typeof(ScrollPane));
            var barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(AuthoredScrollbar));
            try
            {
                var hidden = new List<CanvasGroup>();
                typeof(Migrate).GetMethod("BindScrollBar", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { barObject, paneObject.GetComponent<ScrollPane>(), false,
                        Schema.ScrollBarDisplay.Auto, hidden });
                Assert.That(hidden, Has.Count.EqualTo(1));
                Assert.That(hidden[0], Is.SameAs(barObject.GetComponent<CanvasGroup>()));
                Assert.That(hidden[0].alpha, Is.Zero);
                Assert.That(hidden[0].interactable || hidden[0].blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(barObject);
                Object.DestroyImmediate(paneObject);
            }
        }

        [Test]
        public void Schema_preserves_explicit_scrollbars_controller_and_typed_flags()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "<component size='200,120' overflow='scroll' scrollBar='auto' scrollBarRes='ui://fixture1vertical,' scrollBarFlags='1032'><displayList><list id='items' pageController='Pages' scrollBarFlags='38' scrollBar='visible' scrollBarRes=',ui://fixture1horizontal'/></displayList></component>");
                var xml = Schema.Component.Load(path);
                Assert.AreEqual(Schema.ScrollBarDisplay.Auto, xml.ScrollBar);
                Assert.AreEqual("ui://fixture1vertical", xml.ScrollBarRes[0]);
                Assert.IsTrue(xml.ScrollBarFlags.HasFlag(Schema.ScrollFlags.Floating | Schema.ScrollFlags.PageMode));
                Assert.AreEqual("Pages", xml.DisplayList[0].PageController);
                Assert.IsTrue(xml.DisplayList[0].ScrollBarFlags.HasFlag(Schema.ScrollFlags.TouchOff | Schema.ScrollFlags.SnapToItem | Schema.ScrollFlags.DisplayInDemand));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Importer_reparents_the_same_grip_and_preserves_its_actual_rectangle()
        {
            var root = new GameObject("Scrollbar", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.sizeDelta = new Vector2(20, 120);
                rt.pivot = new Vector2(0, 1);
                var track = Rect("Track", rt, new Vector2(20, 100), new Vector2(0, -10));
                var grip = Rect("Grip", rt, new Vector2(12, 25), new Vector2(3, -30));
                var before = new Vector3[4];
                grip.GetWorldCorners(before);
                var method = typeof(Migrate).GetMethod("SetupScrollBar", BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new object[] { root, new List<(Schema.Display, GameObject)> {
                    (new Schema.Display { Name = "bar" }, track.gameObject),
                    (new Schema.Display { Name = "grip" }, grip.gameObject),
                } });
                var bar = root.GetComponent<AuthoredScrollbar>();
                Assert.AreSame(grip, bar.handleRect);
                Assert.AreSame(track, grip.parent);
                Assert.That(bar.size, Is.EqualTo(.25f).Within(.0001f));
                var after = new Vector3[4];
                grip.GetWorldCorners(after);
                for (var i = 0; i < 4; i++) Assert.That(Vector3.Distance(before[i], after[i]), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }
    }
}
