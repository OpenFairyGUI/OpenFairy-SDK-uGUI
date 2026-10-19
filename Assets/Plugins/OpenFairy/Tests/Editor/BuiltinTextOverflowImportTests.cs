using System;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class BuiltinTextOverflowImportTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

        [TestCase(Schema.TextAutoSize.Shrink, false)]
        [TestCase(Schema.TextAutoSize.Shrink, true)]
        [TestCase(Schema.TextAutoSize.Ellipsis, false)]
        [TestCase(Schema.TextAutoSize.Ellipsis, true)]
        [TestCase(Schema.TextAutoSize.None, false)]
        [TestCase(Schema.TextAutoSize.Height, false)]
        public void Import_preserves_fixed_overflow_policy_across_prefab_reload(Schema.TextAutoSize mode, bool rich)
        {
            var folder = "Assets/__BuiltinText_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var common = typeof(Migrate).GetField("_settings", Static);
            var previous = common.GetValue(null);
            var root = new GameObject("Text", typeof(RectTransform));
            root.SetActive(false);
            GameObject instance = null;
            try
            {
                common.SetValue(null, Activator.CreateInstance(common.FieldType));
                var text = root.AddComponent<TextField>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.rectTransform.sizeDelta = new Vector2(100, 50);
                var element = new Schema.Display
                {
                    Kind = rich ? Schema.DisplayKind.RichText : Schema.DisplayKind.Text,
                    Text = rich ? "<b>Several words</b>" : "Several words",
                    FontSize = 30, AutoSize = mode,
                };
                var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var owner = Activator.CreateInstance(resourceType, true);
                resourceType.GetField("PackageId").SetValue(owner, "fitfonts");
                typeof(Migrate).GetMethod("ConfigureText", Static).Invoke(null, new object[] { text, element, owner });
                typeof(Migrate).GetMethod("ConfigureTextLayout", Static).Invoke(null, new object[] { root, element });
                root.SetActive(true);
                var path = folder + "/Text.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True);
                Object.DestroyImmediate(root); root = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var loaded = instance.GetComponent<TextField>();
                var expected = mode switch
                {
                    Schema.TextAutoSize.Shrink => TextField.OverflowMode.Shrink,
                    Schema.TextAutoSize.Ellipsis => TextField.OverflowMode.Ellipsis,
                    _ => TextField.OverflowMode.Default,
                };
                Assert.That(loaded.overflowMode, Is.EqualTo(expected));
                Assert.That(loaded.fontSize, Is.EqualTo(30));
                Assert.That(loaded.text, Is.EqualTo(element.Text));
                Assert.That(loaded.html, Is.EqualTo(rich));
                if (mode is Schema.TextAutoSize.Shrink or Schema.TextAutoSize.Ellipsis)
                {
                    Assert.That(loaded.rectTransform.rect.size, Is.EqualTo(new Vector2(100, 50)));
                    Assert.That(instance.GetComponent<ContentSizeFitter>(), Is.Null);
                }
                if (mode == Schema.TextAutoSize.Ellipsis)
                    Assert.That(loaded.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (instance != null) Object.DestroyImmediate(instance);
                common.SetValue(null, previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Hidden_inline_images_keep_serialized_references_and_reappear_after_resize()
        {
            var folder = "Assets/__BuiltinImage_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Text", typeof(RectTransform));
            root.SetActive(false);
            GameObject instance = null;
            try
            {
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white); texture.Apply();
                AssetDatabase.CreateAsset(texture, folder + "/Image.asset");
                var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AssetDatabase.SaveAssets();
                var text = root.AddComponent<TextField>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 30;
                text.rectTransform.sizeDelta = new Vector2(90, 50);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.overflowMode = TextField.OverflowMode.Ellipsis;
                text.ubb = true;
                text.text = "MMMM[img]image[/img]MMMM";
                text.imageSprites = new[] { sprite };
                text.RebuildImages();
                var path = folder + "/Text.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True);
                Object.DestroyImmediate(root); root = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                text = instance.GetComponent<TextField>();
                var images = new SerializedObject(text).FindProperty("_images");
                Assert.That(images.arraySize, Is.EqualTo(1));
                var image = (RectTransform)images.GetArrayElementAtIndex(0).objectReferenceValue;
                Assert.That(image, Is.Not.Null);
                Assert.That(image.gameObject.activeSelf, Is.False);
                instance.SetActive(true);
                text.rectTransform.sizeDelta = new Vector2(600, 100);
                Assert.That(text.preferredWidth, Is.GreaterThan(90));
                Assert.That(image.gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (instance != null) Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
