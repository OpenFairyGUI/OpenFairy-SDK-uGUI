# Blending inside object capture

Object capture is an isolated group: its children draw into a transparent framebuffer, then one premultiplied presentation composites that result. The group's external backdrop is not sampled. The existing `BlendModeEffect` RGB factors and source premultiplication flag are preserved; capture only supplies the appropriate separate coverage-alpha factors.

Let `a` be source opacity after tint and clipping, and `b` the existing capture coverage. Capture alpha is:

| Mode | Result alpha |
| --- | --- |
| Normal, Screen, One_OneMinusSrcAlpha, Custom1/2/3 | `a + b * (1 - a)` |
| None, Add, Multiply | `b` |
| Erase | `b * (1 - a)` |
| Mask | `b * a` |
| Below | `a * (1 - b) + b * b` |
| Off | `a` |

`Add` contributes alpha-weighted RGB emission without adding coverage. `None` retains its authored unweighted RGB addition, also without adding coverage. RGB may therefore be nonzero when alpha is zero. The color-matrix PMA pass transforms RGB directly and weights its offset by coverage, so gray, brightness and other RGB transforms do not discard this emission or create opaque background pixels.

`Below` preserves the existing RGB factors `OneMinusDstAlpha, DstAlpha`; it is not renamed or replaced with a different under operator. Its capture alpha uses the same factors. `Off` replaces the local capture framebuffer. Custom modes retain the current fallback to normal; this change does not add a runtime factor-override API.

This intentionally does not reproduce additive accumulation of framebuffer alpha. Coverage is needed for correct final composition and for erase/mask behavior. Normal capture keeps its existing over semantics. The original source materials, native input and raycast flags are unchanged.

An isolated group is not always equivalent to drawing those children directly against the external screen. In particular, Multiply/Screen act on the group's existing color; they cannot modify a backdrop that was never captured. Off and destination-alpha-dependent operators also have local framebuffer semantics. This is a group boundary, not a reason to silently convert them to Normal. Captures retain the existing LDR format, source bounds, native mask and shader-adapter limitations. Arbitrary custom shaders and non-normal SDF shader variants are not added by this change.

The native GPU regressions enumerate all authored modes against independent RGBA equations, include an active alpha-zero Add, and render actual gray/Add/Screen presentations over a visible nonblack external backdrop. The latter checks zero-coverage emission, coverage-weighted color offset and application of the source CanvasGroup alpha once. Compilation alone cannot verify those pixels.
