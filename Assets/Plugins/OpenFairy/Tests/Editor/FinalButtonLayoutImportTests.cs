#if FAIRYGUI_TMPRO
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class FinalButtonLayoutImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void Saved_button_root_follows_final_state_caption_not_the_earlier_gear_caption()
        {
            var key = "__ButtonLayout_" + Guid.NewGuid().ToString("N");
            var output = "Assets/UIProject/Assets/" + key;
            var sourceDirectory = "UIProject/assets/" + key;
            Directory.CreateDirectory(output);
            AssetDatabase.ImportAsset(output);
            var settingsField = typeof(Migrate).GetField("_codeGenerationSettings", PrivateStatic);
            var previousSettings = settingsField.GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", PrivateStatic).GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var prefabPath = output + "/ContentSizeTestButton.prefab";
            GameObject canvas = null;
            try
            {
                var font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
                Assert.That(font.TryAddCharacters("iW"), Is.True);
                var fontPath = output + "/Font.asset";
                AssetDatabase.CreateAsset(font, fontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures)
                    if (texture != null) AssetDatabase.AddObjectToAsset(texture, font);
                AssetDatabase.SaveAssets();

                Directory.CreateDirectory(sourceDirectory);
                var sourceFile = sourceDirectory + "/ContentSizeTestButton.xml";
                File.WriteAllText(sourceFile, "<component size='200,40' extention='Button'>"
                    + "<controller name='button' pages='0,up,1,down' selected='1'/>"
                    + "<relation target='caption' sidePair='width-width'/>"
                    + "<displayList><text id='caption' name='title' size='180,40' fontSize='20' tmpFont='" + fontPath
                    + "' autoSize='both' text='i'><gearText controller='button' pages='1' values='WWWWWWWWWWWW' default='i'/></text></displayList>"
                    + "<Button mode='Common'/></component>");
                var source = Schema.Component.Load(sourceFile);
                var settings = Activator.CreateInstance(settingsField.FieldType);
                settingsField.FieldType.GetField("packageName").SetValue(settings, "OpenFairy.UGUI");
                settingsField.SetValue(null, settings);
                var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(resourceType);
                resourceType.GetField("Id").SetValue(resource, key);
                resourceType.GetField("File").SetValue(resource, sourceFile);
                resourceType.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                resourceType.GetField("Package").SetValue(resource, "Tests");
                resourceType.GetField("PackageId").SetValue(resource, key);
                resourceType.GetField("_componentXml", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resource, source);
                resources.Add(key, resource);
                using var localization = new ImportLocalizationFixture();
                localization.Prepare(resource);
                localization.AssertGenerated(resource, typeof(ContentSizeTestButton));
                typeof(Migrate).GetMethod("BuildPrefab", PrivateStatic).Invoke(null, new[] { resource });
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var button = prefab.GetComponent<ContentSizeTestButton>();
                Assert.That(button.controller.page, Is.EqualTo(ContentSizeTestButton.button.up));
                Assert.That(button.m_title.text, Is.EqualTo("i"));
                var savedWidth = ((RectTransform)prefab.transform).rect.width;

                // Independent native measurement; neither SDK Relation nor saved fitted dimensions is the oracle.
                canvas = new GameObject("Metrics", typeof(RectTransform), typeof(Canvas));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var title = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
                title.transform.SetParent(canvas.transform, false);
                var text = title.GetComponent<TextMeshProUGUI>();
                EditorUtility.CopySerialized(button.m_title, text);
                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(180, 40);
                var fitter = title.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                text.ForceMeshUpdate(true, true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                var shortWidth = rect.rect.width;
                Assert.That(savedWidth, Is.EqualTo(200 - 180 + shortWidth).Within(.01f));
                text.text = "WWWWWWWWWWWW";
                text.ForceMeshUpdate(true, true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(rect.rect.width, Is.GreaterThan(shortWidth + 20), "The pre-finalization caption must have a meaningfully different width.");
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                settingsField.SetValue(null, previousSettings);
                prefabs.Remove(prefabPath);
                resources.Remove(key);
                AssetDatabase.DeleteAsset(output);
                if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
            }
        }
    }
}
#endif
