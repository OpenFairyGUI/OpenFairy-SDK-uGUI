# Import settings

The importer reads the existing FairyGUI project settings in `UIProject/settings`.

`Publish.json` supports `codeGeneration.packageName` as an optional namespace prefix. Each generated package keeps its own namespace beneath that prefix. For example, prefix `Example.UI` and package `Controls` generate `Example.UI.Controls`. Cross-package fields use fully qualified generated types. An empty prefix preserves package-only namespaces.

`Common.json` accepts `captureLayer` for the dedicated object-filter capture layer. It defaults to 31. Reserve that layer for capture geometry, exclude it from normal UI cameras, and choose another index when the application already uses layer 31. The importer bakes the selected layer into object-filter components; no runtime resource lookup is needed.

Default-only look gears are valid even without a page/value list. Their default appearance applies to controller pages that have no explicit override. Empty or whitespace-only icon overrides clear the icon.

Generated members avoid the native MonoBehaviour serialization header and the base component's `controllerChildren` field. A colliding identifier receives a trailing underscore. For example, the usual member prefix and an authored child named `Name` produce `m_Name_`, since `m_Name` belongs to Unity's object header. Regenerate scripts and prefabs together after changing generation settings.
