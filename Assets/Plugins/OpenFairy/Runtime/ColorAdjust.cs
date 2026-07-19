using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // 复刻 FairyGUI ColorFilter 的 4x5 颜色矩阵（亮度/对比度/饱和度/色相依次连乘）。
    public class ColorAdjust : UIBehaviour
    {
        private const float LumaR = 0.299f;
        private const float LumaG = 0.587f;
        private const float LumaB = 0.114f;

        public float brightness;
        public float contrast;
        public float saturation;
        public float hue;
        public Shader shader; // Migrate 烘焙

        private static readonly int ColorMatrixId = Shader.PropertyToID("_ColorMatrix");
        private static readonly int ColorOffsetId = Shader.PropertyToID("_ColorOffset");

        private BlendModeEffect _blend;
        private Material _material;
        private readonly float[] _matrix = new float[20];
        private readonly float[] _tmp = new float[20];

        public void Set(float brightnessValue, float contrastValue, float saturationValue, float hueValue)
        {
            brightness = brightnessValue;
            contrast = contrastValue;
            saturation = saturationValue;
            hue = hueValue;
            Apply();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _blend = GetComponent<BlendModeEffect>();
            Apply();
        }

        protected override void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
            base.OnDestroy();
        }

        private void Apply()
        {
            if (shader == null)
                return; // Migrate AddComponent 时 OnEnable 先于字段注入
            if (_material == null)
            {
                _material = new Material(shader);
                GetComponent<Graphic>().material = _material;
            }

            System.Array.Clear(_matrix, 0, 20);
            _matrix[0] = _matrix[6] = _matrix[12] = _matrix[18] = 1;
            AdjustBrightness(brightness);
            AdjustContrast(contrast);
            AdjustSaturation(saturation);
            AdjustHue(hue);

            var matrix = new Matrix4x4();
            matrix.SetRow(0, new Vector4(_matrix[0], _matrix[1], _matrix[2], _matrix[3]));
            matrix.SetRow(1, new Vector4(_matrix[5], _matrix[6], _matrix[7], _matrix[8]));
            matrix.SetRow(2, new Vector4(_matrix[10], _matrix[11], _matrix[12], _matrix[13]));
            matrix.SetRow(3, new Vector4(_matrix[15], _matrix[16], _matrix[17], _matrix[18]));
            _material.SetMatrix(ColorMatrixId, matrix);
            _material.SetVector(ColorOffsetId, new Vector4(_matrix[4], _matrix[9], _matrix[14], _matrix[19]));
            if (_blend != null)
                _blend.RefreshMaterial();
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
