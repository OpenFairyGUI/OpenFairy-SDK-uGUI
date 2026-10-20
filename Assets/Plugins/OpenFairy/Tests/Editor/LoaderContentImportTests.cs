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

        [TestCase(Loader.FillType.None, 64, 48)]
        [TestCase(Loader.FillType.Scale, 84, 63)]
        [TestCase(Loader.FillType.ScaleMatchHeight, 84, 63)]
        [TestCase(Loader.FillType.ScaleMatchWidth, 101, 75.75f)]
        [TestCase(Loader.FillType.ScaleFree, 101, 63)]
        [TestCase(Loader.FillType.ScaleNoBorder, 101, 75.75f)]
        public void Sprite_padding_matches_native_image_for_every_alignment(Loader.FillType fill, float width, float height)
        {
            var folder = "Assets/__LoaderSpriteTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var texture = new Texture2D(64, 48, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 48];
            for (var y = 6; y < 36; ++y)
                for (var x = 12; x < 48; ++x)
                    pixels[y * 64 + x] = new Color32(255, 128, 64, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var path = folder + "/Sprite.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var loaderObject = new GameObject("Loader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Loader));
            loaderObject.transform.SetParent(root.transform, false);
            var loader = loaderObject.GetComponent<Loader>();
            loader.rectTransform.sizeDelta = new Vector2(101, 63);
            loader.rectTransform.pivot = new Vector2(.3f, .7f);
            loader.fill = fill;
            var referenceObject = new GameObject("Reference", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            referenceObject.transform.SetParent(loader.transform, false);
            var reference = referenceObject.GetComponent<UnityEngine.UI.Image>();
            reference.rectTransform.anchorMin = reference.rectTransform.anchorMax = reference.rectTransform.pivot = new Vector2(0, 1);
            reference.rectTransform.sizeDelta = new Vector2(width, height);
            loader.color = reference.color = new Color(.7f, .3f, .9f, .6f);
            try
            {
                foreach (var meshType in new[] { SpriteMeshType.FullRect, SpriteMeshType.Tight })
                {
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = meshType;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (meshType == SpriteMeshType.Tight)
                        Assert.That(UnityEngine.Sprites.DataUtility.GetPadding(sprite).x, Is.GreaterThan(0), "Positive control: the imported sprite must have transparent padding.");
                    loader.sprite = reference.sprite = sprite;
                    foreach (Loader.AlignType align in Enum.GetValues(typeof(Loader.AlignType)))
                    foreach (Loader.VertAlignType vertical in Enum.GetValues(typeof(Loader.VertAlignType)))
                    {
                        loader.align = align;
                        loader.vAlign = vertical;
                        reference.rectTransform.anchoredPosition = new Vector2(
                            align == Loader.AlignType.Center ? (101 - width) / 2 : align == Loader.AlignType.Right ? 101 - width : 0,
                            vertical == Loader.VertAlignType.Middle ? -(63 - height) / 2 : vertical == Loader.VertAlignType.Bottom ? height - 63 : 0);
                        loader.SetVerticesDirty();
                        loader.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                        reference.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                        var actual = loader.canvasRenderer.GetMesh();
                        var expected = reference.canvasRenderer.GetMesh();
                        Assert.That(actual.vertexCount, Is.EqualTo(4));
                        Assert.That(expected.vertexCount, Is.EqualTo(4));
                        for (var i = 0; i < 4; ++i)
                        {
                            var position = loader.transform.InverseTransformPoint(reference.transform.TransformPoint(expected.vertices[i]));
                            Assert.That(Vector3.Distance(actual.vertices[i], position), Is.LessThan(.0001f), $"{meshType}/{align}/{vertical} vertex {i}");
                            Assert.That(actual.uv[i], Is.EqualTo(expected.uv[i]));
                            Assert.That(actual.colors32[i], Is.EqualTo(expected.colors32[i]));
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(folder);
            }
        }

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
