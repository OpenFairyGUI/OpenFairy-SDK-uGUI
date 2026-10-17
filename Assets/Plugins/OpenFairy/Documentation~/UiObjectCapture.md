# Local uGUI capture

`UiObjectCapture` captures an existing UI subtree into a caller-owned render texture. It creates a private world-space Canvas containing only render data: transforms, generated meshes, native masks, CanvasGroups, and Canvas sorting state. Source behaviours, text/layout engines, and event handlers are never cloned. The original subtree is not reparented, disabled, or hidden.

```csharp
using var capture = new UiObjectCapture(target, captureShader, dedicatedCaptureLayer);
capture.Capture(texture, (camera, destination) =>
{
    RenderPipeline.SubmitRenderRequest(camera,
        new RenderPipeline.StandardRequest { destination = destination });
});
Rect bounds = capture.bounds;
```

The caller supplies the serialized `OpenFairy/UI Capture` shader and an exclusive capture layer. The render callback executes synchronously and may configure its pipeline's camera settings before rendering. It must preserve RGBA and avoid scene postprocessing, upscaling, or unrelated camera-stack content. The SDK does not reference a particular render pipeline. The callback must not recursively capture the same object or start asynchronous rendering after it returns.

The destination must have an alpha channel. Stencil masks require a depth/stencil attachment; capturing them into a texture without stencil produces an argument error. Native tests use a single-sample RGBA render texture with 24-bit depth/stencil. Resolve multisampled textures before CPU readback or use in operations that require a single sample.

The returned pixels use premultiplied alpha. Straight UI images and native TMP sprites use the capture shader, which premultiplies after native rectangular clipping. SDF fonts retain their native PMA shader and atlas; TMP sprite/font fallback submeshes are included. Existing SDK materials with configurable alpha blending preserve their shader and receive normal Porter–Duff alpha accumulation. Non-normal blend modes and unknown custom shaders currently require an explicit adapter and produce an error rather than silently changing appearance.

The capture includes descendant CanvasGroup opacity and internal native `Mask`/`RectMask2D` behavior, including masks on the target itself. It excludes CanvasGroups on the target itself and all ancestor opacity/masks. Final presentation must apply those once. A sibling presentation node under the original parent can inherit external masks while copying the target's own CanvasGroup and transform; putting it inside the target's already captured mask would apply that mask again. Do not put fully inherited alpha into the texture and then multiply it again on the presentation image. No division by inherited alpha is used, so an outer alpha of zero is valid.

Bounds are expressed in target-local coordinates. By default they conservatively include the target rect and active mesh overflow, then add padding and expand to match the texture's aspect ratio. For a clipped scroll viewport, pass `localBounds: target.rect` to avoid allocating resolution to hidden content beyond that viewport. The actual bounds used for mapping the texture are returned through `bounds`.

The operation synchronizes source UI geometry before reading it. A culled source Graphic is temporarily allowed to rebuild its native mesh, then its previous cull flag is restored immediately; cull is never used to hide the source. Source raycast targets, colours, hierarchy, and active states stay intact. The private capture Canvas is active only for the render callback. Render failures restore the previous active render texture, clear the capture camera target, and deactivate the private Canvas.

The source remains visible. Replacing its display with a blurred quad, backdrop capture, and pointer-preserving visual suppression are separate operations. This API alone is not a complete object blur filter. The result may be passed to `TextureBlur.Apply` with `TextureAlphaMode.Premultiplied`; its final display shader must also use PMA blending.

Dispose the capture to release private nodes, materials, and its camera. Source meshes, materials, textures, and the caller's destination are not owned or destroyed by the capture. The native tests cover opacity separation and PMA pixels, internal stencil/soft rectangular masks, source lifecycle and failure recovery, same-frame node recycling, and mixed TMP/Sprite geometry and atlas sampling.
