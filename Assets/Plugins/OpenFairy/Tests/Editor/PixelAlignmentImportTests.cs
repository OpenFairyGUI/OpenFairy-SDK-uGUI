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
        public void Editor_controller_position_pages_are_baked_before_their_sign_changes()
        {
            var key = "__EditorPivotPages_" + Guid.NewGuid().ToString("N");
            var source = "Temp/" + key;
            var output = "Assets/" + key;
            var path = output + "/Assets/Views/SelectionControllerTestPanel.prefab";
            var settings = MigrationSettings.instance;
            var previous = (settings.sourceDirectory, settings.outputDirectory, settings.dependencyDirectories,
                settings.editorPivotRounding, settings.makePixelPerfect);
            var codeField = typeof(Migrate).GetField("_codeGenerationSettings", Static);
            var previousCode = codeField.GetValue(null);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", Static).GetValue(null);
            GameObject instance = null;
            try
            {
                settings.sourceDirectory=source;
                settings.outputDirectory=output;
                settings.dependencyDirectories=Array.Empty<string>();
                settings.editorPivotRounding=true;
                settings.makePixelPerfect=false;
                Directory.CreateDirectory(source + "/assets/Views");
                var file = source + "/assets/Views/SelectionControllerTestPanel.xml";
                File.WriteAllText(file,"<component size='800,100'><controller name='Page' pages='18,First,2,Second,90,Third'/>"
                    + "<displayList><graph id='item' name='Items' xy='275,0' size='125,80' pivot='0.5,0.5' type='rect'>"
                    + "<gearXY controller='Page' pages='18,2,90' values='275,0|-127,0|23.75,0' default='275,0' tween='false'/>"
                    + "</graph></displayList></component>");
                var code = Activator.CreateInstance(codeField.FieldType);
                codeField.FieldType.GetField("packageName").SetValue(code,"OpenFairy.UGUI");
                codeField.SetValue(null,code);
                var type = typeof(Migrate).GetNestedType("Resource",BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(type);
                type.GetField("Id").SetValue(resource,key);
                type.GetField("File").SetValue(resource,file);
                type.GetField("Type").SetValue(resource,Schema.ResourceKind.Component);
                type.GetField("Package").SetValue(resource,"Tests");
                type.GetField("PackageId").SetValue(resource,key);
                resources.Add(key,resource);
                using var localization = new ImportLocalizationFixture();
                localization.Prepare(resource);
                typeof(Migrate).GetMethod("BuildPrefab",Static).Invoke(null,new[]{resource});
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var panel = instance.GetComponent<SelectionControllerTestPanel>();
                Assert.That(panel.m_Items.anchoredPosition.x,Is.EqualTo(337));
                panel.m_Page.page=SelectionControllerTestPanel.Page.Second;
                Assert.That(panel.m_Items.anchoredPosition.x,Is.EqualTo(-64));
                panel.m_Page.page=SelectionControllerTestPanel.Page.Third;
                Assert.That(panel.m_Items.anchoredPosition.x,Is.EqualTo(86));
                Assert.That(panel.GetComponentsInChildren<PixelAlignment>(true),Is.Empty);
            }
            finally
            {
                if(instance!=null) UnityEngine.Object.DestroyImmediate(instance);
                (settings.sourceDirectory, settings.outputDirectory, settings.dependencyDirectories,
                    settings.editorPivotRounding, settings.makePixelPerfect)=previous;
                codeField.SetValue(null,previousCode);
                resources.Remove(key);
                prefabs.Remove(path);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source,true);
            }
        }

        [TestCase(125, 275, 1, false, true, 274)]
        [TestCase(147, 543, .8f, false, true, 557)]
        [TestCase(148, 0, .8f, false, true, 15)]
        [TestCase(125, -127, 1, false, true, -126)]
        [TestCase(125, 23.75f, 1, true, true, -40)]
        [TestCase(125, 275, 1, false, false, 274.5f)]
        public void Editor_pivot_is_truncated_before_RectTransform_conversion(float width, float x, float scale,
            bool anchor, bool pixels, float expected)
        {
            var settings = MigrationSettings.instance;
            var previous = settings.editorPivotRounding;
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                settings.editorPivotRounding = true;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var parent = (RectTransform)canvas.transform;
                parent.pivot = new Vector2(0, 1);
                parent.sizeDelta = new Vector2(2560, 1440);
                var rect = (RectTransform)new GameObject("Panel", typeof(RectTransform)).transform;
                rect.SetParent(parent, false);
                typeof(Migrate).GetMethod("SetRect", Static).Invoke(null, new object[] {rect, new Schema.Display {
                    Kind=Schema.DisplayKind.Graph,Position=new Vector2(x,0),Pivot=new Vector2(.5f,.5f),
                    Anchor=anchor,Scale=new Vector2(scale,scale)}, new Vector2(width,80), parent});
                if (pixels) rect.gameObject.AddComponent<PixelAlignment>();
                for (var i = 0; i < 8; i++) Canvas.ForceUpdateCanvases();
                var left = parent.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax))).x;
                Assert.That(left, Is.EqualTo(expected).Within(.0001));
                Assert.That(rect.rect.size, Is.EqualTo(new Vector2(width, 80)));
                Assert.That(rect.localScale.x, Is.EqualTo(scale));
            }
            finally { settings.editorPivotRounding=previous; UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Option_bakes_author_coordinates_and_can_be_removed_without_saving_render_offsets(bool pixels, bool editor)
        {
            var key = "__PixelAlignment_" + Guid.NewGuid().ToString("N");
            var outputRoot = "Assets/" + key;
            var output = outputRoot + "/Assets/Views";
            var sourceRoot = "Temp/" + key;
            var source = sourceRoot + "/assets/Views";
            var path = output + "/GroupLayoutTestPanel.prefab";
            var settings = MigrationSettings.instance;
            var oldPixels = settings.makePixelPerfect;
            var oldEditor = settings.editorPivotRounding;
            var oldSource = settings.sourceDirectory;
            var oldOutput = settings.outputDirectory;
            var oldDependencies = settings.dependencyDirectories;
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
                settings.sourceDirectory = sourceRoot;
                settings.outputDirectory = outputRoot;
                settings.dependencyDirectories = Array.Empty<string>();
                var file = source + "/GroupLayoutTestPanel.xml";
                File.WriteAllText(file, "<component size='101,83'><displayList>"
                    + "<graph id='a' name='First' xy='10.5,20.5' size='10.4,8.6' pivot='0.5,0.5' type='rect' group='g'/>"
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
                    settings.makePixelPerfect = enabled && pixels;
                    settings.editorPivotRounding = enabled && editor;
                    typeof(Migrate).GetMethod("BuildPrefab", Static).Invoke(null, new[] { resource });
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(asset.GetComponentsInChildren<PixelAlignment>(true).Length, Is.EqualTo(enabled && pixels ? 3 : 0));
                    var panel = asset.GetComponent<GroupLayoutTestPanel>();
                    Assert.That(panel.m_First.parent, Is.SameAs(panel.m_Group));
                    Assert.That(panel.m_Group.GetComponent<PixelAlignment>(), Is.Null);
                    Assert.That(panel.m_First.GetComponent<PixelAlignment>()?.coordinateSpace, Is.EqualTo(enabled && pixels ? panel.transform : null));
                    var bytes = File.ReadAllBytes(path);
                    if (enabled && previous != null) Assert.That(bytes, Is.EqualTo(previous), "Repeated publication must remain stable.");
                    previous = bytes;
                    canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    panel = UnityEngine.Object.Instantiate(asset, canvas.transform, false).GetComponent<GroupLayoutTestPanel>();
                    var root = (RectTransform)panel.transform;
                    Canvas.ForceUpdateCanvases();
                    var point = root.InverseTransformPoint(panel.m_First.TransformPoint(new Vector3(panel.m_First.rect.xMin, panel.m_First.rect.yMax)));
                    Assert.That(point.x - root.rect.xMin, Is.EqualTo(!enabled ? 10.5f : pixels ? 10 : 9.8f).Within(.0001));
                    Assert.That(root.rect.yMax - point.y, Is.EqualTo(!enabled ? 20.5f : pixels ? 20 : 19.7f).Within(.0001));
                    Assert.That(Vector2.Distance(panel.m_First.rect.size, new Vector2(10.4f, 8.6f)), Is.LessThan(.0001));
                    UnityEngine.Object.DestroyImmediate(canvas);
                    canvas = null;
                }
            }
            finally
            {
                if (canvas != null) UnityEngine.Object.DestroyImmediate(canvas);
                settings.makePixelPerfect = oldPixels;
                settings.editorPivotRounding = oldEditor;
                settings.sourceDirectory = oldSource;
                settings.outputDirectory = oldOutput;
                settings.dependencyDirectories = oldDependencies;
                codeSettings.SetValue(null, previousCodeSettings);
                prefabs.Remove(path);
                resources.Remove(key);
                AssetDatabase.DeleteAsset(outputRoot);
                Directory.Delete(sourceRoot, true);
            }
        }
    }
}
