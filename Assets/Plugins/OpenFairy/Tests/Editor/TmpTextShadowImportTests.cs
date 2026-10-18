#if FAIRYGUI_TMPRO
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class TmpTextShadowImportTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Import_bakes_authored_shadow_as_a_persistent_native_preset_without_mutating_the_font(bool shadow)
        {
            var key = "__Shadow_" + Guid.NewGuid().ToString("N");
            var folder = "Assets/UIProject/Assets/" + key;
            Directory.CreateDirectory(folder); AssetDatabase.ImportAsset(folder);
            var file = Path.GetTempFileName();
            var first = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            GameObject second = null;
            try
            {
                File.WriteAllText(file, "<component><displayList><text id='label' name='Label' size='150,60' text='White' fontSize='40' color='#ffffff'"
                    + (shadow ? " shadowColor='#80000000' shadowOffset='2,2'" : "") + "/></displayList></component>");
                var display = Schema.Component.Load(file).DisplayList[0];
                var migrate = typeof(OpenFairy.UGUI.Editor.Migrate);
                var type = migrate.GetNestedType("Resource", BindingFlags.NonPublic);
                var resource = Activator.CreateInstance(type);
                type.GetField("File").SetValue(resource, "UIProject/assets/" + key + "/Panel.xml");
                var configure = migrate.GetMethod("ConfigureTmpText", BindingFlags.Static | BindingFlags.NonPublic);
                var font = TMP_Settings.defaultFontAsset;
                var original = font.material;
                var crc = original.ComputeCRC();
                var text = first.GetComponent<TextMeshProUGUI>();
                configure.Invoke(null, new object[] { text, display, resource, false });
                var path = folder + "/Panel_label_TextMaterial.mat";
                Assert.That(original.ComputeCRC(), Is.EqualTo(crc));
                Assert.That(text.GetComponent<TextShadow>(), Is.Null, "TMP uses its native SDF material, not BaseMeshEffect.");
                if (!shadow)
                {
                    Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path), Is.Null);
                    Assert.That(text.fontSharedMaterial, Is.SameAs(original));
                    return;
                }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null);
                Assert.That(text.fontSharedMaterial == material, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(text.fontSharedMaterial), Is.EqualTo(path));
                Assert.That(material.IsKeywordEnabled("UNDERLAY_ON"), Is.True);
                Assert.That(material.GetColor(ShaderUtilities.ID_UnderlayColor).a, Is.EqualTo(128 / 255f).Within(.0001f));
                var factor = material.GetFloat(ShaderUtilities.ID_GradientScale) * material.GetFloat(ShaderUtilities.ID_ScaleRatio_C)
                    * text.fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
                Assert.That(material.GetFloat(ShaderUtilities.ID_UnderlayOffsetX) * factor, Is.EqualTo(2).Within(.0001f));
                Assert.That(material.GetFloat(ShaderUtilities.ID_UnderlayOffsetY) * factor, Is.EqualTo(-2).Within(.0001f));
                AssetDatabase.SaveAssetIfDirty(material);
                var bytes = File.ReadAllBytes(path); var guid = AssetDatabase.AssetPathToGUID(path);
                second = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                configure.Invoke(null, new object[] { second.GetComponent<TextMeshProUGUI>(), display, resource, false });
                AssetDatabase.SaveAssetIfDirty(material);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
                Assert.That(second.GetComponent<TextMeshProUGUI>().fontSharedMaterial == material, Is.True);
                var prefab = folder + "/Label.prefab";
                PrefabUtility.SaveAsPrefabAsset(first, prefab);
                AssetDatabase.ImportAsset(prefab, ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent<TextMeshProUGUI>().fontSharedMaterial == material, Is.True);
                Assert.That(Directory.GetFiles(folder, "*.mat").Length, Is.EqualTo(1));
                Assert.That(original.ComputeCRC(), Is.EqualTo(crc));
            }
            finally
            {
                File.Delete(file);
                UnityEngine.Object.DestroyImmediate(first);
                if (second != null) UnityEngine.Object.DestroyImmediate(second);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
#endif
