using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class ObjectFilterBuilder
    {
        // A direct image modifier has no captured presentation to move.
        private static RawImage CapturePresentation(ObjectFilter filter)
        {
            if (filter is ColorAdjust { graphic: not null } color)
            {
                if (color.graphic.gameObject != filter.gameObject)
                    throw new InvalidOperationException("The direct filter graphic must belong to its source.");
                return null;
            }
            var image = filter.presentation;
            if (image == null || image.gameObject == filter.gameObject
                || filter.presentationGroup != image.GetComponent<CanvasGroup>()
                || filter.presentationGroup == null || filter.captureShader == null || filter.presentationMaterial == null
                || filter.TryGetComponent<Canvas>(out _) && (filter.presentationCanvas == null || filter.presentationCanvas != image.GetComponent<Canvas>()))
                throw new InvalidOperationException($"Invalid pre-baked filter presentation binding on '{filter.name}'.");
            return image;
        }

        public static bool ReparentPresentation(ObjectFilter filter, bool worldPositionStays = true)
        {
            var image = CapturePresentation(filter);
            if (image == null || image.transform.parent == filter.transform.parent) return false;
            image.transform.SetParent(filter.transform.parent, worldPositionStays);
            image.transform.SetSiblingIndex(filter.transform.GetSiblingIndex() + 1);
            return true;
        }

        // Validate the whole loaded prefab before changing any existing companion transform.
        public static int RepairPresentations(GameObject root, bool apply = true)
        {
            var pending = new List<ObjectFilter>();
            var images = new HashSet<RawImage>();
            foreach (var filter in root.GetComponentsInChildren<ObjectFilter>(true))
            {
                var image = CapturePresentation(filter);
                if (image == null) continue;
                if (!image.transform.IsChildOf(root.transform) || !images.Add(image))
                    throw new InvalidOperationException($"Filter presentation is external or shared in '{root.name}'.");
                if (filter.transform.parent == null)
                    throw new InvalidOperationException("A captured filter needs a source parent inside its prefab.");
                if (image.transform.parent != filter.transform.parent) pending.Add(filter);
            }
            if (apply) foreach (var filter in pending) ReparentPresentation(filter);
            return pending.Count;
        }

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
