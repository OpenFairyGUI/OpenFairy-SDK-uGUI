#if FAIRYGUI_TMPRO
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using OpenFairyTextFixtures.Localization;
using OpenFairyTextFixtures.NativeTextFixture;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class LocalizationImportTests
    {
        private const string Source = "UIProject/assets/NativeTextFixture";
        private const string Output = "Assets/UIProject/Assets/NativeTextFixture";
        private const string LocaleOutput = "Assets/__OpenFairyTextFixture/Locales";
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static readonly Type Migrator = typeof(Migrate);
        private readonly Dictionary<string, object> _fields = new();
        private readonly Dictionary<string, Dictionary<object, object>> _dictionaries = new();
        private bool _ownsSource;
        private bool _ownsAssets;

        private static object Call(string method, params object[] arguments)
        {
            try { return Migrator.GetMethod(method, All).Invoke(null, arguments); }
            catch (TargetInvocationException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static object New(string type) => Activator.CreateInstance(Migrator.GetNestedType(type, All), true);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, All).SetValue(target, value);
        private static void Static(string name, object value) => Migrator.GetField(name, All).SetValue(null, value);

        [OneTimeSetUp]
        public void Bake_real_prefabs_and_language_assets()
        {
            Assert.That(Directory.Exists(Source), Is.False, "The isolated source fixture must not replace existing content.");
            Assert.That(AssetDatabase.IsValidFolder(Output), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/__OpenFairyTextFixture"), Is.False);
            foreach (var name in new[] { "_settings", "_codeGenerationSettings", "_translations", "_localization" })
                _fields.Add(name, Migrator.GetField(name, All).GetValue(null));
            foreach (var name in new[] { "Resources", "Prefabs", "LocalViews" })
            {
                var data = (IDictionary)Migrator.GetField(name, All).GetValue(null);
                var snapshot = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in data) snapshot.Add(entry.Key, entry.Value);
                _dictionaries.Add(name, snapshot);
                data.Clear();
            }
            var script = Array.Find(AssetDatabase.FindAssets("LocalizationImportTests t:MonoScript"),
                guid => Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid)) == "LocalizationImportTests.cs");
            var inputs = Path.Combine(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(script)), "LocalizationInputs");
            Directory.CreateDirectory(Source);
            _ownsSource = true;
            Directory.CreateDirectory(LocaleOutput);
            _ownsAssets = true;
            var font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
            var fontPath = LocaleOutput + "/FixtureFont.asset";
            AssetDatabase.CreateAsset(font, fontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                if (texture != null) AssetDatabase.AddObjectToAsset(texture, font);
            foreach (var file in Directory.GetFiles(inputs, "*.xml"))
                if (Path.GetFileName(file) is not ("English.xml" or "French.xml"))
                {
                    var document = XDocument.Load(file);
                    foreach (var text in document.Descendants("text")) text.SetAttributeValue("tmpFont", fontPath);
                    document.Save(Path.Combine(Source, Path.GetFileName(file)));
                }
            var code = New("CodeGenerationSettings");
            Set(code, "packageName", "OpenFairyTextFixtures");
            Static("_codeGenerationSettings", code);
            Static("_settings", New("CommonSettings"));
            var languages = Array.CreateInstance(Migrator.GetNestedType("LanguageSource", All), 2);
            var catalogs = new FairyTranslations[2];
            var names = new[] { "English", "French" };
            for (var i = 0; i < names.Length; i++)
            {
                var language = New("LanguageSource");
                Set(language, "name", names[i]);
                Set(language, "stringsFile", Path.Combine(inputs, names[i] + ".xml"));
                languages.SetValue(language, i);
                catalogs[i] = new FairyTranslations(File.ReadAllText(Path.Combine(inputs, names[i] + ".xml")));
            }
            var localization = New("LocalizationSettings");
            Set(localization, "languages", languages);
            Static("_localization", localization);
            Static("_translations", catalogs);
            Call("IndexPackage", Source + "/package.xml");
            var resourceType = Migrator.GetNestedType("Resource", All);
            var components = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resourceType));
            var images = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(resourceType));
            foreach (DictionaryEntry entry in (IDictionary)Migrator.GetField("Resources", All).GetValue(null))
                Call("Collect", entry.Value, components, images);
            Call("BuildLocalizationPlans", components);
            foreach (var resource in components)
            {
                var name = (string)Call("ClassName", resource);
                var frozen = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(inputs)), "Runtime", "LocalizedGenerated", name + ".cs"));
                // The support assembly is a runtime friend; its three controller overrides keep internal visibility.
                Assert.That(((string)Call("ScriptCode", resource)).Replace("\r\n", "\n"), Is.EqualTo(frozen.Replace("protected internal override", "protected override").Replace("\r\n", "\n")),
                    "The compiled fixture must be produced by the actual generator.");
                Call("BuildPrefab", resource);
            }
            Call("SaveLocaleAssets", LocaleOutput);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Open() => PrefabUtility.LoadPrefabContents(Output + "/NativeTextPanel.prefab");
        private static LocaleTable French => AssetDatabase.LoadAssetAtPath<LocaleTable>(LocaleOutput + "/French.asset");

        [Test]
        public void Reopened_prefab_preserves_templates_and_shared_managed_gear_identity()
        {
            var root = Open();
            try
            {
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    foreach (var component in node.GetComponents<UnityEngine.Component>()) Assert.That(component, Is.Not.Null);
                var view = root.GetComponent<NativeTextPanel>();
                using var serialized = new SerializedObject(view);
                var row = serialized.FindProperty("_LocalizedNoticeTexts");
                Assert.That(row.FindPropertyRelative("Text").FindPropertyRelative("slots").arraySize, Is.EqualTo(1));
                var gear = (GearText<NativeTextPanel.State>)view.GetType().GetField("_localizedGear1_0", All).GetValue(view);
                Assert.That(gear, Is.SameAs(view.m_State.gears[0]));
                Assert.That(gear.pages, Is.EqualTo(new[] { NativeTextPanel.State.Short, NativeTextPanel.State.Long }));
                var controllerGear = serialized.FindProperty("m_State").FindPropertyRelative("gears").GetArrayElementAtIndex(0);
                Assert.That(serialized.FindProperty("_localizedGear1_0").managedReferenceId, Is.EqualTo(controllerGear.managedReferenceId));
                Assert.That(view.m_Choice.selectedIndex, Is.EqualTo(1));
                Assert.That(view.m_Choice.value, Is.EqualTo("two"));
                Assert.That(French.NativeTextFixture_NativeTextPanel.Override0.Text.slots, Is.Not.Empty);
                var languages = AssetDatabase.LoadAssetAtPath<LocaleSet>(LocaleOutput + "/Languages.asset");
                Assert.That(languages.Get(LocaleSet.Language.French), Is.SameAs(French));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Locale_switch_preserves_variables_pages_input_and_combobox_values()
        {
            var root = Open();
            try
            {
                var view = root.GetComponent<NativeTextPanel>();
                view.SetLocalizedNoticeVariable(NativeTextPanel.LocalizedNoticeVariable.count, "7");
                view.RefreshLocalizedNoticeText();
                view.m_State.page = NativeTextPanel.State.Long;
                view.m_Main.SetLocalizedCaptionVariable(NativeTextButton.LocalizedCaptionVariable.user, "Alice");
                view.m_Main.RefreshLocalizedCaptionText();
                view.m_Main.m_button.page = NativeTextButton.button.over;
                view.m_Edit.m_title.SetTextWithoutNotify("Player input");
                var index = view.m_Choice.selectedIndex;
                var values = view.m_Choice.values;
                view.ApplyLocale(French);
                Assert.That(view.m_Notice.text, Is.EqualTo("Longue 7"));
                Assert.That(view.m_State.page, Is.EqualTo(NativeTextPanel.State.Long));
                Assert.That(view.m_Main.title, Is.EqualTo("Bonjour Alice"));
                Assert.That(view.m_Main.m_button.page, Is.EqualTo(NativeTextButton.button.over));
                Assert.That(view.m_Edit.m_title.text, Is.EqualTo("Player input"));
                Assert.That(view.m_Edit.m_title.placeholder.GetComponent<TMP_Text>().text, Is.EqualTo("Entrez valeur"));
                Assert.That(view.m_Choice.selectedIndex, Is.EqualTo(index));
                Assert.That(view.m_Choice.values, Is.SameAs(values));
                Assert.That(view.m_Choice.value, Is.EqualTo("two"));
                Assert.That(view.m_Choice.text, Is.EqualTo("Deuxième"));
                Assert.That(view.m_Property.title, Is.EqualTo("Propriété Enfant"));
                Assert.That(view.m_Items.GetComponent<ListSource>().content.GetChild(0).GetComponent<NativeTextButton>().title, Is.EqualTo("Ligne Un"));
                Assert.That(view.m_Geared.title, Is.EqualTo("Bouger"));
                view.m_Geared.RefreshState();
                Assert.That(view.m_Geared.m_title.text, Is.EqualTo("Bouger"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Removed_authored_list_items_are_not_followed_after_the_list_has_been_rebound()
        {
            var root = Open();
            try
            {
                var view = root.GetComponent<NativeTextPanel>();
                var source = view.m_Items.GetComponent<ListSource>();
                // Fill increments version before releasing authored children. Enter the same persisted state
                // without starting a runtime pool in an EditMode test.
                typeof(ListSource).GetField("<version>k__BackingField", All).SetValue(source, 1);
                while (source.content.childCount > 0) Object.DestroyImmediate(source.content.GetChild(0).gameObject);
                Assert.DoesNotThrow(() => view.ApplyLocale(French));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void Repeated_asset_save_preserves_language_asset_guids()
        {
            var tableGuid = AssetDatabase.AssetPathToGUID(LocaleOutput + "/French.asset");
            var setGuid = AssetDatabase.AssetPathToGUID(LocaleOutput + "/Languages.asset");
            Call("SaveLocaleAssets", LocaleOutput);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.AssetPathToGUID(LocaleOutput + "/French.asset"), Is.EqualTo(tableGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(LocaleOutput + "/Languages.asset"), Is.EqualTo(setGuid));
        }

        [OneTimeTearDown]
        public void Remove_only_the_owned_fixture_and_restore_importer_state()
        {
            if (_ownsAssets)
            {
                AssetDatabase.DeleteAsset(Output);
                AssetDatabase.DeleteAsset("Assets/__OpenFairyTextFixture");
            }
            if (_ownsSource) Directory.Delete(Source, true);
            foreach (var field in _fields) Static(field.Key, field.Value);
            foreach (var item in _dictionaries)
            {
                var dictionary = (IDictionary)Migrator.GetField(item.Key, All).GetValue(null);
                dictionary.Clear();
                foreach (var entry in item.Value) dictionary.Add(entry.Key, entry.Value);
            }
        }
    }
}
#endif
