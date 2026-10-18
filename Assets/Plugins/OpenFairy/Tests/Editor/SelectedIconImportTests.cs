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
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class SelectedIconImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static object Call(string method, params object[] arguments) =>
            typeof(Migrate).GetMethod(method, PrivateStatic).Invoke(null, arguments);
        private static IDictionary Cache(string name) => (IDictionary)typeof(Migrate).GetField(name, PrivateStatic).GetValue(null);
        private readonly List<GameObject> _objects = new();
        private string _source, _output, _package;
        private object _previousSettings;
        private Sprite _normal, _chosen;

        private object Resource(string file, Schema.ResourceKind kind)
        {
            var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var resource = Activator.CreateInstance(type, true);
            type.GetField("File").SetValue(resource, _source + "/" + file);
            type.GetField("Type").SetValue(resource, kind);
            type.GetField("Package").SetValue(resource, "Tests");
            type.GetField("PackageId").SetValue(resource, _package);
            return resource;
        }

        [SetUp]
        public void Setup()
        {
            _package = Guid.NewGuid().ToString("N");
            string name = "__SelectedIconTest_" + _package;
            _source = "UIProject/assets/" + name;
            _output = "Assets/UIProject/Assets/" + name;
            Directory.CreateDirectory(_source);
            var settings = typeof(Migrate).GetField("_codeGenerationSettings", PrivateStatic);
            _previousSettings = settings.GetValue(null);
            var value = Activator.CreateInstance(settings.FieldType, true);
            settings.FieldType.GetField("packageName").SetValue(value, "OpenFairy.UGUI");
            settings.FieldType.GetField("classNamePrefix").SetValue(value, "");
            settings.FieldType.GetField("memberNamePrefix").SetValue(value, "m_");
            settings.SetValue(null, value);
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(_source + "/Normal.png", texture.EncodeToPNG());
                File.WriteAllBytes(_source + "/Selected.png", texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            foreach (var image in new[] { ("normal", "Normal.png"), ("chosen", "Selected.png") })
            {
                var resource = Resource(image.Item2, Schema.ResourceKind.Image);
                Cache("Resources").Add(_package + image.Item1, resource);
                Call("ImportImage", resource);
            }
            _normal = AssetDatabase.LoadAssetAtPath<Sprite>(_output + "/Normal.png");
            _chosen = AssetDatabase.LoadAssetAtPath<Sprite>(_output + "/Selected.png");
            Assert.That(_normal, Is.Not.Null);
            Assert.That(_chosen, Is.Not.Null);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
            foreach (var key in new[] { "normal", "chosen", "button" }) Cache("Resources").Remove(_package + key);
            Cache("Prefabs").Remove(_output + "/SelectedIconTestButton.prefab");
            typeof(Migrate).GetField("_codeGenerationSettings", PrivateStatic).SetValue(null, _previousSettings);
            AssetDatabase.DeleteAsset(_output);
            if (Directory.Exists(_source)) Directory.Delete(_source, true);
        }

        private SelectedIconTestButton Button()
        {
            var root = new GameObject("Button", typeof(RectTransform));
            _objects.Add(root);
            var button = root.AddComponent<SelectedIconTestButton>();
            button.mode = ButtonMode.Check;
            var icon = new GameObject("icon", typeof(RectTransform), typeof(Loader));
            icon.transform.SetParent(root.transform, false);
            button.iconLoader = icon.GetComponent<Loader>();
            return button;
        }

        [Test]
        public void Schema_preserves_absent_empty_and_list_item_selected_icons()
        {
            string file = _source + "/Schema.xml";
            File.WriteAllText(file, "<component><displayList><component><Button/></component>"
                + "<component><Button selectedIcon=''/></component>"
                + "<list><item selectedIcon='ui://example'/></list></displayList></component>");
            var rows = Schema.Component.Load(file).DisplayList;
            Assert.That(rows[0].Button.SelectedIcon, Is.Null);
            Assert.That(rows[1].Button.SelectedIcon, Is.Empty);
            Assert.That(rows[2].Items[0].SelectedIcon, Is.EqualTo("ui://example"));
        }

        [Test]
        public void Instance_overrides_preserve_missing_selected_icon_and_distinguish_empty_or_unresolved()
        {
            var button = Button();
            Call("ConfigureButton", button.gameObject, new Schema.Extension {
                Icon = "ui://" + _package + "normal", SelectedIcon = "ui://" + _package + "chosen" }, _package);
            Call("ConfigureButton", button.gameObject, new Schema.Extension { Checked = true }, _package);
            Assert.That(button.selectedIcon, Is.SameAs(_chosen));
            Assert.That(button.iconLoader.sprite, Is.SameAs(_chosen));
            Assert.That(button.icon, Is.SameAs(_normal));
            Call("ConfigureButton", button.gameObject, new Schema.Extension { SelectedIcon = "", Checked = true }, _package);
            Assert.That(button.useSelectedIcon, Is.True);
            Assert.That(button.iconLoader.sprite, Is.Null);
            Assert.That(button.iconLoader.enabled, Is.False);
            Call("ConfigureButton", button.gameObject, new Schema.Extension { Checked = false }, _package);
            Assert.That(button.iconLoader.sprite, Is.SameAs(_normal));
            LogAssert.Expect(LogType.Warning, "Unresolved FairyGUI icon: ui://missing");
            Call("ConfigureButton", button.gameObject, new Schema.Extension { SelectedIcon = "ui://missing", Checked = true }, _package);
            Assert.That(button.useSelectedIcon && button.selected, Is.True);
            Assert.That(button.iconLoader.sprite, Is.Null);
            Call("ConfigureButton", button.gameObject, new Schema.Extension { Mode = ButtonMode.Common, Checked = true }, _package);
            Assert.That(button.selected, Is.False);
            Assert.That(button.iconLoader.sprite, Is.SameAs(_normal));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Saved_prefab_retains_both_static_images_and_the_authored_initial_selection(bool selected)
        {
            string file = _source + "/SelectedIconTestButton.xml";
            File.WriteAllText(file, "<component size='32,32' extention='Button'>"
                + "<controller name='button' pages='0,up,1,down,2,over,3,selectedOver,4,disabled,5,selectedDisabled'/>"
                + "<displayList><loader id='icon' name='icon' size='32,32' url='ui://" + _package + "normal'/></displayList>"
                + "<Button mode='Check' selectedIcon='ui://" + _package + "chosen' checked='" + (selected ? "true" : "false") + "'/></component>");
            var resource = Resource("SelectedIconTestButton.xml", Schema.ResourceKind.Component);
            Cache("Resources").Add(_package + "button", resource);
            // Collect follows all authored image URLs, including selection-only references.
            var resourceType = resource.GetType();
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType));
            var imageSet = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(resourceType));
            Call("Collect", resource, list, imageSet);
            Assert.That((int)imageSet.GetType().GetProperty("Count").GetValue(imageSet), Is.EqualTo(2));
            using var localization = new ImportLocalizationFixture();
            localization.Prepare(resource);
            localization.AssertGenerated(resource, typeof(SelectedIconTestButton));
            Call("BuildPrefab", resource);
            string path = _output + "/SelectedIconTestButton.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var button = prefab.GetComponent<SelectedIconTestButton>();
            Assert.That(button.icon, Is.SameAs(_normal));
            Assert.That(button.selectedIcon, Is.SameAs(_chosen));
            Assert.That(button.useSelectedIcon, Is.True);
            Assert.That(button.selected, Is.EqualTo(selected));
            Assert.That(button.iconLoader.sprite, Is.SameAs(selected ? _chosen : _normal));
            var dependencies = AssetDatabase.GetDependencies(path);
            CollectionAssert.Contains(dependencies, _output + "/Normal.png");
            CollectionAssert.Contains(dependencies, _output + "/Selected.png");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _objects.Add(instance);
            var view = instance.GetComponent<SelectedIconTestButton>();
            view.selected = !selected;
            Assert.That(view.iconLoader.sprite, Is.SameAs(selected ? _normal : _chosen));
            Assert.That(view.icon, Is.SameAs(_normal));
        }
    }
}
