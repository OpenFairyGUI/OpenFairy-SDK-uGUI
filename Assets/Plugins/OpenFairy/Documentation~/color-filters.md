# Color filters

The importer preserves `filter="color"` with four invariant numeric channels:
brightness, contrast, saturation and hue. Values are applied in that order.
The runtime matrix also drives transition color-filter samples. A static filter
remains active before a transition starts; setting a transition-only filter
activates its pre-baked component.

Image primitives use an `IMaterialModifier`. Their original material, native
stencil state, clipping, alpha clipping, tint and blend properties are preserved.
Disabling the filter restores the original native material path.

Containers and text use `ObjectFilter` and `UiObjectCapture`. Their contents are
composited before the color matrix is applied, rather than applying separate
filters to overlapping children. Native text keeps its original font material.
The captured input and filtered output are premultiplied; the matrix operates on
straight RGB exactly once. Presentation, opacity, input and capture disposal
share the same implementation as object blur.

Play Mode refreshes captured filters automatically through the same path as
blur; see [Automatic object filters](AutomaticFilters.md). Edit Mode and offscreen
tools use the synchronous `ObjectFilter.Refresh` callback after UI changes and
before rendering. The importer pre-bakes source, sibling
presentation, materials and references. Its default capture layer is 31; that
layer must be reserved for capture. `ColorFilterBuilder.Configure` accepts an
explicit layer for integrations that reserve another one. Presentation and source
must be siblings owned by the same prefab.

The tests cover invariant schema parsing, direct material restoration, composed
overlap colors, premultiplied alpha, root opacity, transition activation and native
text material preservation. Native rendering and object lifetime tests require
Unity; compiling them does not execute those checks.
