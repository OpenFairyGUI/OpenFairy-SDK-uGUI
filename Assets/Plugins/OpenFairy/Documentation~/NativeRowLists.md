# Native nonvirtual row layout

Nonvirtual single-row lists follow each item's actual width. Imported lists bake a HorizontalLayoutGroup, ContentSizeFitter and minimum viewport extent. The native layout owns item positions and content extent; ListSource only creates/binds items and applies the authored cross-axis sizing policy. Changes from native text or button layout can resize an item without another Fill call.

The XML `autoItemSize` value defaults to true. For a single row it controls item height: true resizes the item to the viewport height, false preserves the item's height. Both values retain variable item widths. Alignment and spacing come from the authored list. The viewport minimum preserves alignment when the items are shorter than the viewport.

Editor tools may call ListLayoutBuilder.Configure on an existing baked ListSource. Repeated calls reuse the same native components. Switching that source to virtual mode disables the baked native drivers. Virtual single-row lists retain the existing fixed-extent and loop semantics; variable-width virtual rows are not introduced here. Other list layouts retain their current path.

The regression sources cover authored defaults, repeated baking, unequal widths, later content resizing, cross-axis sizing, viewport alignment and the switch to virtual fixed rows. Compilation alone does not verify those native behaviors.
