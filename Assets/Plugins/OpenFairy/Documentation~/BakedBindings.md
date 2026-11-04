# Baked bindings

Migrate resolves authored resources and hierarchy names in the Editor and saves
Unity object references. Generated controller methods dispatch directly to their
typed fields. Runtime controls do not use reflection or resolve resource URLs.

A component's authored `list` child is saved through `PopupContent.list`.
ComboBox and PopupMenu read that reference from the instantiated popup; renaming
the resulting hierarchy does not change the binding. Custom popup prefabs must
carry this component and its serialized list reference. Rerun Migrate after
updating the SDK to generate these bindings and the current group/scroll data.

Runtime creation remains necessary for dynamic list entries, popup instances,
programmatically assembled UI and render resources whose size follows the current
view. It is not a fallback for missing data in an imported prefab.

Lifecycle and input checks have concrete roles: a Unity component can be enabled
before its references are assigned during construction; a popup callback can
close or replace the session being dispatched; pooled items can change parent;
and uGUI forbids unregistering Graphics during a rebuild. Optional authored
features also remain optional. Invalid imported bindings should be fixed in the
source or Migrate rather than silently reconstructed at runtime.
