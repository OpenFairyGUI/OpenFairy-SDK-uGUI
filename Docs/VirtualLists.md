# Virtual lists

`ListSource.Fill` retains the full-instantiation path unless `SetVirtual` is called. Virtual lists reuse the same object pool and `ScrollPane`; they do not add a second scrolling component.

```csharp
source.SetVirtual();
source.Fill(records.Count, (item, index) => Bind(item, records[index]));
source.ScrollToIndex(selectedIndex);
```

Fixed-size rows and grids use the baked `itemSize`, gaps, layout and optional `lineItemCount`. Otherwise the lane count follows viewport size. `alignment` uses the native `TextAnchor` enum. Only visible items plus one extra line at either edge are instantiated. The scrollable content size describes all logical items, rather than only the instantiated children.

For variable-height single-column lists, supply a synchronous measurement callback after binding:

```csharp
source.SetVirtual();
source.Fill(records.Count, BindRow, measureHeight: MeasureRowHeight);
```

The callback returns the full row height from the actual drawing/layout implementation, including padding. For example, a text-only TMP row can use its text component's `GetPreferredValues` with the real available width; do not estimate height from character count. The source applies that height to the row RectTransform, maintains prefix offsets and checks visible measurements for changes. Unvisited rows initially use `itemSize.y`; scrolling refines their cached measurements. When measurements change, the first visible row retains its screen position. A list already at the bottom remains at the bottom when appended. The first Fill starts at the top; callers may explicitly set `pane.percY = 1` when initially opening a log.

Horizontal looping is available for `SingleRow`:

```csharp
source.SetVirtual(loop: true);
source.Fill(records.Count, BindRow);
source.ScrollToIndex(index, center: true, animated: true);
```

Repeated physical slots always bind logical indices. Loop rebasing changes content/drag/tween origins together, retaining viewport positions and ongoing motion. Looping also supports a single logical item. The finite backing extent is an implementation detail, not an end boundary.

`ListSelection.selectedIndex`, `GetSelection` and `onClickItem` use logical data indices even when a row is recycled or a loop contains duplicate copies. Read business data from the logical collection, rather than indexing currently instantiated children. `visibleItems` exposes physical slot keys for effects based on screen positions; `GetItemIndex(item)` retrieves their logical indices. `firstVisibleIndex` and `centeredIndex` return logical indices.

Async binders should capture `source.version` and their logical index before awaiting, then check `source.IsBound(item, index, version)` before applying a late result. Both recycling and a later Fill can invalidate the original binding.

The independent layout test exercises interval coverage against brute-force intersection, viewport-bounded realization, variable-size boundaries and loop identity/coordinate invariants. PlayMode tests exercise actual pooled GameObjects, logical selection after recycling, measured-row anchoring, bottom retention and a drag across a loop seam. Compiler diagnostics and pure-layout execution do not substitute for running the PlayMode tests.
