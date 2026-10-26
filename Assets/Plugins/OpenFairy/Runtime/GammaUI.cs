using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // Baked by Migrate. Canvas colors must stay in gamma before interpolation.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GammaUI : UIBehaviour
    {
        private sealed class Binding
        {
            public int users;
            public bool original;
        }

        private static readonly Dictionary<Canvas, Binding> Bindings = new();
        private readonly List<Canvas> _canvases = new();
        public static bool Contains(Transform source) => source.GetComponentInParent<GammaUI>(true) != null;

        protected override void OnEnable()
        {
            base.OnEnable();
            Bind();
        }

        protected override void OnDisable()
        {
            Unbind();
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (isActiveAndEnabled) { Unbind(); Bind(); }
        }

        private void Bind()
        {
            GetComponentsInParent(true, _canvases);
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                if (!_canvases.Contains(canvas)) _canvases.Add(canvas);
            foreach (var canvas in _canvases)
            {
                if (!Bindings.TryGetValue(canvas, out var binding))
                    Bindings.Add(canvas, binding = new Binding { original = canvas.vertexColorAlwaysGammaSpace });
                binding.users++;
                canvas.vertexColorAlwaysGammaSpace = true;
            }
        }

        private void Unbind()
        {
            foreach (var canvas in _canvases)
            {
                var binding = Bindings[canvas];
                if (--binding.users != 0) continue;
                if (canvas != null) canvas.vertexColorAlwaysGammaSpace = binding.original;
                Bindings.Remove(canvas);
            }
            _canvases.Clear();
        }
    }
}
