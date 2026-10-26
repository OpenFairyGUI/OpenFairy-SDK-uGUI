#if !FAIRYGUI_TMPRO
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class ExternalFontTests
    {
        private const string Output = "Assets/__ExternalFontTests";
        private Font _font;
        private readonly List<Object> _owned = new();

        [SetUp]
        public void Setup()
        {
            ExternalFont.Clear();
            Directory.CreateDirectory(Output);
            File.Copy(EditorApplication.applicationContentsPath + "/Resources/Fonts/Inter-Regular.ttf", Output + "/FileFont.ttf", true);
            AssetDatabase.ImportAsset(Output + "/FileFont.ttf");
            _font = AssetDatabase.LoadAssetAtPath<Font>(Output + "/FileFont.ttf");
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var value in _owned) if (value != null) Object.DestroyImmediate(value);
            _owned.Clear();
            ExternalFont.Clear();
            AssetDatabase.DeleteAsset(Output);
        }

        [TestCase(GlyphRenderMode.SMOOTH)]
        [TestCase(GlyphRenderMode.SMOOTH_HINTED)]
        [TestCase(GlyphRenderMode.RASTER)]
        public void Multi_size_atlas_preserves_engine_metrics_and_every_raster_sample(GlyphRenderMode mode)
        {
            var cache = ExternalFont.Get(_font, mode);
            foreach (var size in new[] { 13, 17, 41 })
            {
                cache.Request("Agj2? ", size);
                var reference = FontAsset.CreateFontAsset(_font, size, 0, mode, 256, 256, AtlasPopulationMode.Dynamic);
                try
                {
                    reference.TryAddCharacters("Agj2? ");
                    foreach (var code in "Agj2? ")
                    {
                        Assert.That(cache.TryGet(code, size, out var actual), Is.True);
#pragma warning disable CS0618
                        var glyph = reference.characterLookupTable[code].glyph;
#pragma warning restore CS0618
                        Assert.That(actual.metrics, Is.EqualTo(glyph.metrics));
                        var rect = glyph.glyphRect;
                        Assert.That(actual.pixels.width, Is.EqualTo(rect.width));
                        Assert.That(actual.pixels.height, Is.EqualTo(rect.height));
                        var source = reference.atlasTextures[glyph.atlasIndex];
                        var expected = source.GetPixelData<byte>(0);
                        var pixels = cache.texture.GetPixelData<byte>(0);
                        for (var y = 0; y < rect.height; y++)
                            for (var x = 0; x < rect.width; x++)
                                Assert.That(pixels[(actual.pixels.y + y) * cache.texture.width + actual.pixels.x + x],
                                    Is.EqualTo(expected[(rect.y + y) * source.width + rect.x + x]), $"{size}/{code}/{x}/{y}");
                    }
                }
                finally
                {
                    foreach (var texture in reference.atlasTextures) Object.DestroyImmediate(texture);
                    Object.DestroyImmediate(reference.material);
                    Object.DestroyImmediate(reference);
                }
            }
        }

        private TextField MakeText()
        {
            var go = new GameObject("File font", typeof(RectTransform));
            _owned.Add(go);
            go.SetActive(false);
            var text = go.AddComponent<TextField>();
            text.font = _font;
            text.externalFont = true;
            text.fontSize = 17;
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.sizeDelta = new Vector2(500, 100);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.SetActive(true);
            return text;
        }

        private static List<UIVertex> Mesh(TextField text)
        {
            using var helper = new VertexHelper();
            typeof(TextField).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(text, new object[] { helper });
            var result = new List<UIVertex>();
            for (var i = 0; i < helper.currentVertCount; i++)
            {
                var vertex = new UIVertex();
                helper.PopulateUIVertex(ref vertex, i);
                result.Add(vertex);
            }
            return result;
        }

        [Test]
        public void File_font_geometry_uses_fractional_metrics_and_native_italic_and_bold_offsets()
        {
            var text = MakeText();
            text.text = "A";
            text.fontStyle = FontStyle.BoldAndItalic;
            var vertices = Mesh(text);
            FontEngine.LoadFontFace(_font, 17);
            Assert.That(FontEngine.TryGetGlyphWithUnicodeValue('A', GlyphLoadFlags.LOAD_NO_HINTING, out var glyph), Is.True);
            var metrics = glyph.metrics;
            Assert.That(vertices.Count, Is.EqualTo(20));
            Assert.That(vertices[0].position.x, Is.EqualTo(2 + metrics.horizontalBearingX + (metrics.horizontalBearingY - metrics.height) * .35f).Within(1e-6));
            Assert.That(vertices[1].position.x, Is.EqualTo(2 + metrics.horizontalBearingX + metrics.horizontalBearingY * .35f).Within(1e-6));
            Assert.That(vertices[0].position.y, Is.EqualTo(-(2 + 17 - metrics.horizontalBearingY + metrics.height)).Within(1e-6));
            Assert.That(text.preferredWidth, Is.EqualTo(Mathf.Ceil(metrics.horizontalAdvance) + 4));
            var offsets = new[] { new Vector3(-.5f, 0), new(.5f, 0), new(0, -.5f), new(0, .5f) };
            for (var copy = 0; copy < 4; copy++)
                for (var vertex = 0; vertex < 4; vertex++)
                    Assert.That(vertices[4 + copy * 4 + vertex].position, Is.EqualTo(vertices[vertex].position + offsets[copy]));
        }

        [Test]
        public void Atlas_growth_preserves_samples_and_updates_preexisting_text_uvs()
        {
            var text = MakeText();
            text.text = "A";
            var before = Mesh(text);
            var cache = ExternalFont.Get(_font, GlyphRenderMode.SMOOTH);
            var old = cache.texture;
            cache.TryGet('A', 17, out var character);
            var alpha = new byte[character.pixels.width * character.pixels.height];
            var data = cache.texture.GetPixelData<byte>(0);
            for (var y = 0; y < character.pixels.height; y++)
                for (var x = 0; x < character.pixels.width; x++) alpha[y * character.pixels.width + x] = data[(character.pixels.y + y) * old.width + character.pixels.x + x];
            cache.Request("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 250);
            Assert.That(cache.version, Is.GreaterThan(0));
            Assert.That(old == null, Is.True);
            var after = Mesh(text);
            var scale = 1024f / cache.texture.width;
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].position, Is.EqualTo(before[i].position));
                Assert.That(after[i].uv0, Is.EqualTo(before[i].uv0 * scale));
            }
            Assert.That(text.mainTexture, Is.SameAs(cache.texture));
            data = cache.texture.GetPixelData<byte>(0);
            for (var y = 0; y < character.pixels.height; y++)
                for (var x = 0; x < character.pixels.width; x++)
                    Assert.That(data[(character.pixels.y + y) * cache.texture.width + character.pixels.x + x], Is.EqualTo(alpha[y * character.pixels.width + x]));
        }

        [UnityTest]
        public IEnumerator Saved_file_font_settings_survive_play_and_accept_new_sizes_and_characters()
        {
            var text = MakeText();
            text.text = "A";
            text.fontRenderMode = GlyphRenderMode.SMOOTH_HINTED;
            PrefabUtility.SaveAsPrefabAsset(text.gameObject, Output + "/Text.prefab");
            Object.DestroyImmediate(text.gameObject);
            yield return new EnterPlayMode();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Text.prefab");
            var instance = Object.Instantiate(prefab);
            _owned.Add(instance);
            var loaded = instance.GetComponent<TextField>();
            Assert.That(loaded.externalFont, Is.True);
            Assert.That(loaded.fontRenderMode, Is.EqualTo(GlyphRenderMode.SMOOTH_HINTED));
            Assert.That(Mesh(loaded).Count, Is.EqualTo(4));
            loaded.ubb = true;
            loaded.text = "A[size=41]j[/size]";
            Assert.That(Mesh(loaded).Count, Is.EqualTo(8));
            loaded.text = "…";
            Assert.That(Mesh(loaded).Count, Is.EqualTo(4));
            yield return new ExitPlayMode();
        }
    }
}
#endif
