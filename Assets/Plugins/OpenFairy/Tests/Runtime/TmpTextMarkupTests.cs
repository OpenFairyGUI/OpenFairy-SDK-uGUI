#if FAIRYGUI_TMPRO
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.TextCore;

namespace OpenFairy.UGUI.Tests
{
    public class TmpTextMarkupTests
    {
        private readonly List<Object> _owned = new();
        private TextMeshProUGUI _text;
        private TmpTextMarkup _markup;

        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        private int CountSprites()
        {
            int count = 0;
            for (int i = 0; i < _text.textInfo.characterCount; i++)
                if (_text.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite)
                    count++;
            return count;
        }

        private void SetUpText()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "Configure a TMP default font before running layout tests.");
            var font = Own(TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile));
            Assert.That(font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz 0123456789./:&\u2026"), Is.True);
            for (int i = 0; i < font.atlasTextureCount; i++)
                Own(font.atlasTextures[i]);
            Own(font.material);
            var canvas = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Text", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(canvas.transform, false);
            _text = go.AddComponent<TextMeshProUGUI>();
            _text.font = font;
            _text.fontSize = 24;
            _text.richText = true;
            _text.margin = Vector4.zero;
            _text.rectTransform.sizeDelta = new Vector2(500, 160);
            _text.alignment = TextAlignmentOptions.TopLeft;
            go.SetActive(true);
            _markup = go.AddComponent<TmpTextMarkup>();
        }

        private TMP_SpriteAsset SpriteAsset(string name, Color color, float aspect = 1)
        {
            var texture = Own(new Texture2D(8, 8));
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
            asset.name = name;
            JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\"}", asset);
            asset.faceInfo = new FaceInfo { pointSize = 1, scale = 1, ascentLine = 0.8f, descentLine = -0.2f, lineHeight = 1 };
            asset.spriteSheet = texture;
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(aspect, 1, 0, 0.8f, aspect), new GlyphRect(0, 0, 8, 8), 1, 0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = name, scale = 1 });
            asset.UpdateLookupTables();
            asset.material = Own(new Material(Shader.Find("TextMeshPro/Sprite")) { name = name + " Material", mainTexture = texture });
            return asset;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in _owned)
                Object.Destroy(obj);
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpriteDimensionsBaselineAndTextureFallbackUseNativeGeometry()
        {
            SetUpText();
            var first = SpriteAsset("first", Color.red, 2);
            var second = SpriteAsset("second", Color.green);
            first.fallbackSpriteAssets = new List<TMP_SpriteAsset> { second };
            _text.spriteAsset = first;
            _text.text = TmpTextMarkup.SpriteTag(_text, "first", 50, 20, 0.6f)
                + TmpTextMarkup.SpriteTag(_text, "second", 18, 30);
            yield return null;
            _text.ForceMeshUpdate();
            Assert.That(CountSprites(), Is.EqualTo(2));
            var a = _text.textInfo.characterInfo[0];
            var b = _text.textInfo.characterInfo[1];
            Assert.That(a.topRight.x - a.bottomLeft.x, Is.EqualTo(50).Within(0.1f));
            Assert.That(a.topRight.y - a.bottomLeft.y, Is.EqualTo(20).Within(0.1f));
            Assert.That(b.topRight.x - b.bottomLeft.x, Is.EqualTo(18).Within(0.1f));
            Assert.That(b.topRight.y - b.bottomLeft.y, Is.EqualTo(30).Within(0.1f));
            Assert.That(a.topRight.y - b.topRight.y, Is.EqualTo(12 - 24).Within(0.1f));
            Assert.That(a.materialReferenceIndex, Is.Not.EqualTo(b.materialReferenceIndex));
            Assert.That(_text.textInfo.meshInfo[a.materialReferenceIndex].material.mainTexture, Is.SameAs(first.spriteSheet));
            Assert.That(_text.textInfo.meshInfo[b.materialReferenceIndex].material.mainTexture, Is.SameAs(second.spriteSheet));
            _text.text = TmpTextMarkup.SpriteTag("second");
            _text.ForceMeshUpdate();
            Assert.That(CountSprites(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LinkClicksUseCurrentNativeLayoutAndPreserveSource()
        {
            SetUpText();
            _markup.ubb = true;
            const string source = "[url=https://example.invalid/?a=1&b=2]Click[/url] after";
            string clicked = null;
            _text.raycastTarget = false;
            _markup.onClickLink = value => clicked = value;
            _text.text = source;
            yield return null;
            _text.ForceMeshUpdate();
            Assert.That(_text.textInfo.linkCount, Is.EqualTo(1));
            var character = _text.textInfo.characterInfo[0];
            var center = _text.transform.TransformPoint((character.bottomLeft + character.topRight) * 0.5f);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, center);
            var events = Own(new GameObject("Events", typeof(EventSystem))).GetComponent<EventSystem>();
            var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Right };
            _markup.OnPointerClick(data);
            Assert.That(clicked, Is.Null);
            data.button = PointerEventData.InputButton.Left;
            _markup.OnPointerClick(data);
            Assert.That(clicked, Is.EqualTo("https://example.invalid/?a=1&b=2"));
            Assert.That(_text.text, Is.EqualTo(source));
            clicked = null;
            _text.text = "No link";
            _markup.OnPointerClick(data);
            Assert.That(clicked, Is.Null);
            _markup.onClickLink = null;
            Assert.That(_text.raycastTarget, Is.False);
        }

        [UnityTest]
        public IEnumerator HtmlAndUbbImagesRetainColorLinksAndLiteralMarkup()
        {
            SetUpText();
            _text.spriteAsset = SpriteAsset("badge", Color.blue);
            _markup.ubb = true;
            _markup.html = true;
            _text.text = "[color=#80402010]T[/color][img]badge[/img]<img src='badge' width='40' height='20'/>"
                + "<a href='event:hello&amp;world'>link</a>";
            yield return null;
            _text.ForceMeshUpdate();
            Assert.That(CountSprites(), Is.EqualTo(2));
            Assert.That(_text.textInfo.linkCount, Is.EqualTo(1));
            Assert.That(_text.textInfo.characterInfo[0].color, Is.EqualTo(new Color32(0x40, 0x20, 0x10, 0x80)));
            var explicitImage = _text.textInfo.characterInfo[2];
            Assert.That(explicitImage.topRight.x - explicitImage.bottomLeft.x, Is.EqualTo(40).Within(0.1f));
            Assert.That(explicitImage.topRight.y - explicitImage.bottomLeft.y, Is.EqualTo(20).Within(0.1f));
            const string literal = "<noparse>\\[img]badge[/img]<a href='x'>literal</a></noparse>";
            Assert.That(_markup.PreprocessText(literal), Is.EqualTo(literal));
            Assert.That(_markup.PreprocessText("\\[img]badge[/img]"), Is.EqualTo("[img]badge[/img]"));
            Assert.That(_markup.PreprocessText("[url]https://example.invalid[/url]"), Does.Contain("<link=\"https://example.invalid\">"));
            Assert.That(_markup.PreprocessText("<a href=https://example.invalid/path?a=1&b=2>go</a>"),
                Does.Contain("<link=\"https://example.invalid/path?a=1&amp;b=2\">"));
            Assert.That(_markup.PreprocessText("<img src=badge/path />"), Is.EqualTo("<sprite name=\"badge/path\">"));
            _text.richText = false;
            Assert.That(_markup.PreprocessText("[img]badge[/img]"), Is.EqualTo("[img]badge[/img]"));
        }

        [UnityTest]
        public IEnumerator LinkHandlerOnlyOverridesRaycastWhenNeededAndRestoresItOnDisable()
        {
            SetUpText();
            Assert.That(_text.raycastTarget, Is.True);
            _markup.Bind();
            Assert.That(_text.raycastTarget, Is.True);
            _markup.onClickLink = _ => { };
            _markup.onClickLink = null;
            Assert.That(_text.raycastTarget, Is.True);

            _text.raycastTarget = false;
            _markup.onClickLink = _ => { };
            Assert.That(_text.raycastTarget, Is.True);
            _markup.enabled = false;
            Assert.That(_text.raycastTarget, Is.False);
            _markup.enabled = true;
            Assert.That(_text.raycastTarget, Is.True);
            _markup.onClickLink = null;
            Assert.That(_text.raycastTarget, Is.False);
            _markup.enabled = false;
            _markup.onClickLink = _ => { };
            Assert.That(_text.raycastTarget, Is.False);
            _markup.enabled = true;
            Assert.That(_text.raycastTarget, Is.True);
            _markup.onClickLink = null;
            Assert.That(_text.raycastTarget, Is.False);
            yield return null;
        }
    }
}
#endif
