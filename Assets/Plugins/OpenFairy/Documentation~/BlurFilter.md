# Object blur

`BlurFilter` filters a UI object's own pixels. It does not sample the background behind that object. `TextureBlur` remains available for callers that already own a texture.

## Static setup

Inside the prefab, author a `RawImage` immediately after the source under the same parent. Keep both inside the saved prefab. A root-level source therefore needs a common prefab container; do not save a reference to a sibling outside that prefab. The presentation must not be a child of the source: the source's own mask is captured before blurring, while its parent's mask clips the final result. Set `LayoutElement.ignoreLayout`, disable raycasts, and keep the presentation inactive until its first successful refresh.

The Editor helper `BlurFilterBuilder.Configure` wires the source, presentation, presentation `CanvasGroup`, capture shader, blur shader, shared presentation material, and an exclusive capture layer. Create the shared material with `OpenFairy/UI Premultiplied`. It also adds `ObjectFilterSourceVisibility` after the existing native material modifiers. These are serialized native references; the runtime does not resolve resource URLs or search for shaders.

Do not insert the presentation into a list's managed item collection. For an item prefab, place the source and presentation inside the item's common container. The item's layout and lifecycle operate on that container. The source's transform and business components stay in place at runtime.

## Refresh

Play Mode refreshes active filters automatically; see [Automatic object filters](AutomaticFilters.md). Edit Mode and offscreen tools can call `filter.Refresh(render)` after UI changes and before the UI camera renders. `render` has the same synchronous `(Camera, RenderTexture)` contract as `UiObjectCapture.Capture`. The SDK has no pipeline-specific assembly dependency. Explicit scriptable-pipeline callbacks must use a supported synchronous render request.

An outer filter refreshes active child filters in reverse native hierarchy order before capturing itself. The capture skips their hidden source subtrees and captures their presentation instead. Do not separately refresh every nested filter as well unless another refresh is wanted.

`blurSize` is the kernel tap spacing in source-local UI units. The nine-tap separable kernel has support out to four spacings, so capture and presentation include `4 * blurSize` transparent padding on each edge. `resolutionScale` selects capture pixels per local unit. These controls do not promise pixel-identical output to a different blur algorithm. Capture uses the source's explicit rectangle, as with a UI object filter; give overflowing content an appropriately sized source rectangle.

The source stays active with its original color, material reference, raycast flag, and hierarchy. `ObjectFilterSourceVisibility` suppresses only framebuffer color writes, retaining native stencil operations. `UiObjectCapture` ignores that suppression while sampling the source. Static modifiers are baked; newly generated graphics, including TMP fallback or sprite submeshes, receive the same small modifier on refresh. Existing native masks must precede this modifier. If changing a source's material-modifier component order, rebake that source's visibility modifier after the native modifiers.

## Alpha, masks, and ownership

Capture and blur both use premultiplied alpha. The presentation shader multiplies premultiplied RGB and alpha by the final opacity and soft-clip factor; it does not premultiply the texture a second time. Source-root opacity and `ignoreParentGroups` are copied to the presentation group, while outer groups and masks remain in the existing parent hierarchy. Descendant groups and internal masks are baked into the local capture.

Object filters isolate their content as a group. In particular, a descendant `CanvasGroup.ignoreParentGroups` is evaluated within that captured group; it cannot opt an individual captured child out of opacity applied to the final composite. Author an independent sibling filter when that independent opacity is required.

The shared `ObjectFilter` base owns its capture object and render texture; `BlurFilter` owns one reusable blur material. It borrows all source meshes, textures, shader assets, and the shared presentation material. Disabling or disposing the component or destroying it releases its owned resources, hides the presentation, and restores source color writes. A zero blur size or a failed render releases capture resources and restores the source; the enabled effect may retain its one blur-material cache until disabled. Re-enabling permits a fresh capture. There is no static scene-object cache.

The capture's shader and blend support restrictions still apply. This is not a background-blur feature or a general offscreen effect graph.

## Validation

The shared lifecycle and native-reference setup are documented in `ObjectFilter.md`. `BlurFilterTests` exercises real camera output, parent/root opacity, retained native raycasts, internal stencil versus external soft clipping, content and size changes, failed render cleanup, repeated disable/enable, and nested filter composition. Run it together with `UiObjectCaptureTests` and `TextureBlurTests`; compiler diagnostics alone do not validate shader output.
