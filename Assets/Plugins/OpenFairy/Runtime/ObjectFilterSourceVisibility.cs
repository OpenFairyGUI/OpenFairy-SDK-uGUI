using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // Added after the Graphic's native mask and other material modifiers.
    [DisallowMultipleComponent, RequireComponent(typeof(Graphic))]
    public sealed class ObjectFilterSourceVisibility : UIBehaviour, IMaterialModifier
    {
        private static readonly int ColorMask = Shader.PropertyToID("_ColorMask");
        private Material _material;
        private Material _original;

        public Material GetModifiedMaterial(Material original)
        {
            bool hidden = false;
            for (var ancestor = transform; ancestor != null; ancestor = ancestor.parent)
                if (ancestor.TryGetComponent(out ObjectFilter filter) && filter.isPresenting)
                {
                    hidden = true;
                    break;
                }
            if (!isActiveAndEnabled || !hidden)
                return original;
            if (_material == null || _original != original || _material.shader != original.shader)
            {
                Release();
                _original = original;
                _material = new Material(original) { hideFlags = HideFlags.HideAndDontSave };
            }
            _material.CopyPropertiesFromMaterial(original);
            _material.SetInt(ColorMask, 0);
            return _material;
        }

        internal void RefreshVisibility()
        {
            Release();
            GetComponent<Graphic>().SetMaterialDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshVisibility();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            RefreshVisibility();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            RefreshVisibility();
        }

        protected override void OnDisable()
        {
            RefreshVisibility();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Release();
            base.OnDestroy();
        }

        private void Release()
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
}
