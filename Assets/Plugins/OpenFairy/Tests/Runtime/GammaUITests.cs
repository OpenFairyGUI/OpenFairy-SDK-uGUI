using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class GammaUITests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Shared_canvas_is_restored_only_after_the_last_gamma_root_stops(bool original)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.vertexColorAlwaysGammaSpace = original;
            var first = new GameObject("First", typeof(RectTransform));
            var second = new GameObject("Second", typeof(RectTransform));
            first.transform.SetParent(canvas.transform); second.transform.SetParent(canvas.transform);
            first.AddComponent<GammaUI>(); second.AddComponent<GammaUI>();
            try
            {
                Assert.That(canvas.vertexColorAlwaysGammaSpace, Is.True);
                first.SetActive(false);
                Assert.That(canvas.vertexColorAlwaysGammaSpace, Is.True);
                second.SetActive(false);
                Assert.That(canvas.vertexColorAlwaysGammaSpace, Is.EqualTo(original));
            }
            finally { Object.DestroyImmediate(canvasGo); }
        }

        [Test]
        public void Reparenting_releases_the_previous_canvas_and_binds_the_new_canvas()
        {
            var first = new GameObject("First", typeof(RectTransform), typeof(Canvas));
            var second = new GameObject("Second", typeof(RectTransform), typeof(Canvas));
            var ui = new GameObject("UI", typeof(RectTransform));
            ui.transform.SetParent(first.transform); ui.AddComponent<GammaUI>();
            try
            {
                ui.transform.SetParent(second.transform);
                Assert.That(first.GetComponent<Canvas>().vertexColorAlwaysGammaSpace, Is.False);
                Assert.That(second.GetComponent<Canvas>().vertexColorAlwaysGammaSpace, Is.True);
                ui.SetActive(false);
                Assert.That(second.GetComponent<Canvas>().vertexColorAlwaysGammaSpace, Is.False);
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
    }
}
