using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class TextStrokeTests
    {
        [Test]
        public void Zero_width_preserves_glyph_geometry_and_alpha_after_changing_width()
        {
            var go = new GameObject("Stroke", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TextStroke));
            try
            {
                var stroke = go.GetComponent<TextStroke>();
                stroke.effectColor = Color.yellow;
                var glyph = new UIVertex[4];
                for (var i = 0; i < glyph.Length; i++)
                {
                    glyph[i] = UIVertex.simpleVert;
                    glyph[i].color = new Color32(204, 204, 204, 96);
                    glyph[i].position = new Vector3(i / 2, i % 2);
                }
                foreach (var width in new[] { 0f, 2f, 0f })
                {
                    using var mesh = new VertexHelper();
                    mesh.AddUIVertexQuad(glyph);
                    stroke.effectDistance = new Vector2(width, width);
                    stroke.ModifyMesh(mesh);
                    Assert.That(mesh.currentVertCount, Is.EqualTo(width == 0 ? 4 : 30));
                    if (width != 0) continue;
                    for (var i = 0; i < glyph.Length; i++)
                    {
                        var actual = default(UIVertex);
                        mesh.PopulateUIVertex(ref actual, i);
                        Assert.That(actual.position, Is.EqualTo(glyph[i].position));
                        Assert.That(actual.color, Is.EqualTo(glyph[i].color));
                        Assert.That(actual.uv0, Is.EqualTo(glyph[i].uv0));
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
