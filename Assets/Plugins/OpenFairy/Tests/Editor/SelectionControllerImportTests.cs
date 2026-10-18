using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;
using Page = OpenFairy.UGUI.Tests.SelectionControllerTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class SelectionControllerImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void Import_reports_a_failed_prefab_save_instead_of_caching_null()
        {
            Import_bakes_direct_bidirectional_selection_only_for_a_named_existing_controller("Missing", false, true);
        }

        [TestCase("Page", true)]
        [TestCase("Missing", false)]
        [TestCase(null, false)]
        public void Import_bakes_direct_bidirectional_selection_only_for_a_named_existing_controller(string name, bool bound, bool invalidScript = false)
        {
            var key = "__Selection_" + Guid.NewGuid().ToString("N");
            var output = "Assets/UIProject/Assets/" + key;
            var sourceDirectory = "UIProject/assets/" + key;
            Directory.CreateDirectory(output);
            AssetDatabase.ImportAsset(output);
            Directory.CreateDirectory(sourceDirectory);
            var settingsField = typeof(Migrate).GetField("_codeGenerationSettings", PrivateStatic);
            var previousSettings = settingsField.GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", PrivateStatic).GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var itemKey = key + "item";
            var prefabPath = output + "/SelectionControllerTestPanel.prefab";
            var itemPath = output + "/SoundImportButton.prefab";
            GameObject item = null, instance = null;
            try
            {
                var settings = Activator.CreateInstance(settingsField.FieldType);
                settingsField.FieldType.GetField("packageName").SetValue(settings, "OpenFairy.UGUI");
                settingsField.SetValue(null, settings);
                var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                object Resource(string id, string file)
                {
                    var resource = Activator.CreateInstance(type);
                    type.GetField("Id").SetValue(resource, id);
                    type.GetField("File").SetValue(resource, file);
                    type.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                    type.GetField("Package").SetValue(resource, "Tests");
                    type.GetField("PackageId").SetValue(resource, key);
                    type.GetField("_componentXml", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resource, Schema.Component.Load(file));
                    return resource;
                }
                var itemFile = sourceDirectory + "/SoundImportButton.xml";
                File.WriteAllText(itemFile, "<component size='20,20' extention='Button'><Button mode='Radio'/></component>");
                var itemResource = Resource("item", itemFile);
                resources.Add(itemKey, itemResource);
                item = new GameObject("Item", typeof(RectTransform), typeof(SoundImportButton));
                ((RectTransform)item.transform).sizeDelta = new Vector2(20, 20);
                item.GetComponent<SoundImportButton>().mode = ButtonMode.Radio;
                prefabs[itemPath] = PrefabUtility.SaveAsPrefabAsset(item, itemPath);

                if (invalidScript)
                {
                    var script = MonoScript.FromMonoBehaviour(item.GetComponent<SoundImportButton>());
                    var scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
                    File.WriteAllText(itemPath, Regex.Replace(File.ReadAllText(itemPath),
                        @"m_Script:\s*\{[^}]*\bguid:\s*" + scriptGuid + @"[^}]*\}", "m_Script: {fileID: 0}"));
                    AssetDatabase.ImportAsset(itemPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    prefabs[itemPath] = AssetDatabase.LoadAssetAtPath<GameObject>(itemPath);
                }
                var file = sourceDirectory + "/SelectionControllerTestPanel.xml";
                var attribute = name == null ? "" : " selectionController='" + name + "'";
                File.WriteAllText(file, "<component size='120,80'><controller name='Page' pages='18,First,2,Second,90,Third' selected='2'/>"
                    + "<displayList><list id='items' name='Items' size='120,80' defaultItem='ui://" + itemKey + "'" + attribute
                    + "><item/><item/><item/></list></displayList></component>");
                var resource = Resource(key, file);
                resources.Add(key, resource);
                using var localization = new ImportLocalizationFixture();
                var dependencies = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type));
                dependencies.Add(itemResource); dependencies.Add(resource);
                typeof(Migrate).GetMethod("BuildLocalizationPlans", PrivateStatic).Invoke(null, new object[] { dependencies });
                if (invalidScript)
                {
                    for (var i = 0; i < 3; i++)
                        LogAssert.Expect(LogType.Error, new Regex("Error while saving Prefab:.*missing script", RegexOptions.Singleline));
                    var exception = Assert.Throws<TargetInvocationException>(() =>
                        typeof(Migrate).GetMethod("BuildPrefab", PrivateStatic).Invoke(null, new[] { resource }));
                    Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Does.Contain("failed to save prefab"));
                    Assert.That(prefabs.Contains(prefabPath), Is.False);
                    return;
                }
                typeof(Migrate).GetMethod("BuildPrefab", PrivateStatic).Invoke(null, new[] { resource });
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var missing = contents.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    Assert.That(missing, Is.Zero, "A saved list selection must preserve every attached MonoScript.");
                    foreach (var behaviour in contents.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        var script = MonoScript.FromMonoBehaviour(behaviour);
                        Assert.That(script, Is.Not.Null, behaviour.GetType().FullName);
                        Assert.That(script.GetClass(), Is.EqualTo(behaviour.GetType()));
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                var resave = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var parentPath = output + "/Parent.prefab";
                    Assert.That(PrefabUtility.SaveAsPrefabAsset(resave, parentPath, out var success), Is.Not.Null);
                    Assert.That(success, Is.True);
                    var reloaded = PrefabUtility.LoadPrefabContents(parentPath);
                    try { Assert.That(reloaded.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)), Is.Zero); }
                    finally { PrefabUtility.UnloadPrefabContents(reloaded); }
                }
                finally { PrefabUtility.UnloadPrefabContents(resave); }
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<SelectionControllerTestPanel>();
                var savedSelection = saved.m_Items.GetComponent<ListSelection>();
                Assert.That(saved.m_Page.page, Is.EqualTo(Page.Third));
                Assert.That(savedSelection.selectionOwner, bound ? Is.SameAs(saved) : Is.Null);
                Assert.That(savedSelection.selectionController, Is.EqualTo(bound ? 0 : -1));
                if (bound)
                {
                    Assert.That(saved.m_Page.gears[0], Is.TypeOf<GearListSelection<Page>>());
                    Assert.That(((GearListSelection<Page>)saved.m_Page.gears[0]).selection, Is.SameAs(savedSelection));
                    instance = UnityEngine.Object.Instantiate(saved.gameObject);
                    var panel = instance.GetComponent<SelectionControllerTestPanel>();
                    var selection = panel.m_Items.GetComponent<ListSelection>();
                    selection.Rebind();
                    Assert.That(selection.selectedIndex, Is.EqualTo(2));
                    panel.m_Page.page = Page.First;
                    Assert.That(selection.selectedIndex, Is.Zero);
                    selection.selectedIndex = 1;
                    Assert.That(panel.m_Page.page, Is.EqualTo(Page.Second));
                }
            }
            finally
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                settingsField.SetValue(null, previousSettings);
                prefabs.Remove(prefabPath); prefabs.Remove(itemPath);
                resources.Remove(key); resources.Remove(itemKey);
                AssetDatabase.DeleteAsset(output);
                if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
            }
        }
    }
}
