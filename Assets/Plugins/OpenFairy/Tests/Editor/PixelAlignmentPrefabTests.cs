using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public class PixelAlignmentPrefabTests
    {
        private string _folder;
        private Scene _scene;
        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__PixelPrefab_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(_folder));
            _scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, _scene);
            var parent = (RectTransform)root.transform;
            parent.pivot = new Vector2(0, 1);
            parent.sizeDelta = new Vector2(800, 300);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var child = new GameObject("Button", typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(147, 105);
            rect.localScale = new Vector3(.8f, .8f, 1);
            rect.anchoredPosition = new Vector2(616, -175);
            child.AddComponent<PixelAlignment>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, _folder + "/Original.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            AssetDatabase.DeleteAsset(_folder);
        }

        private RectTransform Rect => (RectTransform)_instance.transform.GetChild(0);
        private void Render()
        {
            for (var i = 0; i < 8; i++) Canvas.ForceUpdateCanvases();
        }
        private PropertyModification[] PositionOverrides() => PrefabUtility.GetPropertyModifications(_instance)
            .Where(m => m.target != null && m.target.name == "Button" &&
                (m.propertyPath.StartsWith("m_AnchoredPosition") || m.propertyPath.StartsWith("m_LocalPosition"))).ToArray();

        [Test]
        public void Display_offset_does_not_become_a_prefab_override_or_saved_position()
        {
            Render();
            Assert.That(Rect.anchoredPosition.x, Is.EqualTo(615.8f).Within(.0001));
            PrefabUtility.RecordPrefabInstancePropertyModifications(Rect);
            Assert.That(PositionOverrides(), Is.Empty);
            var saved = PrefabUtility.SaveAsPrefabAsset(_instance, _folder + "/Saved.prefab");
            Assert.That(((RectTransform)saved.transform.GetChild(0)).anchoredPosition.x, Is.EqualTo(616));
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(saved, _scene);
            try
            {
                Render();
                PixelAlignment.RestoreEditorPositions();
                Assert.That(((RectTransform)clone.transform.GetChild(0)).anchoredPosition.x, Is.EqualTo(616));
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Source_updates_propagate_and_real_instance_overrides_remain(bool customized)
        {
            if (customized)
            {
                Rect.anchoredPosition = new Vector2(617.25f, -175);
                PrefabUtility.RecordPrefabInstancePropertyModifications(Rect);
            }
            Render();
            var contents = PrefabUtility.LoadPrefabContents(_folder + "/Original.prefab");
            try
            {
                ((RectTransform)contents.transform.GetChild(0)).anchoredPosition = new Vector2(615, -175);
                PrefabUtility.SaveAsPrefabAsset(contents, _folder + "/Original.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            PixelAlignment.RestoreEditorPositions();
            Assert.That(Rect.anchoredPosition.x, Is.EqualTo(customized ? 617.25f : 615));
            Render();
            PrefabUtility.RecordPrefabInstancePropertyModifications(Rect);
            var overrides = PositionOverrides();
            Assert.That(overrides.Length, Is.EqualTo(customized ? 1 : 0));
            if (customized) Assert.That(overrides[0].value, Is.EqualTo("617.25"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Parent_prefab_owns_its_nested_changes_without_creating_scene_overrides(bool customized)
        {
            if (customized)
            {
                Rect.anchoredPosition = new Vector2(617.25f, -175);
                PrefabUtility.RecordPrefabInstancePropertyModifications(Rect);
            }
            var parent = new GameObject("Parent");
            SceneManager.MoveGameObjectToScene(parent, _scene);
            _instance.transform.SetParent(parent.transform, false);
            var saved = PrefabUtility.SaveAsPrefabAsset(parent, _folder + "/Parent.prefab");
            UnityEngine.Object.DestroyImmediate(parent);
            parent = (GameObject)PrefabUtility.InstantiatePrefab(saved, _scene);
            _instance = parent.transform.GetChild(0).gameObject;
            Render();
            PrefabUtility.RecordPrefabInstancePropertyModifications(Rect);
            Assert.That(PositionOverrides(), Is.Empty);
            PixelAlignment.RestoreEditorPositions();
            Assert.That(Rect.anchoredPosition.x, Is.EqualTo(customized ? 617.25f : 616));
        }

        [Test]
        public void Group_and_child_fitters_keep_the_same_driven_fields_after_prefab_reload()
        {
            Rect.GetComponent<PixelAlignment>().enabled = false;
            var own = new GameObject("GroupCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(own, _scene);
            own.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)own.transform).sizeDelta = new Vector2(800, 300);
            var child = UnityEngine.Object.Instantiate(Rect.gameObject, own.transform, false);
            var rect = (RectTransform)child.transform;
            child.name = "Button";
            var group = own.AddComponent<GroupLayout>();
            group.axis = GroupLayoutAxis.Vertical;
            var element = child.AddComponent<LayoutElement>();
            element.preferredHeight = 20;
            var fitter = child.AddComponent<AuthoredSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)own.transform);
            var saved = PrefabUtility.SaveAsPrefabAsset(own, _folder + "/Group.prefab");
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(saved, _scene);
            try
            {
                Render();
                var overrides = PrefabUtility.GetPropertyModifications(clone)
                    .Where(m => !PrefabUtility.IsDefaultOverride(m)).ToArray();
                Assert.That(overrides.Select(m => m.target.name + ":" + m.propertyPath + "=" + m.value), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [TestCase(.4f, false)]
        [TestCase(.4f, true)]
        [TestCase(1f, false)]
        [TestCase(1f, true)]
        public void Scrollbar_geometry_and_visibility_are_preview_state_in_scene_instances(float size, bool pixels)
        {
            var importing = PixelAlignment.importing;
            GameObject own = null;
            GameObject clone = null;
            try
            {
                PixelAlignment.importing = true;
                own = new GameObject("BarCanvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(own, _scene);
                own.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)own.transform).sizeDelta = new Vector2(400, 100);
                var barObject = new GameObject("Bar", typeof(RectTransform));
                barObject.transform.SetParent(own.transform, false);
                ((RectTransform)barObject.transform).sizeDelta = new Vector2(100, 10);
                var handle = new GameObject("Handle", typeof(RectTransform));
                handle.transform.SetParent(barObject.transform, false);
                if (pixels) handle.AddComponent<PixelAlignment>().editorPositionDriven = true;
                var bar = barObject.AddComponent<AuthoredScrollbar>();
                bar.handleRect = (RectTransform)handle.transform;
                bar.direction = Scrollbar.Direction.LeftToRight;
                bar.size = size;
                bar.SetValueWithoutNotify(.25f);
                Render();
                var saved = PrefabUtility.SaveAsPrefabAsset(own, _folder + "/Bar.prefab");
                UnityEngine.Object.DestroyImmediate(own);
                own = null;
                PixelAlignment.importing = false;
                clone = (GameObject)PrefabUtility.InstantiatePrefab(saved, _scene);
                Render();
                var overrides = PrefabUtility.GetPropertyModifications(clone)
                    .Where(m => !PrefabUtility.IsDefaultOverride(m)).ToArray();
                var current = clone.GetComponentInChildren<AuthoredScrollbar>(true);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(current.handleRect);
                Assert.That(overrides.Select(m => m.target.name + ":" + m.propertyPath + "=" + m.value), Is.Empty,
                    "source=" + source.anchoredPosition.x + ", live=" + current.handleRect.anchoredPosition.x
                    + ", preview=" + typeof(AuthoredScrollbar).GetField("_previewing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(current));
            }
            finally
            {
                PixelAlignment.importing = importing;
                if (own != null) UnityEngine.Object.DestroyImmediate(own);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void External_position_writes_and_native_layout_ownership_survive_preview_restoration()
        {
            var fitter = Rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Render();
            Assert.That(Rect.drivenByObject, Is.SameAs(fitter));
            Rect.anchoredPosition = new Vector2(620.125f, -175);
            Render();
            Rect.GetComponent<PixelAlignment>().enabled = false;
            Assert.That(Rect.anchoredPosition.x, Is.EqualTo(620.125f));
            Assert.That(Rect.drivenByObject, Is.SameAs(fitter));
        }
    }
}
