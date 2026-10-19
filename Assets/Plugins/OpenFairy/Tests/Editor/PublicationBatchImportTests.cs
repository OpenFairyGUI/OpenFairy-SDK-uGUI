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
#if FAIRYGUI_TMPRO
using TMPro;
#endif
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PublicationBatchImportTests
    {
        private sealed class Fixture : IDisposable
        {
            public const string Source = "Temp/OpenFairyPublicationSource";
            public const string Output = "Assets/__OpenFairyPublication";
            public const string Export = "Artifacts/OpenFairyPublicationExport";
            public const string RootPath = Output + "/Assets/Publication/Root.prefab";
            private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
            private readonly string source = MigrationSettings.instance.sourceDirectory;
            private readonly string output = MigrationSettings.instance.outputDirectory;
            private readonly string[] dependencies = MigrationSettings.instance.dependencyDirectories;
            private readonly Dictionary<string, object> settings = new();
            private readonly Dictionary<string, Dictionary<object, object>> tables = new();
            public IList components;

            public static object Call(string name, params object[] args) => typeof(Migrate).GetMethod(name, Static).Invoke(null, args);
            public Fixture()
            {
                MigrationSettings.instance.sourceDirectory = Source;
                MigrationSettings.instance.outputDirectory = Output;
                MigrationSettings.instance.dependencyDirectories = Array.Empty<string>();
                foreach (var name in new[] { "_settings", "_codeGenerationSettings", "_translations", "_localization" })
                {
                    var field = typeof(Migrate).GetField(name, Static); settings[name] = field.GetValue(null); field.SetValue(null, null);
                }
                foreach (var name in new[] { "Resources", "Prefabs", "LocalViews" })
                {
                    var table = (IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null);
                    var copy = new Dictionary<object, object>(); foreach (DictionaryEntry row in table) copy.Add(row.Key, row.Value);
                    tables.Add(name, copy); table.Clear();
                }
                Directory.CreateDirectory(Source + "/settings");
                File.WriteAllText(Source + "/settings/Publish.json", "{\"codeGeneration\":{\"memberNamePrefix\":\"m_\",\"packageName\":\"OpenFairyPublicationFixtures\"}}");
                File.WriteAllText(Source + "/settings/Common.json", "{}");
                var folder = Source + "/assets/Publication"; Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/package.xml", "<packageDescription id='publish1'><resources>"
                    + "<image id='image' name='image.png' path='/'/>"
                    + "<component id='a' name='LeafA.xml' path='/'/>"
                    + "<component id='b' name='LeafB.xml' path='/'/>"
                    + "<component id='branch' name='Branch.xml' path='/'/>"
                    + "<component id='root' name='Root.xml' path='/'/>"
                    + "</resources></packageDescription>");
                var texture = new Texture2D(16, 16); texture.SetPixels(new Color[256]); texture.Apply();
                File.WriteAllBytes(folder + "/image.png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                WriteLeaves("First");
                File.WriteAllText(folder + "/Branch.xml", "<component size='240,100'><displayList>"
                    + "<component id='a' name='First' src='a'/><component id='b' name='Second' src='b' xy='120,0'/>"
                    + "</displayList></component>");
                File.WriteAllText(folder + "/Root.xml", "<component size='240,100'><displayList>"
                    + "<component id='branch' name='Body' src='branch'/></displayList></component>");
                Prepare();
            }

            public void WriteLeaves(string value)
            {
                foreach (var name in new[] { "LeafA", "LeafB" })
                    File.WriteAllText(Source + "/assets/Publication/" + name + ".xml",
                        new XElement("component", new XAttribute("size", "120,100"),
                            new XElement("displayList",
                                new XElement("text", new XAttribute("id", "text"), new XAttribute("name", "Caption"),
                                    new XAttribute("size", "120,60"), new XAttribute("fontSize", "24"),
                                    new XAttribute("ubb", "true"), new XAttribute("shadowColor", "#000000"),
                                    new XAttribute("shadowOffset", "2,2"), new XAttribute("text", value + " [img]ui://publish1image[/img]")),
                                new XElement("image", new XAttribute("id", "image"), new XAttribute("name", "Picture"),
                                    new XAttribute("src", "image"), new XAttribute("xy", "0,60"), new XAttribute("size", "16,16")))).ToString());
            }

            public void Prepare()
            {
                foreach (var name in new[] { "Resources", "Prefabs", "LocalViews" }) ((IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null)).Clear();
                Call("IndexSources");
                var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
                var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
                var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(type));
                Call("Collect", resources["publish1root"], components, images);
                foreach (var image in (IEnumerable)images) Call("ImportImage", image);
                Call("PrepareLocalization", components);
            }

            public void Build(bool batched)
            {
                if (batched) Call("BuildPrefabs", components);
                else foreach (var component in components) Call("BuildPrefab", component);
                AssetDatabase.SaveAssets();
            }

            public void Dispose()
            {
                PublicationImportObserver.output = null;
                foreach (var row in settings) typeof(Migrate).GetField(row.Key, Static).SetValue(null, row.Value);
                foreach (var row in tables)
                {
                    var table = (IDictionary)typeof(Migrate).GetField(row.Key, Static).GetValue(null); table.Clear();
                    foreach (var item in row.Value) table.Add(item.Key, item.Value);
                }
                MigrationSettings.instance.sourceDirectory = source; MigrationSettings.instance.outputDirectory = output;
                MigrationSettings.instance.dependencyDirectories = dependencies;
                AssetDatabase.DeleteAsset(Output);
                if (Directory.Exists(Source)) Directory.Delete(Source, true);
            }
        }

        public static void Export()
        {
            using var fixture = new Fixture();
            foreach (var resource in fixture.components)
            {
                var folder = Fixture.Export + "/" + (string)Fixture.Call("GeneratedNamespace", resource);
                Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/" + (string)Fixture.Call("ClassName", resource) + ".cs", (string)Fixture.Call("ScriptCode", resource));
            }
            MigrationSettings.instance.outputDirectory = Fixture.Export;
            Fixture.Call("GenerateLocaleScripts");
        }

        private static GameObject VerifyReloaded()
        {
            AssetDatabase.ImportAsset(Fixture.RootPath, ImportAssetOptions.ForceSynchronousImport);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Fixture.RootPath);
            Assert.That(root, Is.Not.Null);
            var body = root.transform.Find("Body");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(body.gameObject), Is.Not.Null);
            foreach (var name in new[] { "First", "Second" })
            {
                var leaf = body.Find(name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(leaf.gameObject), Is.Not.Null);
                var component = leaf.GetComponent<Component>();
                Assert.That(component.GetType().GetField("m_Caption").GetValue(component), Is.Not.Null);
                var picture = (UnityEngine.UI.Image)component.GetType().GetField("m_Picture").GetValue(component);
                Assert.That(picture.sprite, Is.Not.Null);
#if FAIRYGUI_TMPRO
                var text = leaf.GetComponentInChildren<TextMeshProUGUI>();
                Assert.That(text.font, Is.Not.Null); Assert.That(text.font.atlasTexture, Is.Not.Null);
                Assert.That(text.fontSharedMaterial, Is.Not.Null); Assert.That(EditorUtility.IsPersistent(text.fontSharedMaterial), Is.True);
                Assert.That(text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
                Assert.That(text.spriteAsset, Is.Not.Null); Assert.That(text.spriteAsset.material, Is.Not.Null);
                Assert.That(text.spriteAsset.spriteCharacterTable.Count, Is.EqualTo(1));
                Assert.That(text.spriteAsset.spriteGlyphTable[0].sprite, Is.Not.Null);
#endif
            }
            return root;
        }

        [Test]
        public void Dependency_levels_include_component_instance_dropdowns()
        {
            using var fixture = new Fixture();
            File.WriteAllText(Fixture.Source + "/assets/Publication/Branch.xml", "<component size='120,100'><displayList>"
                + "<component id='a' name='First' src='a'><ComboBox dropdown='ui://publish1b'/></component>"
                + "</displayList></component>");
            fixture.Prepare();
            var names = new List<string>();
            foreach (var component in fixture.components)
                names.Add((string)component.GetType().GetField("Id").GetValue(component));
            Assert.That(names.IndexOf("b"), Is.GreaterThanOrEqualTo(0));
            Assert.That(names.IndexOf("b"), Is.LessThan(names.IndexOf("branch")));
        }

        [Test]
        public void Fresh_three_level_publication_preserves_nested_prefabs_and_fresh_native_assets()
        {
            using var fixture = new Fixture();
            fixture.Build(true);
            VerifyReloaded();
            var guid = AssetDatabase.AssetPathToGUID(Fixture.RootPath);
            var first = File.ReadAllBytes(Fixture.RootPath);
            fixture.Build(true);
            VerifyReloaded();
            Assert.That(AssetDatabase.AssetPathToGUID(Fixture.RootPath), Is.EqualTo(guid));
            Assert.That(File.ReadAllBytes(Fixture.RootPath), Is.EqualTo(first));
        }

        [Test]
        public void Independent_leaf_saves_coalesce_existing_downstream_imports()
        {
            using var fixture = new Fixture(); fixture.Build(true);
            var parent = new GameObject("Dependent", typeof(RectTransform));
            try
            {
                PrefabUtility.InstantiatePrefab(VerifyReloaded(), parent.transform);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(parent, Fixture.Output + "/Dependent.prefab"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(parent); }
            PublicationImportObserver.output = Fixture.Output;
            fixture.WriteLeaves("Sequential"); fixture.Prepare(); PublicationImportObserver.counts.Clear();
            fixture.Build(false);
            var sequential = PublicationImportObserver.Count(Fixture.Output + "/Dependent.prefab");
            fixture.WriteLeaves("Batched"); fixture.Prepare(); PublicationImportObserver.counts.Clear();
            fixture.Build(true);
            var batched = PublicationImportObserver.Count(Fixture.Output + "/Dependent.prefab");
            VerifyReloaded();
            Directory.CreateDirectory("Artifacts/OpenFairyPublication");
            File.WriteAllText("Artifacts/OpenFairyPublication/Imports.json", "{\"sequential\":" + sequential + ",\"batched\":" + batched + "}");
            Assert.That(sequential, Is.GreaterThan(0));
            Assert.That(batched, Is.LessThan(sequential));
        }
    }

    internal sealed class PublicationImportObserver : AssetPostprocessor
    {
        internal static string output;
        internal static readonly Dictionary<string, int> counts = new();
        internal static int Count(string path) => counts.TryGetValue(path, out var value) ? value : 0;
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (output == null) return;
            foreach (var path in imported) if (path.StartsWith(output, StringComparison.Ordinal)) counts[path] = Count(path) + 1;
        }
    }
}
