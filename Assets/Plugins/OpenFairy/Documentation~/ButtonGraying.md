# Graying and input

`ButtonBase.grayed` controls appearance, including the authored disabled and selected-disabled controller pages. It does not disable clicking, checked/radio selection, `onChanged`, related-controller page changes or combo-box dropdowns. A common button with a disabled page uses selected-disabled while pressed, then returns to disabled. Buttons without those pages keep their normal pointer states.

To exclude an authored object from pointer targeting, set `touchable="false"`; the importer bakes the independent native `CanvasGroup.blocksRaycasts` channel. This also affects descendants. Calling an event handler directly bypasses raycasting and is not a test of touchability.

Native `UnityEngine.UI.Selectable.interactable` is a separate API that controls interaction of a native Selectable. OpenFairy's buttons are event-driven components, rather than Selectable subclasses; a CanvasGroup's `interactable` flag alone does not disable their custom handlers. Use the baked raycast channel when disabling a native interface region, optionally together with visual graying.

The runtime tests distinguish these behaviors using actual button handlers, controller pages and a GraphicRaycaster. Run them in Unity; offline compilation does not execute the native event or layout systems.
