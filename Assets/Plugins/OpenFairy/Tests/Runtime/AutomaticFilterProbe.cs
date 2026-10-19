using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class AutomaticFilterProbe : ObjectFilter
    {
        public readonly List<int> frames = new();
        public readonly List<Vector2> sizes = new();
        public Action<AutomaticFilterProbe> processed;
        public Color pixel { get; private set; }
        private Texture2D _pixel;
        private Material _presentation;
        protected override bool ShouldRender => true;
        protected override float Padding => 0;

        public static AutomaticFilterProbe Create(Transform parent, Vector2 size, Material presentationMaterial = null)
        {
            var source = new GameObject("Filtered Source", typeof(RectTransform));
            source.layer = parent.gameObject.layer;
            source.transform.SetParent(parent, false);
            ((RectTransform)source.transform).sizeDelta = size;
            var output = new GameObject("Presentation", typeof(RectTransform), typeof(RawImage),
                typeof(CanvasGroup), typeof(LayoutElement));
            output.layer = source.layer;
            output.transform.SetParent(parent, false);
            output.GetComponent<LayoutElement>().ignoreLayout = true;
            var filter = source.AddComponent<AutomaticFilterProbe>();
            filter.presentation = output.GetComponent<RawImage>();
            filter.presentationGroup = output.GetComponent<CanvasGroup>();
            filter.captureShader = Shader.Find("OpenFairy/UI Capture");
            filter.captureLayer = 31;
            if (presentationMaterial == null)
                filter._presentation = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            filter.presentationMaterial = presentationMaterial != null ? presentationMaterial : filter._presentation;
            output.SetActive(false);
            return filter;
        }

        protected override void ProcessPmaTexture(RenderTexture texture)
        {
            frames.Add(Time.frameCount);
            sizes.Add(bounds.size);
            if (_pixel == null) _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                _pixel.ReadPixels(new Rect(texture.width / 2, texture.height / 2, 1, 1), 0, 0);
                _pixel.Apply();
                pixel = _pixel.GetPixel(0, 0);
            }
            finally { RenderTexture.active = previous; }
            processed?.Invoke(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Application.isPlaying)
            {
                Destroy(_pixel);
                Destroy(_presentation);
            }
            else
            {
                DestroyImmediate(_pixel);
                DestroyImmediate(_presentation);
            }
        }
    }
}
