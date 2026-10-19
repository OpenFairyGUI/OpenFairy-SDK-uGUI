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

namespace OpenFairy.UGUI.Tests
{
    public sealed class FilterHierarchyFixture : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly string _key = "__FilterHierarchy_" + Guid.NewGuid().ToString("N");
        private readonly string _source;
        private readonly string _output;
        private readonly byte[] _previousPackage;
        private readonly FieldInfo _settings = typeof(Migrate).GetField("_codeGenerationSettings", Flags);
        private readonly object _previousSettings;
        private readonly IDictionary _resources = (IDictionary)typeof(Migrate).GetField("Resources", Flags).GetValue(null);
        private readonly IDictionary _prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", Flags).GetValue(null);
        private readonly Dictionary<object, object> _previousResources = new();
        private readonly Dictionary<object, object> _previousPrefabs = new();
        private readonly ImportLocalizationFixture _localization;
        public string PrefabPath { get; }
        public string ParentPath => _output + "/Parent.prefab";

        public FilterHierarchyFixture(int mode)
        {
            _source = "UIProject/assets/Tests/" + _key;
            _output = "Assets/UIProject/Assets/Tests/" + _key;
            PrefabPath = _output + "/FilterHierarchyTestPanel.prefab";
            Directory.CreateDirectory(_source); Directory.CreateDirectory(_output); AssetDatabase.ImportAsset(_output);
            const string package = "UIProject/assets/Tests/package.xml";
            _previousPackage = File.Exists(package) ? File.ReadAllBytes(package) : null;
            _previousSettings = _settings.GetValue(null);
            var settings = Activator.CreateInstance(_settings.FieldType);
            _settings.FieldType.GetField("packageName").SetValue(settings, "OpenFairy.UGUI");
            _settings.SetValue(null, settings);
            foreach (DictionaryEntry pair in _resources) _previousResources.Add(pair.Key, pair.Value);
            foreach (DictionaryEntry pair in _prefabs) _previousPrefabs.Add(pair.Key, pair.Value);
            _resources.Clear(); _prefabs.Clear();
            File.WriteAllText(package, "<packageDescription id='" + _key + "'><resources><component id='panel' name='FilterHierarchyTestPanel.xml' path='/" + _key + "/' exported='true'/></resources></packageDescription>");
            var attributes = mode == 1 ? " mask='clip'" : mode == 2 ? " overflow='scroll' scrollBar='hidden'" : "";
            var mask = mode == 1 ? "<graph id='clip' name='Mask' xy='0,0' size='80,80' type='rect' fillColor='#ffffff' lineSize='0'/>" : "";
            var group = mode == 0 ? "<group id='group' name='Group' xy='0,0' size='80,80' advanced='true' filter='color' filterData='0,0,0,0'/>" : "";
            var member = mode == 0 ? " group='group'" : "";
            File.WriteAllText(_source + "/FilterHierarchyTestPanel.xml", "<component size='80,80'" + attributes + "><displayList>" + mask + group
                + "<graph id='source' name='Source' xy='20,20' size='40,40' grayed='true' type='rect' fillColor='#ff0000' lineSize='0'" + member + "/>"
                + "<graph id='direct' name='Direct' xy='0,0' size='5,5' filter='color' filterData='0,0,0,0' type='rect' fillColor='#ff0000' lineSize='0'/></displayList></component>");
            typeof(Migrate).GetMethod("IndexPackage", Flags).Invoke(null, new object[] { package });
            var resource = _resources[_key + "panel"];
            _localization = new ImportLocalizationFixture(); _localization.Prepare(resource);
            typeof(Migrate).GetMethod("BuildPrefab", Flags).Invoke(null, new[] { resource });
        }

