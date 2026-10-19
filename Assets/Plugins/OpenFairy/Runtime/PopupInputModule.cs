using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace OpenFairy.UGUI
{
    public sealed class PopupInputModule : InputSystemUIInputModule
    {
        private readonly List<RaycastResult> _hits = new();
        private PointerEventData _pointer;

        public override void Process()
        {
            // Read the same mouse/pen actions before the native module dispatches pointer events.
            if (Root.inst != null && Root.inst.isActiveAndEnabled &&
                (eventSystem.isFocused || UnityEngine.Application.runInBackground ||
                 InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus) &&
                (Pressed(leftClick) || Pressed(rightClick) || Pressed(middleClick)))
            {
                _pointer ??= new PointerEventData(eventSystem);
                _pointer.Reset();
                _pointer.position = point.action.ReadValue<UnityEngine.Vector2>();
                _hits.Clear();
                eventSystem.RaycastAll(_pointer, _hits);
                Root.inst.CheckPopups(_hits.Count == 0 ? null : _hits[0].gameObject);
            }
            base.Process();
        }

        private static bool Pressed(InputActionReference action) => action != null && action.action.WasPressedThisFrame();

        protected override void OnDisable()
        {
            _hits.Clear();
            _pointer = null;
            base.OnDisable();
        }
    }
}
