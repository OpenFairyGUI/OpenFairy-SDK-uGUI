using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public sealed class RootPrefabTests
    {
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        [SetUp]
        public void Setup()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            AssetDatabase.DeleteAsset("Assets/__OpenFairyRootPrefab");
            EditorSettings.enterPlayModeOptions = _options;
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
        }

        [UnityTest]
        public IEnumerator Baked_canvas_root_restores_native_references_on_instantiation() => RoundTrip(true);

        [UnityTest]
        public IEnumerator Baked_sibling_root_restores_its_design_size_binding_on_instantiation() => RoundTrip(false);

        private static IEnumerator RoundTrip(bool canvasRoot)
        {
            const string folder = "Assets/__OpenFairyRootPrefab";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var template = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            template.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)template.transform;
            canvasRect.sizeDelta = new Vector2(240, 160);
            var design = canvasRect;
            if (!canvasRoot)
            {
                design = (RectTransform)new GameObject("Design", typeof(RectTransform)).transform;
                design.SetParent(template.transform, false);
                design.sizeDelta = new Vector2(120, 80);
            }
            var path = folder + "/Root.prefab";
            GameObject instance = null;
            try
            {
                var root = Root.Create(design);
                Assert.That(root.soundSource, Is.Not.Null);
                PrefabUtility.SaveAsPrefabAsset(template, path, out var saved);
                Assert.That(saved, Is.True);
                Object.DestroyImmediate(template);
                template = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                yield return new EnterPlayMode(false);
                instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                yield return null;
                root = instance.GetComponentInChildren<Root>();
                Assert.That(Root.inst, Is.SameAs(root));
                Assert.That(root.rect, Is.SameAs(root.transform));
                Assert.That(root.soundSource, Is.SameAs(root.GetComponent<AudioSource>()));
                var source = canvasRoot ? (RectTransform)instance.transform : (RectTransform)instance.transform.Find("Design");
                Assert.That(root.size, Is.EqualTo(source.rect.size));
                source.sizeDelta = new Vector2(180, 100);
                yield return null;
                Assert.That(root.size, Is.EqualTo(new Vector2(180, 100)));
                Assert.That(instance.GetComponentsInChildren<Root>().Length, Is.EqualTo(1));
                Object.Destroy(instance);
                instance = null;
                yield return null;
                Assert.That(Root.inst, Is.Null);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (template != null) Object.DestroyImmediate(template);
            }
        }
    }
}
