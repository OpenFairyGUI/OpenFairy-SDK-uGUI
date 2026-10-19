using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ColorAdjust : ObjectFilter, IMaterialModifier
    {
        public float brightness, contrast, saturation, hue;
        [SerializeField] internal Shader shader;
        // Only image primitives use the direct path; text and containers are captured.
        [SerializeField] internal Graphic graphic;
        [SerializeField] internal Grayed grayed;

        private Material _material;
        private Material _original;
        private readonly ColorMatrix _matrix = new();
        private bool _colorActive;
        protected override bool ShouldRender => graphic == null;
        protected override bool IsFilterActive => _colorActive || grayed != null && grayed.active;
        protected override float Padding => 0;

        public void Set(float brightnessValue, float contrastValue, float saturationValue, float hueValue)
        {
            brightness = brightnessValue;
            contrast = contrastValue;
            saturation = saturationValue;
            hue = hueValue;
            enabled = true;
            if (graphic != null) graphic.SetMaterialDirty();
        }

        public Material GetModifiedMaterial(Material original)
        {
            if (!IsFilterActive || graphic == null || shader == null) return original;
            if (_material == null || _original != original)
            {
                ReleaseMaterial();
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _original = original;
            }
            _material.CopyMatchingPropertiesFromMaterial(original);
            _material.shaderKeywords = original.shaderKeywords;
            ApplyMatrix();
            return _material;
        }

        protected override void ProcessPmaTexture(RenderTexture texture)
        {
            if (_material == null) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            ApplyMatrix();
            var descriptor = texture.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            var temporary = RenderTexture.GetTemporary(descriptor);
            var active = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, temporary, _material, 1);
                Graphics.Blit(temporary, texture);
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void ApplyMatrix()
        {
            _matrix.Set(_colorActive ? brightness : 0, _colorActive ? contrast : 0,
                _colorActive ? saturation : 0, _colorActive ? hue : 0,
                grayed != null && grayed.active);
            var values = _matrix.Values;
            var matrix = new Matrix4x4();
            for (var row = 0; row < 4; row++)
            {
                var i = row * 5;
                matrix.SetRow(row, new Vector4(values[i], values[i + 1], values[i + 2], values[i + 3]));
            }
            _material.SetMatrix("_ColorMatrix", matrix);
            _material.SetVector("_ColorOffset", new Vector4(values[4], values[9], values[14], values[19]));
        }

        protected override void OnEnable()
        {
            _colorActive = true;
            base.OnEnable();
            if (graphic != null) graphic.SetMaterialDirty();
        }
        internal void RefreshGrayed()
        {
            RefreshActivation();
            if (graphic != null) graphic.SetMaterialDirty();
        }
        public override void Dispose()
        {
            if (grayed != null) grayed.enabled = false;
            base.Dispose();
        }
        protected override void OnDisable()
        {
            _colorActive = false;
            base.OnDisable();
            if (graphic != null) graphic.SetMaterialDirty();
            ReleaseMaterial();
            if (grayed != null && grayed.active) RefreshActivation();
        }
        protected override void OnDestroy()
        {
            ReleaseMaterial();
            base.OnDestroy();
        }
        private void ReleaseMaterial()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
            _material = null;
            _original = null;
        }
    }

    internal sealed class ColorMatrix
    {
        private const float LumaR = 0.299f, LumaG = 0.587f, LumaB = 0.114f;
        private readonly float[] _matrix = new float[20];
        private readonly float[] _tmp = new float[20];
        internal float[] Values => _matrix;
        internal void Set(float brightness, float contrast, float saturation, float hue, bool grayed = false)
        {
            System.Array.Clear(_matrix, 0, 20);
            _matrix[0] = _matrix[6] = _matrix[12] = _matrix[18] = 1;
            AdjustBrightness(brightness);
            AdjustContrast(contrast);
            AdjustSaturation(saturation);
            AdjustHue(hue);
            if (grayed) AdjustSaturation(-1);
        }

        private void AdjustBrightness(float value)
        {
            Concat(0, 1, 0, 0, 0, value);
            Concat(1, 0, 1, 0, 0, value);
            Concat(2, 0, 0, 1, 0, value);
            Concat(3, 0, 0, 0, 1, 0);
        }

        private void AdjustContrast(float value)
        {
            var s = value + 1;
            var o = 128f / 255 * (1 - s);
            Concat(0, s, 0, 0, 0, o);
            Concat(1, 0, s, 0, 0, o);
            Concat(2, 0, 0, s, 0, o);
            Concat(3, 0, 0, 0, 1, 0);
        }

        private void AdjustSaturation(float value)
        {
            value += 1;
            var invSat = 1 - value;
            var invLumR = invSat * LumaR;
            var invLumG = invSat * LumaG;
            var invLumB = invSat * LumaB;
            Concat(0, invLumR + value, invLumG, invLumB, 0, 0);
            Concat(1, invLumR, invLumG + value, invLumB, 0, 0);
            Concat(2, invLumR, invLumG, invLumB + value, 0, 0);
            Concat(3, 0, 0, 0, 1, 0);
        }

        private void AdjustHue(float value)
        {
            value *= Mathf.PI;
            var cos = Mathf.Cos(value);
            var sin = Mathf.Sin(value);
            Concat(0, LumaR + cos * (1 - LumaR) + sin * -LumaR, LumaG + cos * -LumaG + sin * -LumaG, LumaB + cos * -LumaB + sin * (1 - LumaB), 0, 0);
            Concat(1, LumaR + cos * -LumaR + sin * 0.143f, LumaG + cos * (1 - LumaG) + sin * 0.14f, LumaB + cos * -LumaB + sin * -0.283f, 0, 0);
            Concat(2, LumaR + cos * -LumaR + sin * -(1 - LumaR), LumaG + cos * -LumaG + sin * LumaG, LumaB + cos * (1 - LumaB) + sin * LumaB, 0, 0);
            Concat(3, 0, 0, 0, 1, 0);
        }

        private void Concat(int index, float f0, float f1, float f2, float f3, float f4)
        {
            var i = index * 5;
            for (var x = 0; x < 5; x++)
                _tmp[i + x] = f0 * _matrix[x] + f1 * _matrix[x + 5] + f2 * _matrix[x + 10] + f3 * _matrix[x + 15] + (x == 4 ? f4 : 0);
            if (index == 3)
                System.Array.Copy(_tmp, _matrix, 20);
        }
    }
}
