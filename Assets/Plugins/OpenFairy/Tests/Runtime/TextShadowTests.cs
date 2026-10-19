#if FAIRYGUI_TMPRO && UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class TextShadowTests
    {
        private readonly List<Object> _owned = new();
        private readonly List<string> _assets = new();
        private Camera _camera;
        private RenderTexture _target;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private Material BakeShadow(TextMeshProUGUI text, Color color)
        {
            var path = "Assets/__TextShadow_" + Guid.NewGuid().ToString("N") + ".mat";
            _assets.Add(path);
            var builder = Type.GetType("OpenFairy.UGUI.Editor.TmpTextMaterialBuilder, OpenFairy.UGUI.Editor", true);
            builder.GetMethod("ConfigureShadow", BindingFlags.Static | BindingFlags.Public).Invoke(null,
                new object[] { text, path, color, new Vector2(2, 2) });
            return text.fontSharedMaterial;
        }
        private TextMeshProUGUI CreateText()
        {
            _camera = Own(new GameObject("Camera", typeof(Camera))).GetComponent<Camera>();
            _camera.orthographic = true; _camera.orthographicSize = 64; _camera.aspect = 2;
            _camera.transform.position = new Vector3(128, -64, -10);
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.white;
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 100; _camera.cullingMask = 1 << 30;
            var root = Own(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
            root.layer = 30;
            var rect = (RectTransform)root.transform;
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(256, 128);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = _camera;
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = 30; go.transform.SetParent(rect, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.sizeDelta = new Vector2(180, 70); text.rectTransform.anchoredPosition = new Vector2(30, -25);
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = 50; text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft; text.extraPadding = true;
            var material = Own(new Material(text.fontSharedMaterial));
            material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER");
            material.SetFloat("_OutlineWidth", 0); material.SetColor("_FaceColor", Color.white);
            text.fontSharedMaterial = material; text.text = "MM";
            // 线性目标：读回值与混合结果成正比，不受工程色彩空间的 sRGB 编码影响。
            _target = Own(new RenderTexture(256, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 1 }); _target.Create();
            return text;
        }
        private float Darkness(TextMeshProUGUI text, string imageName)
        {
            text.UpdateMeshPadding(); text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
            _camera.targetTexture = _target;
            if (GraphicsSettings.currentRenderPipeline == null) _camera.Render();
            else RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
            _camera.targetTexture = null;
            var previous = RenderTexture.active; RenderTexture.active = _target;
            var image = Own(new Texture2D(256, 128, TextureFormat.RGBA32, false));
            image.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OpenFairyTextShadow");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, imageName + ".png"), image.EncodeToPNG());
            var count = 0f;
            foreach (var pixel in image.GetPixels32()) count += (255 - pixel.r) / 255f;
            return count;
        }
        [TearDown]
        public void TearDown()
        {
            for (var i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] is RenderTexture rt) rt.Release();
                Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            foreach (var path in _assets) AssetDatabase.DeleteAsset(path);
            _assets.Clear();
        }
        [UnityTest]
        public IEnumerator Authored_shadow_produces_visible_pixels_on_white_text_and_background()
        {
            var text = CreateText();
            yield return null;
            var before = Darkness(text, "WithoutShadow");
            BakeShadow(text, Color.black);
            yield return null;
            var after = Darkness(text, "AuthoredShadow");
            Debug.Log("SHADOW_PIXEL before=" + before + " after=" + after);
            Assert.That(before, Is.Zero);
            Assert.That(after, Is.GreaterThan(20), "The authored shadow must render independently of white glyphs.");
        }
        [UnityTest]
        public IEnumerator Native_SDF_underlay_is_a_visible_positive_control()
        {
            var text = CreateText();
            var material = text.fontSharedMaterial;
            Assert.That(material.HasProperty("_UnderlayOffsetX"), Is.True);
            material.SetColor("_UnderlayColor", Color.black);
            material.SetFloat("_UnderlayOffsetX", .5f); material.SetFloat("_UnderlayOffsetY", -.5f);
            material.EnableKeyword("UNDERLAY_ON");
            yield return null;
            var count = Darkness(text, "NativeUnderlay");
            Debug.Log("NATIVE_UNDERLAY_PIXEL count=" + count);
            Assert.That(count, Is.GreaterThan(20));
        }

        [UnityTest]
        public IEnumerator Native_shadow_obeys_text_and_parent_alpha_and_disable_without_extra_materials()
        {
            var text = CreateText();
            var preset = BakeShadow(text, Color.black);
            var original = text.font.material;
            var crc = original.ComputeCRC();
            yield return null;
            var full = Darkness(text, "ShadowFullAlpha");
            var parent = text.transform.parent.gameObject.AddComponent<CanvasGroup>(); parent.alpha = .5f;
            yield return null;
            var parentHalf = Darkness(text, "ShadowParentHalf");
            text.color = new Color(1, 1, 1, .5f);
            yield return null;
            var quarter = Darkness(text, "ShadowQuarter");
            Assert.That(parentHalf / full, Is.EqualTo(.5f).Within(.03f));
            Assert.That(quarter / full, Is.EqualTo(.25f).Within(.03f));
            text.gameObject.SetActive(false);
            yield return null;
            Assert.That(Darkness(text, "ShadowDisabled"), Is.LessThan(.01f));
            text.gameObject.SetActive(true); parent.alpha = 1; text.color = Color.white;
            yield return null;
            Assert.That(Darkness(text, "ShadowReenabled"), Is.EqualTo(full).Within(.1f));
            Assert.That(text.fontSharedMaterial, Is.SameAs(preset));
            Assert.That(original.ComputeCRC(), Is.EqualTo(crc));
        }

        [UnityTest]
        public IEnumerator Native_fallback_material_keeps_shadow_atlas_and_owned_cleanup()
        {
            var text = CreateText();
            TMP_FontAsset Font(string glyphs)
            {
                var font = Own(TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile));
                font.TryAddCharacters(glyphs);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                Own(font.material);
                foreach (var atlas in font.atlasTextures) Own(atlas);
                return font;
            }
            var main = Font("M"); var fallback = Font("W");
            main.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            text.font = main; text.fontSharedMaterial = main.material;
            var mainCrc = main.material.ComputeCRC(); var fallbackCrc = fallback.material.ComputeCRC();
            var preset = BakeShadow(text, Color.black);
            text.text = "MW";
            yield return null;
            Assert.That(Darkness(text, "ShadowFallback"), Is.GreaterThan(20));
            Assert.That(text.textInfo.characterInfo[1].fontAsset, Is.SameAs(fallback));
            var submesh = text.GetComponentInChildren<TMP_SubMeshUI>();
            Assert.That(submesh, Is.Not.Null);
            Assert.That(submesh.sharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
            Assert.That(submesh.sharedMaterial.mainTexture, Is.SameAs(fallback.material.mainTexture));
            var fallbackMaterial = submesh.fallbackMaterial;
            Assert.That(main.material.ComputeCRC(), Is.EqualTo(mainCrc));
            Assert.That(fallback.material.ComputeCRC(), Is.EqualTo(fallbackCrc));
            Object.DestroyImmediate(text.transform.parent.gameObject);
            yield return null;
            yield return null;
            Assert.That(fallbackMaterial == null, Is.True, "TMP releases its fallback preset after the owning text is destroyed.");
            var path = AssetDatabase.GetAssetPath(preset);
            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            _assets.Remove(path);
            Assert.That(preset == null, Is.True, "The preset is an asset, not an unowned runtime clone.");
        }

        [UnityTest]
        public IEnumerator Captured_SDF_material_keeps_native_underlay_and_alpha()
        {
            var text = CreateText();
            BakeShadow(text, Color.black);
            yield return null;
            var direct = Darkness(text, "ShadowBeforeCapture");
            var root = (RectTransform)text.transform.parent;
            using var capture = new UiObjectCapture(root, Shader.Find("OpenFairy/UI Capture"), 31);
            capture.Capture(_target, (camera, destination) =>
            {
                if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = destination });
            }, localBounds: root.rect);
            var previous = RenderTexture.active; RenderTexture.active = _target;
            var image = Own(new Texture2D(256, 128, TextureFormat.RGBA32, false));
            image.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
            var captured = 0f;
            foreach (var pixel in image.GetPixels32()) captured += (pixel.a - pixel.r) / 255f;
            Assert.That(captured, Is.GreaterThan(20));
            Assert.That(captured, Is.EqualTo(direct).Within(2), "PMA capture composited on white matches native SDF underlay.");
        }
    }
}
#endif
