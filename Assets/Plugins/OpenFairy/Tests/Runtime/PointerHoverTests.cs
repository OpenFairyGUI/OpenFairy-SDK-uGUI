using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public sealed class HoverTestPointerModule : PointerInputModule
    {
        public override void Process() { }
        public void Move(PointerEventData pointer, GameObject target) => HandlePointerExitAndEnter(pointer, target);
    }

    public sealed class HoverTestScrollReceiver : UIBehaviour, IScrollHandler
    {
        public Vector2 delta;
        public void OnScroll(PointerEventData pointer) => delta += pointer.scrollDelta;
    }

    public enum HoverTestState { up, down, over, selectedOver, disabled, selectedDisabled }
    public sealed class HoverTestButton : NamedStateButton<HoverTestState> { }

    public class PointerHoverTests
    {
        private readonly List<GameObject> _objects = new();

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _objects.Add(go);
            go.transform.SetParent(parent, false);
            return go;
        }

        private HoverTestPointerModule Module(out PointerEventData pointer)
        {
            Assert.That(Application.isPlaying, Is.True);
            var go = Make("Events");
            var events = go.AddComponent<EventSystem>();
            var module = go.AddComponent<HoverTestPointerModule>();
            pointer = new PointerEventData(events) { pointerId = -1 };
            return module;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var go in _objects)
                if (go != null) Object.Destroy(go);
            _objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Native_pointer_movement_preserves_parent_hover_between_sibling_items()
        {
            var module = Module(out var pointer);
            var panel = Make("Panel");
            var first = Make("First", panel.transform);
            var second = Make("Second", panel.transform);
            var panelHover = panel.AddComponent<PointerHover>();
            var firstHover = first.AddComponent<PointerHover>();
            var secondHover = second.AddComponent<PointerHover>();
            int panelEnter = 0, panelExit = 0, firstExit = 0, secondEnter = 0;
            panelHover.onEnter.AddListener(_ => panelEnter++);
            panelHover.onExit.AddListener(_ => panelExit++);
            firstHover.onExit.AddListener(_ => firstExit++);
            secondHover.onEnter.AddListener(_ => secondEnter++);

            module.Move(pointer, first);
            Assert.That(panelEnter, Is.EqualTo(1));
            module.Move(pointer, second);
            Assert.That(firstExit, Is.EqualTo(1));
            Assert.That(secondEnter, Is.EqualTo(1));
            Assert.That(panelEnter, Is.EqualTo(1));
            Assert.That(panelExit, Is.Zero);
            module.Move(pointer, null);
            Assert.That(panelExit, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Native_button_hover_click_and_parent_scroll_keep_their_original_handlers()
        {
            var module = Module(out var pointer);
            var panel = Make("Panel");
            var scroll = panel.AddComponent<HoverTestScrollReceiver>();
            var child = Make("Button", panel.transform);
            var button = child.AddComponent<HoverTestButton>();
            var hover = child.AddComponent<PointerHover>();
            int enters = 0, clicks = 0;
            hover.onEnter.AddListener(_ => enters++);
            button.onClick.AddListener(() => clicks++);
            module.Move(pointer, child);
            Assert.That(enters, Is.EqualTo(1));
            Assert.That(button.controller.page, Is.EqualTo(HoverTestState.over));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(child), Is.SameAs(child));
            ExecuteEvents.ExecuteHierarchy(child, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(child), Is.SameAs(panel));
            pointer.scrollDelta = new Vector2(0, -2);
            ExecuteEvents.ExecuteHierarchy(child, pointer, ExecuteEvents.scrollHandler);
            Assert.That(scroll.delta, Is.EqualTo(new Vector2(0, -2)));

            module.Move(pointer, null);
            button.grayed = true;
            module.Move(pointer, child);
            Assert.That(enters, Is.EqualTo(2));
            Assert.That(button.controller.page, Is.EqualTo(HoverTestState.disabled));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pooling_a_hovered_item_exits_before_reuse_and_does_not_leak_old_state()
        {
            var module = Module(out var pointer);
            var child = Make("Item");
            var hover = child.AddComponent<PointerHover>();
            int enters = 0, exits = 0;
            hover.onEnter.AddListener(_ => enters++);
            hover.onExit.AddListener(_ => exits++);
            module.Move(pointer, child);
            child.SetActive(false);
            Assert.That(exits, Is.EqualTo(1));
            module.Move(pointer, null);
            Assert.That(exits, Is.EqualTo(1));
            child.SetActive(true);
            module.Move(pointer, child);
            Assert.That(enters, Is.EqualTo(2));
            module.Move(pointer, null);
            Assert.That(exits, Is.EqualTo(2));
            hover.enabled = false;
            Assert.That(exits, Is.EqualTo(2));
            yield return null;
        }
    }
}
