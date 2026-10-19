using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public static class AutoClearItemsFixture
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static string _folder;
        public static GameObject Build(bool clear, string overflow = "scroll")
        {
            Assert.That(_folder, Is.Null);
            var key = "__PublishItems_" + Guid.NewGuid().ToString("N");
            _folder = "Assets/UIProject/Assets/" + key;
            Directory.CreateDirectory(_folder);
            AssetDatabase.ImportAsset(_folder);
            var source = Path.GetTempFileName();
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", PrivateStatic).GetValue(null);
            var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var settings = typeof(Migrate).GetField("_settings", PrivateStatic);
            var previousSettings = settings.GetValue(null);
            var item = new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var root = new GameObject("List", typeof(RectTransform));
            var virtualFile = "UIProject/assets/" + key + "/Item.xml";
            var cacheKey = Path.ChangeExtension((string)typeof(Migrate).GetMethod("AssetPath", PrivateStatic).Invoke(null, new object[] { virtualFile }), ".prefab");
            try
            {
                settings.SetValue(null, Activator.CreateInstance(settings.FieldType));
                ((RectTransform)item.transform).sizeDelta = new Vector2(40, 20);
                var template = PrefabUtility.SaveAsPrefabAsset(item, _folder + "/Item.prefab");
                var resource = Activator.CreateInstance(resourceType);
                resourceType.GetField("File").SetValue(resource, virtualFile);
                resourceType.GetField("PackageId").SetValue(resource, key);
                resourceType.GetField("_componentXml", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resource, new Schema.Component());
                resources.Add(key + "item", resource);
                prefabs.Add(cacheKey, template);
                File.WriteAllText(source, "<component size='100,60'><displayList><list id='list' name='Items' size='100,60' defaultItem='item'"
                    + " selectionMode='none' overflow='" + overflow + "' scrollBar='hidden' autoClearItems='" + clear.ToString().ToLowerInvariant()
                    + "'><item title='Preview one'/><item title='Preview two'/></list></displayList></component>");
                var element = Schema.Component.Load(source).DisplayList[0];
                ((RectTransform)root.transform).sizeDelta = element.Size.Value;
                typeof(Migrate).GetMethod("BuildList", PrivateStatic).Invoke(null, new[] { root, element, resource });
                var asset = PrefabUtility.SaveAsPrefabAsset(root, _folder + "/List.prefab", out var success);
                Assert.That(success && asset != null, Is.True);
                AssetDatabase.ImportAsset(_folder + "/List.prefab", ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(_folder + "/List.prefab");
            }
            finally
            {
                resources.Remove(key + "item"); prefabs.Remove(cacheKey);
                settings.SetValue(null, previousSettings);
                Object.DestroyImmediate(root); Object.DestroyImmediate(item); File.Delete(source);
            }
        }

        public static void Delete()
        {
            if (_folder == null) return;
            AssetDatabase.DeleteAsset(_folder); _folder = null;
        }
    }

    public sealed class AutoClearItemsImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        [TestCase(null, 2)]
        [TestCase("false", 2)]
        [TestCase("true", 0)]
        public void Published_projection_clears_only_enabled_preview_items_and_keeps_authoring_bytes(string flag, int expected)
        {
            var file = Path.GetTempFileName();
            var attribute = flag == null ? "" : " autoClearItems='" + flag + "'";
            var body = "<component extention='ComboBox'><ComboBox" + attribute + "><item title='One'/><item title='Two'/></ComboBox><displayList>"
                + "<list id='a' defaultItem='ui://template'" + attribute + "><item/><item/></list>"
                + "<list id='b'><item/></list><component id='c'><ComboBox" + attribute + "><item title='One'/><item title='Two'/></ComboBox></component>"
                + "</displayList></component>";
            try
            {
                File.WriteAllText(file, body); var before = File.ReadAllBytes(file);
                var parsed = Schema.Component.Load(file);
                Assert.That(parsed.DisplayList[0].Items.Length, Is.EqualTo(expected));
                Assert.That(parsed.DisplayList[0].DefaultItem, Is.EqualTo("ui://template"));
                Assert.That(parsed.DisplayList[1].Items.Length, Is.EqualTo(1));
                Assert.That(parsed.ComboBox.Items.Length, Is.EqualTo(expected));
                Assert.That(parsed.DisplayList[2].ComboBox.Items.Length, Is.EqualTo(expected));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
            }
            finally { File.Delete(file); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Reference_collection_uses_the_same_published_projection(bool clear)
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component designImage='ui://design'><displayList><list defaultItem='ui://template' autoClearItems='"
                    + clear.ToString().ToLowerInvariant() + "'><item icon='ui://preview' selectedIcon='ui://selected'/></list>"
                    + "<richtext text='[img]ui://runtime[/img]'/><component><ComboBox autoClearItems='" + clear.ToString().ToLowerInvariant()
                    + "'><item icon='ui://comboPreview'/></ComboBox></component></displayList></component>");
                var matches = (MatchCollection)typeof(Migrate).GetMethod("RuntimeResourceReferences", PrivateStatic).Invoke(null, new object[] { file });
                var values = new List<string>(); foreach (Match match in matches) values.Add(match.Value);
                Assert.That(values, Does.Contain("ui://template").And.Contain("ui://runtime"));
                Assert.That(values.Contains("ui://preview"), Is.EqualTo(!clear));
                Assert.That(values.Contains("ui://selected"), Is.EqualTo(!clear));
                Assert.That(values.Contains("ui://comboPreview"), Is.EqualTo(!clear));
                Assert.That(values, Does.Not.Contain("ui://design"));
            }
            finally { File.Delete(file); }
        }

        [TestCase(false, "scroll")]
        [TestCase(true, "scroll")]
        [TestCase(false, "hidden")]
        [TestCase(true, "hidden")]
        public void Actual_import_save_reload_keeps_default_item_and_only_published_instances(bool clear, string overflow)
        {
            GameObject instance = null;
            try
            {
                var prefab = AutoClearItemsFixture.Build(clear, overflow);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var source = instance.GetComponent<ListSource>();
                Assert.That(source.itemPrefab, Is.Not.Null);
                Assert.That(source.itemSize, Is.EqualTo(new Vector2(40, 20)));
                Assert.That(source.count, Is.EqualTo(clear ? 0 : 2));
                Assert.That(source.content.childCount, Is.EqualTo(clear ? 0 : 2));
                foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); AutoClearItemsFixture.Delete(); }
        }

        [Test]
        public void Localization_plan_omits_preview_nodes_and_preserves_an_explicit_empty_combo_override()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PublishedItems_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var fields = new Dictionary<FieldInfo, object>();
            var data = new Dictionary<IDictionary, Dictionary<object, object>>();
            var key = Guid.NewGuid().ToString("N");
            var all = PrivateStatic | BindingFlags.Public | BindingFlags.Instance;
            var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            try
            {
                foreach (var name in new[] { "_codeGenerationSettings", "_translations" })
                {
                    var field = typeof(Migrate).GetField(name, PrivateStatic); fields.Add(field, field.GetValue(null));
                    field.SetValue(null, field.FieldType.IsArray ? Array.CreateInstance(field.FieldType.GetElementType(), 0) : Activator.CreateInstance(field.FieldType));
                }
                foreach (var name in new[] { "Resources", "LocalViews" })
                {
                    var dictionary = (IDictionary)typeof(Migrate).GetField(name, PrivateStatic).GetValue(null);
                    var saved = new Dictionary<object, object>(); foreach (DictionaryEntry entry in dictionary) saved.Add(entry.Key, entry.Value);
                    data.Add(dictionary, saved); dictionary.Clear();
                }
                var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
                object Resource(string id, string body)
                {
                    var file = Path.Combine(folder, id + ".xml"); File.WriteAllText(file, body);
                    var value = Activator.CreateInstance(resourceType);
                    foreach (var pair in new[] { ("File", file), ("Id", id), ("PackageId", key), ("Package", "Fixture") })
                        resourceType.GetField(pair.Item1).SetValue(value, pair.Item2);
                    resourceType.GetField("Type").SetValue(value, Schema.ResourceKind.Component);
                    resources.Add(key + id, value); return value;
                }
                var item = Resource("Item", "<component extention='Button' size='40,20'><displayList><text id='caption' name='title' size='40,20' text='Default'/></displayList><Button/></component>");
                var choice = Resource("Choice", "<component extention='ComboBox' size='40,20'><displayList><text id='caption' name='title' size='40,20' text='Choice'/></displayList><ComboBox><item title='Inherited'/></ComboBox></component>");
                var owner = Resource("Owner", "<component size='100,80'><displayList>"
                    + "<list id='empty' name='Empty' autoClearItems='true' defaultItem='ui://" + key + "Item'><item title='Preview'/></list>"
                    + "<list id='kept' name='Kept' defaultItem='ui://" + key + "Item'><item title='Published'/></list>"
                    + "<component id='choice' name='Choice' src='Choice' pkg='" + key + "'><ComboBox autoClearItems='true'><item title='Preview choice'/></ComboBox></component>"
                    + "</displayList></component>");
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType)); list.Add(item); list.Add(choice); list.Add(owner);
                typeof(Migrate).GetMethod("BuildLocalizationPlans", PrivateStatic).Invoke(null, new object[] { list });
                var views = (IDictionary)typeof(Migrate).GetField("LocalViews", PrivateStatic).GetValue(null);
                var view = views[owner];
                var nodes = (IList)view.GetType().GetField("nodes", all).GetValue(view);
                Assert.That(nodes.Count, Is.EqualTo(2), "Only the published item and component instance have localization targets.");
                foreach (var node in nodes) Assert.That(node.GetType().GetField("element", all).GetValue(node), Is.Not.EqualTo("empty"));
                var overrides = (IList)view.GetType().GetField("overrides", all).GetValue(view);
                var emptyOverrides = 0;
                foreach (var row in overrides)
                    if (((Array)row.GetType().GetField("choices", all).GetValue(row)).Length == 0)
                    {
                        emptyOverrides++; Assert.That(row.GetType().GetField("initialIndex", all).GetValue(row), Is.EqualTo(-1));
                    }
                Assert.That(emptyOverrides, Is.EqualTo(1));
            }
            finally
            {
                foreach (var pair in fields) pair.Key.SetValue(null, pair.Value);
                foreach (var pair in data) { pair.Key.Clear(); foreach (var row in pair.Value) pair.Key.Add(row.Key, row.Value); }
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Explicit_combo_cleanup_replaces_inherited_preview_arrays_without_runtime_clear_logic()
        {
            var file = Path.GetTempFileName();
            var root = new GameObject("Combo", typeof(RectTransform)); root.SetActive(false);
            try
            {
                var combo = root.AddComponent<OpenFairyTextFixtures.NativeTextFixture.NativeTextCombo>();
                combo.items = new[] { "Inherited" }; combo.values = new[] { "value" };
                File.WriteAllText(file, "<component extention='ComboBox'><ComboBox autoClearItems='true'><item title='Preview' value='preview'/></ComboBox></component>");
                var extension = Schema.Component.Load(file).ComboBox;
                typeof(Migrate).GetMethod("ConfigureComboBox", PrivateStatic).Invoke(null, new object[] { root, extension, "fixture" });
                Assert.That(combo.items, Is.Empty); Assert.That(combo.values, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); File.Delete(file); }
        }
    }
}
