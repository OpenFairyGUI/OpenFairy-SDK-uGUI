# Hidden list clipping

`overflow="hidden"` on a list bakes a native `RectMask2D` on the list root. Its existing root remains both viewport and content; no scrolling container is added. Resizing the list's rectangle changes the clipping boundary without repositioning items.

Visible lists remain unmasked. Scrolling lists keep their existing separate masked viewport and content. Item sizing, native row layout, selection and virtual scrolling are unchanged.

The importer tests exercise the actual list builder, then save and instantiate its prefab to check the native mask, viewport/content references, item dimensions, authored auto-size and row layout. The PlayMode test fills two real rows, verifies GPU clipping with an unmasked positive control, collapses the root to zero height and restores it. A successful compiler check alone does not establish rendering behavior.
