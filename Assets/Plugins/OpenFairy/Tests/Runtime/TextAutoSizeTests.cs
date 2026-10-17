#if FAIRYGUI_TMPRO
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class TextAutoSizeTests
    {
        private enum Page { Small, Large }
        private GameObject _canvas;
        private TMP_FontAsset _font;

        private TextMeshProUGUI MakeText(Vector2 size)
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "Configure a TMP default font before running layout tests.");
            _font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
            Assert.That(_font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz 0123456789.\u2026"), Is.True);
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Text", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_canvas.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.margin = Vector4.zero;
            text.rectTransform.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            go.SetActive(true);
            return text;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_canvas);
            if (_font != null)
            {
                for (int i = 0; i < _font.atlasTextureCount; i++)
                    Object.Destroy(_font.atlasTextures[i]);
                Object.Destroy(_font.material);
                Object.Destroy(_font);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShrinkKeepsRectAndRestoresAuthoredSizeAfterTextChanges()
        {
            var size = new Vector2(120, 48);
            var text = MakeText(size);
            TextAutoSize.ConfigureShrink(text, 36);
            const string source = "A long single line of text";
            text.text = source;
            yield return null;
            text.ForceMeshUpdate();
            Assert.That(text.fontSize, Is.InRange(1, 35.99f));
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(size.x + 0.5f));
            Assert.That(text.text, Is.EqualTo(source));
            Assert.That(text.rectTransform.sizeDelta, Is.EqualTo(size));
            Assert.That(text.GetComponent<ContentSizeFitter>(), Is.Null);
            Assert.That(text.characterWidthAdjustment, Is.Zero);
            Assert.That(text.lineSpacingAdjustment, Is.Zero);

            text.text = "Hi";
            text.ForceMeshUpdate();
            Assert.That(text.fontSize, Is.EqualTo(36).Within(0.05f));
            text.text = "";
            text.ForceMeshUpdate();
            Assert.That(text.rectTransform.sizeDelta, Is.EqualTo(size));
        }

        [UnityTest]
        public IEnumerator ShrinkUsesNativeWrappingAndFontSizeGearChangesItsUpperBound()
        {
            var size = new Vector2(120, 64);
            var text = MakeText(size);
            text.textWrappingMode = TextWrappingModes.Normal;
            TextAutoSize.ConfigureShrink(text, 32);
            text.text = "Several words wrap into multiple lines inside the fixed rectangle";
            yield return null;
            text.ForceMeshUpdate();
            Assert.That(text.textInfo.lineCount, Is.GreaterThan(1));
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(size.x + 0.5f));
            Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(size.y + 0.5f));
            var gear = new GearFontSize<Page>
            {
                target = text.gameObject,
                pages = new[] { Page.Small, Page.Large },
                values = new[] { 20, 48 },
                defaultValue = 30,
            };
            gear.Apply(Page.Large);
            Assert.That(text.fontSizeMax, Is.EqualTo(48));
            text.text = "Hi";
            text.ForceMeshUpdate();
            Assert.That(text.fontSize, Is.EqualTo(48).Within(0.05f));
            gear.Apply(Page.Small);
            text.ForceMeshUpdate();
            Assert.That(text.fontSizeMax, Is.EqualTo(20));
            Assert.That(text.fontSize, Is.EqualTo(20).Within(0.05f));
            Assert.That(text.rectTransform.sizeDelta, Is.EqualTo(size));
        }

        [UnityTest]
        public IEnumerator EllipsisKeepsPointSizeAndSourceWhileNativeLayoutTruncates()
        {
            var text = MakeText(new Vector2(100, 48));
            TextAutoSize.ConfigureEllipsis(text, 24);
            const string source = "This sentence cannot fit on one line";
            text.text = source;
            yield return null;
            text.ForceMeshUpdate();
            Assert.That(text.enableAutoSizing, Is.False);
            Assert.That(text.fontSize, Is.EqualTo(24));
            Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(text.isTextTruncated, Is.True);
            Assert.That(text.text, Is.EqualTo(source));
            bool hasEllipsis = false;
            for (int i = 0; i < text.textInfo.characterCount; i++)
                hasEllipsis |= text.textInfo.characterInfo[i].character == '\u2026';
            Assert.That(hasEllipsis, Is.True);
        }

        [UnityTest]
        public IEnumerator AbsoluteRichSizesParticipateInShrinkingAndPlainTextStaysLiteral()
        {
            var text = MakeText(new Vector2(120, 48));
            TextAutoSize.ConfigureShrink(text, 30);
            text.richText = true;
            var markup = text.gameObject.AddComponent<TmpTextMarkup>();
            markup.ubb = false;
            markup.html = true;
            const string source = "<size=60>Wide text must shrink</size>";
            text.text = source;
            yield return null;
            text.ForceMeshUpdate();
            Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(120.5f));
            Assert.That(text.fontSize, Is.LessThan(30));
            Assert.That(text.textInfo.characterInfo[0].pointSize, Is.EqualTo(text.fontSize * 2).Within(0.1f));
            Assert.That(text.text, Is.EqualTo(source));
            text.richText = false;
            Assert.That(markup.PreprocessText(source), Is.EqualTo(source));
            text.richText = true;
            markup.ubb = true;
            Assert.That(markup.PreprocessText("[size=60]Hi[/size]"), Is.EqualTo("<size=200%>Hi</size>"));
        }

        [Test]
        public void SizeNormalizationPreservesRelativeTagsAndUsesInvariantNumbers()
        {
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                const string source = "<size=30>a</size><size='15px'>b</size><SIZE=\"+15\">c</SIZE><size=-15>d</size><size=125%>e</size><size=1.5em>f</size>";
                const string expected = "<size=100%>a</size><size=50%>b</size><size=150%>c</SIZE><size=50%>d</size><size=125%>e</size><size=1.5em>f</size>";
                Assert.That(TextAutoSize.NormalizeSizeTags(source, 30), Is.EqualTo(expected));
                Assert.That(TextAutoSize.NormalizeSizeTags(expected, 30), Is.EqualTo(expected));
                const string literal = "<noparse><size=30>literal\n<size=60></noparse><size=15>text</size>";
                Assert.That(TextAutoSize.NormalizeSizeTags(literal, 30), Is.EqualTo("<noparse><size=30>literal\n<size=60></noparse><size=50%>text</size>"));
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }
    }
}
#endif
