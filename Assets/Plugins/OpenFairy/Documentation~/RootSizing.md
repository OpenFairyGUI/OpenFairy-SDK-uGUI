# Overlay root sizing

`Root.Create(canvasRect)` creates an overlay under that Canvas with native stretch anchors and zero edge offsets. Canvas layout and resolution changes therefore resize it through native layout.

`Root.Create(designRoot)` for a non-Canvas design root keeps the overlay as a sibling. Putting it inside the design root would incorrectly apply that root's mask and opacity to popups. `RootSizeBinding` copies the design root's native anchors, pivot, size delta, anchored position, rotation and scale. It responds to dimension-change callbacks and checks the source rectangle and local-to-world matrix in `LateUpdate`, synchronizing when either changes. This includes position, rotation, scale and ancestor-layout changes. The overlay ignores its parent's layout group.

The public design-coordinate APIs retain a top-left origin even when the source pivot is not top-left. Coordinate conversion uses the overlay's actual rectangle; it does not assume a particular pivot. Change the design root rather than independently resizing or moving its bound overlay. The singleton continues to use the coordinate host supplied on its initial creation. Pure transform changes made after the binding's `LateUpdate` are not guaranteed to propagate in the same frame.

The source binding lives with the design root and is reused if a new overlay is created; destroying the overlay clears its binding. No renderer, raycast target or business object is cloned or reparented. Popups and modal blockers keep their native anchors, and placement uses the root's current rectangle.

`RootSizingTests` exercises native Canvas resize; independent and ancestor-driven design-root resize; non-default pivots, rotation, scale and later transform changes; mask/opacity separation; binding reuse; and popup/blocker placement after resizing. The tooltip fixture supplies a fixed-size design root instead of overriding the overlay's own size.
