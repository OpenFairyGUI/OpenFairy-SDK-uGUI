# Relations

The importer preserves each relation's rule order and percentage flag. `size`
expands to width and height. Both the standard names and the width/height aliases
are accepted, and repeated sides keep their first definition.

Parent constraints that are affine in the parent's size are baked as native
`RectTransform` anchors. Sibling targets, extension rules, and order-dependent
percentage rules use one `Relation` component on the owner. References and typed
rules are baked into the prefab; no runtime name lookup is required.

`reference` is the authored parent's size source. `coordinateSpace` is the
container in which the authored positions are expressed. They normally refer to
the same rectangle. A scroll viewport uses its own coordinate space so padding
is not included in percentage scaling. Group and mask reparenting preserves the
authored rectangle and its reference space.

Runtime target changes are sampled after `PostLateUpdate`. `Sync()` can apply a
completed group of changes explicitly. Changes that happen several times in
one frame are sampled as one change, rather than reproducing every intermediate
setter notification. Owner size changes preserve center/right/bottom alignment.
Rotation, rendered bounds, groups' layout algorithms, and construction-time
minimum/maximum size clamping are outside the relation geometry calculations.

Import the authoring files again after upgrading from the previous relation
serialization. The old enum-array representation is replaced by typed rules and
direct target bindings.

The runtime tests exercise 400 independent reference fixtures through actual
`RectTransform` instances, including all 24 rules, percentage flags, pivots,
zero-sized targets, parent/sibling/child targets, and rule order. They also cover
owner resizing, reparenting, viewport padding, and resetting the polling loop.
The loop resets at `SubsystemRegistration`, including when domain reload is
disabled. A generation marker invalidates a pending continuation from an earlier
play session.
