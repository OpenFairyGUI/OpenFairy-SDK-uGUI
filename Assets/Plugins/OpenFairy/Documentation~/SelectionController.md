# List selection controllers

A list's authored `selectionController` is resolved during import. The generated controller receives a typed `GearListSelection<T>`, and the list stores a direct owner reference, controller index and page count. No runtime controller-name lookup is needed.

The selected item corresponds to the controller's page index, not its serialized page identifier. Applying a controller page updates the list without raising `onClickItem`. Rebinding or refilling the list applies the current page to its current logical items, including virtual items.

Clicking a selectable button updates the controller before `onClickItem`. Setting a valid, changed `selectedIndex` also updates it. Clearing selection, deselecting a multiple-selection item, or selecting an item beyond the controller's page count leaves the page unchanged. Controller changes already in progress are not written back. During an outgoing list-to-controller update, the incoming gear is suppressed so a multiple selection is not collapsed by its own notification.

Missing or unknown authored controller names keep the list's existing independent behavior. The importer does not create new controllers or change their definitions. A list with selection disabled does not gain controller selection.

Native tests cover baked initial pages, programmatic changes, direct pointer clicks, event counts, clearing, extra items, multiple selection, re-enable, virtual recycling/refill, and real prefab import with valid/absent/stale metadata. The import fixture uses nonsequential page identifiers to verify index semantics. Builtin compilation and native TMP behavior are separate evidence; neither is a complete demo/golden or Player validation.

The item click relay has its own `ListSelectionItem.cs` MonoScript. Controller initialization may attach it while baking the initial selection, so it must survive prefab serialization as well as runtime rebinding. Import tests force reimport, unload/reload and a second nested save, and verify every persisted behaviour maps to its exact script class. Failed prefab saves now throw instead of caching a null result or reporting a completed migration. Existing assets with a missing script must be regenerated from source; adding the script file cannot recover a lost serialized reference.
