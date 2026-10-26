using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
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
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        private static object Invoke(string name, params object[] arguments) =>
            typeof(Migrate).GetMethod(name, Static).Invoke(null, arguments);

        [SetUp]
        public void Setup()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
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
            foreach (var name in new[] { "Resources", "Prefabs", "MovieClips" })
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
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
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

        [UnityTearDown]
        public IEnumerator ExitPlay()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
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

        [UnityTest]
        public IEnumerator Fitted_component_instance_does_not_apply_its_authored_size_increase_twice()
        {
            var template = new GameObject("Default", typeof(RectTransform), typeof(Component));
            var parent = new GameObject("Parent", typeof(RectTransform));
            GameObject child = null, copy = null;
            try
            {
                var rect = (RectTransform)template.transform;
                rect.pivot = rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(40, 20);
                var group = (RectTransform)new GameObject("Group", typeof(RectTransform)).transform;
                group.SetParent(rect, false);
                group.pivot = group.anchorMin = group.anchorMax = new Vector2(0, 1);
                group.anchoredPosition = new Vector2(10, -5);
                group.sizeDelta = new Vector2(20, 10);
                var preferred = group.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                preferred.preferredWidth = 100;
                preferred.preferredHeight = 30;
                var fitter = group.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.horizontalFit = fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var relation = template.AddComponent<Relation>();
                relation.reference = relation.coordinateSpace = rect;
                relation.bindings = new[] { new RelationBinding { target = group, rules = new[] {
                    new RelationRule { side = RelationSide.RightExtRight },
                    new RelationRule { side = RelationSide.BottomExtBottom },
                } } };
                relation.Record();
                PrefabUtility.SaveAsPrefabAsset(template, Output + "/Assets/BindingFixtures/Default.prefab");
                var controllerType = typeof(Migrate).GetNestedType("ControllerData", BindingFlags.NonPublic);
                var controllers = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), controllerType));
                child = (GameObject)Invoke("CreateChild", new Schema.Display {
                    Kind = Schema.DisplayKind.Component, Source = "default", Name = "Fitted", Size = new Vector2(120, 40),
                }, (RectTransform)parent.transform, Resource("default"), controllers);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)child.transform.Find("Group"));
                Invoke("CompleteRelations", child);
                Assert.That(((RectTransform)child.transform).rect.size, Is.EqualTo(new Vector2(120, 40)));
                copy = SaveReload(child);
                Assert.That(((RectTransform)copy.transform).rect.size, Is.EqualTo(new Vector2(120, 40)));
                Assert.That(copy.GetComponent<Relation>().bindings[0].previous.size, Is.EqualTo(new Vector2(100, 30)), "Saved target baseline");
                yield return new EnterPlayMode();
                Assert.That(((RectTransform)copy.transform).rect.size, Is.EqualTo(new Vector2(120, 40)), "First active frame");
                var target = copy.transform.Find("Group").GetComponent<UnityEngine.UI.LayoutElement>();
                target.preferredWidth = 140;
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)target.transform);
                copy.GetComponent<Relation>().Sync();
                Assert.That(((RectTransform)copy.transform).rect.width, Is.EqualTo(160));
            }
            finally
            {
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(parent);
                if (copy != null) Object.DestroyImmediate(copy);
            }
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

        // FairyGUI Image (also MovieClip) sets TouchDisabled. Container.HitTest skips
        // these children even when GObject.touchable is true; GLoader remains a hit surface.
        [UnityTest] public IEnumerator Image_overlay_allows_clicking_the_saved_button_beneath() => SavedHitSurface(Schema.DisplayKind.Image, false);
        [UnityTest] public IEnumerator Movie_overlay_allows_clicking_the_saved_button_beneath() => SavedHitSurface(Schema.DisplayKind.MovieClip, false);
        [UnityTest] public IEnumerator Loader_keeps_its_saved_hit_surface() => SavedHitSurface(Schema.DisplayKind.Loader, true);

        private static IEnumerator SavedHitSurface(Schema.DisplayKind kind, bool intercepts)
        {
            var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var resource = Activator.CreateInstance(type, true);
            type.GetField("File").SetValue(resource, Source + "/assets/BindingFixtures/Overlay.png");
            type.GetField("Type").SetValue(resource, kind == Schema.DisplayKind.MovieClip ? Schema.ResourceKind.MovieClip : Schema.ResourceKind.Image);
            type.GetField("PackageId").SetValue(resource, "edgesrc1");
            type.GetField("Id").SetValue(resource, "overlay");
            ((IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null)).Add("edgesrc1overlay", resource);
            var path = (string)Invoke("SpritePath", resource);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(2, 2);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            if (kind == Schema.DisplayKind.MovieClip)
            {
                var dataType = typeof(Migrate).GetNestedType("MovieClipData", BindingFlags.NonPublic);
                var data = Activator.CreateInstance(dataType, true);
                dataType.GetField("Size").SetValue(data, new Vector2(100, 100));
                dataType.GetField("FrameCount").SetValue(data, 1);
                dataType.GetField("AddDelays").SetValue(data, new[] { 0f });
                ((IDictionary)typeof(Migrate).GetField("MovieClips", Static).GetValue(null)).Add(resource, data);
            }
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var button = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(root.transform, false);
            ((RectTransform)button.transform).sizeDelta = new Vector2(100, 100);
            GameObject clone = null;
            try
            {
                var create = typeof(Migrate).GetMethod("CreateChild", Static);
                var overlay = (GameObject)create.Invoke(null, new[] { new Schema.Display {
                    Kind = kind, Name = "Overlay", Source = "overlay", Size = new Vector2(100, 100), Playing = false,
                }, root.transform, resource, Activator.CreateInstance(create.GetParameters()[3].ParameterType) });
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                clone = SaveReload(root);
                Object.DestroyImmediate(root);
                root = null;
                yield return new EnterPlayMode(false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var saved = clone.transform.Find("Overlay").GetComponent<UnityEngine.UI.Graphic>();
                Assert.That(saved.raycastTarget, Is.EqualTo(intercepts));
                var events = new GameObject("Events", typeof(UnityEngine.EventSystems.EventSystem));
                try
                {
                    var point = RectTransformUtility.WorldToScreenPoint(null, clone.transform.Find("Button").position);
                    var pointer = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>()) { position = point };
                    var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                    clone.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(pointer, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(hits[0].gameObject.name, Is.EqualTo(intercepts ? "Overlay" : "Button"));
                    var clicks = 0;
                    clone.transform.Find("Button").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => clicks++);
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer,
                        UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                    Assert.That(clicks, Is.EqualTo(intercepts ? 0 : 1));
                }
                finally { Object.DestroyImmediate(events); }
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
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

        [TestCase("text", "true", "")]
        [TestCase("richtext", "true", "")]
        [TestCase("inputtext", "true", "")]
        [TestCase("text", "false", "Preview")]
        [TestCase("richtext", "false", "Preview")]
        [TestCase("inputtext", "false", "Preview")]
        [TestCase("text", null, "Preview")]
        [TestCase("richtext", null, "Preview")]
        [TestCase("inputtext", null, "Preview")]
        public void Auto_clear_text_is_shared_by_import_and_generated_localization(string kind, string flag, string expected)
        {
            var file = Source + "/assets/BindingFixtures/CollisionBindingFixture.xml";
            var attribute = flag == null ? "" : $" autoClearText='{flag}'";
            File.WriteAllText(file, $"<component size='100,100'><displayList><{kind} id='body' name='Body' size='100,40'{attribute} text='Preview'/></displayList></component>");
            Assert.That(FairyXml.LoadComponent(file).DisplayList[0].Text, Is.EqualTo(expected));
            var resource = Resource("collision");
            using var localization = new ImportLocalizationFixture();
            localization.Prepare(resource);
            var views = (IDictionary)typeof(Migrate).GetField("LocalViews", Static).GetValue(null);
            var view = views[resource];
            var family = ((IList)view.GetType().GetField("families").GetValue(view))[0];
            var source = family.GetType().GetField("source").GetValue(family);
            var choice = ((Array)source.GetType().GetField("choices").GetValue(source)).GetValue(0);
            Assert.That(choice.GetType().GetField("text").GetValue(choice), Is.EqualTo(expected));
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
            Assert.That(code, Does.Contain("public global::UnityEngine.RectTransform m_Status_2;"));
            Assert.That(code, Does.Contain("public global::UnityEngine.RectTransform m_Status_2_2;"));
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
