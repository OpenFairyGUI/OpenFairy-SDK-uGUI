# Import settings

The importer reads the existing FairyGUI project settings in `UIProject/settings`.

`Publish.json` supports `codeGeneration.packageName` as an optional namespace prefix. Each generated package keeps its own namespace beneath that prefix. For example, prefix `Example.UI` and package `Controls` generate `Example.UI.Controls`. Cross-package fields use fully qualified generated types. An empty prefix preserves package-only namespaces.

`Common.json` accepts `captureLayer` for the dedicated object-filter capture layer. It defaults to 31. Reserve that layer for capture geometry, exclude it from normal UI cameras, and choose another index when the application already uses layer 31. The importer bakes the selected layer into object-filter components; no runtime resource lookup is needed.

Default-only look gears are valid even without a page/value list. Their default appearance applies to controller pages that have no explicit override. Empty or whitespace-only icon overrides clear the icon.

Generated members avoid the native MonoBehaviour serialization header and the base component's `controllerChildren` field. A colliding identifier receives a trailing underscore. For example, the usual member prefix and an authored child named `Name` produce `m_Name_`, since `m_Name` belongs to Unity's object header. Regenerate scripts and prefabs together after changing generation settings.

## Preview-only list items

The authoring `autoClearItems="true"` setting is a publish directive, not a
runtime lifecycle rule. The importer removes the corresponding List/Tree and
ComboBox item nodes from an in-memory publication view. Prefab construction,
resource-reference collection and generated localization consume this same view.
The authoring files are never rewritten. Omitted or false settings retain their
items, and `defaultItem` remains a direct prefab reference for later `Fill` calls.
Explicit empty ComboBox overrides also replace inherited item/value arrays and
remain empty localization rows.

For already generated assets, regenerate affected view scripts, wait for Unity
compilation, then rebuild their prefabs and save language assets with the complete
localization context. Runtime code does not clear stale published samples or
repair old generated bindings. Hiding and showing a populated list does not clear
its runtime items.

This follows the FairyGUI authoring documentation for
[lists](https://www.fairygui.com/docs/editor/list/) and
[combo boxes](https://www.fairygui.com/docs/editor/combobox/).

## Button instance title colors

An authored button instance `titleColor` changes its title and the title color
gear's current controller page before the instance's selected state is applied.
When that page uses the gear's fallback, the importer adds an explicit page value;
it does not change the fallback or other state colors. Returning to the initial
page restores the instance color. A title without a color gear receives the color
directly, while an omitted override preserves the definition.

Existing published views must be rebuilt where they contain the affected button
instances. This does not change generated fields or require runtime XML lookup.

## Authored names and generated identifiers

Authoring names are not C# identifiers. The importer preserves them in the source
and published object/transition names, while allocating C# names in the appropriate
namespace, class or enum scope. Keywords and invalid leading characters receive an
underscore; punctuation becomes underscores and valid Unicode identifier parts
are retained. Collisions receive stable suffixes instead of silently dropping a
different authored entry. Page IDs and controller indices keep their original
meaning. A blank page name uses its page ID as before.

Controllers, child fields, transition fields, hook types/methods, localized text
families and private bindings share the generated member scope. Enum members avoid the reserved backing-field name. A member may retain the
same name as its containing enum, as permitted by C#. Exact duplicate
authored child names retain the first public reference; private localization
bindings still distinguish their source IDs. Duplicate controller names remain an
ambiguous authoring contract; the FairyGUI controller documentation advises against
them. Blank resource filenames or otherwise invalid authoring structures do not
become valid merely because a C# placeholder can be allocated.

Generated framework and component type references are fully qualified. Namespace
and component-name allocation is deterministic for the complete indexed source
set. Index the same source/dependency packages for script generation and prefab
construction, including narrow re-publication. Regenerate scripts, wait for Unity
compilation, then rebuild affected prefabs and locale assets together when an old
identifier must change. Runtime code does not repair stale serialized field or
managed-reference identities.

See the authoring guidance for [publishing](https://www.fairygui.com/docs/editor/publish/)
and [controllers](https://www.fairygui.com/docs/editor/controller/).
