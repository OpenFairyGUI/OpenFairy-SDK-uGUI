# Button right-click events

`ButtonBase.onRightClick` receives the native right-button click independently of `onClick` and `onChanged`.

```csharp
button.onRightClick.AddListener(OpenContextMenu);
```

`Button<T>` and `ComboBox<T>` forward right clicks from their existing `IPointerClickHandler`. A right click does not toggle selection, open a combo dropdown, change its value, or play the left-click sound. Middle clicks remain ignored. Left-click behavior is unchanged.

The event is available on generated button/combobox classes through their base class. No importer configuration or runtime resource lookup is required. Ordinary `Component` instances do not become click handlers. Native uGUI chooses the nearest handler; this event does not add bubbling or redispatch to ancestor buttons.

Use normal native raycast and component activation to control hit testing. `grayed` remains a visual state, independent of input.

`RightClickTests` uses an isolated official Input System test fixture and a native `PopupInputModule`, GraphicRaycaster and queued mouse events. It verifies independent left/right/middle behavior, left-click selection/audio, combo dropdown behavior, nearest-handler dispatch and disabled-component routing. These tests require execution in Unity Play Mode; offline compilation is not behavioral proof.

The three native Input System runtime cases pass as part of the current package suite. They cover right/left/middle behavior, ComboBox inheritance, no synthetic ancestor click, and native module disable/reenable; no hardware pointer or complete example-scene claim is made.
