using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TMPro;
using NativeInput = TMPro.TMP_InputField;
#else
using NativeInput = UnityEngine.UI.InputField;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class InputEditingTests
    {
        private readonly List<GameObject> _objects = new();
        private readonly InputTestFixture _input = new();
        private Keyboard _keyboard, _previousKeyboard;
        private NativeLeakDetectionMode _leakMode;
        private EventSystem _events;
        private GameObject _previousSelection;
        private Transform _canvas;
#if FAIRYGUI_TMPRO
        private TMP_FontAsset _font;
#endif

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            _objects.Add(go);
            go.transform.SetParent(parent, false);
            return go;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True);
            _previousKeyboard = Keyboard.current;
            _leakMode = NativeLeakDetection.Mode;
            _input.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _events = EventSystem.current;
            if (_events == null) _events = Make("Events").AddComponent<EventSystem>();
            _previousSelection = _events.currentSelectedGameObject;
            var canvas = Make("Canvas");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas.transform;
#if FAIRYGUI_TMPRO
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            _font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
#endif
            yield return null;
        }

        private InputEditing Input(string name, bool multiline = false)
        {
            var go = Make(name, _canvas);
            go.SetActive(false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.clear;
            var field = go.AddComponent<NativeInput>();
            field.targetGraphic = image;
            field.lineType = multiline ? NativeInput.LineType.MultiLineNewline : NativeInput.LineType.SingleLine;
            var viewport = Make("Viewport", go.transform);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(400, 80);
            var textGo = Make("Text", viewport.transform);
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(400, 80);
#if FAIRYGUI_TMPRO
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = 24;
            text.richText = false;
#else
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.supportRichText = false;
#endif
            field.textComponent = text;
#if FAIRYGUI_TMPRO
            field.textViewport = (RectTransform)viewport.transform;
            field.fontAsset = _font;
            field.richText = false;
#endif
            var editing = go.AddComponent<InputEditing>();
            editing.field = field;
            go.SetActive(true);
            return editing;
        }

        private IEnumerator Focus(InputEditing editing)
        {
            _events.SetSelectedGameObject(editing.gameObject);
            editing.field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.That(editing.field.isFocused, Is.True);
        }

        private static void Type(InputEditing editing, string value)
        {
            foreach (char c in value)
                editing.field.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            editing.field.ForceLabelUpdate();
#if FAIRYGUI_TMPRO
            editing.field.textComponent.ForceMeshUpdate();
#else
            Canvas.ForceUpdateCanvases();
#endif
        }

        private static void Selection(InputEditing editing, int anchor, int focus)
        {
#if FAIRYGUI_TMPRO
            editing.field.selectionStringAnchorPosition = anchor;
            editing.field.selectionStringFocusPosition = focus;
#else
            editing.field.selectionAnchorPosition = anchor;
            editing.field.selectionFocusPosition = focus;
#endif
        }

        private static void AssertSelection(InputEditing editing, int anchor, int focus)
        {
#if FAIRYGUI_TMPRO
            Assert.That(editing.field.selectionStringAnchorPosition, Is.EqualTo(anchor));
            Assert.That(editing.field.selectionStringFocusPosition, Is.EqualTo(focus));
#else
            Assert.That(editing.field.selectionAnchorPosition, Is.EqualTo(anchor));
            Assert.That(editing.field.selectionFocusPosition, Is.EqualTo(focus));
#endif
        }

        private IEnumerator Tap(params Key[] keys)
        {
            Assert.That(_keyboard.enabled, Is.True);
            Assert.That(Keyboard.current, Is.SameAs(_keyboard));
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                _events.SetSelectedGameObject(_previousSelection);
                foreach (var go in _objects)
                    if (go != null) Object.Destroy(go);
                _objects.Clear();
                yield return null;
                InputSystem.RemoveDevice(_keyboard);
#if FAIRYGUI_TMPRO
                if (_font != null)
                {
                    for (int i = 0; i < _font.atlasTextureCount; i++)
                        if (_font.atlasTextures[i] != null) Object.Destroy(_font.atlasTextures[i]);
                    Object.Destroy(_font.material);
                    Object.Destroy(_font);
                }
#endif
                yield return null;
            }
            finally
            {
                _input.TearDown();
                NativeLeakDetection.Mode = _leakMode;
                if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator History_is_bounded_and_ends_with_the_native_focus_session()
        {
            var first = Input("First");
            var second = Input("Second");
            yield return Focus(first);
            foreach (char c in "abcdefg")
            {
                Type(first, c.ToString());
                yield return null;
            }
            for (int i = 0; i < 5; i++) Assert.That(first.Undo(), Is.True);
            Assert.That(first.field.text, Is.EqualTo("ab"));
            Assert.That(first.Undo(), Is.False);
            yield return Focus(second);
            Assert.That(first.Redo(), Is.False);
            yield return Focus(first);
            Assert.That(first.Redo(), Is.False);
            Assert.That(first.Undo(), Is.False);
        }

        [UnityTest]
        public IEnumerator Keyboard_shortcuts_support_control_command_redo_readonly_and_ime()
        {
            var input = Input("Input");
            yield return Focus(input);
            Type(input, "a");
            yield return null;
            Type(input, "b");
            yield return Tap(Key.LeftCtrl, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("a"));
            yield return Tap(Key.LeftCtrl, Key.LeftShift, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("ab"));
            yield return Tap(Key.LeftMeta, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("a"));
            yield return Tap(Key.LeftMeta, Key.Y);
            Assert.That(input.field.text, Is.EqualTo("ab"));
            input.field.readOnly = true;
            yield return Tap(Key.LeftCtrl, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("ab"));
            input.field.readOnly = false;
            _keyboard.OnIMECompositionChanged(new IMECompositionString("x"));
            yield return Tap(Key.LeftCtrl, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("ab"));
            _keyboard.OnIMECompositionChanged(new IMECompositionString(""));
            yield return Tap(Key.LeftCtrl, Key.Z);
            Assert.That(input.field.text, Is.EqualTo("a"));
        }

        [UnityTest]
        public IEnumerator Paste_undo_restores_the_selection_and_new_edits_discard_redo()
        {
            var input = Input("Input");
            input.SetTextWithoutNotify("abcdef");
            yield return Focus(input);
            Selection(input, 5, 2);
            yield return null;
            string clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer = "XY";
                var modifier = Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer
                    ? EventModifiers.Command : EventModifiers.Control;
                input.field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.V, modifiers = modifier });
            }
            finally { GUIUtility.systemCopyBuffer = clipboard; }
            Assert.That(input.field.text, Is.EqualTo("abXYf"));
            int notifications = 0;
            input.field.onValueChanged.AddListener(_ => notifications++);
            Assert.That(input.Undo(), Is.True);
            Assert.That(input.field.text, Is.EqualTo("abcdef"));
            AssertSelection(input, 5, 2);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(input.Redo(), Is.True);
            Assert.That(input.field.text, Is.EqualTo("abXYf"));
            AssertSelection(input, 4, 4);
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(input.Undo(), Is.True);
            Type(input, "Z");
            Assert.That(input.field.text, Is.EqualTo("abZf"));
            Assert.That(input.Redo(), Is.False);
        }

        [UnityTest]
        public IEnumerator Programmatic_values_reset_history_without_notifying_or_undoing_server_data()
        {
            var input = Input("Input");
            var label = Make("Label", _canvas).AddComponent<Label>();
#if FAIRYGUI_TMPRO
            label.titleInput = input.field;
            label.titleEditing = input;
#else
            var wrapper = input.gameObject.AddComponent<TextInput>();
            wrapper.field = input.field;
            wrapper.editing = input;
            label.titleInput = wrapper;
#endif
            yield return Focus(input);
            Type(input, "old");
            int notifications = 0;
            input.field.onValueChanged.AddListener(_ => notifications++);
            label.title = "server";
            Assert.That(notifications, Is.Zero);
            Assert.That(input.Undo(), Is.False);
            Type(input, "!");
            Assert.That(input.Undo(), Is.True);
            Assert.That(input.field.text, Is.EqualTo("server"));
            input.field.SetTextWithoutNotify("silent native update");
            yield return null;
            Assert.That(input.Undo(), Is.False);
            input.field.text = "notified native update";
            input.ResetHistory();
            Assert.That(input.Undo(), Is.False);
            input.field.readOnly = true;
            input.field.text = "readonly server update";
            input.field.readOnly = false;
            Assert.That(input.Undo(), Is.False);
        }

        private static void Link(params InputEditing[] inputs)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                var navigation = inputs[i].field.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnDown = inputs[(i + 1) % inputs.Length].field;
                navigation.selectOnUp = inputs[(i + inputs.Length - 1) % inputs.Length].field;
                inputs[i].field.navigation = navigation;
            }
        }

        [UnityTest]
        public IEnumerator Tab_uses_one_native_navigation_step_skips_invalid_fields_and_wraps()
        {
            var first = Input("First");
            var hidden = Input("Hidden");
            var disabled = Input("Disabled");
            var last = Input("Last");
            Link(first, hidden, disabled, last);
            hidden.gameObject.SetActive(false);
            disabled.field.interactable = false;
            last.field.readOnly = true;
            yield return Focus(first);
            first.field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab, character = '\t' });
            yield return Tap(Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(last.gameObject));
            Assert.That(first.field.text, Is.Empty);
            Assert.That(last.field.text, Is.Empty);
            yield return Tap(Key.LeftShift, Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(first.gameObject));
            yield return Focus(last);
            yield return Tap(Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(first.gameObject));
        }

        [UnityTest]
        public IEnumerator Multiline_tab_stays_text_and_ime_does_not_change_focus()
        {
            var first = Input("First");
            var multiline = Input("Multiline", true);
            Link(first, multiline);
            yield return Focus(multiline);
            multiline.field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Tab, character = '\t' });
            yield return Tap(Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(multiline.gameObject));
            Assert.That(multiline.field.text, Is.EqualTo("\t"));
            yield return Focus(first);
            _keyboard.OnIMECompositionChanged(new IMECompositionString("x"));
            yield return Tap(Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(first.gameObject));
            _keyboard.OnIMECompositionChanged(new IMECompositionString(""));
            yield return Tap(Key.Tab);
            Assert.That(_events.currentSelectedGameObject, Is.SameAs(multiline.gameObject));
            Assert.That(multiline.field.text, Is.EqualTo("\t"));
        }
    }
}
