using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class PixelAlignmentImportTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void Option_bakes_author_coordinates_and_can_be_removed_without_saving_render_offsets()
        {
            var key = "__PixelAlignment_" + Guid.NewGuid().ToString("N");
            var output = "Assets/UIProject/Assets/" + key;
            var source = "UIProject/assets/" + key;
            var path = output + "/GroupLayoutTestPanel.prefab";
            var settings = MigrationSettings.instance;
            var oldPixels = settings.makePixelPerfect;
            var codeSettings = typeof(Migrate).GetField("_codeGenerationSettings", Static);
            var previousCodeSettings = codeSettings.GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", Static).GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(source);
            AssetDatabase.ImportAsset(output);
            GameObject canvas = null;
            try
            {
                var file = source + "/GroupLayoutTestPanel.xml";
                File.WriteAllText(file, "<component size='101,83'><displayList>"
                    + "<graph id='a' name='First' xy='10.5,20.5' size='10.4,8.6' type='rect' group='g'/>"
                    + "<graph id='b' name='Second' xy='30.25,40.75' size='12.6,9.4' type='rect' group='g'/>"
                    + "<group id='g' name='Group' xy='10.25,20.75' size='35,40'/>"
                    + "</displayList></component>");
                var code = Activator.CreateInstance(codeSettings.FieldType);
                codeSettings.FieldType.GetField("packageName").SetValue(code, "OpenFairy.UGUI");
                codeSettings.SetValue(null, code);
                var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(type);
                type.GetField("Id").SetValue(resource, key);
                type.GetField("File").SetValue(resource, file);
                type.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
                type.GetField("Package").SetValue(resource, "Tests");
                type.GetField("PackageId").SetValue(resource, key);
                type.GetField("_componentXml", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resource, Schema.Component.Load(file));
                resources.Add(key, resource);
                using var localization = new ImportLocalizationFixture();
                localization.Prepare(resource);
                byte[] previous = null;
                foreach (var enabled in new[] { true, true, false })
                {
                    settings.makePixelPerfect = enabled;
                    typeof(Migrate).GetMethod("BuildPrefab", Static).Invoke(null, new[] { resource });
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(asset.GetComponentsInChildren<PixelAlignment>(true).Length, Is.EqualTo(enabled ? 3 : 0));
                    var panel = asset.GetComponent<GroupLayoutTestPanel>();
                    Assert.That(panel.m_First.parent, Is.SameAs(panel.m_Group));
                    Assert.That(panel.m_Group.GetComponent<PixelAlignment>(), Is.Null);
                    Assert.That(panel.m_First.GetComponent<PixelAlignment>()?.coordinateSpace, Is.EqualTo(enabled ? panel.transform : null));
                    var bytes = File.ReadAllBytes(path);
                    if (enabled && previous != null) Assert.That(bytes, Is.EqualTo(previous), "Repeated publication must remain stable.");
                    previous = bytes;
                    canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    panel = UnityEngine.Object.Instantiate(asset, canvas.transform, false).GetComponent<GroupLayoutTestPanel>();
                    var root = (RectTransform)panel.transform;
                    Canvas.ForceUpdateCanvases();
                    var point = root.InverseTransformPoint(panel.m_First.TransformPoint(new Vector3(panel.m_First.rect.xMin, panel.m_First.rect.yMax)));
                    Assert.That(point.x - root.rect.xMin, Is.EqualTo(enabled ? 10 : 10.5f).Within(.0001));
                    Assert.That(root.rect.yMax - point.y, Is.EqualTo(enabled ? 20 : 20.5f).Within(.0001));
                    Assert.That(Vector2.Distance(panel.m_First.rect.size, new Vector2(10.4f, 8.6f)), Is.LessThan(.0001));
                    UnityEngine.Object.DestroyImmediate(canvas);
                    canvas = null;
                }
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                settings.makePixelPerfect = oldPixels;
                codeSettings.SetValue(null, previousCodeSettings);
                prefabs.Remove(path);
                resources.Remove(key);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }
    }
}
