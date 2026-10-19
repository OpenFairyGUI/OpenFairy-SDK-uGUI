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
        public void Authored_overflow_keeps_its_viewport_and_saved_native_layout(string overflow, string layout, bool autoSize)
        {
            var key = "__ListClip_" + Guid.NewGuid().ToString("N");
            var output = "Assets/UIProject/Assets/" + key;
            Directory.CreateDirectory(output);
            AssetDatabase.ImportAsset(output);
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
                resourceType.GetField("File").SetValue(resource, "UIProject/assets/" + key + "/Item.xml");
                resourceType.GetField("PackageId").SetValue(resource, key);
                resources.Add(key + "item", resource);
                File.WriteAllText(sourceFile, "<component size='100,60'><displayList><list id='list' size='100,60' defaultItem='item'"
                    + (overflow.Length == 0 ? "" : " overflow='" + overflow + "'") + " scrollBar='hidden' selectionMode='none' layout='" + layout
                    + "' autoItemSize='" + autoSize.ToString().ToLowerInvariant() + "' lineGap='7' colGap='5'/></displayList></component>");
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
                Assert.That(saved.count, Is.Zero);
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
                AssetDatabase.DeleteAsset(output);
                File.Delete(sourceFile);
            }
        }
    }
}
