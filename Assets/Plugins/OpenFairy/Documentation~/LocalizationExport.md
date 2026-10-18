# Static localization export

The converter compiles authored text, controller text pages, translations, and instance overrides before generating scripts. Generated views keep ordinary text as strings and template text as literal/slot programs. Variable names become a view-specific enum. Runtime updates use existing native text, input, label, button, and controller fields.

## Editor input

Add `UIProject/settings/OpenFairyLocalization.json` with explicit language sources:

```json
{
  "languages": [
    { "name": "English", "stringsFile": "UIProject/localization/English.xml" },
    { "name": "French", "stringsFile": "UIProject/localization/French.xml" }
  ]
}
```

Paths are read by the editor. They are not included in runtime data. Language order determines the generated `LocaleSet.Language` enum and direct table array. With no configuration, authored template APIs are still generated; there are no configured language tables.

Run the ordinary converter. Its existing script-reload continuation builds prefabs after the generated types compile. It writes:

- the existing view scripts with typed variable and locale methods;
- `Scripts/Localization/LocaleTable.cs` and `LocaleSet.cs` in the generated output;
- one typed language asset per configured language plus `Locales/Languages.asset`;
- template programs, direct native references, and the authored default text in each prefab.

`LocaleTable` contains generated view-specific rows. An instance override belongs to its containing component's translation key and uses its destination view's row type and variable slots. Applying a language processes child defaults before containing instance overrides. Static authored list items are updated while the list is still authored; runtime item renderers apply the same typed table when binding their own items.

The generated namespace is the configured code namespace followed by `.Localization`, or `OpenFairyGenerated.Localization` without a code namespace.

## Runtime use

```csharp
view.ApplyLocale(languages.Get(LocaleSet.Language.French));
view.SetLocalizedNoticeVariable(View.LocalizedNoticeVariable.count, "12");
view.RefreshLocalizedNoticeText();
```

The family method names follow authored object names, using the converter's identifier rules. Several setters can be followed by one refresh. Applying another language preserves the instance's assigned variables and the selected controller pages. Missing variables use occurrence-specific defaults; assigned null means an explicitly empty value. Clear returns a slot to its default behavior.

Input prompts update without replacing edited input. Explicit template refresh can update authored input text with native silent assignment. Combo-box items update without replacing values or the selected index. Button title updates preserve the visual state. Object tooltips refresh through the existing root presentation when already visible.

Text gear references are shared managed references to the exact objects already held by their controllers. Localization updates their values/default and applies the current page; it does not assign the controller page or run controller actions. Stale source page IDs use the converter's existing retained-page mapping.

All known inline images across one text family's languages, gears, and instance rows are baked into one native TMP SpriteAsset. Template formatting does not parse resource URLs at runtime. Existing TMP markup continues to handle authored rich-text formatting.

`Migrate.Localization.cs` participates in the ordinary converter. It binds compiled text after component initialization and before the final input, layout and relation synchronization and prefab save. No separate runtime localization loader or additional conversion step is required.

## Tests

`LocalizationImportTests` uses isolated source/output folders and real converter phases. It compares the compiled support classes with the actual generated scripts, bakes prefabs and locale assets, reopens the prefabs, and checks managed-reference identity, variable/gear behavior, instance/property/list overrides, input/selection preservation, and stable asset GUIDs. Its support components and assets belong in the existing runtime test assembly; the editor test assembly references it. The runtime friend assembly requires internal visibility on its three generated controller overrides.

These tests must execute in Unity before native serialization or UI behavior is considered verified. Reference compilation and managed row construction do not establish native asset or visual correctness.
