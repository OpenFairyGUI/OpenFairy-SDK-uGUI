#if FAIRYGUI_TMPRO
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    public class TextShadowRefreshTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Refresh_repeated_names_resolves_authored_groups_and_sibling_order_without_relaxing_bindings(int invalid)
        {
            var key = "__DuplicateShadow_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + key;
            var output = "Assets/UIProject/Assets/" + key;
            var publish = "UIProject/settings/Publish.json";
            var previousPublish = File.Exists(publish) ? File.ReadAllBytes(publish) : null;
            var cache = typeof(Migrate).GetField("_codeGenerationSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var previousCache = cache.GetValue(null);
            Directory.CreateDirectory(source); Directory.CreateDirectory(output); AssetDatabase.ImportAsset(output);
            Directory.CreateDirectory(Path.GetDirectoryName(publish));
            File.WriteAllText(publish, "{\"codeGeneration\":{\"memberNamePrefix\":\"m_\"}}");
            var root = new GameObject("Panel", typeof(RectTransform), typeof(ShadowRefreshTestPanel));
            GameObject parent = null;
            try
            {
                File.WriteAllText(source + "/package.xml", "<packageDescription id='duplicate'><resources><component id='panel' name='Panel.xml' path='/' exported='true'/></resources></packageDescription>");
                File.WriteAllText(source + "/Panel.xml", "<component><displayList>"
                    + "<group id='firstGroup' name='FirstGroup'/>"
                    + "<text id='first' name='Title' group='firstGroup' fontSize='40' shadowColor='#000000' shadowOffset='1,1'/>"
                    + "<text id='second' name='Title' group='firstGroup' fontSize='40' shadowColor='#ff0000' shadowOffset='2,1'/>"
                    + "<group id='secondGroup' name='SecondGroup'/>"
                    + "<text id='third' name='Title' group='secondGroup' fontSize='40' shadowColor='#0000ff' shadowOffset='3,2'/>"
                    + "</displayList></component>");
                var firstGroup = new GameObject("FirstGroup", typeof(RectTransform)); firstGroup.transform.SetParent(root.transform, false);
                var secondGroup = new GameObject("SecondGroup", typeof(RectTransform)); secondGroup.transform.SetParent(root.transform, false);
                var texts = new TextMeshProUGUI[3];
                for (var i = 0; i < texts.Length; i++)
                {
                    var go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(i < 2 ? firstGroup.transform : secondGroup.transform, false);
                    texts[i] = go.GetComponent<TextMeshProUGUI>();
                    texts[i].font = TMP_Settings.defaultFontAsset; texts[i].fontSize = 40; texts[i].text = "TEXT";
                }
                root.GetComponent<ShadowRefreshTestPanel>().m_Title = texts[invalid == 1 ? 2 : 0];
                if (invalid == 2) texts[2].name = "Stale";
                var path = output + "/Panel.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                var guid = AssetDatabase.AssetPathToGUID(path);
                parent = new GameObject("Parent", typeof(RectTransform));
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(saved); nested.transform.SetParent(parent.transform, false);
                var parentPath = output + "/Parent.prefab"; PrefabUtility.SaveAsPrefabAsset(parent, parentPath);
                var parentBytes = File.ReadAllBytes(parentPath);
                var before = File.ReadAllBytes(path);
                if (invalid != 0)
                {
                    var error = Assert.Throws<InvalidOperationException>(() => Migrate.RefreshTextShadows());
                    Assert.That(error.Message, Does.Contain(invalid == 1 ? "missing or mismatched" : "published hierarchy"));
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                    return;
                }
                var localIds = new long[3];
                var originals = saved.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (var i = 0; i < originals.Length; i++)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originals[i], out string _, out localIds[i]);
                Assert.That(Migrate.RefreshTextShadows(), Is.EqualTo(3));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var actual = reloaded.GetComponentsInChildren<TextMeshProUGUI>(true);
                    var ids = new[] { "first", "second", "third" };
                    var colors = new[] { Color.black, Color.red, Color.blue };
                    for (var i = 0; i < actual.Length; i++)
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(output + "/Panel_" + ids[i] + "_TextMaterial.mat");
                        Assert.That(actual[i].fontSharedMaterial == material, Is.True, ids[i]);
                        Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
                        Assert.That(material.GetColor("_UnderlayColor"), Is.EqualTo(colors[i]));
                    }
                    Assert.That(reloaded.GetComponent<ShadowRefreshTestPanel>().m_Title, Is.SameAs(actual[0]));
                }
                finally { PrefabUtility.UnloadPrefabContents(reloaded); }
                saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var persisted = saved.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (var i = 0; i < persisted.Length; i++)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(persisted[i], out string _, out long actualId);
                    Assert.That(actualId, Is.EqualTo(localIds[i]));
                }
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                var inherited = AssetDatabase.LoadAssetAtPath<GameObject>(parentPath).GetComponentsInChildren<TextMeshProUGUI>(true);
                for (var i = 0; i < inherited.Length; i++) Assert.That(inherited[i].fontSharedMaterial == persisted[i].fontSharedMaterial, Is.True);
                CollectionAssert.AreEqual(parentBytes, File.ReadAllBytes(parentPath));
                var after = File.ReadAllBytes(path);
                Assert.That(Migrate.RefreshTextShadows(), Is.EqualTo(3));
                CollectionAssert.AreEqual(after, File.ReadAllBytes(path));
                Assert.That(Directory.GetFiles(output, "*.mat").Length, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                AssetDatabase.DeleteAsset(output); Directory.Delete(source, true);
                if (previousPublish == null) File.Delete(publish); else File.WriteAllBytes(publish, previousPublish);
                cache.SetValue(null, previousCache);
            }
        }

        [Test]
        public void Refresh_existing_direct_bindings_preserves_nested_hierarchy_and_native_identity()
        {
            var key = "__ShadowRefresh_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + key;
            var output = "Assets/UIProject/Assets/" + key;
            var publish = "UIProject/settings/Publish.json";
            var previousPublish = File.Exists(publish) ? File.ReadAllBytes(publish) : null;
            var cache = typeof(Migrate).GetField("_codeGenerationSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var previousCache = cache.GetValue(null);
            Directory.CreateDirectory(source); Directory.CreateDirectory(output); AssetDatabase.ImportAsset(output);
            Directory.CreateDirectory(Path.GetDirectoryName(publish));
            File.WriteAllText(publish, "{\"codeGeneration\":{\"memberNamePrefix\":\"m_\"}}");
            var root = new GameObject("Panel", typeof(RectTransform), typeof(ShadowRefreshTestPanel));
            GameObject parent = null;
            try
            {
                File.WriteAllText(source + "/package.xml", "<packageDescription id='test'><resources><component id='panel' name='Panel.xml' path='/' exported='true'/></resources></packageDescription>");
                File.WriteAllText(source + "/Panel.xml", "<component><displayList><group id='group' name='Group'/><text id='title' name='Title' group='group' fontSize='40' shadowColor='#000000' shadowOffset='2,2'/>"
                    + "<text id='input' name='Input' input='true' fontSize='40' shadowColor='#000000' shadowOffset='1,1'/></displayList></component>");
                var panel = root.GetComponent<ShadowRefreshTestPanel>();
                var group = new GameObject("Group", typeof(RectTransform)); group.transform.SetParent(root.transform, false);
                var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)); title.transform.SetParent(group.transform, false);
                panel.m_Title = title.GetComponent<TextMeshProUGUI>(); panel.m_Title.font = TMP_Settings.defaultFontAsset; panel.m_Title.fontSize = 40;
                var input = new GameObject("Input", typeof(RectTransform), typeof(TMP_InputField)); input.transform.SetParent(root.transform, false);
                panel.m_Input = input.GetComponent<TMP_InputField>();
                var inputText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); inputText.transform.SetParent(input.transform, false);
                var text = inputText.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = 40;
                panel.m_Input.textComponent = text;
                var prefab = output + "/Panel.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefab);
                var savedTitle = saved.GetComponent<ShadowRefreshTestPanel>().m_Title;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(savedTitle, out string titleGuid, out long titleId);
                var guid = AssetDatabase.AssetPathToGUID(prefab);
                var children = saved.GetComponentsInChildren<Transform>(true).Length;
                parent = new GameObject("Parent", typeof(RectTransform));
                var nested = (GameObject)PrefabUtility.InstantiatePrefab(saved);
                nested.transform.SetParent(parent.transform, false);
                var parentPath = output + "/Parent.prefab";
                PrefabUtility.SaveAsPrefabAsset(parent, parentPath);
                var parentBytes = File.ReadAllBytes(parentPath);
                Assert.That(Migrate.RefreshTextShadows(), Is.EqualTo(2));
                saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                var reloaded = saved.GetComponent<ShadowRefreshTestPanel>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(output + "/Panel_title_TextMaterial.mat");
                Assert.That(reloaded.m_Title.fontSharedMaterial == material, Is.True);
                Assert.That(reloaded.m_Input.textComponent.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reloaded.m_Title, out string newGuid, out long newId);
                Assert.That((newGuid, newId), Is.EqualTo((titleGuid, titleId)));
                Assert.That(AssetDatabase.AssetPathToGUID(prefab), Is.EqualTo(guid));
                Assert.That(saved.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
                Assert.That(reloaded.m_Title.transform.parent.name, Is.EqualTo("Group"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(parentPath).GetComponentInChildren<ShadowRefreshTestPanel>(true).m_Title.fontSharedMaterial == material, Is.True);
                CollectionAssert.AreEqual(parentBytes, File.ReadAllBytes(parentPath));
                var bytes = File.ReadAllBytes(prefab);
                Assert.That(Migrate.RefreshTextShadows(), Is.EqualTo(2));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(prefab));
                Assert.That(Directory.GetFiles(output, "*.mat").Length, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
                if (previousPublish == null) File.Delete(publish); else File.WriteAllBytes(publish, previousPublish);
                cache.SetValue(null, previousCache);
            }
        }
    }
}
#endif
