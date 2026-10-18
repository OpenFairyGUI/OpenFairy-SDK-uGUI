using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class LoaderContentImportTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

        [TestCase(false)]
        [TestCase(true)]
        public void Component_loader_is_baked_with_direct_references_and_survives_repeated_prefab_round_trip(bool autoSize)
        {
            var id = "__LoaderContentTest_" + Guid.NewGuid().ToString("N");
            var folder = "Assets/" + id;
            AssetDatabase.CreateFolder("Assets", id);
            var sourcePath = folder + "/Content.prefab";
            var targetPath = folder + "/Loader.prefab";
            var cacheKey = "Assets/UIProject/Assets/" + id + "/Content.prefab";
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            var prefabs = (IDictionary)typeof(Migrate).GetField("Prefabs", Static).GetValue(null);
            var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var resource = Activator.CreateInstance(type, true);
            type.GetField("File").SetValue(resource, "UIProject/assets/" + id + "/Content.xml");
            type.GetField("Type").SetValue(resource, Schema.ResourceKind.Component);
            type.GetField("PackageId").SetValue(resource, id);
            type.GetField("Id").SetValue(resource, "Content");
            var original = new GameObject("Content", typeof(RectTransform), typeof(LoaderContentTestPanel));
            var originalRect = (RectTransform)original.transform;
            originalRect.anchorMin = originalRect.anchorMax = originalRect.pivot = new Vector2(0, 1);
            originalRect.sizeDelta = new Vector2(160, 80);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            icon.transform.SetParent(original.transform, false);
            original.GetComponent<LoaderContentTestPanel>().icon = icon.GetComponent<UnityEngine.UI.Image>();
            GameObject parent = null;
            try
            {
                var input = PrefabUtility.SaveAsPrefabAsset(original, sourcePath);
                prefabs.Add(cacheKey, input);
                resources.Add(id, resource);
                var create = typeof(Migrate).GetMethod("CreateChild", Static);
                var controllers = Activator.CreateInstance(create.GetParameters()[3].ParameterType);
                var element = new Schema.Display {
                    Kind = Schema.DisplayKind.Loader, Name = "Viewport", Id = "viewport", Source = "ui://" + id,
                    Position = Vector2.zero, Size = new Vector2(100, 60), Fill = Schema.LoaderFill.Scale,
                    Align = Schema.Align.Center, VAlign = Schema.VAlign.Middle, LoaderAutoSize = autoSize,
                };
                string outputGuid = null;
                for (var pass = 0; pass < 2; ++pass)
                {
                    parent = new GameObject("Panel", typeof(RectTransform));
                    ((RectTransform)parent.transform).sizeDelta = new Vector2(200, 140);
                    var go = (GameObject)create.Invoke(null, new object[] { element, parent.transform, resource, controllers });
                    var loader = go.GetComponent<Loader>();
                    Assert.That(loader.enabled, Is.True);
                    Assert.That(loader.content, Is.TypeOf<LoaderContentTestPanel>());
                    Assert.That(loader.content, Is.Not.SameAs(input.GetComponent<LoaderContentTestPanel>()));
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(loader.content), Is.SameAs(input.GetComponent<LoaderContentTestPanel>()));
                    Assert.That(((RectTransform)loader.content.transform).rect.size, Is.EqualTo(new Vector2(160, 80)));
                    Assert.That(loader.rectTransform.rect.size, Is.EqualTo(autoSize ? new Vector2(160, 80) : new Vector2(100, 60)));
                    PrefabUtility.SaveAsPrefabAsset(parent, targetPath);
                    Object.DestroyImmediate(parent);
                    parent = null;
                    var guid = AssetDatabase.AssetPathToGUID(targetPath);
                    if (pass == 0) outputGuid = guid;
                    else Assert.That(guid, Is.EqualTo(outputGuid));
                    var reopened = PrefabUtility.LoadPrefabContents(targetPath);
                    try
                    {
                        var saved = reopened.GetComponentInChildren<Loader>();
                        Assert.That(saved.content, Is.TypeOf<LoaderContentTestPanel>());
                        var content = (LoaderContentTestPanel)saved.content;
                        Assert.That(content.icon, Is.Not.Null);
                        Assert.That(content.icon.transform.IsChildOf(content.transform), Is.True);
                        Assert.That(saved.componentSize, Is.EqualTo(new Vector2(160, 80)));
                        Assert.That(saved.transform.childCount, Is.EqualTo(1));
                        Assert.That(content.transform.localScale.x, Is.EqualTo(autoSize ? 1 : .625f).Within(.0001f));
                        Assert.That(content.transform.localScale.y, Is.EqualTo(autoSize ? 1 : .625f).Within(.0001f));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(reopened); }
                }
            }
            finally
            {
                resources.Remove(id);
                prefabs.Remove(cacheKey);
                if (parent != null) Object.DestroyImmediate(parent);
                Object.DestroyImmediate(original);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
