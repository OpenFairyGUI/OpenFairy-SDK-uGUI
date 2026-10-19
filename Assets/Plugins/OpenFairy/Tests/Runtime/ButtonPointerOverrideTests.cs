using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using State = OpenFairy.UGUI.Tests.PointerOverrideButton.State;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class ButtonPointerOverrideTests
    {
        [UnityTest]
        public IEnumerator Native_pointer_dispatch_preserves_base_visuals_with_derived_handlers()
        {
            var previous = EventSystem.current;
            var events = new GameObject("Pointer events", typeof(EventSystem));
            var go = new GameObject("Derived button", typeof(RectTransform), typeof(PointerOverrideButton));
            try
            {
                var button = go.GetComponent<PointerOverrideButton>();
                var pointer = new PointerEventData(events.GetComponent<EventSystem>());
                var cases = 0;
                foreach (ButtonMode mode in Enum.GetValues(typeof(ButtonMode)))
                foreach (var selected in new[] { false, true })
                foreach (var grayed in new[] { false, true })
                {
                    if (mode == ButtonMode.Common && selected) continue;
                    button.mode = mode;
                    button.selected = selected;
                    button.grayed = grayed;
                    var idle = grayed ? selected ? State.selectedDisabled : State.disabled : selected ? State.down : State.up;
                    var hover = grayed ? idle : selected ? State.selectedOver : State.over;
                    pointer.button = PointerEventData.InputButton.Left;
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(button.controller.page, Is.EqualTo(hover));
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerDownHandler);
                    var pressed = mode == ButtonMode.Common ? grayed ? State.selectedDisabled : State.down : hover;
                    Assert.That(button.controller.page, Is.EqualTo(pressed));
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerExitHandler);
                    Assert.That(button.controller.page, Is.EqualTo(pressed));
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerUpHandler);
                    Assert.That(button.controller.page, Is.EqualTo(mode == ButtonMode.Common ? idle : pressed));
                    button.RefreshState();
                    Assert.That(button.controller.page, Is.EqualTo(idle));

                    pointer.button = PointerEventData.InputButton.Right;
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerEnterHandler);
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerDownHandler);
                    Assert.That(button.controller.page, Is.EqualTo(hover));
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerExitHandler);
                    Assert.That(button.controller.page, Is.EqualTo(idle));
                    cases++;
                }
                Assert.That(button.enters, Is.EqualTo(cases * 2));
                Assert.That(button.exits, Is.EqualTo(cases * 2));
                Assert.That(button.presses, Is.EqualTo(cases * 2));
                Assert.That(button.releases, Is.EqualTo(cases * 2));
            }
            finally
            {
                Object.Destroy(go);
                Object.Destroy(events);
                if (previous) EventSystem.current = previous;
            }
            yield return null;
        }
    }
}
