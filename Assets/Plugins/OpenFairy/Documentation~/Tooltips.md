# Tooltips

Set `tipsRes` in the authoring project's `settings/Common.json` to a Label component. The importer collects its dependencies before consumers and bakes the Label prefab directly into each `Tooltip.template` field. Authored `tooltips` text is preserved. No resource URL lookup occurs at runtime. Without a configured template, text is retained but no tooltip is shown.

`Tooltip` uses native pointer-enter and pointer-exit events. It waits 0.1 unscaled seconds by default, positions at the pointer's current location when the delay expires, and keeps the presentation within the root bounds. Leaving, disabling, pressing or dragging cancels it. It observes the existing pointer event data instead of consuming pointer-down or drag events from parent controls. An authored untouchable object remains excluded by its native CanvasGroup. Tooltip presentations do not block raycasts.

One Label instance is reused by the existing Root overlay. Code-created interfaces can assign `Tooltip.template` directly or call `Root.ShowTooltip(template, message, designPosition, delay)` and `Root.HideTooltip()`. Design positions use the same top-left coordinates as popups. Callers of the manual API own its dismissal.

## Automatic text size and root relations

Component-level relations are imported as well as child relations. A Label can therefore follow the dimensions of its title. Ordinary text with `autoSize="height"` receives a native ContentSizeFitter on height; `both` fits both axes. Input, fixed, shrink and ellipsis modes keep their existing rectangle behavior.

Content owns automatic axes. Size and extension relations on those axes are excluded during baking, while positional constraints and the nonautomatic width of height-only text remain active. Size gears also leave fitted axes to native layout and retain scale changes. This prevents a parent-following-child constraint from feeding back through a child-stretching-to-parent constraint. The built-in text renderer reports preferred dimensions using its own existing line layout, including inline images and markup.

Native preferred-size layout is used rather than a second text measurement or late-update resizing system. Text metrics still depend on the selected backend and font; equal pixel metrics across text backends are not promised.

## Verification

The runtime tests exercise delayed hover, cancellation, stale exits, cache reuse, parent input ownership, native raycast exclusion, long-to-short sizing, edge placement, size gears and multi-frame relation stability. Editor tests exercise XML retention, automatic-axis precedence, input sizing and direct root-to-child relation baking. Run these in Unity with the project's configured text backend. Offline compilation is not a substitute for native layout, pointer or rendering execution.
