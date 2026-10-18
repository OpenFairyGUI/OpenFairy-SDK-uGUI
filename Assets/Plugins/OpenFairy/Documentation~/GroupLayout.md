# Authored group layout

Advanced horizontal and vertical groups are baked with a `GroupLayout` and a native `ContentSizeFitter`. Members keep direct prefab references and their hierarchy; no runtime resource lookup is needed.

Horizontal groups advance the left edge only. Each member keeps its authored vertical offset and height. Vertical groups advance the top edge only and preserve horizontal offsets and widths. A zero-size member is positioned at the current cursor but does not consume another gap. Negative gaps allow overlapping members. Hidden members keep their space unless `excludeInvisibles` is enabled.

Layout uses native layout invalidation and rebuild ordering. When a child has an active fitter, the group measures the same native preferred/minimum size before layout controls resize that child. This lets text changes propagate through a group and a parent relation without requiring a polling loop. Plain members use their actual rectangle. Updating cross-axis bounds preserves member positions in parent coordinates.

The importer retains the explicit `layout` attribute separately from a list's default column layout. A group without an advanced horizontal/vertical flow keeps its authored rectangle. This implementation does not add proportional member resizing, a main grid member, or constrained group sizing.

Tests cover horizontal/vertical flow, authored cross-axis offsets, zero-size members, hidden-member participation, negative gaps, real TMP preferred widths, close affordance resizing, parent-width propagation, row spacing, re-enable, and the real prefab importer. Unity omits fitted driven sizes from serialized prefab rectangles; the import test checks the saved components/references and measures a newly instantiated prefab using native layout.

Native coverage is the configured TMP backend. Builtin compilation is reported separately and does not imply native text-layout parity or complete demo/golden validation.
