using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class GroupLayoutImportTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase("hz", GroupLayoutAxis.Horizontal, 6)]
        [TestCase("vt", GroupLayoutAxis.Vertical, 4)]
        public void Schema_and_builder_preserve_authored_axis_gap_and_visibility(string axis, GroupLayoutAxis expected, float gap)
        {
            var file = Path.GetTempFileName();
            var root = new GameObject("Group", typeof(RectTransform));
            try
            {
                File.WriteAllText(file, "<component size='100,80'><displayList><group id='a' advanced='true' layout='"
                    + axis + "' colGap='6' lineGap='4' excludeInvisibles='true'/></displayList></component>");
                var source = Schema.Component.Load(file).DisplayList[0];
                GroupLayoutBuilder.Configure(root, source);
                var layout = root.GetComponent<GroupLayout>();
                Assert.That(layout.axis, Is.EqualTo(expected));
                Assert.That(layout.spacing, Is.EqualTo(gap));
                Assert.That(layout.excludeInvisibles, Is.True);
                var fitter = root.GetComponent<ContentSizeFitter>();
                Assert.That(fitter.horizontalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                Assert.That(fitter.verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            }
            finally { File.Delete(file); UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("<group id='a' advanced='true'/>")]
        [TestCase("<group id='a' layout='hz'/>")]
        public void Groups_without_an_advanced_flow_keep_their_authored_geometry(string element)
        {
            var file = Path.GetTempFileName();
            var root = new GameObject("Group", typeof(RectTransform));
            try
            {
                File.WriteAllText(file, "<component size='100,80'><displayList>" + element + "</displayList></component>");
                GroupLayoutBuilder.Configure(root, Schema.Component.Load(file).DisplayList[0]);
                Assert.That(root.GetComponent<GroupLayout>(), Is.Null);
                Assert.That(root.GetComponent<ContentSizeFitter>(), Is.Null);
            }
            finally { File.Delete(file); UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Real_prefab_import_bakes_the_group_after_reparenting_its_members()
        {
            var key = "__GroupLayout_" + Guid.NewGuid().ToString("N");
            var output = "Assets/UIProject/Assets/" + key;
            var sourceDirectory = "UIProject/assets/" + key;
            Directory.CreateDirectory(output);
            AssetDatabase.ImportAsset(output);
            var settingsField = typeof(Migrate).GetField("_codeGenerationSettings", PrivateStatic);
            var previousSettings = settingsField.GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", PrivateStatic).GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", PrivateStatic).GetValue(null);
            var prefabPath = output + "/GroupLayoutTestPanel.prefab";
            GameObject instance = null;
            try
            {
                Directory.CreateDirectory(sourceDirectory);
                var file = sourceDirectory + "/GroupLayoutTestPanel.xml";
                File.WriteAllText(file, "<component size='100,60'><displayList>"
                    + "<graph id='a' name='First' xy='10,5' size='30,14' type='none' group='g'/>"
                    + "<graph id='b' name='Second' xy='46,12' size='20,12' type='none' group='g'/>"
                    + "<group id='g' name='Group' xy='10,5' size='56,19' advanced='true' layout='hz' colGap='6'/>"
                    + "</displayList></component>");
                var settings = Activator.CreateInstance(settingsField.FieldType);
                settingsField.FieldType.GetField("packageName").SetValue(settings, "OpenFairy.UGUI");
                settingsField.SetValue(null, settings);
                var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(resourceType);
                resourceType.GetField("Id").SetValue(resource, key);
                resourceType.GetField("File").SetValue(resource, file);
                resourceType.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                resourceType.GetField("Package").SetValue(resource, "Tests");
                resourceType.GetField("PackageId").SetValue(resource, key);
                resourceType.GetField("_componentXml", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resource, Schema.Component.Load(file));
                resources.Add(key, resource);
                using var localization = new ImportLocalizationFixture();
                localization.Prepare(resource);
                typeof(Migrate).GetMethod("BuildPrefab", PrivateStatic).Invoke(null, new[] { resource });
                var panel = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<GroupLayoutTestPanel>();
                Assert.That(panel.m_First.parent, Is.EqualTo(panel.m_Group));
                Assert.That(panel.m_Second.parent, Is.EqualTo(panel.m_Group));
                Assert.That(panel.m_Group.GetComponent<GroupLayout>(), Is.Not.Null);
                // Unity omits CSF-driven sizes from prefab serialization; measure the saved recipe after loading it.
                instance = UnityEngine.Object.Instantiate(panel.gameObject);
                panel = instance.GetComponent<GroupLayoutTestPanel>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.m_Group);
                Assert.That(panel.m_Group.rect.size, Is.EqualTo(new Vector2(56, 19)));
                Assert.That(panel.m_First.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(panel.m_Second.anchoredPosition, Is.EqualTo(new Vector2(36, -7)));
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                settingsField.SetValue(null, previousSettings);
                prefabs.Remove(prefabPath);
                resources.Remove(key);
                AssetDatabase.DeleteAsset(output);
                if (Directory.Exists(sourceDirectory)) Directory.Delete(sourceDirectory, true);
            }
        }
    }
}
