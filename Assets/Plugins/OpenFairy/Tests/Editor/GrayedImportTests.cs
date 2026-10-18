using System;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class GrayedImportTests
    {
        [Test]
        public void Capture_and_grayed_references_roundtrip_without_duplicate_effects_or_material_replacement()
        {
            string directory = "GrayedImport_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + directory;
            AssetDatabase.CreateFolder("Assets", directory);
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var source = new GameObject("Source", typeof(RectTransform), typeof(Loader));
                source.transform.SetParent(root.transform, false);
                var child = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                child.transform.SetParent(source.transform, false);
                var textObject = new GameObject("Text", typeof(RectTransform));
                textObject.transform.SetParent(source.transform, false);
#if FAIRYGUI_TMPRO
                var text = textObject.AddComponent<TextMeshProUGUI>();
                Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
                text.font = TMP_Settings.defaultFontAsset;
                var original = text.fontSharedMaterial;
#else
                var text = textObject.AddComponent<UnityEngine.UI.Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var original = text.material;
#endif
                var material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
                AssetDatabase.CreateAsset(material, folder + "/Presentation.mat");
                var filter = ColorFilterBuilder.Configure(source, true, Shader.Find("OpenFairy/UI ColorMatrix"),
                    Shader.Find("OpenFairy/UI Capture"), material, 31);
                filter.Set(.2f, .3f, -.4f, .1f);
                var effect = GrayedBuilder.Configure(filter, true);
                var presentation = filter.presentation;
                GrayedBuilder.Configure(filter, false);
                Assert.That(filter.enabled, Is.True, "Grayed must not overwrite the authored color-filter state.");
                Assert.That(filter.brightness, Is.EqualTo(.2f));
                Assert.That(filter.contrast, Is.EqualTo(.3f));
                Assert.That(filter.saturation, Is.EqualTo(-.4f));
                Assert.That(filter.hue, Is.EqualTo(.1f));
                GrayedBuilder.Configure(filter, true);
                Assert.That(source.GetComponents<ColorAdjust>().Length, Is.EqualTo(1));
                Assert.That(source.GetComponents<Grayed>().Length, Is.EqualTo(1));
                Assert.That(filter.presentation, Is.SameAs(presentation));
                Assert.That(filter.graphic, Is.Null, "Component loaders and native text use final-subtree capture.");
                Assert.That(child.GetComponent<ObjectFilterSourceVisibility>(), Is.Not.Null);
                Assert.That(text.GetComponent<ObjectFilterSourceVisibility>(), Is.Not.Null);
#if FAIRYGUI_TMPRO
                Assert.That(text.fontSharedMaterial, Is.SameAs(original));
#else
                Assert.That(text.material, Is.SameAs(original));
#endif
                string path = folder + "/Fixture.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var copy = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var copied = copy.GetComponentInChildren<Grayed>(true);
                    Assert.That(copied.filter, Is.Not.Null);
                    Assert.That(copied.filter.grayed, Is.SameAs(copied));
                    Assert.That(copied.filter.presentation, Is.Not.Null);
                    Assert.That(copied.filter.presentationGroup, Is.Not.Null);
                    Assert.That(copied.filter.captureShader, Is.Not.Null);
                    Assert.That(copied.filter.presentationMaterial == AssetDatabase.LoadAssetAtPath<Material>(folder + "/Presentation.mat"), Is.True);
                    Assert.That(AssetDatabase.GetAssetPath(copied.filter.presentationMaterial), Is.EqualTo(folder + "/Presentation.mat"));
                    Assert.That(copied.filter.captureLayer, Is.EqualTo(31));
                    Assert.That(copied.filter.presentation.transform.parent, Is.SameAs(copied.transform.parent));
                }
                finally { PrefabUtility.UnloadPrefabContents(copy); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Disabled_authored_color_filter_and_disabled_grayed_are_baked_without_active_presentation()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            try
            {
                var source = new GameObject("Source", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                source.transform.SetParent(root.transform, false);
                var filter = ColorFilterBuilder.Configure(source, true, Shader.Find("OpenFairy/UI ColorMatrix"),
                    Shader.Find("OpenFairy/UI Capture"), material, 31);
                filter.enabled = false;
                var effect = GrayedBuilder.Configure(filter, false);
                Assert.That(filter.isFiltering, Is.False);
                Assert.That(filter.texture, Is.Null);
                Assert.That(filter.presentation.gameObject.activeSelf, Is.False);
                effect.enabled = true;
                Assert.That(filter.isFiltering, Is.True);
                effect.enabled = false;
                Assert.That(filter.isFiltering, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
