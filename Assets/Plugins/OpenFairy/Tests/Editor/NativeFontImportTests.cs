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

namespace OpenFairy.UGUI.Tests
{
    public class NativeFontImportTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        private static object Invoke(string name, params object[] args) =>
            typeof(Migrate).GetMethod(name, Static).Invoke(null, args);

        [TestCase(null)]
        [TestCase("")]
        public void Missing_design_font_keeps_the_default_family(string font)
        {
            var common = typeof(Migrate).GetField("_settings", Static);
            var previous = common.GetValue(null);
            try
            {
                var settings = Activator.CreateInstance(common.FieldType);
                common.FieldType.GetField("font").SetValue(settings, font);
                common.SetValue(null, settings);
                Assert.That(Invoke("DefaultFont"), Is.EqualTo("Arial"));
            }
            finally { common.SetValue(null, previous); }
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void Authored_dynamic_fonts_survive_prefab_reload(bool explicitFont, bool input, bool dependency)
        {
            var source = "Temp/OpenFairyFontSource";
            var dependencySource = "Temp/OpenFairyFontDependency";
            var output = "Assets/__OpenFairyFontImport";
            var settings = MigrationSettings.instance;
            var previousSource = settings.sourceDirectory;
            var previousOutput = settings.outputDirectory;
            var previousDependencies = settings.dependencyDirectories;
            var common = typeof(Migrate).GetField("_settings", Static);
            var previousCommon = common.GetValue(null);
            var tables = new[] { "Resources", "DynamicFonts" };
            var previous = new Dictionary<string, Dictionary<object, object>>();
            GameObject root = null;
            GameObject instance = null;
            foreach (var name in tables)
            {
                var table = (IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null);
                var copy = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in table) copy.Add(entry.Key, entry.Value);
                previous.Add(name, copy);
                table.Clear();
            }
            try
            {
                settings.sourceDirectory = source;
                settings.outputDirectory = output;
                settings.dependencyDirectories = dependency ? new[] { dependencySource } : Array.Empty<string>();
                var fontSource = dependency ? dependencySource : source;
                Directory.CreateDirectory(fontSource + "/assets/Fonts");
                Directory.CreateDirectory(source + "/assets/Views");
                Directory.CreateDirectory(source + "/settings");
                var editorFonts = Directory.GetFiles(Path.Combine(EditorApplication.applicationContentsPath,
                    "Resources/Fonts"), "*.ttf", SearchOption.AllDirectories);
                Assert.That(editorFonts, Is.Not.Empty);
                File.Copy(editorFonts[0], fontSource + "/assets/Fonts/Caption.ttf", true);
                File.WriteAllText(fontSource + "/assets/Fonts/package.xml", "<packageDescription id='fonttest'><resources>"
                    + "<font id='font' name='Caption.ttf' path='/'/></resources></packageDescription>");
                File.WriteAllText(source + "/assets/Views/package.xml", "<packageDescription id='viewtest'><resources>"
                    + "<component id='view' name='Caption.xml' path='/'/></resources></packageDescription>");
                File.WriteAllText(source + "/settings/Common.json", "{\"font\":\"ui://fonttestfont\"}");
                Invoke("IndexSources");
                common.SetValue(null, null);
                Assert.That(Invoke("DefaultFont"), Is.EqualTo("Caption"));
                var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
                root = new GameObject("Caption", typeof(RectTransform), typeof(TextField));
                Invoke("ConfigureText", root.GetComponent<TextField>(), new Schema.Display
                {
                    Text = "Caption", Font = explicitFont ? "ui://fonttestfont" : null,
                }, resources["viewtestview"]);
                var expected = AssetDatabase.LoadAssetAtPath<Font>(output + "/Assets/Fonts/Caption.ttf");
                Assert.That(expected, Is.Not.Null);
                Assert.That(root.GetComponent<TextField>().font, Is.SameAs(expected));
                if (input)
                    Invoke("ConfigureInput", root, root.GetComponent<TextField>(), new Schema.Display
                    {
                        Text = "Value", Prompt = "Caption", SingleLine = true,
                    });
                var path = output + "/Caption.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                if (input)
                {
                    var field = instance.GetComponent<UnityEngine.UI.InputField>();
                    Assert.That(field.textComponent.font, Is.SameAs(expected));
                    Assert.That(((TextField)field.placeholder).font, Is.SameAs(expected));
                }
                else Assert.That(instance.GetComponent<TextField>().font, Is.SameAs(expected));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                settings.sourceDirectory = previousSource;
                settings.outputDirectory = previousOutput;
                settings.dependencyDirectories = previousDependencies;
                common.SetValue(null, previousCommon);
                foreach (var name in tables)
                {
                    var table = (IDictionary)typeof(Migrate).GetField(name, Static).GetValue(null);
                    table.Clear();
                    foreach (var entry in previous[name]) table.Add(entry.Key, entry.Value);
                }
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
                if (dependency) Directory.Delete(dependencySource, true);
            }
        }
    }
}
