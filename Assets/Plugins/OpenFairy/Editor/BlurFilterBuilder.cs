using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Editor
{
    public static class BlurFilterBuilder
    {
        public static BlurFilter Configure(RectTransform source, RawImage presentation,
            Shader captureShader, Shader blurShader, Material presentationMaterial, int captureLayer)
        {
            if (!source.TryGetComponent(out BlurFilter filter)) filter = source.gameObject.AddComponent<BlurFilter>();
            ObjectFilterBuilder.Configure(filter, presentation, captureShader, presentationMaterial, captureLayer);
            filter.blurShader = blurShader;
            return filter;
        }
    }
}
