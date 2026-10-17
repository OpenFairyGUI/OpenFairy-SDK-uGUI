# Native input editing

`InputEditing` adds desktop undo/redo and Tab navigation to the selected native input field. It uses the Input System package and shares one implementation between native TMP fields (`FAIRYGUI_TMPRO`) and uGUI InputField. It does not replace their text rendering, clipboard, validation, caret, selection or IME implementation.

## Authoring

The importer calls `InputEditingBuilder.Bake(root)` before saving each component prefab. This serializes one InputEditing per input, forward/backward native Selectable links in hierarchy order, and the Label/TextInput references used for programmatic assignment. Repeating the bake retains the existing components. For manually authored dialogs, call the same builder on the dialog root; it defines that root as one Tab scope. Do not bake unrelated windows as a single scope.

Navigation uses Selectable's explicit down/up links. Tab moves forward and Shift+Tab moves backward, skipping inactive or noninteractable controls and wrapping within the authored chain. Read-only fields can still receive focus and selection. Multiline fields retain native Tab insertion and do not initiate focus navigation. A single-line field can navigate to a multiline field, without inserting the original Tab into the new target. The shortcut is processed after the native EventSystem update. Its per-frame consumption flag is reset by Unity's SubsystemRegistration lifecycle, including sessions with Domain Reload disabled.

## History and commands

The component retains at most five edit groups while its field is selected. Changes in one rendered frame are coalesced; caret-only changes do not add history. Focus loss or disable clears the session. Ctrl or Cmd + Z undoes, Shift + Ctrl/Cmd + Z and Ctrl/Cmd + Y redo. A new edit after undo discards redo. Editing commands are ignored for read-only fields and while the focused keyboard reports an active IME composition. Multiline behavior otherwise remains native.

Undo and redo restore the stored raw text and native selection anchor/focus, then invoke the field's existing onValueChanged once, so ordinary input bindings observe the restored value. Public `Undo`, `Redo` and `MoveFocus` return whether an operation succeeded and use the same focus/read-only/composition rules as keyboard commands.

## Programmatic values

Use `InputEditing.SetTextWithoutNotify(value)` for programmatic/server assignments. It updates the native value without a change event and immediately clears undo/redo. Imported `Label.title` and the builtin `TextInput.text` binding use this path through static baked references.

Native `field.SetTextWithoutNotify` performed directly is detected at the next update, or before an Undo/Redo command, and clears history. If application code sets the native notified `field.text` property, it must call `ResetHistory()` when that assignment is programmatic. A native onValueChanged event does not contain its origin; the component intentionally does not guess from keyboard state. Call the component setter for same-frame programmatic updates followed by user editing as well. An assignment through a read-only native field is automatically treated as programmatic, because native user insertion is disabled there.

Selection snapshots use TMP raw-string selection positions, rather than assuming rendered character indices equal UTF-16 offsets. A focused selection changed between rendered frames is captured before subsequent native editing. The component does not intercept or replay TMP's internal key-event queue; application code making multiple programmatic selection/value changes in one call should use the native APIs and explicitly reset history for programmatic values.

## Verification

InputEditingTests are PlayMode tests. They exercise real native input edits/clipboard replacement, virtual Input System keyboard state events driving the normal component lifecycle, focus changes, five-group history, Ctrl/Cmd combinations, redo truncation, selection restoration, programmatic assignments, read-only/IME gates, skipped navigation targets, wrapping and multiline Tab behavior. They restore the previous keyboard, selection and clipboard and own their generated TMP font rather than modifying the shared font asset. They inject input and are not physical keyboard automation or visual verification.

InputEditingImportTests check baked references/navigation and repeated baking in EditMode. Compiler success alone does not establish these runtime behaviors; run the native Unity tests before accepting the integration.
