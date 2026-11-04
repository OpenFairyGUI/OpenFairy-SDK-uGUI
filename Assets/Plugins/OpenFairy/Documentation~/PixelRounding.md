# Pixel rounding

`MigrationSettings.makePixelPerfect` controls the original SDK's optional display-origin rounding. Enable **Project Settings > OpenFairy > Make Pixel Perfect**, then run Migrate. Its default is false. Canvas.pixelPerfect is a separate uGUI setting and does not implement these rules.

The SDK also rounds intermediate layout values even when that option is off. These rules always apply:

| Path | Rule |
|---|---|
| Display origin | With makePixelPerfect, Round in the authored parent coordinate system after layout; skip rotated objects. Restore the exact logical pose before the next update. |
| Loader | Editor center/middle Floor is separate from optional origin Round. Right/bottom offsets retain fractions. |
| List single column/row | Advance by Ceil of each item's height/width; Ceil the cross-axis extent. Center the whole content block with an integer cast. |
| Horizontal flow | Ceil item widths and line heights. With fixed column count and auto sizing, distribute remaining width with Round. |
| Vertical flow | Preserve individual height fractions in the non-auto branch; Ceil column widths and final height. Auto heights use remaining-space Round. |
| Pagination | Floor fixed row height; Round proportional widths; preserve the native difference between raw and ceiled line heights. Initial Migrate and runtime Fill use the same layout arithmetic. |
| Virtual list | Ceil cell measurements; truncate auto-size partitions; preserve fractional item geometry. Pagination uses complete pages. |
| Advanced group resize | Round each proportional share of remaining space, on either axis, including the priority main grid. |
| Scroll content | Round final content bounds, Ceil overlap, truncate rendered position. A small physical padding on the unrendered content helper lets ScrollRect reach the ceiled endpoint. Public contentSize remains the logical size. |
| Scroll tween | Truncate the eased delta before adding the start position. Logical scroll state survives render alignment. |
| Scrollbar | Floor variable grip length, preserve fixed grip length, Round its position in the scrollbar's authored space. Dragging uses the resulting physical grip length. |
| Pages | Floor page quotient, then advance only when strictly past half; exact half remains on the previous page. ScrollToView uses Floor to find the containing page. |
| Root/window/popup/tooltip | Ceil logical root extent; truncate center offsets; Round popup/tooltip positions. |
| Text | Floor shrink size; Round total text dimensions; truncate center offsets. Scale bitmap advances, spaces and line metrics before Round. Bake the .fnt resizable flag. Glyph geometry and file-font metrics retain fractions. |
| Progress/slider | Floor title percentage, Math.Round numeric title, Round bar lengths and animation frame. |
| Capture | Round scaled render texture dimensions; keep a positive texture allocation. |

Round uses Unity's midpoint-to-even behavior. C# integer casts truncate toward zero, including negative values. Replacing casts with Floor changes negative-coordinate behavior.

Rounding an origin does not imply rounding its size, scale, vertices or UVs. Tessellation Ceil operations determine segment counts and do not snap geometry.

The audit includes integer casts and inactive TMP/platform branches. Native data decoding, enum casts, capacities, indices and alpha-byte conversions are recorded separately from display positions. uGUI Image owns tiling, InputField owns caret/draw ranges, TMP owns its own font metrics/UV packing, and ScrollRect owns inertia and elasticity. Those substitutions do not establish identical glyph rasterization or motion trajectories.

FairyGUI-only features without an OpenFairy implementation remain explicit boundaries: packed pixel-hit masks, arbitrary PlaneMesh deformation, standalone gesture APIs, Builtin RTL and superscript/subscript/strike layout, full HTML form/percentage-image layout, and per-window modal wait panes. Virtual grids use fixed cells; variable-height SingleColumn rows use the explicit measurement callback. Unrestricted renderer-driven variable widths/cross sizes and native inertial/page-direction heuristics are separate feature boundaries. The rendering backend's own behavior applies where a native uGUI/TMP component is used.

Validation includes captured results from the unmodified native SDK: 180 list cases, 18 proportional-group cases and 144 scrollbar cases, plus package tests for fractional virtual cells, paginated lists, measured rows, center ties, fixed grips, exact logical scroll state and ceiled scroll endpoints. Editor-specific pivot truncation and editor camera softness are separate from these SDK rules.
