using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests.Editor
{
    public sealed class HiddenListImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase("hidden", "flow_hz", false)]
        [TestCase("hidden", "row", true)]
        [TestCase("hidden", "row", false)]
        [TestCase("", "flow_hz", true)]
        [TestCase("scroll", "flow_hz", true)]
        [TestCase("scroll", "row", false)]
        [TestCase("scroll", "flow_hz", false, 7)]
        public void Authored_overflow_keeps_its_viewport_and_saved_native_layout(string overflow, string layout, bool autoSize, int itemCount = 0)
        {
            var key = "__ListClip_" + Guid.NewGuid().ToString("N");
            var source = "Temp/" + key;
            var outputRoot = "Assets/" + key;
            var output = outputRoot + "/Assets/Views";
            var settings = MigrationSettings.instance;
            var previousSource = settings.sourceDirectory;
            var previousOutput = settings.outputDirectory;
            var previousDependencies = settings.dependencyDirectories;
            settings.sourceDirectory = source;
            settings.outputDirectory = outputRoot;
            settings.dependencyDirectories = Array.Empty<string>();
            Directory.CreateDirectory(source + "/assets/Views");
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh();
            var sourceFile = Path.GetTempFileName();
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var settingsField = typeof(Migrate).GetField("_settings", PrivateStatic);
            var previousSettings = settingsField.GetValue(null);
            var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            GameObject item = null, root = null, clone = null;
            try
            {
                settingsField.SetValue(null, Activator.CreateInstance(settingsField.FieldType));
                item = new GameObject("Item", typeof(RectTransform));
                ((RectTransform)item.transform).sizeDelta = new Vector2(40, 20);
                var template = PrefabUtility.SaveAsPrefabAsset(item, output + "/Item.prefab");
                var resource = Activator.CreateInstance(resourceType);
                resourceType.GetField("File").SetValue(resource, source + "/assets/Views/Item.xml");
                resourceType.GetField("PackageId").SetValue(resource, key);
                File.WriteAllText(source + "/assets/Views/Item.xml", "<component size='40,20'><displayList/></component>");
                resources.Add(key + "item", resource);
                var items = "";
                for (var i = 0; i < itemCount; i++) items += "<item/>";
                File.WriteAllText(sourceFile, "<component size='100,60'><displayList><list id='list' size='100,60' defaultItem='item'"
                    + (overflow.Length == 0 ? "" : " overflow='" + overflow + "'") + " scrollBar='hidden' selectionMode='none' layout='" + layout
                    + "' autoItemSize='" + autoSize.ToString().ToLowerInvariant() + "' lineGap='7' colGap='5'>" + items + "</list></displayList></component>");
                var element = Schema.Component.Load(sourceFile).DisplayList[0];
                root = new GameObject("List", typeof(RectTransform));
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(100, 60);
                typeof(Migrate).GetMethod("BuildList", PrivateStatic).Invoke(null, new[] { root, element, resource });
                var path = output + "/List.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var saved = clone.GetComponent<ListSource>();
                var savedRect = (RectTransform)clone.transform;
                Assert.That(savedRect.rect.size, Is.EqualTo(new Vector2(100, 60)));
                Assert.That(saved.itemPrefab, Is.EqualTo(template));
                Assert.That(saved.itemSize, Is.EqualTo(new Vector2(40, 20)));
                Assert.That(saved.autoItemSize, Is.EqualTo(autoSize));
                Assert.That(saved.lineGap, Is.EqualTo(7));
                Assert.That(saved.colGap, Is.EqualTo(5));
                Assert.That(saved.count, Is.EqualTo(itemCount));
                Assert.That(saved.isVirtual, Is.False);
                Assert.That(clone.GetComponent<RectMask2D>() != null, Is.EqualTo(overflow == "hidden"));
                Assert.That(clone.GetComponentsInChildren<RectMask2D>(true).Length, Is.EqualTo(overflow.Length == 0 ? 0 : 1));
                if (overflow == "scroll")
                {
                    Assert.That(saved.scrollPane, Is.Not.Null);
                    Assert.That(saved.scrollPane.viewport.GetComponent<RectMask2D>().enabled, Is.True);
                    Assert.That(saved.content, Is.SameAs(saved.scrollPane.content));
                    Assert.That(saved.content.parent, Is.SameAs(saved.scrollPane.viewport));
                    Assert.That(saved.content, Is.Not.SameAs(savedRect));
                    if (itemCount > 0)
                    {
                        Assert.That(saved.scrollPane.contentSize, Is.EqualTo(new Vector2(85, 101)));
                        Assert.That(saved.scrollPane.maxScroll, Is.EqualTo(new Vector2(0, 41)));
                        // Refresh after reload must keep the baked logical size, including deliberate trailing space.
                        UnityEngine.Object.DestroyImmediate(saved.content.GetChild(saved.content.childCount - 1).gameObject);
                        saved.scrollPane.RefreshContent();
                        Assert.That(saved.scrollPane.contentSize, Is.EqualTo(new Vector2(85, 101)));
                        saved.scrollPane.SetPosition(new Vector2(0, 41));
                        Assert.That(saved.scrollPane.position.y, Is.EqualTo(41));
                    }
                }
                else
                {
                    Assert.That(saved.scrollPane, Is.Null);
                    Assert.That(saved.content, Is.SameAs(savedRect));
                }
                Assert.That(saved.rowLayout != null, Is.EqualTo(layout == "row"));
                if (layout == "row")
                {
                    Assert.That(saved.rowLayout.transform, Is.SameAs(saved.content));
                    Assert.That(saved.rowLayout.childControlWidth || saved.rowLayout.childControlHeight, Is.False);
                    Assert.That(saved.rowSize.horizontalFit, Is.EqualTo(overflow == "scroll"
                        ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained));
                }
            }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
                resources.Remove(key + "item");
                settingsField.SetValue(null, previousSettings);
                settings.sourceDirectory = previousSource;
                settings.outputDirectory = previousOutput;
                settings.dependencyDirectories = previousDependencies;
                AssetDatabase.DeleteAsset(outputRoot);
                Directory.Delete(source, true);
                File.Delete(sourceFile);
            }
        }
    }
}
