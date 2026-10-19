using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class SelectedIconTests
    {
        private readonly List<Object> _owned = new();
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private Sprite Sprite()
        {
            var texture = Own(new Texture2D(4, 4));
            return Own(UnityEngine.Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f));
        }
        private SelectedIconTestButton Button(bool withIcon = true)
        {
            var root = Own(new GameObject("Button", typeof(RectTransform)));
            var button = root.AddComponent<SelectedIconTestButton>();
            button.mode = ButtonMode.Check;
            if (withIcon)
            {
                var child = new GameObject("Icon", typeof(RectTransform), typeof(Loader));
                child.transform.SetParent(root.transform, false);
                button.iconLoader = child.GetComponent<Loader>();
            }
            return button;
        }
        private PointerEventData Pointer()
        {
            var events = Own(new GameObject("Events", typeof(EventSystem))).GetComponent<EventSystem>();
            return new PointerEventData(events) { button = PointerEventData.InputButton.Left };
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in _owned) Object.Destroy(item);
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Check_clicks_switch_icons_but_common_pressed_visuals_do_not_select()
        {
            var button = Button();
            var normal = Sprite();
            var chosen = Sprite();
            button.icon = normal;
            button.selectedIcon = chosen;
            Assert.That(button.iconLoader.sprite, Is.SameAs(normal));
            var pointer = Pointer();
            button.OnPointerClick(pointer);
            Assert.That(button.selected, Is.True);
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            Assert.That(button.icon, Is.SameAs(normal), "Getter preserves the ordinary icon while selected.");
            button.grayed = true;
            button.OnPointerClick(pointer);
            Assert.That(button.selected, Is.False);
            Assert.That(button.iconLoader.sprite, Is.SameAs(normal));
            button.mode = ButtonMode.Common;
            button.OnPointerDown(pointer);
            button.OnPointerClick(pointer);
            Assert.That(button.controller.page, Is.EqualTo(SelectedIconTestButton.button.selectedDisabled));
            Assert.That(button.selected, Is.False);
            Assert.That(button.iconLoader.sprite, Is.SameAs(normal));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sprite_setters_preserve_normal_fallback_and_explicit_selected_clear()
        {
            var button = Button();
            var normal = Sprite();
            var next = Sprite();
            var chosen = Sprite();
            button.icon = normal;
            button.selectedIcon = chosen;
            button.selected = true;
            button.icon = next;
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            Assert.That(button.icon, Is.SameAs(next));
            button.selectedIcon = null;
            Assert.That(button.useSelectedIcon, Is.False);
            Assert.That(button.iconLoader.sprite, Is.SameAs(next));
            button.useSelectedIcon = true;
            Assert.That(button.iconLoader.sprite, Is.Null);
            Assert.That(button.iconLoader.enabled, Is.False);
            button.selected = false;
            Assert.That(button.iconLoader.sprite, Is.SameAs(next));
            Assert.That(button.iconLoader.enabled, Is.True);
            button.selectedIcon = chosen;
            button.selected = true;
            button.icon = null;
            Assert.That(button.icon, Is.Null);
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            button.useSelectedIcon = false;
            Assert.That(button.iconLoader.sprite, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Controller_selection_and_icon_gears_use_the_baked_button_reference()
        {
            var button = Button();
            var panel = Own(new GameObject("Panel", typeof(RectTransform), typeof(Canvas)));
            button.transform.SetParent(panel.transform, false);
            var normal = Sprite();
            var next = Sprite();
            var chosen = Sprite();
            button.icon = normal;
            button.selectedIcon = chosen;
            var selection = new Controller<SelectedIconTestButton.button> {
                gears = new Gear<SelectedIconTestButton.button>[] { new GearButton<SelectedIconTestButton.button> {
                    target = button.gameObject, button = button, pages = new[] { SelectedIconTestButton.button.down } } } };
            selection.page = SelectedIconTestButton.button.down;
            Assert.That(button.selected, Is.True);
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            var gear = new GearIcon<SelectedIconTestButton.button> { target = button.gameObject,
                button = button, pages = new[] { SelectedIconTestButton.button.down }, values = new[] { next }, defaultValue = normal };
            gear.Apply(SelectedIconTestButton.button.down);
            Assert.That(button.icon, Is.SameAs(next));
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            selection.page = SelectedIconTestButton.button.up;
            Assert.That(button.iconLoader.sprite, Is.SameAs(next));
            button.mode = ButtonMode.Radio;
            button.OnPointerClick(Pointer());
            Assert.That(button.selected && button.useSelectedIcon, Is.True);
            Assert.That(button.iconLoader.sprite, Is.SameAs(chosen));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Without_selected_override_visual_controller_icon_gears_keep_their_effect()
        {
            var button = Button();
            var up = Sprite();
            var down = Sprite();
            button.icon = up;
            button.controller.gears = new Gear<SelectedIconTestButton.button>[] { new GearIcon<SelectedIconTestButton.button> {
                target = button.iconLoader.gameObject, loader = button.iconLoader,
                pages = new[] { SelectedIconTestButton.button.down }, values = new[] { down }, defaultValue = up } };
            button.selected = true;
            Assert.That(button.iconLoader.sprite, Is.SameAs(down));
            Assert.That(button.icon, Is.SameAs(up));
            button.selected = false;
            Assert.That(button.iconLoader.sprite, Is.SameAs(up));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Optional_icon_receiver_is_not_required_to_retain_assigned_state()
        {
            var button = Button(false);
            var normal = Sprite();
            var chosen = Sprite();
            button.icon = normal;
            button.selectedIcon = chosen;
            button.selected = true;
            Assert.That(button.icon, Is.SameAs(normal));
            Assert.That(button.selectedIcon, Is.SameAs(chosen));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Non_button_icon_gears_use_their_exact_baked_receiver()
        {
            var root = Own(new GameObject("Label", typeof(RectTransform), typeof(Label)));
            var label = root.GetComponent<Label>();
            var decoy = new GameObject("Other", typeof(RectTransform), typeof(Loader));
            decoy.transform.SetParent(root.transform, false);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Loader));
            icon.transform.SetParent(root.transform, false);
            label.iconLoader = icon.GetComponent<Loader>();
            var sprite = Sprite();
            var gear = new GearIcon<SelectedIconTestButton.button> { target = root, label = label,
                pages = new[] { SelectedIconTestButton.button.down }, values = new[] { sprite } };
            gear.Apply(SelectedIconTestButton.button.down);
            Assert.That(label.icon, Is.SameAs(sprite));
            Assert.That(decoy.GetComponent<Loader>().sprite, Is.Null);
            gear.label = null;
            gear.loader = label.iconLoader;
            gear.Apply(SelectedIconTestButton.button.up);
            Assert.That(label.iconLoader.sprite, Is.Null);
            Assert.That(label.iconLoader.enabled, Is.False);
            yield return null;
        }
    }
}
