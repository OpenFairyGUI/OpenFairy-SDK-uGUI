using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class ColorFilterBuilder
    {
        public static ColorAdjust Configure(GameObject source, bool capture, Shader colorShader,
            Shader captureShader, Material presentationMaterial, int captureLayer)
        {
            if (!source.TryGetComponent(out ColorAdjust filter)) filter = source.AddComponent<ColorAdjust>();
            filter.shader = colorShader;
            filter.graphic = capture ? null : source.GetComponent<Graphic>();
            if (capture)
            {
                var presentation = filter.presentation;
                if (presentation == null)
                {
                    var go = new GameObject("Color Filter Presentation", typeof(RectTransform), typeof(RawImage));
                    go.transform.SetParent(source.transform.parent, false);
                    presentation = go.GetComponent<RawImage>();
                }
                ObjectFilterBuilder.Configure(filter, presentation, captureShader, presentationMaterial, captureLayer);
            }
            return filter;
        }
    }
}
