using System;
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
    public class SoftClippingImportTests
    {
        [Test]
        public void Hidden_clip_softness_and_material_survive_prefab_reload()
        {
            var folder = "Assets/__SoftClip_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var root = new GameObject("Clip", typeof(RectTransform));
            var child = new GameObject("Image", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            child.transform.SetParent(root.transform, false);
            var path = folder + "/Clip.prefab";
            try
            {
                typeof(Migrate).GetMethod("ConfigureRectMask", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { root, "100,50" });
                typeof(Migrate).GetMethod("ConfigureSoftClipping", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { root, path });
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Assert.That(loaded.GetComponent<RectMask2D>().softness, Is.EqualTo(new Vector2Int(100, 50)));
                    var image = loaded.GetComponentInChildren<UnityEngine.UI.Image>();
                    Assert.That(image.material.shader.name, Is.EqualTo("OpenFairy/UI Blend"));
                    Assert.That(AssetDatabase.GetAssetPath(image.material), Is.EqualTo(folder + "/Clip_SoftClip.mat"));
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
