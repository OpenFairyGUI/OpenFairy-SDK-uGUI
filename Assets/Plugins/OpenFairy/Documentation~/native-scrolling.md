# Native scrolling

Imported scroll views use Unity's `ScrollRect` and `Scrollbar` for pointer input,
dragging, wheel input, inertia, elasticity and scrollbar feedback. `ScrollPane`
adds design coordinates, indexed scrolling, looping rebases, page controllers and
the SDK's scroll event. Virtual item windows and measured row offsets remain in
`ListSource`; off-screen items do not need instantiated layout children.

The importer bakes the viewport, content, clipping graphic, native components and
direct references. Explicit scrollbar resources take precedence over defaults.
An authored grip is reparented into its track without replacing its object or
changing its rectangle, so generated fields keep referring to the same object.
Imported prefabs must be regenerated when upgrading from `ScrollPaneHost`.

Programmatically assembled views can still call `ScrollPane.Attach`. This path
expects the SDK's viewport structure; imported views do not discover children or
add scrolling components at runtime.

Scrolling uses native unscaled timing and native elastic/inertial behavior.
Programmatic animated moves use an unscaled tween. Track clicks preserve the
authored four-step increment, while grip dragging uses Unity's native pointer
offset. Demand visibility uses native viewport expansion where its layout can
represent the authored rectangle. Nonzero viewport margins, floating bars or
left-side bars retain their authored rectangle instead of having it overwritten
by `AutoHideAndExpandViewport`; hidden bars can therefore leave reserved space
in those configurations.

`NativeScrollingTests` covers native input, loop rebasing, measured row changes,
grip dragging, track increments and page-controller feedback. The importer tests
cover typed XML metadata and grip identity/rectangle preservation. These tests
require Unity for native `RectTransform`, `ScrollRect` and frame execution.
