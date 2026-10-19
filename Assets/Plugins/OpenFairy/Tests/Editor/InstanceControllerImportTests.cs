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
using Page = OpenFairy.UGUI.Tests.InstanceControllerTestPanel.Page;

namespace OpenFairy.UGUI.Tests
{
    public class InstanceControllerImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static object Call(string name, params object[] values) => typeof(Migrate).GetMethod(name, PrivateStatic).Invoke(null, values);

        private static Schema.Component Definition() => new() {
            Controllers = new[] {
                new Schema.Controller { Name = "First", Pages = new[] { ("40", "Alpha"), ("7", "Beta"), ("2", "Gamma") }, Selected = 1 },
                new Schema.Controller { Name = "Second", Pages = new[] { ("9", "Alpha"), ("3", "Beta"), ("6", "Gamma") }, Selected = 2 },
            },
        };

        [Serializable]
        private sealed class RecordAction : ControllerAction<Page>
        {
            public Action<Page> record;
            protected override void Enter(Page page) => record(page);
        }

        private static InstanceControllerTestPanel Panel(GameObject root)
        {
            var panel = root.AddComponent<InstanceControllerTestPanel>();
            panel.first.Bake(Page.Second);
            panel.second.Bake(Page.Third);
            return panel;
        }

        [Test]
        public void Schema_retains_component_and_list_assignments_without_synthesizing_defaults()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component size='100,80'><displayList>"
                    + "<component id='a' controller='Second,3,First,40'/><component id='b'/>"
                    + "<list id='c'><item controllers='First,7,Second,9'/><item/></list>"
                    + "</displayList></component>");
                var xml = Schema.Component.Load(file);
                Assert.That(xml.DisplayList[0].Controller, Is.EqualTo("Second,3,First,40"));
                Assert.That(xml.DisplayList[1].Controller, Is.Null);
                Assert.That(xml.DisplayList[2].Items[0].Controllers, Is.EqualTo("First,7,Second,9"));
                Assert.That(xml.DisplayList[2].Items[1].Controllers, Is.Null);
            }
            finally { File.Delete(file); }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Missing,40,First,missing")]
        public void Absent_or_stale_assignments_keep_component_defaults(string values)
        {
            var root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var panel = Panel(root);
                Call("ConfigureInstanceControllers", panel, Definition(), values);
                Assert.That(panel.first.page, Is.EqualTo(Page.Second));
                Assert.That(panel.second.page, Is.EqualTo(Page.Third));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Page_identifiers_are_resolved_to_generated_enum_indices()
        {
            var root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var panel = Panel(root);
                Call("ConfigureInstanceControllers", panel, Definition(), "Second,3,First,2");
                Assert.That(panel.first.page, Is.EqualTo(Page.Third));
                Assert.That(panel.second.page, Is.EqualTo(Page.Second));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Ordered_assignments_run_actions_and_do_not_reapply_the_same_page()
        {
            var root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var panel = Panel(root);
                var applied = new List<string>();
                panel.first.actions = new ControllerAction<Page>[] { new RecordAction { record = page => applied.Add("first:" + page) } };
                panel.second.actions = new ControllerAction<Page>[] { new RecordAction { record = page => applied.Add("second:" + page) } };
                Call("ConfigureInstanceControllers", panel, Definition(), "Second,9,First,40,First,40,Second,3");
                CollectionAssert.AreEqual(new[] { "second:First", "first:First", "second:Second" }, applied);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("First,40,Second,3", Page.Second)]
        [TestCase("Second,3,First,40", Page.Third)]
        public void Change_page_actions_and_later_explicit_assignments_keep_source_order(string values, Page expected)
        {
            var root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var panel = Panel(root);
                panel.first.actions = new ControllerAction<Page>[] { new ChangePageAction<Page> {
                    target = panel, controller = 1, sourcePages = new[] { Page.First }, targetPages = new[] { 2 },
                } };
                Call("ConfigureInstanceControllers", panel, Definition(), values);
                Assert.That(panel.second.page, Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(false, "First,40", Page.First)]
        [TestCase(false, null, Page.Second)]
        [TestCase(true, "First,40,Second,3", Page.First)]
        [TestCase(true, null, Page.Second)]
        public void Actual_component_or_plain_list_item_import_preserves_assignment_or_default(bool list, string values, Page expected)
        {
            string folder = "Assets/__ControllerImport_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var root = new GameObject("Owner", typeof(RectTransform));
            var template = new GameObject("Panel", typeof(RectTransform));
            string key = Guid.NewGuid().ToString("N");
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", PrivateStatic).GetValue(null);
            string virtualFile = "UIProject/assets/" + key + "/Panel.xml";
            string cacheKey = Path.ChangeExtension((string)Call("AssetPath", virtualFile), ".prefab");
            try
            {
                Panel(template);
                var prefab = PrefabUtility.SaveAsPrefabAsset(template, folder + "/Panel.prefab");
                var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(resourceType);
                resourceType.GetField("File").SetValue(resource, virtualFile);
                resourceType.GetField("PackageId").SetValue(resource, "fixture");
                resourceType.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                resourceType.GetField("_componentXml", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(resource, Definition());
                resources.Add(key, resource);
                prefabs.Add(cacheKey, prefab);
                var parent = (RectTransform)root.transform;
                parent.sizeDelta = new Vector2(300, 200);
                if (list)
                {
                    Call("BuildList", root, new Schema.Display { Kind = Schema.DisplayKind.List, Size = parent.sizeDelta,
                        DefaultItem = "ui://" + key, SelectionMode = ListSelectionMode.None,
                        Items = new[] { new Schema.Extension { Controllers = values } } }, resource);
                }
                else
                {
                    var controllerData = typeof(Migrate).GetNestedType("ControllerData", BindingFlags.NonPublic);
                    var dictionary = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), controllerData));
                    Call("CreateChild", new Schema.Display { Kind = Schema.DisplayKind.Component, Name = "Child", Source = "ui://" + key,
                        Controller = values }, parent, resource, dictionary);
                }
                var instance = root.GetComponentInChildren<InstanceControllerTestPanel>();
                Assert.That(instance.first.page, Is.EqualTo(expected));
                Assert.That(prefab.GetComponent<InstanceControllerTestPanel>().first.page, Is.EqualTo(Page.Second));
            }
            finally
            {
                resources.Remove(key);
                prefabs.Remove(cacheKey);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(template);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
