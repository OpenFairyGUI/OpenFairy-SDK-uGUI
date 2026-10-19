using UnityEngine;

namespace OpenFairy.UGUI
{
    public sealed class BlurFilter : ObjectFilter
    {
        [SerializeField] internal Shader blurShader;
        [Min(0)] public float blurSize = 1;
        private Material _material;

        protected override bool ShouldRender => blurSize > 0;
        // The nine-tap kernel reaches four tap spacings in each direction.
        protected override float Padding => blurSize * 4;

        protected override void ProcessPmaTexture(RenderTexture texture)
        {
            if (_material == null)
                _material = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            float pixelsPerUnit = texture.width / bounds.width;
            TextureBlur.Apply(texture, texture, _material, blurSize * pixelsPerUnit, TextureAlphaMode.Premultiplied);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseMaterial();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseMaterial();
        }

        private void ReleaseMaterial()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
            _material = null;
        }
    }
}
