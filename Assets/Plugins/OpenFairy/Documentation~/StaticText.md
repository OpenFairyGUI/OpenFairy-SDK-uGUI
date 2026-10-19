# Compiled text and static localization

## Builtin text fitting

The Builtin `TextField` renderer preserves authored text and `fontSize`. Its `overflowMode` is `Default`, `Shrink` or `Ellipsis`; Migrate saves the mode from the authored `autoSize` setting. `Default` retains native wrapping and overflow flags. `Both` and `Height` continue to use the baked ContentSizeFitter.

`Shrink` chooses the largest fitting point-size scale up to the authored size, remeasuring wrapped lines and dynamic-font rich-text sizes. Dynamic and bitmap glyphs participate; inline images retain their authored size. Changing text, font size, alignment or the rectangle recalculates the result, so short text and larger rectangles restore the authored size. The rectangle itself is unchanged.

`Ellipsis` keeps the authored size and replaces the last visible tail with U+2026 when the fixed rectangle cannot show the full content. Hidden glyphs and image placements are excluded from the output. Inline Image nodes are baked once with serialized references, including initially hidden images; fitting updates their placement and visibility without rebuilding them. Rerun Migrate after updating to persist these references and overflow settings.

The converter compiles authored templates and configured translations into typed view methods, native references and direct language assets. Runtime template state formats already compiled literal/slot programs. Generated assignments target existing TMP, Label, Button, Tooltip, ComboBox and gear fields; there is no alternate UI object model. See [Static localization export](LocalizationExport.md) for configuration and asset flow.

## Runtime surface

`TextTemplate` stores literal spans, variable slot indices and occurrence-specific defaults. `TextTemplateState` owns one text's assigned values and a reused StringBuilder. A null assignment differs from an unassigned slot: null renders empty, while unassigned renders that occurrence's default. `Clear` restores the default. Parsing and variable-name lookup happen only in the editor; formatting uses integer slots.

The editor groups a text's base, selected title, gear choices and translations under one variable schema. Generated methods are typed:

```csharp
panel.SetCaptionVariable(Panel.CaptionVariable.count, "8");
panel.RefreshCaptionText();
panel.ApplyCaptionTexts(locale.Caption);
```

Setting a variable does not flush. Refresh formats all choices, writes their native destinations and reapplies the current text gear. Applying a new locale keeps assigned values and refreshes. It does not set controller.page, invoke controller events or rerun controller actions. Ordinary localized text emits string fields and direct assignments, with no template state.

Variable enum names use the importer's identifier function and deterministic suffixes for collisions. Table fields reference generated view-specific row types. A generated language asset contains a fixed array of direct typed table references, addressed by its generated Language enum. The application owns the current language/table and passes it to the generated ApplyLocale methods. There is no global string registry or runtime resource-key lookup.

## Editor inputs

`FairyTranslations` consumes the raw standard translation XML string. Its XmlTextReader disables normalization, preserving authored CR, CRLF and LF as the original text reader did; receiving an already normalized XDocument would lose that information. Duplicate names use the last value. Missing keys keep authored text; present empty values stay empty. Keys only exist in the editor input.

`FairyTextSlots` produces explicit addresses for text, prompt, tooltip, text gear choices/default, title/selected title, ComboBox items, component properties and list item properties. Suffixes follow the authored component's translation contract:

| Property | Suffix after component key |
| --- | --- |
| Text/title | element ID |
| Prompt / tooltip | ID-prompt / ID-tips |
| Selected title | ID-0 |
| Text gear value / default | ID-texts_N / ID-texts_def |
| Component property | ID-cp-target |
| List item title / selected title | ID-N / ID-N-0 |
| List item property | ID-N-target |
| ComboBox item | ID-N |

Component/list overrides inherit the actual target text's vars flag. The reader accepts an editor-only resolver for that target. It must resolve the source component and target path, rather than assume the parent XML has a vars attribute. `TextChoice.component` preserves a parent's key ownership for an override while retaining child-owned gear choices.

Share the editor schema List across a source text and all its instance override families. Collect every locale/override before emitting variable enums or allocating runtime states. Existing compiled slot indices remain stable as the schema grows. This prevents an extra translated/override variable from changing the meaning of a child's earlier slot.

## Conversion pipeline

1. Parse the authored vars flag. Index source translation addresses and direct native destinations while source IDs and binding metadata are available.
2. Collect all effective text families, including parent/list overrides. Resolve referenced title text vars from source metadata. Compile all locales and the authored fallback with shared schemas.
3. Emit row classes and template APIs into the existing generated views. Emit one typed table class and a language-reference asset class. Use the existing identifier/reserved-name allocator; allocate private native references for localized children omitted by ignoreNoname.
4. After scripts compile and native children/controllers are bound, serialize authored row defaults into the prefab, format the initial displayed text and save. Serialize language rows into direct ScriptableObject table assets. Do not change the template parser or resolve keys at runtime.
5. Generate ApplyLocale calls in order: child defaults first, then owner-specific instance overrides. An override updates the child's row/state, rather than formatting via a second independent state in the parent. Dynamic list renderers apply their current typed table when binding pooled items.

Gear assignments must use the importer's retained native page mapping, not the original XML ordinal after stale pages are removed. When no explicit gear default exists, use the base text choice. Update all gear strings before applying the current page. Selected titles update title/selectedTitle in the owning generated button and call its inherited protected RefreshTitle; do not call RefreshState, which can change a pressed visual page. Prompt writes the existing placeholder rather than editable input contents. ComboBox item translation preserves values and selectedIndex.

`StaticTextCode` accepts importer-owned target expressions and final refresh statements. `StaticLocaleCode` emits typed table/language classes; the converter emits child-first ApplyLocale methods. They deliberately do not discover objects or properties at runtime. The Migrate partial compiles and binds these structures during the ordinary export and saves the direct language assets after prefab creation.

## Validation

Pure tests cover defaults, escapes, missing/empty/null values, repeated variables, literal braces without vars, locale/gear switching, instance-owned keys, stable shared schemas and translation suffixes. Native tests cover JSON serialization, real TMP/Label assignment and generated gear refresh without controller-page changes. Generated fixtures are emitted by the same code generator. Compilation against native assemblies is a syntax/reference check, not native execution or visual acceptance.
