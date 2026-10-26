using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OpenFairy.UGUI
{
    [DisallowMultipleRendererFeature("OpenFairy Gamma UI")]
    public sealed class GammaUIRendererFeature : ScriptableRendererFeature
    {
        private GammaPass _pass;
        private Camera _uiCamera;
        private RenderTargetIdentifier _colorTarget, _depthTarget;
        private List<Camera> _cameras;
        private bool _changedOverlay, _previousOverlay;
        public override void Create()
        {
            Unsubscribe();
            _pass = new GammaPass(this) { renderPassEvent = RenderPassEvent.AfterRendering };
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endCameraRendering += EndCamera;
            RenderPipelineManager.endContextRendering += EndContext;
        }

        // URP sorts this same list after beginContextRendering. Read its final order
        // in AddRenderPasses so multiple base cameras do not draw the UI twice.
        private void BeginContext(ScriptableRenderContext context, List<Camera> cameras) => _cameras = cameras;
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreOverlay();
            if (_uiCamera != camera) return;
            _uiCamera = null;
            // The pipeline has now submitted every scene/gizmo RendererList. A
            // Submit inside Render Graph would invalidate lists used by later passes.
            var commands = CommandBufferPool.Get("OpenFairy Gamma UI");
            try
            {
                bool previousWrite = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = false;
                    commands.SetRenderTarget(_colorTarget, _depthTarget);
                    commands.DrawRendererList(context.CreateUIOverlayRendererList(camera, UISubset.UIToolkit_UGUI));
                    context.ExecuteCommandBuffer(commands);
                    commands.Clear();
                    context.Submit();
                }
                finally { GL.sRGBWrite = previousWrite; }
                // Keep IMGUI/software cursors after uGUI, using their original state.
                commands.SetRenderTarget(_colorTarget, _depthTarget);
                commands.DrawRendererList(context.CreateUIOverlayRendererList(camera, UISubset.LowLevel));
                context.ExecuteCommandBuffer(commands);
                commands.Clear();
                context.Submit();
            }
            finally { CommandBufferPool.Release(commands); }
        }

        private void EndContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            RestoreOverlay();
            _cameras = null;
            _uiCamera = null;
        }

        private bool IsLastBaseCamera(Camera camera)
        {
            if (_cameras == null) return false;
            for (int index = _cameras.Count - 1; index >= 0; index--)
            {
                var candidate = _cameras[index];
                if (!candidate.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType == CameraRenderType.Base)
                    return candidate == camera;
            }
            return false;
        }

        private void RestoreOverlay()
        {
            if (!_changedOverlay) return;
            SupportedRenderingFeatures.active.rendersUIOverlay = _previousOverlay;
            _changedOverlay = false;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData;
            if (renderer is not UniversalRenderer || QualitySettings.activeColorSpace != ColorSpace.Linear
                || camera.cameraType != CameraType.Game
                || camera.targetTexture != null || !camera.resolveFinalTarget || camera.isHDROutputActive || camera.xr.enabled
                || !IsLastBaseCamera(camera.renderType == CameraRenderType.Overlay ? camera.baseCamera : camera.camera)) return;
            var api = SystemInfo.graphicsDeviceType;
            if (api != GraphicsDeviceType.Direct3D11 && api != GraphicsDeviceType.Direct3D12) return;
            _pass.camera = camera.camera;
            renderer.EnqueuePass(_pass);
        }

        private void Unsubscribe()
        {
            RenderPipelineManager.beginContextRendering -= BeginContext;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.endContextRendering -= EndContext;
            RestoreOverlay();
            _cameras = null;
            _uiCamera = null;
        }

        protected override void Dispose(bool disposing) => Unsubscribe();

        private sealed class GammaPass : ScriptableRenderPass
        {
            private readonly GammaUIRendererFeature _owner;
            public Camera camera;
            public GammaPass(GammaUIRendererFeature owner) => _owner = owner;
            private sealed class Data
            {
                public GammaUIRendererFeature owner;
                public Camera camera;
                public TextureHandle color, depth;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                if (!frameData.Get<UniversalCameraData>().rendersOverlayUI) return;
                var resources = frameData.Get<UniversalResourceData>();
                // Suppress URP's later default Overlay passes while recording only.
                // Restore before execution, so Unity does not also draw native fallback UI.
                _owner._previousOverlay = SupportedRenderingFeatures.active.rendersUIOverlay;
                _owner._changedOverlay = true;
                SupportedRenderingFeatures.active.rendersUIOverlay = false;
                using var builder = graph.AddUnsafePass<Data>("OpenFairy Gamma UI", out var data);
                data.owner = _owner; data.camera = camera;
                // Imported screen targets remain valid after Render Graph releases
                // its transient resources. No target or texture is allocated here.
                data.color = resources.backBufferColor; data.depth = resources.backBufferDepth;
                builder.UseTexture(data.color, AccessFlags.ReadWrite);
                builder.UseTexture(data.depth, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (Data data, UnsafeGraphContext graphContext) =>
                {
                    data.owner.RestoreOverlay();
                    RTHandle color = data.color, depth = data.depth;
                    data.owner._colorTarget = color.nameID;
                    data.owner._depthTarget = depth.nameID;
                    data.owner._uiCamera = data.camera;
                });
            }
        }
    }
}