        public void Dispose()
        {
            _localization?.Dispose();
            _resources.Clear(); foreach (var pair in _previousResources) _resources.Add(pair.Key, pair.Value);
            _prefabs.Clear(); foreach (var pair in _previousPrefabs) _prefabs.Add(pair.Key, pair.Value);
            _settings.SetValue(null, _previousSettings);
            AssetDatabase.DeleteAsset(_output);
            if (Directory.Exists(_source)) Directory.Delete(_source, true);
            const string package = "UIProject/assets/Tests/package.xml";
            if (_previousPackage == null) File.Delete(package); else File.WriteAllBytes(package, _previousPackage);
        }
    }

    public sealed class FilterHierarchyImportTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Import_and_narrow_repair_preserve_group_mask_scroll_bindings_and_native_identity(int mode)
        {
            using var fixture = new FilterHierarchyFixture(mode);
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(fixture.PrefabPath);
            var panel = saved.GetComponent<FilterHierarchyTestPanel>();
            var filter = panel.m_Source.GetComponent<ColorAdjust>();
            Assert.That(filter.presentation.transform.parent == filter.transform.parent, Is.True, "Actual importer must move the pre-baked companion with its source.");
            Assert.That(filter.presentationGroup, Is.Not.Null);
            var direct = panel.m_Direct.GetComponent<ColorAdjust>();
            Assert.That(direct.graphic, Is.SameAs(panel.m_Direct));
            Assert.That(direct.presentation, Is.Null);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.presentation, out string _, out long presentationId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(panel.m_Source, out string _, out long sourceId);
            var guid = AssetDatabase.AssetPathToGUID(fixture.PrefabPath);
            var contents = PrefabUtility.LoadPrefabContents(fixture.PrefabPath);
            try
            {
                var broken = contents.GetComponent<FilterHierarchyTestPanel>().m_Source.GetComponent<ColorAdjust>();
                broken.presentation.transform.SetParent(contents.transform, true);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(contents, fixture.PrefabPath), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fixture.PrefabPath));
                nested.transform.SetParent(parent.transform, false);
                PrefabUtility.SaveAsPrefabAsset(parent, fixture.ParentPath);
            }
            finally { Object.DestroyImmediate(parent); }
            var parentBytes = File.ReadAllBytes(fixture.ParentPath);
            var brokenBytes = File.ReadAllBytes(fixture.PrefabPath);
            Assert.That(Migrate.RepairFilterPresentations(dryRun: true), Is.EqualTo(1));
            CollectionAssert.AreEqual(brokenBytes, File.ReadAllBytes(fixture.PrefabPath));
            Assert.That(Migrate.RepairFilterPresentations(), Is.EqualTo(1));
            AssetDatabase.ImportAsset(fixture.PrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var reloaded = PrefabUtility.LoadPrefabContents(fixture.PrefabPath);
            try
            {
                var copy = reloaded.GetComponent<FilterHierarchyTestPanel>();
                var repaired = copy.m_Source.GetComponent<ColorAdjust>();
                Assert.That(repaired.presentation.transform.parent, Is.SameAs(repaired.transform.parent));
                Assert.That(copy.m_Direct.GetComponent<ColorAdjust>().presentation, Is.Null);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(copy.m_Source.gameObject), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(reloaded); }
            saved = AssetDatabase.LoadAssetAtPath<GameObject>(fixture.PrefabPath); panel = saved.GetComponent<FilterHierarchyTestPanel>();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(panel.m_Source, out string _, out long newSourceId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(panel.m_Source.GetComponent<ColorAdjust>().presentation, out string _, out long newPresentationId);
            Assert.That((newSourceId, newPresentationId), Is.EqualTo((sourceId, presentationId)));
            Assert.That(AssetDatabase.AssetPathToGUID(fixture.PrefabPath), Is.EqualTo(guid));
            var inherited = AssetDatabase.LoadAssetAtPath<GameObject>(fixture.ParentPath).GetComponentInChildren<FilterHierarchyTestPanel>();
            var inheritedFilter = inherited.m_Source.GetComponent<ColorAdjust>();
            Assert.That(inheritedFilter.presentation.transform.parent, Is.SameAs(inheritedFilter.transform.parent));
            CollectionAssert.AreEqual(parentBytes, File.ReadAllBytes(fixture.ParentPath));
            var fixedBytes = File.ReadAllBytes(fixture.PrefabPath);
            Assert.That(Migrate.RepairFilterPresentations(), Is.Zero);
            CollectionAssert.AreEqual(fixedBytes, File.ReadAllBytes(fixture.PrefabPath));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Repair_rejects_missing_or_wrong_bindings_before_moving_any_companion(bool wrongGroup)
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            try
            {
                var source = new GameObject("Source", typeof(RectTransform), typeof(Image)); source.transform.SetParent(root.transform, false);
                var filter = ColorFilterBuilder.Configure(source, true, Shader.Find("OpenFairy/UI ColorMatrix"), Shader.Find("OpenFairy/UI Capture"), material, 31);
                var original = filter.presentation;
                if (wrongGroup) filter.presentationGroup = root.AddComponent<CanvasGroup>(); else filter.presentation = null;
                Assert.Throws<InvalidOperationException>(() => ObjectFilterBuilder.RepairPresentations(root));
                Assert.That(original.transform.parent, Is.SameAs(root.transform));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
        }
    }
}
