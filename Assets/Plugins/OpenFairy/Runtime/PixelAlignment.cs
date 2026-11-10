using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // FairyGUI rounds each object's author position to whole pixels. Here the rounding only moves the rendered mesh:
    // RectTransforms keep their layout positions, so scripts, layout and relations never see display offsets and
    // static elements cost nothing per frame.
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class PixelAlignment : UIBehaviour, IMeshModifier
    {
        [SerializeField] internal RectTransform coordinateSpace;
        internal static bool importing;
        private static readonly List<PixelAlignment> Active = new();
        private static readonly List<Graphic> Graphics = new();
        private static readonly List<RectMask2D> Masks = new();
        private static readonly Pass AlignPass = new();
        private static bool _sorted = true;
        private RectTransform _rect;
        private int _depth;
        private bool _dirty;

        // World displacement of this level's rounding. A graphic's mesh moves by its own and every ancestor's shift,
        // which equals moving each rounded transform in turn.
        private Vector3 _shift;

        // World axes when the meshes below were last built; they hold the rounding in local terms.
        private Vector3 _right, _up;

        protected override void OnEnable()
        {
            base.OnEnable();
            _rect = (RectTransform)transform;
            if (Active.Count == 0) Canvas.preWillRenderCanvases += Queue;
            Active.Add(this);
            UpdateDepth();
            _dirty = true;
#if FAIRYGUI_TMPRO
            // TMP builds its mesh without mesh modifiers; its vertices are moved just before upload.
            if (TryGetComponent(out _text)) _text.OnPreRenderText += ShiftText;
#endif
        }

        protected override void OnDisable()
        {
            Active.Remove(this);
            if (Active.Count == 0) Canvas.preWillRenderCanvases -= Queue;
#if FAIRYGUI_TMPRO
            if (_text != null) _text.OnPreRenderText -= ShiftText;
#endif
            if (!_shift.Equals(Vector3.zero))
            {
                _shift = Vector3.zero;
                Remesh();
            }
#if UNITY_EDITOR
            // A preview must end before its driver is destroyed; enabling again re-clips.
            EditorPropertyPreview.End(this);
#endif
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (coordinateSpace != null && !transform.IsChildOf(coordinateSpace))
                coordinateSpace = transform.parent as RectTransform;
            UpdateDepth();
            _dirty = true;
        }

        // A child anchored to this rect can keep its transform while its position from this rect's top-left changes.
        protected override void OnRectTransformDimensionsChange()
        {
            _dirty = true;
            for (var i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).TryGetComponent(out PixelAlignment child)) child._dirty = true;
        }

        private void UpdateDepth()
        {
            _depth = 0;
            for (var parent = transform.parent; parent != null; parent = parent.parent) _depth++;
            _sorted = false;
        }

        // A single layout element checks every instance parent-first, after the PostLayout writers above them.
        private static void Queue()
        {
            // An earlier subscriber in the same invocation can disable the last instance, e.g. a scrollbar hiding its grip.
            if (importing || Active.Count == 0) return;
            if (!_sorted)
            {
                Active.Sort(static (a, b) => a._depth.CompareTo(b._depth));
                _sorted = true;
            }
            AlignPass.transform = Active[^1]._rect;
            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(AlignPass);
        }

        private sealed class Pass : ICanvasElement
        {
            public Transform transform { get; set; }

            public void Rebuild(CanvasUpdate executing)
            {
                if (executing != CanvasUpdate.PostLayout || importing) return;
                foreach (var alignment in Active) alignment.Align();
            }

            public void LayoutComplete() { }
            public void GraphicUpdateComplete() { }
            public bool IsDestroyed() => false;
        }

        // hasChanged is also raised when an ancestor moves, so unchanged elements are skipped without measuring.
        private void Align()
        {
            if (!_dirty && !_rect.hasChanged) return;
            // A new or reparented subtree can keep its own rounding under ancestors with a different one.
            var enabled = _dirty;
            _dirty = false;
            _rect.hasChanged = false;
            var space = coordinateSpace != null ? coordinateSpace : _rect.parent as RectTransform;
            var shift = Vector3.zero;
            if (space != null && _rect.localEulerAngles == Vector3.zero)
            {
                var rect = _rect.rect;
                var topLeft = AuthorPoint(_rect, new Vector3(rect.xMin, rect.yMax), space);
                var offset = new Vector3(Mathf.Round(topLeft.x) - topLeft.x, Mathf.Round(topLeft.y) - topLeft.y);
                if (offset.x != 0 || offset.y != 0) shift = space.TransformVector(offset);
            }
            var moved = !shift.Equals(_shift);
            _shift = shift;
            // A rotation or scale, e.g. from a transition, turns the local rounding held by the meshes below.
            var right = _rect.TransformVector(Vector3.right);
            var up = _rect.TransformVector(Vector3.up);
            var turned = right != _right || up != _up;
            _right = right;
            _up = up;
            if (moved || turned && !LocalShift(_rect).Equals(Vector3.zero)) Remesh();
            if (moved || enabled) Clip();
        }

        private void Remesh()
        {
            GetComponentsInChildren(Graphics);
            foreach (var graphic in Graphics)
            {
                graphic.SetVerticesDirty();
#if FAIRYGUI_TMPRO
                // TMP only regenerates its mesh after a property change.
                if (graphic is TMPro.TMP_Text text) text.havePropertiesChanged = true;
#endif
            }
        }

        // RectMask2D clips with its layout rect in root canvas space. Its padding, which Migrate never authors, moves the
        // clip with the rounding of every enabled level. The nearest alignment owns the editor preview; inactive masks are
        // re-clipped when the alignments in their subtree are enabled again.
        private void Clip()
        {
            GetComponentsInChildren(Masks);
            foreach (var mask in Masks)
            {
                var shift = Vector3.zero;
                Transform canvas = null;
                PixelAlignment owner = null;
                for (var current = mask.transform; current != null; current = current.parent)
                {
                    if (current.TryGetComponent(out PixelAlignment alignment) && alignment.isActiveAndEnabled)
                    {
                        shift += alignment._shift;
                        if (owner == null) owner = alignment;
                    }
                    if (current.TryGetComponent(out Canvas _)) canvas = current;
                }
                if (owner == null || canvas == null) continue;
                shift = canvas.InverseTransformVector(shift);
                var padding = new Vector4(shift.x, shift.y, -shift.x, -shift.y);
                if (mask.padding == padding) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) EditorPropertyPreview.Begin(owner, mask, "m_Padding");
#endif
                mask.padding = padding;
            }
        }

        // The local displacement of a transform's content: its own and every ancestor's rounding.
        internal static Vector3 LocalShift(Transform transform)
        {
            var shift = Vector3.zero;
            for (var current = transform; current != null; current = current.parent)
                if (current.TryGetComponent(out PixelAlignment alignment)) shift += alignment._shift;
            return shift.Equals(Vector3.zero) ? shift : transform.InverseTransformVector(shift);
        }

        // SDK graphics call this last while populating, before any mesh modifier copies the vertices (e.g. MaskSource),
        // so graphics without their own PixelAlignment still follow their rounded ancestors.
        internal static void Shift(Graphic graphic, VertexHelper vh)
        {
            var local = LocalShift(graphic.transform);
            if (local.Equals(Vector3.zero)) return;
            var vertex = new UIVertex();
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.position += local;
                vh.SetUIVertex(vertex, i);
            }
        }

        // Fallback for uGUI graphics that do not populate through the SDK.
        public void ModifyMesh(VertexHelper vh)
        {
            if (isActiveAndEnabled && TryGetComponent(out Graphic graphic)
                && graphic is not (Image or MovieClip or Graph or Line or TextField or MaskShape or CaptureGraphic))
                Shift(graphic, vh);
        }

        public void ModifyMesh(Mesh mesh) { }

