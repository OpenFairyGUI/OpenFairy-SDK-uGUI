using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // One baked host owns the constraints for an authored element.
    public class Relation : UIBehaviour
    {
        public RectTransform reference;
        public RectTransform coordinateSpace;
        public bool pivotAsAnchor;
        public RelationBinding[] bindings = Array.Empty<RelationBinding>();
        [SerializeField, HideInInspector] private Vector2 _ownerSize;
        [SerializeField, HideInInspector] private Vector2 _referenceSize;
        private bool _recorded;
        private bool _applying;
        private bool _registered;

        private static readonly List<Relation> Active = new();
        private static bool _running;
        private static int _generation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            _running = false;
            _generation++;
        }

        public static Vector2 TopLeft(RectTransform rt) =>
            (Vector2)rt.localPosition + new Vector2(rt.rect.xMin, rt.rect.yMax);

        private static RelationGeometry Read(RectTransform rt, bool asAnchor, RectTransform space)
        {
            if (rt == space)
                return new RelationGeometry { size = rt.rect.size };
            var origin = space.InverseTransformPoint(rt.position);
            var size = rt.rect.size;
            var pivot = new Vector2(rt.pivot.x, 1 - rt.pivot.y);
            return new RelationGeometry {
                position = new Vector2(origin.x - space.rect.xMin - pivot.x * size.x,
                    space.rect.yMax - origin.y - pivot.y * size.y),
                size = size, pivot = pivot, pivotAsAnchor = asAnchor,
            };
        }

        private RelationGeometry ReadTarget(RelationBinding binding) => binding.target == reference
            ? new RelationGeometry { size = reference.rect.size }
            : Read(binding.target, binding.pivotAsAnchor,
                binding.target.parent == transform ? (RectTransform)transform : coordinateSpace);

        private void Write(RelationGeometry geometry)
        {
            var rt = (RectTransform)transform;
            _applying = true;
            try
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, geometry.size.x);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, geometry.size.y);
                var point = new Vector3(coordinateSpace.rect.xMin + geometry.position.x + geometry.pivot.x * geometry.size.x,
                    coordinateSpace.rect.yMax - geometry.position.y - geometry.pivot.y * geometry.size.y, 0);
                var position = coordinateSpace.TransformPoint(point);
                position.z = rt.position.z;
                rt.position = position;
                _ownerSize = geometry.size;
            }
            finally { _applying = false; }
        }

        public void Record()
        {
            if (reference == null) // AddComponent calls OnEnable before the bake assigns references.
                return;
            foreach (var binding in bindings)
                binding.previous = ReadTarget(binding);
            _ownerSize = ((RectTransform)transform).rect.size;
            _referenceSize = reference.rect.size;
            _recorded = true;
            if (!_registered && isActiveAndEnabled && Application.isPlaying
                && Array.Exists(bindings, binding => !binding.nativeAnchors))
            {
                _registered = true;
                Active.Add(this);
                if (!_running)
                    RunLoop().Forget();
            }
        }

        public void Sync()
        {
            if (!_recorded || _applying)
                return;
            var owner = Read((RectTransform)transform, pivotAsAnchor, coordinateSpace);
            var changed = false;
            foreach (var binding in bindings)
            {
                var target = ReadTarget(binding);
                if (!binding.nativeAnchors && (target.origin != binding.previous.origin || target.size != binding.previous.size))
                {
                    RelationMath.Apply(ref owner, binding.previous, target, binding, bindings,
                        binding.target == reference, binding.target.parent == transform);
                    changed = true;
                }
                binding.previous = target;
            }
            if (changed)
            {
                Write(owner);
                foreach (var binding in bindings)
                    if (binding.target.parent == transform)
                        binding.previous = ReadTarget(binding);
            }
            _referenceSize = reference.rect.size;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            if (!_recorded || _applying)
                return;
            var owner = Read((RectTransform)transform, pivotAsAnchor, coordinateSpace);
            var difference = owner.size - _ownerSize;
            _ownerSize = owner.size;
            if (difference == Vector2.zero)
                return;
            // Native anchors have already applied the parent-size constraints.
            var nativeResize = reference.rect.size != _referenceSize && Array.Exists(bindings, binding => binding.nativeAnchors);
            _referenceSize = reference.rect.size;
            if (!nativeResize)
            {
                RelationMath.SelfSize(ref owner, difference, true, bindings);
                Write(owner);
            }
        }

        protected override void OnEnable() => Record();

        protected override void OnDisable()
        {
            Active.Remove(this);
            _registered = false;
        }

        private static async UniTask RunLoop()
        {
            _running = true;
            var generation = _generation;
            while (Active.Count > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                if (generation != _generation)
                    return;
                for (var i = Active.Count - 1; i >= 0; i--)
                    Active[i].Sync();
            }
            _running = false;
        }
    }
}
