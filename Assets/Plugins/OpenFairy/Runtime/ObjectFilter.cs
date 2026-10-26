using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // The source and the pre-baked presentation share a parent. Input stays on the source.
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public abstract class ObjectFilter : UIBehaviour, IDisposable
    {
        [SerializeField] internal RawImage presentation;
        [SerializeField] internal CanvasGroup presentationGroup;
        [SerializeField] internal Canvas presentationCanvas;
        [SerializeField] internal Material presentationMaterial;
        [SerializeField] internal Shader captureShader;
        [SerializeField] internal int captureLayer;
        [Min(0.01f)] public float resolutionScale = 1;

        private UiObjectCapture _capture;
        private RenderTexture _texture;
        private readonly List<ObjectFilterSourceVisibility> _sources = new();
        private readonly List<ObjectFilter> _filters = new();
        private readonly List<Graphic> _graphics = new();
        public bool isPresenting { get; private set; }
        public Rect bounds { get; private set; }
        public RenderTexture texture => _texture;
        public bool isFiltering => IsFilterActive;

        protected virtual bool IsFilterActive => isActiveAndEnabled;
        protected abstract bool ShouldRender { get; }
        protected abstract float Padding { get; }
        protected abstract void ProcessPmaTexture(RenderTexture texture);

        // Call after UI changes, before the UI camera renders. The callback is synchronous.
        public void Refresh(Action<Camera, RenderTexture> render)
        {
            if (!IsFilterActive)
                return;
            // Native hierarchy order lets inner filters finish before their enclosing filter.
            GetComponentsInChildren(false, _filters);
            for (int i = _filters.Count - 1; i >= 0; i--)
                if (_filters[i].IsFilterActive)
                    _filters[i].RefreshSelf(render);
        }

        private void RefreshSelf(Action<Camera, RenderTexture> render)
        {
            if (!ShouldRender)
            {
                Release();
                return;
            }
            var source = (RectTransform)transform;
            try
            {
                if (presentation.transform.parent != source.parent)
                    throw new InvalidOperationException("Filter presentation and source must share a parent.");
                _sources.Clear();
                GetComponentsInChildren(true, _graphics);
                foreach (var graphic in _graphics)
                {
                    if (!graphic.TryGetComponent(out ObjectFilterSourceVisibility visibility))
                        visibility = graphic.gameObject.AddComponent<ObjectFilterSourceVisibility>();
                    _sources.Add(visibility);
                }
                _capture ??= new UiObjectCapture(source, captureShader, captureLayer);
                float padding = Padding;
                int width = Mathf.Max(1, Mathf.CeilToInt((source.rect.width + padding * 2) * resolutionScale));
                int height = Mathf.Max(1, Mathf.CeilToInt((source.rect.height + padding * 2) * resolutionScale));
                if (_texture == null || _texture.width != width || _texture.height != height)
                {
                    ReleaseTexture();
                    _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                        GammaUI.Contains(transform) ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default)
                    {
                        name = "UI Object Filter", hideFlags = HideFlags.HideAndDontSave,
                        antiAliasing = 1, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    };
                    _texture.Create();
                }
                _capture.Capture(_texture, render, padding, source.rect);
                bounds = _capture.bounds;
                ProcessPmaTexture(_texture);
                PositionPresentation(source);
                presentation.texture = _texture;
                presentation.material = presentationMaterial;
                presentation.raycastTarget = false;
                presentation.color = Color.white;
                presentation.maskable = !TryGetComponent(out MaskableGraphic rootGraphic) || rootGraphic.maskable;
                if (TryGetComponent(out Canvas sourceCanvas))
                {
                    presentationCanvas.enabled = sourceCanvas.enabled;
                    presentationCanvas.overrideSorting = sourceCanvas.overrideSorting;
                    presentationCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
                    presentationCanvas.sortingOrder = sourceCanvas.sortingOrder;
                    presentationCanvas.additionalShaderChannels = sourceCanvas.additionalShaderChannels;
                }
                float alpha = 1;
                bool ignoreParents = false;
                foreach (var group in GetComponents<CanvasGroup>())
                    if (group.enabled)
                    {
                        alpha *= group.alpha;
                        ignoreParents |= group.ignoreParentGroups;
                    }
                presentationGroup.alpha = alpha;
                presentationGroup.ignoreParentGroups = ignoreParents;
                presentationGroup.blocksRaycasts = false;
                presentationGroup.interactable = false;
                presentation.gameObject.SetActive(true);
                if (!isPresenting)
                {
                    isPresenting = true;
                    RefreshSourceVisibility();
                }
            }
            catch
            {
                Release();
                throw;
            }
        }

        private void PositionPresentation(RectTransform source)
        {
            var rect = presentation.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = bounds.size;
            rect.localScale = source.localScale;
            rect.localRotation = source.localRotation;
            rect.localPosition = source.localPosition + source.localRotation
                * Vector3.Scale((Vector3)bounds.center, source.localScale);
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
        }

        private void RefreshAutomatically()
        {
            if (!Application.isPlaying || !IsFilterActive)
                return;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.TryGetComponent(out ObjectFilter filter) && filter.IsFilterActive)
                    return;
            Canvas.ForceUpdateCanvases();
            Refresh(Render);
            Canvas.ForceUpdateCanvases();
        }

        private static void Render(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                camera.Render();
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = destination };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new NotSupportedException("The active render pipeline must support synchronous StandardRequest capture.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshActivation();
            if (presentation != null && !isPresenting) presentation.gameObject.SetActive(false);
        }

        protected override void OnDisable()
        {
            Application.onBeforeRender -= RefreshAutomatically;
            Release();
            base.OnDisable();
        }

        protected void RefreshActivation()
        {
            Application.onBeforeRender -= RefreshAutomatically;
            if (IsFilterActive) Application.onBeforeRender += RefreshAutomatically;
            else Release();
        }

        protected override void OnDestroy()
        {
            Application.onBeforeRender -= RefreshAutomatically;
            Release();
            base.OnDestroy();
        }

        public virtual void Dispose() => enabled = false;

        private void RefreshSourceVisibility()
        {
            for (int i = _sources.Count - 1; i >= 0; i--)
                if (_sources[i] == null) _sources.RemoveAt(i);
                else _sources[i].RefreshVisibility();
        }

        private void Release()
        {
            isPresenting = false;
            RefreshSourceVisibility();
            if (presentation != null)
            {
                presentation.texture = null;
                presentation.gameObject.SetActive(false);
            }
            _capture?.Dispose();
            _capture = null;
            ReleaseTexture();
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;
            _texture.Release();
            OwnedObject.Destroy(_texture);
            _texture = null;
        }
    }
}
