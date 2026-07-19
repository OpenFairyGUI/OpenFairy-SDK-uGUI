using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NativeBlendMode = UnityEngine.Rendering.BlendMode;

namespace OpenFairy.UGUI
{
    public enum BlendMode
    {
        Normal,
        None,
        Add,
        Multiply,
        Screen,
        Erase,
        Mask,
        Below,
        Off,
        One_OneMinusSrcAlpha,
        Custom1,
        Custom2,
        Custom3,
    }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class BlendModeEffect : UIBehaviour, IMaterialModifier
    {
        private static readonly int BlendSrcFactor = Shader.PropertyToID("_BlendSrcFactor");
        private static readonly int BlendDstFactor = Shader.PropertyToID("_BlendDstFactor");
        private static readonly int BlendSrcFactorAlpha = Shader.PropertyToID("_BlendSrcFactorAlpha");
        private static readonly int BlendDstFactorAlpha = Shader.PropertyToID("_BlendDstFactorAlpha");
        private static readonly int ColorOption = Shader.PropertyToID("_ColorOption");

        [SerializeField] internal Shader shader;
        [SerializeField] private BlendMode _blendMode;

        private Graphic _graphic;
        private Material _baseMaterial;
        private Material _material;

        public BlendMode blendMode
        {
            get => _blendMode;
            set
            {
                if (_blendMode == value)
                    return;
                _blendMode = value;
                if (_blendMode == BlendMode.Normal)
                    ReleaseMaterial();
                SetMaterialDirty();
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || _blendMode == BlendMode.Normal || shader == null)
                return baseMaterial;

            var targetShader = baseMaterial.HasProperty(BlendSrcFactor) ? baseMaterial.shader : shader;
            if (_material == null || _baseMaterial != baseMaterial || _material.shader != targetShader)
            {
                ReleaseMaterial();
                _material = targetShader == baseMaterial.shader ? new Material(baseMaterial) : new Material(targetShader);
                _material.hideFlags = HideFlags.HideAndDontSave;
                _baseMaterial = baseMaterial;
            }
            RefreshMaterial();
            return _material;
        }

        internal void RefreshMaterial()
        {
            if (_material == null || _baseMaterial == null)
                return;
            _material.CopyPropertiesFromMaterial(_baseMaterial);
            Apply(_material, _blendMode, _graphic is UnityEngine.UI.Text);
        }

        internal static void Apply(Material material, BlendMode blendMode, bool text = false)
        {
            var (src, dst, premultiplyAlpha) = blendMode switch
            {
                BlendMode.None => (NativeBlendMode.One, NativeBlendMode.One, false),
                BlendMode.Add => (NativeBlendMode.SrcAlpha, NativeBlendMode.One, false),
                BlendMode.Multiply => (NativeBlendMode.DstColor, NativeBlendMode.OneMinusSrcAlpha, true),
                BlendMode.Screen => (NativeBlendMode.One, NativeBlendMode.OneMinusSrcColor, true),
                BlendMode.Erase => (NativeBlendMode.Zero, NativeBlendMode.OneMinusSrcAlpha, false),
                BlendMode.Mask => (NativeBlendMode.Zero, NativeBlendMode.SrcAlpha, false),
                BlendMode.Below => (NativeBlendMode.OneMinusDstAlpha, NativeBlendMode.DstAlpha, false),
                BlendMode.Off => (NativeBlendMode.One, NativeBlendMode.Zero, false),
                BlendMode.One_OneMinusSrcAlpha => (NativeBlendMode.One, NativeBlendMode.OneMinusSrcAlpha, false),
                _ => (NativeBlendMode.SrcAlpha, NativeBlendMode.OneMinusSrcAlpha, false),
            };
            material.SetFloat(BlendSrcFactor, (float)src);
            material.SetFloat(BlendDstFactor, (float)dst);
            if (material.HasProperty(BlendSrcFactorAlpha))
            {
                material.SetFloat(BlendSrcFactorAlpha, (float)(text ? src : NativeBlendMode.One));
                material.SetFloat(BlendDstFactorAlpha, (float)(text ? dst : NativeBlendMode.One));
            }
            if (material.HasProperty(ColorOption))
                material.SetFloat(ColorOption, premultiplyAlpha ? 1 : 0);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetMaterialDirty();
            ReleaseMaterial();
        }

        protected override void OnDestroy()
        {
            ReleaseMaterial();
            base.OnDestroy();
        }

        private void SetMaterialDirty()
        {
            if (_graphic == null)
                _graphic = GetComponent<Graphic>();
            _graphic.SetMaterialDirty();
        }

        private void ReleaseMaterial()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
            }
            _material = null;
            _baseMaterial = null;
        }
    }
}
