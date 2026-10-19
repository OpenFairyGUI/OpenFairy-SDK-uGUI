using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class IdentifierFixture : IDisposable
    {
        public const string Source = "Temp/OpenFairyIdentifierSource";
        public const string Output = "Assets/__OpenFairyIdentifiers";
        public const string ExportRoot = "Artifacts/OpenFairyIdentifierExport";
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly string _source = MigrationSettings.instance.sourceDirectory;
        private readonly string _output = MigrationSettings.instance.outputDirectory;
        private readonly string[] _dependencies = MigrationSettings.instance.dependencyDirectories;
        private readonly Dictionary<string, object> _settings = new();
        private readonly Dictionary<string, Dictionary<object, object>> _tables = new();
        public readonly IList components;
        public static object Call(string name, params object[] arguments) => typeof(Migrate).GetMethod(name, Static).Invoke(null, arguments);
        private static object Value(object target, string field) => target.GetType().GetField(field).GetValue(target);
        public static string RawName(object resource) => Path.GetFileNameWithoutExtension((string)Value(resource, "File"));

        public IdentifierFixture()
        {
            var settings = MigrationSettings.instance;
            settings.sourceDirectory = Source; settings.outputDirectory = Output; settings.dependencyDirectories = Array.Empty<string>();
            foreach (var name in new[] { "_settings", "_codeGenerationSettings", "_translations", "_localization" })
            {
                var field = typeof(Migrate).GetField(name, Static); _settings[name] = field.GetValue(null); field.SetValue(null, null);
            }
            foreach (var name in new[] { "Resources", "Prefabs", "LocalViews" })
            {
                var table = (IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null);
                var saved = new Dictionary<object, object>(); foreach (DictionaryEntry item in table) saved.Add(item.Key, item.Value);
                _tables.Add(name, saved); table.Clear();
            }
            Directory.CreateDirectory(Source + "/settings");
            File.WriteAllText(Source + "/settings/Publish.json", "{\"codeGeneration\":{\"memberNamePrefix\":\"\",\"packageName\":\"Example.class\"}}");
            File.WriteAllText(Source + "/settings/Common.json", "{}");
            WritePackage("Name Fixtures", "names001", new[] { ("a", "my-page"), ("b", "my page"), ("c", "System"), ("root", "12 panel!") });
            WritePackage("Name-Fixtures", "names002", new[] { ("leaf", "my-page") });
            foreach (var file in Directory.GetFiles(Source + "/assets", "*.xml", SearchOption.AllDirectories))
                if (Path.GetFileName(file) != "package.xml") File.WriteAllText(file, "<component size='80,30'><displayList/></component>");
            var root = new XElement("component", new XAttribute("size", "800,600"));
            foreach (var name in new[] { "class", "_class", "x y", "x-y", "_12_panel_", "UnityEngine", "状态", "" })
                root.Add(new XElement("controller", new XAttribute("name", name), new XAttribute("pages", "0,default,1,_classPages,2,value__,3,状态,4,é,5,9 first")));
            var list = new XElement("displayList"); root.Add(list);
            var index = 0;
            foreach (var name in new[] { "class", "_class", "foo bar", "foo-bar", "", "name", "m_Name", "m_Name_", "GetControllerPage", "t__class", "_12_panel_", "UnityEngine", "同名", "同名" })
                list.Add(new XElement("graph", new XAttribute("id", "g" + index++), new XAttribute("name", name), new XAttribute("size", "10,20")));
            list.Add(new XElement("component", new XAttribute("id", "local"), new XAttribute("name", "my-page"), new XAttribute("src", "a")));
            list.Add(new XElement("component", new XAttribute("id", "remote"), new XAttribute("name", "my page"), new XAttribute("src", "leaf"), new XAttribute("pkg", "names002")));
            list.Add(new XElement("text", new XAttribute("id", "body"), new XAttribute("name", "class"), new XAttribute("size", "100,30"),
                new XAttribute("text", "Hello {class=One}/{_class=Two}/{value__=Three}"),
                new XElement("gearText", new XAttribute("controller", "x-y"), new XAttribute("pages", "0,1"), new XAttribute("values", "First|Second"))));
            foreach (var name in new[] { "class", "_class", "" })
            {
                var transition = new XElement("transition", new XAttribute("name", name));
                foreach (var label in new[] { "x y", "x-y", "value__", "_classHook", "_classHook_2", "状态" })
                    transition.Add(new XElement("item", new XAttribute("type", "Alpha"), new XAttribute("time", "0"), new XAttribute("target", "g0"), new XAttribute("value", "1"), new XAttribute("label", label)));
                root.Add(transition);
            }
            root.Save(Source + "/assets/Name Fixtures/12 panel!.xml");
            Call("IndexSources");
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
            var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(type));
            foreach (DictionaryEntry row in resources) Call("Collect", row.Value, components, images);
            var localization = Activator.CreateInstance(typeof(Migrate).GetNestedType("LocalizationSettings", BindingFlags.NonPublic));
            typeof(Migrate).GetField("_localization", Static).SetValue(null, localization);
            typeof(Migrate).GetField("_translations", Static).SetValue(null, Array.Empty<FairyTranslations>());
            Call("BuildLocalizationPlans", components);
        }

        private static void WritePackage(string name, string id, (string Id, string Name)[] items)
        {
            var folder = Source + "/assets/" + name; Directory.CreateDirectory(folder);
            var resources = new XElement("resources");
            foreach (var item in items) resources.Add(new XElement("component", new XAttribute("id", item.Id), new XAttribute("name", item.Name + ".xml"), new XAttribute("path", "/")));
            new XElement("packageDescription", new XAttribute("id", id), resources).Save(folder + "/package.xml");
            foreach (var item in items) File.WriteAllText(folder + "/" + item.Name + ".xml", "<component size='80,30'><displayList/></component>");
        }

        public static void Export()
        {
            if (Directory.Exists(ExportRoot)) Directory.Delete(ExportRoot, true);
            using var fixture = new IdentifierFixture();
            foreach (var resource in fixture.components)
            {
                var ns = (string)Call("GeneratedNamespace", resource); var name = (string)Call("ClassName", resource);
                var folder = ExportRoot + "/" + ns; Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/" + name + ".cs", (string)Call("ScriptCode", resource));
            }
            MigrationSettings.instance.outputDirectory = ExportRoot;
            Call("GenerateLocaleScripts");
            Debug.Log("Identifier fixture source export completed.");
        }

        public GameObject Bake()
        {
            foreach (var resource in components)
            {
                var generated = (string)Call("ScriptCode", resource);
                var name = (string)Call("ClassName", resource);
                var scripts = AssetDatabase.FindAssets(name + " t:MonoScript");
                string frozen = null;
                foreach (var scriptGuid in scripts)
                {
                    var path = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    if (path.Contains("/IdentifierGenerated/") && Path.GetFileNameWithoutExtension(path) == name) frozen = File.ReadAllText(path);
                }
                Assert.That(frozen, Is.Not.Null, name);
                Assert.That(generated.Replace("\r\n", "\n"), Is.EqualTo(frozen.Replace("\r\n", "\n").Replace("protected internal override", "protected override")));
                Call("BuildPrefab", resource);
            }
            AssetDatabase.SaveAssets();
            var rootPath = Output + "/Assets/Name Fixtures/12 panel!.prefab";
            AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(rootPath);
        }

        public void Dispose()
        {
            foreach (var row in _settings) typeof(Migrate).GetField(row.Key, Static).SetValue(null, row.Value);
            foreach (var row in _tables)
            {
                var table = (IDictionary)typeof(Migrate).GetField(row.Key, Static).GetValue(null); table.Clear();
                foreach (var item in row.Value) table.Add(item.Key, item.Value);
            }
            MigrationSettings.instance.sourceDirectory = _source; MigrationSettings.instance.outputDirectory = _output; MigrationSettings.instance.dependencyDirectories = _dependencies;
            AssetDatabase.DeleteAsset(Output);
            if (Directory.Exists(Source)) Directory.Delete(Source, true);
        }
    }

    public sealed class IdentifierImportTests
    {
        [Test]
        public void Obsolete_enum_cleanup_preserves_a_script_owned_by_a_current_component()
        {
            using var fixture = new IdentifierFixture();
            var scripts = AssetDatabase.FindAssets("my_page_a t:MonoScript");
            string output = null;
            foreach (var scriptGuid in scripts)
            {
                var file = AssetDatabase.GUIDToAssetPath(scriptGuid);
                if (file.Contains("/IdentifierGenerated/Scripts/Name Fixtures/"))
                    output = file.Substring(0, file.IndexOf("/Scripts/Name Fixtures/", StringComparison.Ordinal));
            }
            Assert.That(output, Is.Not.Null);
            MigrationSettings.instance.outputDirectory = output;
            var path = (string)IdentifierFixture.Call("ScriptPath", fixture.components[0]);
            var bytes = File.ReadAllBytes(path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            IdentifierFixture.Call("DeleteScript", path);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(guid, Is.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Directory.CreateDirectory(IdentifierFixture.Output);
            var obsolete = IdentifierFixture.Output + "/ObsoleteControllerEnum.txt";
            File.WriteAllText(obsolete, "// Obsolete standalone controller enum.\n");
            AssetDatabase.ImportAsset(obsolete, ImportAssetOptions.ForceSynchronousImport);
            IdentifierFixture.Call("DeleteScript", obsolete);
            Assert.That(File.Exists(obsolete), Is.False);
        }

        [TestCase("class", "_class")]
        [TestCase("9 first", "_9_first")]
        [TestCase("", "_")]
        [TestCase("__arglist", "___arglist")]
        [TestCase("é", "é")]
        [TestCase("状态", "状态")]
        public void Identifier_is_a_valid_CSharp_token(string source, string expected) => Assert.That(IdentifierFixture.Call("Identifier", source), Is.EqualTo(expected));

        [Test]
        public void Generated_names_compile_and_saved_bindings_preserve_authored_objects()
        {
            using var fixture = new IdentifierFixture();
            var asset = fixture.Bake();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                var panel = instance.GetComponent<Component>();
                Assert.That(panel.GetType().Namespace, Is.EqualTo("Example._class.Name_Fixtures"));
                Assert.That(panel.GetType().Name, Is.EqualTo("_12_panel_"));
                Assert.That(panel.GetType().GetField("_class"), Is.Not.Null);
                Assert.That(panel.GetType().GetField("_class_2"), Is.Not.Null);
                Assert.That(panel.GetType().GetField("foo_bar").GetValue(panel), Is.Not.Null);
                Assert.That(panel.GetType().GetField("foo_bar_2").GetValue(panel), Is.Not.Null);
                Assert.That(panel.GetType().GetField("name_2").GetValue(panel), Is.Not.Null);
                Assert.That(panel.GetType().GetField("my_page").FieldType.Namespace, Is.EqualTo("Example._class.Name_Fixtures"));
                Assert.That(panel.GetType().GetField("my_page_2").FieldType.Namespace, Is.EqualTo("Example._class.Name_Fixtures_2"));
                var names = new List<string>(); for (var i = 0; i < instance.transform.childCount; i++) names.Add(instance.transform.GetChild(i).name);
                Assert.That(names, Does.Contain("foo bar").And.Contain("foo-bar").And.Contain(""));
                Assert.That(names.FindAll(name => name == "同名").Count, Is.EqualTo(2));
                var transitions = instance.GetComponents<Transition>(); Assert.That(transitions.Length, Is.EqualTo(3));
                Assert.That(transitions[0].transitionName, Is.EqualTo("class")); Assert.That(transitions[1].transitionName, Is.EqualTo("_class"));
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(instance), Is.Zero);
                panel.SetControllerPage(3, 1); Assert.That(panel.GetControllerPage(3), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
