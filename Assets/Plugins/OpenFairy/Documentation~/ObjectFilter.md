# Object-filter capture and presentation

`ObjectFilter` contains the shared native UI lifecycle needed by object blur and container color filtering. It is an abstract component, with three effect-specific members:

```csharp
protected override bool ShouldRender => /* whether local capture is required */;
protected override float Padding => /* transparent padding in local UI units */;
protected override void ProcessPmaTexture(RenderTexture texture)
{
    // Process the captured texture synchronously, keeping premultiplied alpha.
}
```

`bounds` is the actual capture rectangle and is available before `ProcessPmaTexture`. The texture is owned by the base; its dimensions may change on later calls. A processor must not keep borrowed temporary textures, overwrite a texture while reading it without an intermediate, or return straight-alpha data. The base does not provide an effect list, a render graph, or a global update scheduler.

The derived component owns its effect-specific resources. Override standard `OnDisable`/`OnDestroy`, release those resources, and call the base implementations. When `ShouldRender` becomes false or a render fails, the base restores original color writes, hides presentation, and releases its capture and render texture. A derived enabled component may retain its reusable processing material until disabled. `Dispose()` disables the component, invoking this complete cleanup.

## Baked native references

`ObjectFilterBuilder.Configure(filter, presentation, captureShader, presentationMaterial, captureLayer)` wires a pre-existing `RawImage` next to the source and bakes its `CanvasGroup`, optional `Canvas`, `LayoutElement.ignoreLayout`, and the small source visibility modifiers. Both source and presentation must be inside the same saved prefab. The presentation uses the shared `OpenFairy/UI Premultiplied` material, does not receive raycasts, and remains inactive until a successful capture.

Use a common prefab container for a root-level source. In an item prefab, keep the source and its presentation inside the common item container, rather than introducing a second item into a list. The runtime does not wrap or reparent the business object.

`ObjectFilterSourceVisibility` suppresses framebuffer color writes through native `IMaterialModifier`; it does not replace the source's authored material reference or disable its `Graphic`. Native stencil operations remain intact. The helper places this modifier after existing native masks and other material modifiers. Reconfigure after changing that component order. New runtime graphics receive it on refresh. Enable and hierarchy callbacks invalidate their materials so an already rendered graphic can join or leave a filter without keeping a stale hidden material.

## Explicit refresh and grouped semantics

Call `Refresh(Action<Camera, RenderTexture>)` after UI changes and before the UI camera renders. The callback synchronously renders the supplied camera into the supplied texture; it owns render-pipeline-specific camera setup. Do not call this from `Canvas.willRenderCanvases`: capture itself forces a native canvas update.

An enclosing filter refreshes nested filters first, then captures their presentations while excluding their suppressed source subtrees. This preserves compositing order without a separate filter scheduler. Capture dimensions come from the explicit source rectangle and effect padding, at `resolutionScale` pixels per local unit. Parent/root opacity and external clipping apply once to the final presentation; descendant opacity and internal masks belong to the local capture.

The filter boundary isolates its contents as a group. Descendant `ignoreParentGroups` is evaluated within that capture and cannot independently bypass opacity on the final composite. Use separate sibling filter groups where independent final opacity is required. Normal capture shader/blend support rules still apply.

For a color filter, preserve native TMP SDF rendering by using the capture path for TMP text and its sprite/font submeshes. A direct-material fast path must be limited to image renderers that the effect shader actually supports; replacing a TMP font shader with an image shader does not implement text color filtering.
