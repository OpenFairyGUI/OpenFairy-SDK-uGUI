using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class GraphPolylineTests
    {
        private readonly List<Object> _owned = new();

        private Graph MakeGraph(Transform parent = null)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Graph));
            if (parent == null) _owned.Add(go);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(128, 128);
            var graph = go.GetComponent<Graph>();
            graph.kind = Graph.Kind.Polyline;
            graph.lineSize = 8;
            graph.lineColor = Color.red;
            return graph;
        }

        private static List<UIVertex> Mesh(Graph graph)
        {
            using var helper = new VertexHelper();
            typeof(Graph).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(VertexHelper) }, null).Invoke(graph, new object[] { helper });
            var vertices = new List<UIVertex>();
            helper.GetUIVertexStream(vertices);
            return vertices;
        }

        private static bool Covers(List<UIVertex> mesh, Vector2 authoredPoint)
        {
            var point = new Vector2(authoredPoint.x, -authoredPoint.y);
            static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            for (var i = 0; i < mesh.Count; i += 3)
            {
                Vector2 a = mesh[i].position, b = mesh[i + 1].position, c = mesh[i + 2].position;
                var area = Cross(b - a, c - a);
                if (Mathf.Abs(area) < .00001f) continue;
                var p = Cross(b - a, point - a) / area;
                var q = Cross(point - a, c - a) / area;
                if (p >= 0 && q >= 0 && p + q <= 1) return true;
            }
            return false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _owned)
            {
                if (obj is RenderTexture rt) rt.Release();
                Object.DestroyImmediate(obj);
            }
            _owned.Clear();
        }

        [Test]
        public void Open_line_has_centered_thickness_and_never_closes_or_fills_the_path()
        {
            var graph = MakeGraph();
            graph.points = new[] { new Vector2(20, 20), new Vector2(100, 20), new Vector2(100, 100) };
            var mesh = Mesh(graph);
            Assert.That(Covers(mesh, new Vector2(55, 20)), Is.True);
            Assert.That(Covers(mesh, new Vector2(100, 60)), Is.True);
            Assert.That(Covers(mesh, new Vector2(55, 24.1f)), Is.False);
            Assert.That(Covers(mesh, new Vector2(55, 55)), Is.False, "No last-to-first segment or filled interior.");
            Assert.That(Covers(mesh, new Vector2(17, 20)), Is.False, "Butt endpoint.");
            foreach (var vertex in mesh) Assert.That(vertex.color, Is.EqualTo((Color32)Color.red));
        }

        [Test]
        public void Round_caps_extend_by_half_the_stroke_and_percent_points_follow_rect_size()
        {
            var graph = MakeGraph();
            graph.points = new[] { new Vector2(.25f, .5f), new Vector2(.75f, .5f) };
            graph.usePercentPositions = true;
            graph.roundEdge = true;
            var mesh = Mesh(graph);
            Assert.That(Covers(mesh, new Vector2(29, 64)), Is.True);
            Assert.That(Covers(mesh, new Vector2(99, 64)), Is.True);
            Assert.That(Covers(mesh, new Vector2(27.9f, 64)), Is.False);
            graph.rectTransform.sizeDelta = new Vector2(256, 128);
            mesh = Mesh(graph);
            Assert.That(Covers(mesh, new Vector2(61, 64)), Is.True);
            Assert.That(Covers(mesh, new Vector2(195, 64)), Is.True);
        }

        [Test]
        public void Empty_single_and_duplicate_points_do_not_generate_invalid_geometry()
        {
            var graph = MakeGraph();
            Assert.That(Mesh(graph), Is.Empty);
            graph.points = new[] { Vector2.one };
            Assert.That(Mesh(graph), Is.Empty);
            graph.points = new[] { Vector2.one, Vector2.one };
            Assert.That(Mesh(graph), Is.Empty);
            graph.points = new[] { new Vector2(20, 20), new Vector2(20, 20), new Vector2(100, 20), new Vector2(100, 20) };
            graph.roundEdge = true;
            var mesh = Mesh(graph);
            Assert.That(Covers(mesh, new Vector2(55, 20)), Is.True);
            foreach (var vertex in mesh)
                Assert.That(float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y), Is.False);
            graph.lineSize = 0;
            Assert.That(Mesh(graph), Is.Empty);
        }

        private static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = destination };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        [UnityTest]
        public IEnumerator Native_graph_is_visible_with_round_caps_and_respects_internal_mask_and_alpha()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _owned.Add(root);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var target = (RectTransform)new GameObject("Target", typeof(RectTransform)).transform;
            target.SetParent(root.transform, false); target.sizeDelta = new Vector2(128, 128);
            var clip = (RectTransform)new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup)).transform;
            clip.SetParent(target, false);
            clip.anchorMin = clip.anchorMax = clip.pivot = new Vector2(0, 1);
            clip.sizeDelta = new Vector2(64, 128);
            clip.GetComponent<CanvasGroup>().alpha = .5f;
            var graph = MakeGraph(clip);
            graph.points = new[] { new Vector2(20, 40), new Vector2(100, 40), new Vector2(100, 100) };
            graph.roundEdge = true;
            graph.lineColor = new Color(1, 0, 0, .8f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var destination = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 1 };
            _owned.Add(destination); destination.Create();
            using var capture = new UiObjectCapture(target, Shader.Find("OpenFairy/UI Capture"), 31);
            capture.Capture(destination, Render);
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            _owned.Add(image);
            var previous = RenderTexture.active;
            try { RenderTexture.active = destination; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); }
            finally { RenderTexture.active = previous; }
            Color At(float x, float y)
            {
                var p = new Vector2(x - 64, 64 - y);
                return image.GetPixel((int)((p.x - capture.bounds.xMin) / capture.bounds.width * 128), (int)((p.y - capture.bounds.yMin) / capture.bounds.height * 128));
            }
            Assert.That(At(40, 40).a, Is.EqualTo(.4f).Within(.025f));
            Assert.That(At(40, 40).r, Is.EqualTo(.4f).Within(.025f));
            Assert.That(At(17, 40).a, Is.GreaterThan(.3f), "Round cap must render.");
            Assert.That(At(100, 40).a, Is.LessThan(.01f), "Internal native RectMask2D clips the line.");
            Assert.That(At(45, 60).a, Is.LessThan(.01f), "Open path has no filled interior.");
        }
    }
}
