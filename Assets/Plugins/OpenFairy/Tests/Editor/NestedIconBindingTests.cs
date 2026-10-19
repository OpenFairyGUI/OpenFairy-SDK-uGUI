using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    public sealed class NestedIconBindingTests
    {
        private const string Folder = "Assets/__OpenFairyNestedIcon";

        [TearDown]
        public void Cleanup() => AssetDatabase.DeleteAsset(Folder);

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Nested_icon_is_baked_as_a_direct_loader_reference_and_survives_reload(bool parentButton, bool childButton)
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var root = new GameObject("Parent", typeof(RectTransform));
            GameObject clone = null;
            try
            {
                var parent = parentButton ? root.AddComponent<IconBindingButton>() as Component : root.AddComponent<Label>();
                var child = new GameObject("icon", typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                var nested = childButton ? child.AddComponent<IconBindingButton>() as Component : child.AddComponent<Label>();
                var leaf = new GameObject("icon", typeof(RectTransform), typeof(Loader)).GetComponent<Loader>();
                leaf.transform.SetParent(child.transform, false);
                if (nested is Label label) label.iconLoader = leaf;
                else ((ButtonBase)nested).iconLoader = leaf;
                var resolved = (Loader)typeof(Migrate).GetMethod("IconLoader", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { child });
                Assert.That(resolved, Is.SameAs(leaf));
                if (parent is Label parentLabel) parentLabel.iconLoader = resolved;
                else ((ButtonBase)parent).iconLoader = resolved;
                var texture = new Texture2D(2, 2);
                AssetDatabase.CreateAsset(texture, Folder + "/Texture.asset");
                var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
                AssetDatabase.CreateAsset(sprite, Folder + "/Sprite.asset");
                var path = Folder + "/Parent.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                if (parentButton) clone.GetComponent<ButtonBase>().icon = sprite;
                else clone.GetComponent<Label>().icon = sprite;
                Assert.That(clone.transform.Find("icon/icon").GetComponent<Loader>().sprite, Is.SameAs(sprite));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (clone != null) Object.DestroyImmediate(clone);
            }
        }
    }
}
