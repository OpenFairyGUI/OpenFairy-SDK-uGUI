using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class ImportEdgeCaseTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        private const string Source = "Temp/OpenFairyEdgeSource";
        private const string Output = "Assets/__OpenFairyEdgeImport";
        private readonly Dictionary<string, object> _settings = new();
        private readonly Dictionary<string, Dictionary<object, object>> _tables = new();
        private string _source;
        private string _output;
        private string[] _dependencies;

        private static object Invoke(string name, params object[] arguments) =>
            typeof(Migrate).GetMethod(name, Static).Invoke(null, arguments);

        [SetUp]
        public void Setup()
        {
            var settings = MigrationSettings.instance;
            _source = settings.sourceDirectory;
            _output = settings.outputDirectory;
            _dependencies = settings.dependencyDirectories;
            settings.sourceDirectory = Source;
            settings.outputDirectory = Output;
            settings.dependencyDirectories = Array.Empty<string>();
            foreach (var name in new[] { "_settings", "_codeGenerationSettings" })
            {
                var field = typeof(Migrate).GetField(name, Static);
                _settings.Add(name, field.GetValue(null));
                field.SetValue(null, null);
            }
            foreach (var name in new[] { "Resources", "Prefabs" })
            {
                var table = (IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null);
                var copy = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in table) copy.Add(entry.Key, entry.Value);
                _tables.Add(name, copy);
                table.Clear();
            }
            Directory.CreateDirectory(Source + "/assets/BindingFixtures");
            Directory.CreateDirectory(Source + "/settings");
            Directory.CreateDirectory(Output + "/Assets/BindingFixtures");
            File.WriteAllText(Source + "/settings/Common.json", "{}");
            File.WriteAllText(Source + "/settings/Publish.json", "{\"codeGeneration\":{\"memberNamePrefix\":\"m_\"}}");
            File.WriteAllText(Source + "/assets/BindingFixtures/package.xml", "<packageDescription id='edgesrc1'><resources>"
                + "<component id='default' name='Default.xml' path='/'/>"
                + "<component id='variant' name='Variant.xml' path='/'/>"
                + "<component id='collision' name='CollisionBindingFixture.xml' path='/'/>"
                + "</resources></packageDescription>");
            File.WriteAllText(Source + "/assets/BindingFixtures/Default.xml", "<component size='40,20'><displayList/></component>");
            File.WriteAllText(Source + "/assets/BindingFixtures/Variant.xml", "<component size='60,30'><displayList/></component>");
            Invoke("IndexSources");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var entry in _settings) typeof(Migrate).GetField(entry.Key, Static).SetValue(null, entry.Value);
            foreach (var entry in _tables)
            {
                var table = (IDictionary)typeof(Migrate).GetField(entry.Key, Static).GetValue(null);
                table.Clear();
                foreach (var item in entry.Value) table.Add(item.Key, item.Value);
            }
            _settings.Clear();
            _tables.Clear();
            var settings = MigrationSettings.instance;
            settings.sourceDirectory = _source;
            settings.outputDirectory = _output;
            settings.dependencyDirectories = _dependencies;
            AssetDatabase.DeleteAsset(Output);
            Directory.Delete(Source, true);
        }

        private static object Resource(string id) =>
            ((IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null))["edgesrc1" + id];

        private static GameObject SaveReload(GameObject root)
        {
            var path = Output + "/RoundTrip.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
            Assert.That(saved, Is.True);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        private static void Template(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.AddComponent<OpenFairy.UGUI.Component>();
            ((RectTransform)root.transform).sizeDelta = size;
            ((RectTransform)root.transform).anchorMin = ((RectTransform)root.transform).anchorMax = ((RectTransform)root.transform).pivot = new Vector2(0, 1);
            root.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = size.x;
            PrefabUtility.SaveAsPrefabAsset(root, Output + "/Assets/BindingFixtures/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Empty_list_without_a_default_template_survives_prefab_reload()
        {
            var root = new GameObject("Empty", typeof(RectTransform));
            GameObject clone = null;
            try
            {
                Invoke("BuildList", root, new Schema.Display { Size = new Vector2(100, 60) }, Resource("default"));
                clone = SaveReload(root);
                var source = clone.GetComponent<ListSource>();
                Assert.That(source.count, Is.Zero);
                Assert.That(source.itemPrefab, Is.Null);
                source.Fill(0, (_, _) => Assert.Fail("An empty list must not create an item."));
                Assert.That(source.content.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (clone != null) Object.DestroyImmediate(clone);
            }
        }

        [TestCase(Schema.ListLayout.SingleColumn)]
        [TestCase(Schema.ListLayout.SingleRow)]
        [TestCase(Schema.ListLayout.FlowHorizontal)]
        [TestCase(Schema.ListLayout.FlowVertical)]
        public void Authored_item_templates_keep_their_native_sizes_and_saved_variant_references(Schema.ListLayout layout)
        {
            Template("Default", new Vector2(40, 20));
            Template("Variant", new Vector2(60, 30));
            var root = new GameObject("Mixed", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(100, 100);
            GameObject clone = null;
            try
            {
                Invoke("BuildList", root, new Schema.Display
                {
                    Size = new Vector2(100, 100), DefaultItem = "default", Layout = layout, AutoItemSize = false,
                    Items = new[] { new Schema.Extension(), new Schema.Extension { Url = "variant" }, new Schema.Extension() },
                }, Resource("default"));
                clone = SaveReload(root);
                var source = clone.GetComponent<ListSource>();
                Assert.That(source.count, Is.EqualTo(3));
                Assert.That(((RectTransform)source.content.GetChild(0)).rect.size, Is.EqualTo(new Vector2(40, 20)));
                Assert.That(((RectTransform)source.content.GetChild(1)).rect.size, Is.EqualTo(new Vector2(60, 30)));
                Assert.That(((RectTransform)source.content.GetChild(2)).rect.size, Is.EqualTo(new Vector2(40, 20)));
                Assert.That(source.variantItems, Has.Length.EqualTo(1));
                Assert.That(source.variantItems[0], Is.SameAs(source.content.GetChild(1).gameObject));
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(source.content);
                var third = (RectTransform)source.content.GetChild(2);
                Assert.That(third.anchoredPosition, Is.EqualTo(layout switch
                {
                    Schema.ListLayout.SingleRow => new Vector2(100, 0),
                    Schema.ListLayout.FlowHorizontal => new Vector2(0, -30),
                    _ => new Vector2(0, -50),
                }));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (clone != null) Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void Per_item_templates_are_collected_alongside_the_default_template()
        {
            File.WriteAllText(Source + "/assets/BindingFixtures/CollisionBindingFixture.xml",
                "<component size='100,100'><displayList><list id='items' name='Items' size='100,100' defaultItem='default'>"
                + "<item url='variant'/></list></displayList></component>");
            var resource = Resource("collision");
            var components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resource.GetType()));
            var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(resource.GetType()));
            Invoke("Collect", resource, components, images);
            Assert.That(components, Has.Count.EqualTo(3));
            Assert.That(components.Contains(Resource("default")), Is.True);
            Assert.That(components.Contains(Resource("variant")), Is.True);
        }

        [Test]
        public void Caption_overrides_on_components_without_a_title_have_no_localization_effect()
        {
            File.WriteAllText(Source + "/assets/BindingFixtures/CollisionBindingFixture.xml",
                "<component size='100,100'><displayList><component id='child' name='Child' src='variant'>"
                + "<Button title='Ignored'/></component></displayList></component>");
            var resource = Resource("collision");
            using var localization = new ImportLocalizationFixture();
            localization.Prepare(resource, Resource("variant"));
            var code = (string)Invoke("ScriptCode", resource);
            Assert.That(code, Does.Not.Contain("Override0"));
        }

        [Test]
        public void Controller_and_child_name_collisions_keep_distinct_saved_bindings()
        {
            File.WriteAllText(Source + "/assets/BindingFixtures/CollisionBindingFixture.xml",
                "<component size='100,100'><controller name='Status' pages='0,Idle,1,Active'/><displayList>"
                + "<graph id='first' name='Status' size='20,20'/><graph id='duplicate' name='Status' size='30,30'/>"
                + "<graph id='suffix' name='Status_2' size='40,40'/></displayList></component>");
            var resource = Resource("collision");
            using var localization = new ImportLocalizationFixture();
            localization.Prepare(resource);
            var code = (string)Invoke("ScriptCode", resource);
            Assert.That(code, Does.Contain("public UnityEngine.RectTransform m_Status_2;"));
            Assert.That(code, Does.Contain("public UnityEngine.RectTransform m_Status_2_2;"));
            Invoke("BuildPrefab", resource);
            var path = Output + "/Assets/BindingFixtures/CollisionBindingFixture.prefab";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var binding = clone.GetComponent<BindingFixtures.CollisionBindingFixture>();
                Assert.That(binding.m_Status_2.rect.width, Is.EqualTo(20));
                Assert.That(binding.m_Status_2_2.rect.width, Is.EqualTo(40));
                binding.m_Status.page = BindingFixtures.CollisionBindingFixture.Status.Active;
                Assert.That(binding.m_Status.page, Is.EqualTo(BindingFixtures.CollisionBindingFixture.Status.Active));
            }
            finally { Object.DestroyImmediate(clone); }
        }
    }
}
