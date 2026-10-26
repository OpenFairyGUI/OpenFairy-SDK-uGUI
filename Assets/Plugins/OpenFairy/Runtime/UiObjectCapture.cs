using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
#endif
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI
{
    // Captures local content. Its outer masks and root opacity belong to presentation.
    public sealed class UiObjectCapture : IDisposable
    {
        private sealed class Node
        {
            public RectTransform Rect;
            public CaptureGraphic Graphic;
            public Material Material;
            public Canvas Canvas;
            public Mask Mask;
            public RectMask2D RectMask;
            public Color Color;
            public readonly List<CanvasGroup> Groups = new();
            public int Generation;
        }

        private static readonly int[] MaskProperties =
        {
            Shader.PropertyToID("_StencilComp"), Shader.PropertyToID("_Stencil"), Shader.PropertyToID("_StencilOp"),
            Shader.PropertyToID("_StencilReadMask"), Shader.PropertyToID("_StencilWriteMask"),
            Shader.PropertyToID("_ColorMask"), Shader.PropertyToID("_UseUIAlphaClip"),
        };

        private readonly RectTransform _source;
        private readonly Shader _straightShader;
        private readonly int _layer;
        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly Dictionary<Transform, Node> _nodes = new();
        private readonly List<Transform> _removed = new();
        private readonly List<Vector3> _vertices = new();
#if FAIRYGUI_TMPRO
        private readonly List<TMP_Text> _texts = new();
#endif
        private int _generation;
        private bool _requiresStencil;
        private bool _disposed;

        public Rect bounds { get; private set; }

        public UiObjectCapture(RectTransform source, Shader straightCaptureShader, int captureLayer)
        {
            if (captureLayer < 0 || captureLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(captureLayer));
            if (straightCaptureShader == null)
                throw new ArgumentNullException(nameof(straightCaptureShader));
            _source = source;
            _straightShader = straightCaptureShader;
            _layer = captureLayer;
            _root = new GameObject("UI Object Capture", typeof(RectTransform), typeof(Canvas));
            _root.hideFlags = HideFlags.HideAndDontSave;
            _root.layer = captureLayer;
            _root.SetActive(false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.vertexColorAlwaysGammaSpace = GammaUI.Contains(source);
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2
                | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var cameraObject = new GameObject("Capture Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 1000;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << captureLayer;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            canvas.worldCamera = _camera;
        }

        // The callback renders synchronously; it owns pipeline-specific camera setup.
        public void Capture(RenderTexture destination, Action<Camera, RenderTexture> render, float padding = 0, Rect? localBounds = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UiObjectCapture));
            Canvas.ForceUpdateCanvases();
#if FAIRYGUI_TMPRO
            _source.GetComponentsInChildren(false, _texts);
            foreach (var text in _texts)
                text.ForceMeshUpdate();
#endif
            _generation++;
            _requiresStencil = false;
            bounds = _source.rect;
            Sync(_source, _root.transform, true);
            RemoveUnused();
            if (_requiresStencil && !GraphicsFormatUtility.IsStencilFormat(destination.depthStencilFormat))
                throw new ArgumentException("Capturing a stencil mask requires a depth/stencil render texture.", nameof(destination));
            if (localBounds.HasValue)
                bounds = localBounds.Value;
            float width = Mathf.Max(1, bounds.width + padding * 2);
            float height = Mathf.Max(1, bounds.height + padding * 2);
            float aspect = (float)destination.width / destination.height;
            width = Mathf.Max(width, height * aspect);
            height = width / aspect;
            bounds = new Rect(bounds.center - new Vector2(width, height) * 0.5f, new Vector2(width, height));
            var rootRect = (RectTransform)_root.transform;
            rootRect.sizeDelta = bounds.size;
            _camera.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, -100);
            _camera.orthographicSize = height * 0.5f;
            _camera.aspect = aspect;
            _camera.targetTexture = destination;
            var active = RenderTexture.active;
            try
            {
                _root.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (var node in _nodes.Values)
                    if (node.Graphic != null && node.Graphic.enabled)
                        node.Graphic.canvasRenderer.SetColor(node.Color);
                RenderTexture.active = destination;
                GL.Clear(true, true, Color.clear);
                render(_camera, destination);
            }
            finally
            {
                _root.SetActive(false);
                _camera.targetTexture = null;
                RenderTexture.active = active;
            }
        }

        private void Sync(Transform source, Transform parent, bool root)
        {
            if (!source.gameObject.activeInHierarchy)
                return;
            // A nested filter is represented by its sibling presentation, not its hidden source.
            if (source != _source && source.TryGetComponent(out ObjectFilter filter) && filter.isPresenting)
                return;
            if (!_nodes.TryGetValue(source, out var node))
            {
                var go = new GameObject(source.name, typeof(RectTransform));
                go.hideFlags = HideFlags.HideAndDontSave;
                go.layer = _layer;
                node = new Node { Rect = (RectTransform)go.transform };
                _nodes.Add(source, node);
            }
            node.Generation = _generation;
            node.Rect.SetParent(parent, false);
            node.Rect.SetAsLastSibling();
            if (source is RectTransform rect)
            {
                node.Rect.anchorMin = root ? new Vector2(0.5f, 0.5f) : rect.anchorMin;
                node.Rect.anchorMax = root ? new Vector2(0.5f, 0.5f) : rect.anchorMax;
                node.Rect.pivot = rect.pivot;
                node.Rect.sizeDelta = root ? rect.rect.size : rect.sizeDelta;
                node.Rect.anchoredPosition3D = root ? Vector3.zero : rect.anchoredPosition3D;
            }
            else
                node.Rect.localPosition = source.localPosition;
            node.Rect.localRotation = root ? Quaternion.identity : source.localRotation;
            node.Rect.localScale = root ? Vector3.one : source.localScale;

            var groups = source.GetComponents<CanvasGroup>();
            for (int i = 0; i < Mathf.Max(groups.Length, node.Groups.Count); i++)
            {
                if (i == node.Groups.Count)
                    node.Groups.Add(node.Rect.gameObject.AddComponent<CanvasGroup>());
                var proxy = node.Groups[i];
                proxy.enabled = i < groups.Length && groups[i].enabled;
                if (!proxy.enabled) continue;
                proxy.alpha = root ? 1 : groups[i].alpha;
                proxy.ignoreParentGroups = groups[i].ignoreParentGroups;
                proxy.blocksRaycasts = false;
                proxy.interactable = false;
            }
            if (source.TryGetComponent(out Canvas sourceCanvas))
            {
                if (node.Canvas == null) node.Canvas = node.Rect.gameObject.AddComponent<Canvas>();
                node.Canvas.enabled = sourceCanvas.enabled;
                node.Canvas.overrideSorting = sourceCanvas.overrideSorting;
                node.Canvas.sortingLayerID = sourceCanvas.sortingLayerID;
                node.Canvas.sortingOrder = sourceCanvas.sortingOrder;
                node.Canvas.additionalShaderChannels = _root.GetComponent<Canvas>().additionalShaderChannels;
            }
            else if (node.Canvas != null) node.Canvas.enabled = false;

            SyncGraphic(source, node);
            if (source.TryGetComponent(out Mask mask) && mask.MaskEnabled() && mask.graphic.IsActive())
            {
                _requiresStencil = true;
                if (node.Mask == null) node.Mask = node.Rect.gameObject.AddComponent<Mask>();
                node.Mask.showMaskGraphic = mask.showMaskGraphic;
                node.Mask.enabled = true;
            }
            else if (node.Mask != null) node.Mask.enabled = false;
            if (source.TryGetComponent(out RectMask2D rectMask) && rectMask.IsActive())
            {
                if (node.RectMask == null) node.RectMask = node.Rect.gameObject.AddComponent<RectMask2D>();
                node.RectMask.padding = rectMask.padding;
                node.RectMask.softness = rectMask.softness;
                node.RectMask.enabled = true;
            }
            else if (node.RectMask != null) node.RectMask.enabled = false;
            for (int i = 0; i < source.childCount; i++)
                Sync(source.GetChild(i), node.Rect, false);
        }

        private void SyncGraphic(Transform source, Node node)
        {
            if (!source.TryGetComponent(out Graphic graphic) || !graphic.isActiveAndEnabled)
            {
                if (node.Graphic != null) node.Graphic.enabled = false;
                return;
            }
            var renderer = graphic.canvasRenderer;
            node.Color = renderer.GetColor();
            bool culled = renderer.cull;
            try
            {
                renderer.cull = false;
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            finally { renderer.cull = culled; }
            var mesh = renderer.GetMesh();
            if (node.Graphic == null) node.Graphic = node.Rect.gameObject.AddComponent<CaptureGraphic>();
            node.Graphic.enabled = true;
            if (mesh == null || mesh.vertexCount == 0)
            {
                node.Graphic.Bind(mesh, null, null, graphic is MaskableGraphic empty && empty.maskable);
                return;
            }
            var baseMaterial = BaseMaterial(graphic);
            Material original = baseMaterial;
            var blendMode = BlendMode.Normal;
            foreach (var modifier in graphic.GetComponents<IMaterialModifier>())
                if (!(modifier is ObjectFilterSourceVisibility))
                {
                    if (modifier is BlendModeEffect effect && effect.isActiveAndEnabled)
                        blendMode = effect.blendMode;
                    original = modifier.GetModifiedMaterial(original);
                }
            Shader shader = original.shader;
            bool sdf = false;
#if FAIRYGUI_TMPRO
            sdf = original.HasProperty("_GradientScale")
                && (graphic is TMP_Text || graphic is TMP_SubMeshUI sub && sub.spriteAsset == null);
#endif
            if (!sdf && !original.HasProperty("_BlendSrcFactorAlpha"))
            {
                string name = shader.name;
                if (name != "UI/Default" && name != "UI/Default Font" && name != "TextMeshPro/Sprite")
                    throw new NotSupportedException($"UI capture requires a PMA-compatible adapter for shader '{name}'.");
                shader = _straightShader;
            }
            if (node.Material == null || node.Material.shader != shader)
            {
                OwnedObject.Destroy(node.Material);
                node.Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            // Same-shader copies must retain uniforms such as color matrices and offsets.
            if (shader == original.shader) node.Material.CopyPropertiesFromMaterial(original);
            else node.Material.CopyMatchingPropertiesFromMaterial(original);
            node.Material.shaderKeywords = original.shaderKeywords;
            // Keep modifier inputs native; only the owned snapshot sheds source masking.
            foreach (int property in MaskProperties)
                if (baseMaterial.HasProperty(property) && node.Material.HasProperty(property))
                    node.Material.SetFloat(property, baseMaterial.GetFloat(property));
            if (baseMaterial.IsKeywordEnabled("UNITY_UI_ALPHACLIP")) node.Material.EnableKeyword("UNITY_UI_ALPHACLIP");
            else node.Material.DisableKeyword("UNITY_UI_ALPHACLIP");
            if (!sdf && node.Material.HasProperty("_BlendSrcFactorAlpha"))
                BlendModeEffect.ApplyCaptureAlpha(node.Material, blendMode);
            Texture texture = graphic.mainTexture;
#if FAIRYGUI_TMPRO
            // TMP's primary label inherits Graphic.mainTexture (white); its atlas is in the material.
            if (graphic is TMP_Text) texture = original.mainTexture;
#endif
            node.Graphic.Bind(mesh, texture, node.Material, graphic is MaskableGraphic mg && mg.maskable);
            mesh.GetVertices(_vertices);
            var matrix = _source.worldToLocalMatrix * source.localToWorldMatrix;
            foreach (var vertex in _vertices)
            {
                var point = matrix.MultiplyPoint3x4(vertex);
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, point.x), Mathf.Min(bounds.yMin, point.y),
                    Mathf.Max(bounds.xMax, point.x), Mathf.Max(bounds.yMax, point.y));
            }
        }

        private static Material BaseMaterial(Graphic graphic)
        {
#if FAIRYGUI_TMPRO
            if (graphic is TMP_Text text) return text.fontSharedMaterial;
            if (graphic is TMP_SubMeshUI sub) return sub.sharedMaterial;
#endif
            return graphic.material;
        }

        private void RemoveUnused()
        {
            _removed.Clear();
            foreach (var pair in _nodes)
                if (pair.Value.Generation != _generation)
                    _removed.Add(pair.Key);
            foreach (var key in _removed)
            {
                var node = _nodes[key];
                OwnedObject.Destroy(node.Material);
                if (node.Rect != null)
                {
                    node.Rect.gameObject.SetActive(false);
                    OwnedObject.Destroy(node.Rect.gameObject);
                }
                _nodes.Remove(key);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_root != null) _root.SetActive(false);
            foreach (var node in _nodes.Values)
                OwnedObject.Destroy(node.Material);
            _nodes.Clear();
            OwnedObject.Destroy(_root);
        }
    }
}
