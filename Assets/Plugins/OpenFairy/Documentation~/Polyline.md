# Open polylines

Set `Graph.kind` to `Graph.Kind.Polyline` and assign `points`, `lineSize` and `lineColor`. Coordinates start at the rectangle's top-left corner, with positive X to the right and Y down. `usePercentPositions` scales each point by the current rectangle size. Change the data and call native `SetVerticesDirty()` to rebuild.

The path is open: it neither connects the last point to the first nor fills the interior. Stroke width is centered on each segment. `roundEdge` adds a semicircular endpoint of half the stroke width; joins use the existing segment-strip convention. Consecutive duplicate points add no segment, and fewer than two distinct points or nonpositive width produce no mesh. The graph uses the normal uGUI material, clipping and inherited alpha. CanvasRenderer is a declared component dependency.

The importer accepts `type="polyline"`, `points`, `lineSize`, `lineColor` and `roundEdge`. Existing graph enum values are unchanged. This does not introduce curves, textures along a path, a new renderer or a separate line object model.

Native Editor tests cover the schema and actual shape configuration. Runtime tests check mesh coverage, open paths, endpoints, percent sizing and duplicate points, plus a GPU positive control for rounded endpoints, inherited alpha and RectMask2D clipping. At the feature introduction, the full package checkpoint passed 124 Editor and 165 PlayMode tests. The latest same-version package totals are maintained in the SDK AGENTS.md and Docs/BasicsImplementationStatus.md. Those runs use the TMP backend in a Unity Editor/Mono host; other text backends have separate compilation coverage, not native rendering or Player validation.
