using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class ObjectFilterBuilder
    {
        // Both objects must belong to the prefab being baked; no runtime hierarchy wrapping.
        public static void Configure(ObjectFilter filter, RawImage presentation,
            Shader captureShader, Material presentationMaterial, int captureLayer)
        {
            var source = (RectTransform)filter.transform;
            if (source.parent != presentation.transform.parent)
                throw new System.ArgumentException("Filter presentation and source must share a parent.");
            filter.presentation = presentation;
            if (!presentation.TryGetComponent(out CanvasGroup group)) group = presentation.gameObject.AddComponent<CanvasGroup>();
            filter.presentationGroup = group;
            if (source.TryGetComponent(out Canvas _))
            {
                if (!presentation.TryGetComponent(out Canvas canvas)) canvas = presentation.gameObject.AddComponent<Canvas>();
                filter.presentationCanvas = canvas;
            }
            filter.presentationMaterial = presentationMaterial;
            filter.captureShader = captureShader;
            filter.captureLayer = captureLayer;
            presentation.raycastTarget = false;
            presentation.gameObject.layer = source.gameObject.layer;
            if (!presentation.TryGetComponent(out LayoutElement layout)) layout = presentation.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            foreach (var graphic in source.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.TryGetComponent(out ObjectFilterSourceVisibility visibility)) visibility = graphic.gameObject.AddComponent<ObjectFilterSourceVisibility>();
                while (UnityEditorInternal.ComponentUtility.MoveComponentDown(visibility)) { }
            }
            presentation.gameObject.SetActive(false);
        }
    }
}
