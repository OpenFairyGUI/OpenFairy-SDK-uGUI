# TMP inline sprites and links

Assign a native `TMP_SpriteAsset` directly to `TMP_Text.spriteAsset`. Inline sprites use glyph names in that asset and its statically assigned fallbacks. No runtime URL resolver, texture download, atlas builder, or extra image layout component is involved.

```csharp
label.spriteAsset = emotes;
label.text = "Hello " + TmpTextMarkup.SpriteTag("happy");
label.text = TmpTextMarkup.SpriteTag(label, "happy", width: 32, height: 24);
```

The first form uses native em-based sprite sizing. The second emits native TMP size, horizontal scale, and vertical-offset tags for explicit pixel dimensions. `baseline` is the fraction of the image above the text baseline; its default is `0.8`. The source asset is never mutated to resize an individual image. The overload accepting a `TMP_SpriteAsset` can format images with positive asset face metrics; assets using inherited font metrics require the `TMP_Text` overload.

The standard TMP scale/vertical-offset scopes reset their respective native modifiers on closing. Avoid placing explicitly sized sprites inside an additional native scale/vertical-offset scope that must continue after the sprite. Use the sprite's own dimensions/baseline arguments instead.

`TmpTextMarkup` converts `[img]glyph-name[/img]` and, with HTML enabled, `<img src="glyph-name" width="32" height="24"/>` to native sprite tags. Runtime image names identify glyphs in the assigned asset, not files or resource URLs. Dynamic text replacement reuses the same native asset. Absolute size tags participate in shrink mode through the existing size normalization.

For static source markup, `TmpSpriteAssetBuilder.BakeInlineImages` resolves references through an importer-supplied callback, creates the native sprite asset, and replaces image markup with native tags. The callback runs only during import. The generated asset reuses original unpacked textures and sprites, groups different textures into native fallback assets, and owns its generated asset file and subassets. Rebuilding preserves the root asset GUID. `Bake` also accepts named sprites directly for runtime data such as emotes; source art is never repacked or modified.

```csharp
var sprites = TmpSpriteAssetBuilder.Bake(outputPath, new[]
{
    new TmpSpriteAssetBuilder.Entry("happy", happySprite),
    new TmpSpriteAssetBuilder.Entry("wave", waveSprite),
});
```

The importer must provide an existing output directory and assign the returned root asset to the generated text. Unresolved static image references produce an import error. A label without image tags does not create an unused asset.

UBB `[url=target]text[/url]`, `[url]target[/url]`, and HTML `<a href="target">text</a>` become native TMP links. Existing color tags retain their ARGB-to-RGBA conversion. Links default to blue with an underline and expose configurable `linkColor`/`linkUnderline` fields.

```csharp
var markup = label.GetComponent<TmpTextMarkup>();
markup.onClickLink = target => HandleLink(target);
// Clear the callback when the view is unbound.
markup.onClickLink = null;
```

The callback receives the decoded target. The SDK never opens a browser automatically. Native pointer events provide the event camera, and `TMP_TextUtilities.FindIntersectingLink` performs hit testing against the current layout. UGUI's native graphic raycaster, masks, and TMP submeshes remain responsible for clipping and rendering. A callback temporarily enables a non-raycast label while the markup component is active. Clearing the callback or disabling the component releases that temporary override. A label that already accepts raycasts retains its original setting.

Runtime tests exercise native sprite geometry, fallback textures, source preservation, text replacement, and link hits. Editor tests exercise reference resolution, generated asset identity, source preservation, and rebuilding. Both test assemblies require a `Unity.TextMeshPro` reference with `FAIRYGUI_TMPRO` enabled.
