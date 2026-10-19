# Pointer hover

Add `PointerHover` to an authored UI object and serialize listeners or retain a direct reference from its owner. `onEnter` and `onExit` are native `UnityEvent<PointerEventData>` callbacks. They can be used with buttons, panels and list containers without subclassing their visual behavior.

The component handles only pointer enter and exit. It does not register click, drag, submit or scroll handlers, so native EventSystem routing for those events is unchanged. Hovering between sibling items keeps their shared parent hovered. Disabling a hovered object emits an exit with its last pointer event, allowing pooled items and hidden panels to clear their hover presentation before reuse.

This component does not create hit areas or change raycast settings. The authored object needs its existing raycastable Graphic, or a raycastable descendant whose EventSystem hover chain includes it. Use ordinary Button or ScrollRect for clicks and scrolling. A disabled button can still expose hover information while its click is rejected by the button.

`PointerHoverTests` exercise the native PointerInputModule hover chain, button visual state and click handling, parent scroll routing, and pool disable/reuse. They require PlayMode and do not claim physical mouse or image verification.
# Extending native button pointers

`Button<T>.OnPointerEnter`, `OnPointerExit`, `OnPointerDown` and `OnPointerUp` are virtual, like `OnPointerClick`. Override them in a generated component's user partial and call the base implementation to preserve hover, pressed, selected and grayed visuals. No separate event relay component is required.
