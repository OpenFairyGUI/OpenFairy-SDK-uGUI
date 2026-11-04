using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public class NativeScrollingImportTests
    {
        [Test]
        public void Popup_list_is_baked_and_remapped_after_prefab_reload_without_name_lookup()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var key = "__PopupContent_" + Guid.NewGuid().ToString("N");
            var source = "Temp/" + key;
            var output = "Assets/" + key;
            var settings = MigrationSettings.instance;
            var previousSource = settings.sourceDirectory;
            var previousOutput = settings.outputDirectory;
            var previousDependencies = settings.dependencyDirectories;
            var common = typeof(Migrate).GetField("_codeGenerationSettings", flags);
            var previousCommon = common.GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", flags).GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", flags).GetValue(null);
            var path = output + "/Assets/Views/Component.prefab";
            GameObject instance = null;
            try
            {
                settings.sourceDirectory = source;
                settings.outputDirectory = output;
                settings.dependencyDirectories = Array.Empty<string>();
                Directory.CreateDirectory(source + "/assets/Views");
                var file = source + "/assets/Views/Component.xml";
                File.WriteAllText(file, "<component size='100,80'><displayList>"
                    + "<list id='items' name='list' size='100,60' selectionMode='none'/>"
                    + "</displayList></component>");
                var codeSettings = Activator.CreateInstance(common.FieldType);
                common.FieldType.GetField("packageName").SetValue(codeSettings, "OpenFairy");
                common.SetValue(null, codeSettings);
                var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(type);
                type.GetField("Id").SetValue(resource, key);
                type.GetField("File").SetValue(resource, file);
                type.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                type.GetField("Package").SetValue(resource, "UGUI");
                type.GetField("PackageId").SetValue(resource, key);
                resources.Add(key, resource);
                using var localization = new ImportLocalizationFixture();
                localization.Prepare(resource);
                typeof(Migrate).GetMethod("BuildPrefab", flags).Invoke(null, new[] { resource });
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponent<PopupContent>().list, Is.Not.Null);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var list = instance.GetComponent<PopupContent>().list;
                Assert.That(list.transform.IsChildOf(instance.transform), Is.True);
                Assert.That(list, Is.Not.SameAs(prefab.GetComponent<PopupContent>().list));
                list.name = "Renamed list";
                Assert.That(instance.GetComponent<PopupContent>().list, Is.SameAs(list));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                common.SetValue(null, previousCommon);
                settings.sourceDirectory = previousSource;
                settings.outputDirectory = previousOutput;
                settings.dependencyDirectories = previousDependencies;
                prefabs.Remove(path);
                resources.Remove(key);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }

        [TestCase("image", "NotXml.png")]
        [TestCase("font", "NotXml.ttf")]
        [TestCase("component", "Plain.xml")]
        public void Non_scrollbar_resources_do_not_enter_component_collection_or_baked_scrollbars(string kind, string file)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            const string source = "Temp/OpenFairyScrollbarSource";
            const string output = "Assets/__OpenFairyScrollbarImport";
            var settings = MigrationSettings.instance;
            var previousSource = settings.sourceDirectory;
            var previousOutput = settings.outputDirectory;
            var previousDependencies = settings.dependencyDirectories;
            var common = typeof(Migrate).GetField("_settings", flags);
            var previousCommon = common.GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", flags).GetValue(null);
            var previousResources = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in resources) previousResources.Add(entry.Key, entry.Value);
            GameObject root = null;
            GameObject instance = null;
            try
            {
                Directory.CreateDirectory(source + "/assets/Views");
                Directory.CreateDirectory(source + "/settings");
                Directory.CreateDirectory(output);
                File.WriteAllText(source + "/settings/Common.json", "{}");
                File.WriteAllText(source + "/assets/Views/package.xml", "<packageDescription id='barstest'><resources>"
                    + "<component id='view' name='View.xml' path='/'/>"
                    + $"<{kind} id='invalid' name='{file}' path='/'/></resources></packageDescription>");
                File.WriteAllText(source + "/assets/Views/" + file,
                    kind == "component" ? "<component size='10,10'><displayList/></component>" : "Not XML");
                var viewPath = source + "/assets/Views/View.xml";
                File.WriteAllText(viewPath, "<component size='200,120' overflow='scroll' scrollBar='visible' "
                    + "scrollBarRes='ui://barstestinvalid,'><displayList/></component>");
                settings.sourceDirectory = source;
                settings.outputDirectory = output;
                settings.dependencyDirectories = Array.Empty<string>();
                common.SetValue(null, null);
                typeof(Migrate).GetMethod("IndexSources", flags).Invoke(null, null);
                var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType));
                var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(resourceType));
                typeof(Migrate).GetMethod("Collect", flags).Invoke(null,
                    new[] { resources["barstestview"], components, images });
                Assert.That(components.Count, Is.EqualTo(1));
                root = new GameObject("View", typeof(RectTransform));
                ((RectTransform)root.transform).sizeDelta = new Vector2(200, 120);
                var view = typeof(Migrate).GetMethod("BuildScrollView", flags, null,
                    new[] { typeof(GameObject), typeof(Schema.Component), typeof(Vector2) }, null)
                    .Invoke(null, new object[] { root, Schema.Component.Load(viewPath), new Vector2(200, 120) });
                Rect("Dynamic content", root.GetComponent<ScrollPane>().content, new Vector2(200, 300), Vector2.zero);
                typeof(Migrate).GetMethod("SetGrips", flags).Invoke(null, new[] { view, new Vector2(200, 300) });
                AssetDatabase.Refresh();
                var prefabPath = output + "/View.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                Assert.That(saved, Is.True);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                var pane = instance.GetComponent<ScrollPane>();
                Assert.That(pane.verticalScrollbar, Is.Null);
                Assert.That(pane.horizontalScrollbar, Is.Null);
                Assert.That(pane.viewport.rect.size, Is.EqualTo(new Vector2(200, 120)));
                Assert.That(pane.contentSize, Is.EqualTo(new Vector2(200, 300)));
                ((RectTransform)pane.content.GetChild(0)).sizeDelta = new Vector2(200, 450);
                pane.RefreshContent();
                Assert.That(pane.contentSize, Is.EqualTo(new Vector2(200, 450)));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                settings.sourceDirectory = previousSource;
                settings.outputDirectory = previousOutput;
                settings.dependencyDirectories = previousDependencies;
                common.SetValue(null, previousCommon);
                resources.Clear();
                foreach (var entry in previousResources) resources.Add(entry.Key, entry.Value);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }

        [Test]
        public void Auto_scrollbar_without_a_canvas_group_bakes_hidden_and_noninteractive()
        {
            var paneObject = new GameObject("Pane", typeof(RectTransform), typeof(ScrollPane));
            var barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(AuthoredScrollbar));
            try
            {
                var hidden = new List<CanvasGroup>();
                typeof(Migrate).GetMethod("BindScrollBar", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { barObject, paneObject.GetComponent<ScrollPane>(), false,
                        Schema.ScrollBarDisplay.Auto, hidden });
                Assert.That(hidden, Has.Count.EqualTo(1));
                Assert.That(hidden[0], Is.SameAs(barObject.GetComponent<CanvasGroup>()));
                Assert.That(hidden[0].alpha, Is.Zero);
                Assert.That(hidden[0].interactable || hidden[0].blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(barObject);
                Object.DestroyImmediate(paneObject);
            }
        }

        [Test]
        public void Schema_preserves_explicit_scrollbars_controller_and_typed_flags()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "<component size='200,120' overflow='scroll' scrollBar='auto' scrollBarRes='ui://fixture1vertical,' scrollBarFlags='1032'><displayList><list id='items' pageController='Pages' scrollBarFlags='38' scrollBar='visible' scrollBarRes=',ui://fixture1horizontal'/></displayList></component>");
                var xml = Schema.Component.Load(path);
                Assert.AreEqual(Schema.ScrollBarDisplay.Auto, xml.ScrollBar);
                Assert.AreEqual("ui://fixture1vertical", xml.ScrollBarRes[0]);
                Assert.IsTrue(xml.ScrollBarFlags.HasFlag(Schema.ScrollFlags.Floating | Schema.ScrollFlags.PageMode));
                Assert.AreEqual("Pages", xml.DisplayList[0].PageController);
                Assert.IsTrue(xml.DisplayList[0].ScrollBarFlags.HasFlag(Schema.ScrollFlags.TouchOff | Schema.ScrollFlags.SnapToItem | Schema.ScrollFlags.DisplayInDemand));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Importer_reparents_the_same_grip_and_preserves_its_actual_rectangle()
        {
            var root = new GameObject("Scrollbar", typeof(RectTransform));
            try
            {
                var rt = (RectTransform)root.transform;
                rt.sizeDelta = new Vector2(20, 120);
                rt.pivot = new Vector2(0, 1);
                var track = Rect("Track", rt, new Vector2(20, 100), new Vector2(0, -10));
                var grip = Rect("Grip", rt, new Vector2(12, 25), new Vector2(3, -30));
                var before = new Vector3[4];
                grip.GetWorldCorners(before);
                var method = typeof(Migrate).GetMethod("SetupScrollBar", BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new object[] { root, new List<(Schema.Display, GameObject)> {
                    (new Schema.Display { Name = "bar" }, track.gameObject),
                    (new Schema.Display { Name = "grip" }, grip.gameObject),
                }, new Schema.Extension { FixedGripSize = true } });
                var bar = root.GetComponent<AuthoredScrollbar>();
                Assert.IsTrue(bar.fixedGripSize);
                Assert.That(bar.authoredGripSize, Is.EqualTo(25));
                Assert.AreSame(grip, bar.handleRect);
                Assert.AreSame(track, grip.parent);
                Assert.That(bar.size, Is.EqualTo(.25f).Within(.0001f));
                var after = new Vector3[4];
                grip.GetWorldCorners(after);
                for (var i = 0; i < 4; i++) Assert.That(Vector3.Distance(before[i], after[i]), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }
    }
}