#if FAIRYGUI_TMPRO
        private TMPro.TMP_Text _text;

        private void ShiftText(TMPro.TMP_TextInfo info)
        {
            var local = LocalShift(transform);
            if (local.Equals(Vector3.zero)) return;
            foreach (var mesh in info.meshInfo)
                for (var i = 0; i < mesh.vertexCount; i++)
                    mesh.vertices[i] += local;
        }
#endif

        private static Vector3 AuthorPoint(RectTransform rect, Vector3 point, RectTransform space)
        {
            // Cancelling localPosition's parent-pivot offset can move an exact half-pixel across its rounding boundary.
            for (var current = rect; ;)
            {
                var parent = (RectTransform)current.parent;
                var parentRect = parent.rect;
                var anchor = current.anchorMin + Vector2.Scale(current.anchorMax - current.anchorMin, current.pivot);
                point = current.localRotation * Vector3.Scale(current.localScale, point) + current.anchoredPosition3D
                    + new Vector3(parentRect.width * anchor.x, parentRect.height * (anchor.y - 1));
                if (parent == space) return point;
                point += new Vector3(parentRect.xMin, parentRect.yMax);
                current = parent;
            }
        }

        internal static Vector3 LocalPoint(Transform rect, Vector3 point, Transform space)
        {
            for (var current = rect; current != space; current = current.parent)
                point = current.localPosition + current.localRotation * Vector3.Scale(current.localScale, point);
            return point;
        }
    }
}
