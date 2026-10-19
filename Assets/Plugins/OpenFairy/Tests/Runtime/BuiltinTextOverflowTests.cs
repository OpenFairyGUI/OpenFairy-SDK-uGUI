using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class BuiltinTextOverflowTests
    {
        private readonly List<Object> _owned = new();

        private TextField MakeText(bool bitmap, bool wrap, Vector2 size)
        {
            var root = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            _owned.Add(root);
            root.SetActive(false);
            var text = root.AddComponent<TextField>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.leading = 3;
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.sizeDelta = size;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (bitmap)
            {
                var font = ScriptableObject.CreateInstance<BitmapFont>();
                _owned.Add(font);
                font.size = 30;
                font.canTint = true;
                font.texture = Texture2D.whiteTexture;
                var glyphs = new List<BitmapFont.Glyph>();
                var seen = new HashSet<char>();
                foreach (var ch in "WMABCabcde short words…")
                    if (seen.Add(ch)) glyphs.Add(new BitmapFont.Glyph
                    {
                        code = ch, advance = 18, width = ch == ' ' ? 0 : 16, height = 30,
                        lineHeight = 30, uv = new Rect(ch / 256f, 0, 1 / 256f, 1),
                    });
                font.glyphs = glyphs.ToArray();
                text.bitmapFont = font;
            }
            root.SetActive(true);
            return text;
        }

        private static List<UIVertex> Mesh(TextField text)
        {
            using var helper = new VertexHelper();
            typeof(TextField).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(text, new object[] { helper });
            var vertices = new List<UIVertex>();
            for (var index = 0; index < helper.currentVertCount; index++)
            {
                var vertex = new UIVertex();
                helper.PopulateUIVertex(ref vertex, index);
                vertices.Add(vertex);
            }
            return vertices;
        }

        private static void Inside(TextField text, List<UIVertex> vertices)
        {
            Assert.That(vertices, Is.Not.Empty);
            var rect = text.rectTransform.rect;
            foreach (var vertex in vertices)
            {
                Assert.That(vertex.position.x, Is.InRange(rect.xMin - .01f, rect.xMax + .01f));
                Assert.That(vertex.position.y, Is.InRange(rect.yMin - .01f, rect.yMax + .01f));
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (var index = 0; index < _owned.Count; index++) Object.DestroyImmediate(_owned[index]);
            _owned.Clear();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Shrink_fits_without_changing_source_and_restores_after_resize(bool bitmap, bool wrap)
        {
            var text = MakeText(bitmap, wrap, new Vector2(90, 48));
            text.text = "MMMM MMMM MMMM";
            text.overflowMode = TextField.OverflowMode.Shrink;
            Inside(text, Mesh(text));
            Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(90.01f));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(48.01f));
            Assert.That(text.text, Is.EqualTo("MMMM MMMM MMMM"));
            Assert.That(text.fontSize, Is.EqualTo(30));
            text.rectTransform.sizeDelta = new Vector2(600, 100);
            var fitted = Mesh(text);
            text.overflowMode = TextField.OverflowMode.Default;
            var normal = Mesh(text);
            Assert.That(fitted.Count, Is.EqualTo(normal.Count));
            for (var index = 0; index < fitted.Count; index++)
                Assert.That(fitted[index].position, Is.EqualTo(normal[index].position));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Ellipsis_limits_geometry_and_restores_full_text_when_it_fits(bool bitmap, bool wrap)
        {
            var text = MakeText(bitmap, wrap, new Vector2(100, 45));
            text.text = "MMMM MMMM MMMM";
            text.overflowMode = TextField.OverflowMode.Ellipsis;
            var clipped = Mesh(text);
            Inside(text, clipped);
            Assert.That(clipped.Count, Is.LessThan(12 * 4));
            Vector2 uv;
            if (bitmap)
            {
                Assert.That(text.bitmapFont.TryGetGlyph('…', out var glyph), Is.True);
                uv = new Vector2(glyph.uv.xMin, glyph.uv.yMin);
            }
            else
            {
                Assert.That(text.font.GetCharacterInfo('…', out var glyph, 30), Is.True);
                uv = glyph.uvBottomLeft;
            }
            Assert.That((Vector2)clipped[clipped.Count - 4].uv0, Is.EqualTo(uv));
            Assert.That(text.text, Is.EqualTo("MMMM MMMM MMMM"));
            text.rectTransform.sizeDelta = new Vector2(600, 100);
            var visible = Mesh(text);
            text.overflowMode = TextField.OverflowMode.Default;
            var normal = Mesh(text);
            Assert.That(visible.Count, Is.EqualTo(normal.Count));
            for (var index = 0; index < visible.Count; index++)
                Assert.That(visible[index].position, Is.EqualTo(normal[index].position));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Shrink_preserves_rich_text_and_reacts_to_text_and_authored_size_changes(bool bitmap)
        {
            var text = MakeText(bitmap, false, new Vector2(100, 60));
            text.ubb = true;
            text.text = "[size=48][b]MMMM[/b][/size] MM";
            text.overflowMode = TextField.OverflowMode.Shrink;
            Inside(text, Mesh(text));
            Assert.That(text.text, Is.EqualTo("[size=48][b]MMMM[/b][/size] MM"));
            text.text = "MM";
            text.fontSize = 18;
            var actual = Mesh(text);
            text.overflowMode = TextField.OverflowMode.Default;
            var expected = Mesh(text);
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var index = 0; index < actual.Count; index++)
                Assert.That(actual[index].position, Is.EqualTo(expected[index].position));
        }

        [TestCase(TextField.OverflowMode.Shrink)]
        [TestCase(TextField.OverflowMode.Ellipsis)]
        public void Empty_text_and_zero_rectangles_do_not_emit_geometry(TextField.OverflowMode mode)
        {
            var text = MakeText(false, true, new Vector2(100, 50));
            text.overflowMode = mode;
            text.text = "";
            Assert.That(Mesh(text), Is.Empty);
            text.text = "MMMM";
            text.rectTransform.sizeDelta = Vector2.zero;
            Assert.That(Mesh(text), Is.Empty);
            text.rectTransform.sizeDelta = new Vector2(300, 100);
            Inside(text, Mesh(text));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Letter_spacing_only_separates_glyphs_and_empty_text_has_no_size(bool bitmap)
        {
            var text = MakeText(bitmap, false, new Vector2(400, 60));
            text.text = "ab";
            var plain = text.preferredWidth;
            text.letterSpacing = 10;
            text.SetAllDirty();
            Assert.That(text.preferredWidth, Is.EqualTo(plain + 10).Within(1f), "FairyGUI adds letter spacing between glyphs only.");
            text.text = "";
            Assert.That(text.preferredWidth, Is.Zero);
            Assert.That(text.preferredHeight, Is.Zero);
        }

        [Test]
        public void Ubb_tags_are_case_insensitive_and_escaped_brackets_stay_literal()
        {
            var text = MakeText(false, false, new Vector2(400, 60));
            text.ubb = true;
            text.text = @"\[b]x";
            var escaped = text.preferredWidth;
            text.text = "[B]x[/B]";
            var tagged = text.preferredWidth;
            text.text = "x";
            Assert.That(tagged, Is.LessThan(escaped), "A recognised tag must not render as literal text.");
            Assert.That(escaped, Is.GreaterThan(text.preferredWidth), "An escaped bracket renders literally.");
        }

        [Test]
        public void Ellipsis_handles_empty_visible_lines_without_changing_newlines()
        {
            var text = MakeText(false, true, new Vector2(100, 45));
            text.text = "\n\n";
            text.overflowMode = TextField.OverflowMode.Ellipsis;
            Inside(text, Mesh(text));
            Assert.That(text.text, Is.EqualTo("\n\n"));
            text.rectTransform.sizeDelta = new Vector2(100, 200);
            Assert.That(Mesh(text), Is.Empty);
        }

        [TestCase(TextField.OverflowMode.Shrink)]
        [TestCase(TextField.OverflowMode.Ellipsis)]
        public void Inline_images_are_baked_once_and_follow_fitting_and_visibility(TextField.OverflowMode mode)
        {
            var text = MakeText(false, false, new Vector2(90, 50));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            _owned.Add(sprite);
            text.imageSprites = new[] { sprite };
            text.ubb = true;
            text.text = "MMMM[img]image[/img]MMMM";
            text.overflowMode = mode;
            text.RebuildImages();
            Assert.That(text.transform.childCount, Is.EqualTo(1));
            var image = (RectTransform)text.transform.GetChild(0);
            Assert.That(image.gameObject.activeSelf, Is.EqualTo(mode == TextField.OverflowMode.Shrink));
            Inside(text, Mesh(text));
            text.rectTransform.sizeDelta = new Vector2(600, 100);
            Mesh(text);
            Assert.That(image.gameObject.activeSelf, Is.True);
            Assert.That(image.anchoredPosition.x, Is.InRange(0, 599));
            Assert.That(text.transform.childCount, Is.EqualTo(1));
            Assert.That(text.transform.GetChild(0), Is.SameAs(image));
        }

        private enum Page { Small, Large }

#if !FAIRYGUI_TMPRO
        [Test]
        public void Font_size_gears_and_alignment_keep_the_authored_upper_bound()
        {
            var text = MakeText(false, true, new Vector2(100, 60));
            text.text = "MMMM MMMM MMMM";
            text.overflowMode = TextField.OverflowMode.Shrink;
            var gear = new GearFontSize<Page>
            {
                target = text.gameObject, pages = new[] { Page.Small, Page.Large },
                values = new[] { 20, 48 }, defaultValue = 30,
            };
            foreach (TextAnchor anchor in System.Enum.GetValues(typeof(TextAnchor)))
            {
                text.alignment = anchor;
                gear.Apply(Page.Large);
                Inside(text, Mesh(text));
                Assert.That(text.fontSize, Is.EqualTo(48));
                gear.Apply(Page.Small);
                Inside(text, Mesh(text));
                Assert.That(text.fontSize, Is.EqualTo(20));
            }
        }
#endif
    }
}
