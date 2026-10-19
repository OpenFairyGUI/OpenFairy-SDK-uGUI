# TMP text sizing

`autoSize="shrink"` keeps the authored rectangle fixed and uses native TMP auto sizing. The authored point size is its upper bound, its lower bound is one point, and additional character-width/line-spacing compression is disabled. Wrapping follows the source single-line setting. Shorter replacement text can grow back to the authored size.

`autoSize="ellipsis"` keeps the authored rectangle and point size, uses native TMP ellipsis, and preserves the source text in `TMP_Text.text`. Input fields retain their normal editing overflow behavior.

Use `TextAutoSize.SetFontSize(text, size)` when changing an auto-sized label's authored point size. Its `fontSize` property is TMP's current fitted result; setting it alone does not change the native upper bound. Font-size gears use this helper.

The existing markup preprocessor normalizes absolute rich-text size tags to percentages of the authored upper bound for shrinking labels. This keeps inline point sizes in the same global shrink ratio. Relative percent/em tags, `noparse` content, and non-rich text stay unchanged. The source string remains available to bindings and gears. Ellipsis does not normalize size tags.

TMP owns glyph measurement, shaping, wrapping, fitting, and truncation. Its fitting precision and line metrics are native TMP behavior rather than pixel-exact legacy font layout. No layout polling component or secondary text renderer is involved.

The runtime tests create an in-memory font asset from the project's TMP default font source and exercise actual TMP layout, text replacement, font-size gears, ellipsis, and markup preprocessing. Add the test assembly reference `Unity.TextMeshPro` and enable `FAIRYGUI_TMPRO` when running them.
