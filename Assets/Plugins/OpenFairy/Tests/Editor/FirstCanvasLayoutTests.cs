using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class FirstCanvasLayoutTests
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
            AssetDatabase.DeleteAsset("Assets/__OpenFairyFirstCanvas");
            EditorSettings.enterPlayModeOptions = _options;
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
        }

        [UnityTest] public IEnumerator First_scaled_canvas_layout_keeps_baked_right_alignment() => RoundTrip(false);
        [UnityTest] public IEnumerator First_scaled_canvas_layout_keeps_baked_top_extension() => RoundTrip(true);

        private static IEnumerator RoundTrip(bool extendTop)
        {
            const string folder = "Assets/__OpenFairyFirstCanvas";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var template = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = template.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var parent = (RectTransform)template.transform;
            parent.sizeDelta = new Vector2(200, 100);
            var child = (RectTransform)new GameObject("Panel", typeof(RectTransform), typeof(Relation)).transform;
            child.SetParent(parent, false);
            child.anchorMin = child.anchorMax = child.pivot = new Vector2(0, 1);
            child.sizeDelta = new Vector2(80, 300);
            child.anchoredPosition = new Vector2(20, -10);
            var relation = child.GetComponent<Relation>();
            relation.reference = parent;
            relation.coordinateSpace = parent;
            relation.bindings = new[] { new RelationBinding { target = parent, rules = new[] {
                new RelationRule { side = RelationSide.RightRight },
                new RelationRule { side = extendTop ? RelationSide.TopExtBottom : RelationSide.TopTop },
            } } };
            relation.Record();
            var path = folder + "/Canvas.prefab";
            GameObject instance = null;
            try
            {
                template.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(template, path, out var saved);
                Assert.That(saved, Is.True);
                Object.DestroyImmediate(template);
                template = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                yield return new EnterPlayMode(false);
                instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                instance.SetActive(false);
                instance.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = instance.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(400, 200);
                scaler.matchWidthOrHeight = 1;
                instance.SetActive(true);
                yield return null;
                yield return null;
                parent = (RectTransform)instance.transform;
                child = (RectTransform)parent.Find("Panel");
                Assert.That(child.anchoredPosition.x, Is.EqualTo(20 + parent.rect.width - 200).Within(.01f));
                Assert.That(child.anchoredPosition.y, Is.EqualTo(extendTop ? -10 - (parent.rect.height - 100) : -10).Within(.01f));
                Assert.That(child.rect.height, Is.EqualTo(extendTop ? 300 - (parent.rect.height - 100) : 300).Within(.01f));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (template != null) Object.DestroyImmediate(template);
            }
        }
    }
}
