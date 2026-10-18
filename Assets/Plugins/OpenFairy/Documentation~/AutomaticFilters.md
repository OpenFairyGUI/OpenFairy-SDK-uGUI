# Automatic object filters

Active `ObjectFilter` components refresh during Play Mode immediately before rendering. Ordinary UI changes, native layout, scrolling and transitions do not require per-screen `Refresh` calls.

The outermost enabled filter refreshes its active descendants in inside-out hierarchy order. A disabled ancestor leaves its active descendants independently refreshable. A direct image color filter can own this traversal without capturing its own image. Capture cameras are not used as scheduling hooks.

The callback first flushes native Canvas layout, then captures the subtree. The source retains its hierarchy and raycast targets; only its baked sibling presentation renders the filtered pixels. The existing root/parent alpha and clipping separation remains unchanged. Disabling or destroying the component restores the source and releases owned capture resources.

Automatic capture uses `Camera.Render` for the built-in pipeline and the core `RenderPipeline.StandardRequest` for a scriptable pipeline. The latter must report synchronous request support; an unsupported pipeline throws explicitly instead of displaying stale filter output. No pipeline-specific assembly is referenced.

`Refresh(Action<Camera, RenderTexture>)` remains a synchronous explicit operation for offscreen tools and same-frame probes. It is not deduplicated by frame number: multiple requested refreshes can observe multiple changes in one frame. Edit Mode continues to use this explicit operation.

The source and its pre-baked presentation must share a parent. Move both when moving an entire filtered widget; moving an individual source Graphic into or out of the widget does not move the presentation. Automatic refresh does not introduce business-object cloning, a render scheduler or dirty-propagation graph.

The accompanying Play Mode tests exercise actual frame ordering, native layout, multiple Canvases, nested filters, material-only ancestors, clipping/opacity/raycast, scrolling, transitions and repeated explicit refresh. The Editor session test also enters and exits Play twice with both domain and scene reload disabled. These tests need to run with a working graphics device; offline compilation alone does not establish their results.
