#if FAIRYGUI_TMPRO
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public class TextInputTests
    {
        private GameObject _canvas;
        private TMP_FontAsset _font;
        private TMP_InputField _input;
        private Label _label;

        private void CreateInput()
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            _font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Label", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(_canvas.transform, false);
            _label = root.AddComponent<Label>();
            var title = new GameObject("title", typeof(RectTransform));
            title.transform.SetParent(root.transform, false);
            _input = title.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Text Area", typeof(RectTransform));
            viewport.transform.SetParent(title.transform, false);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(400, 80);
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(viewport.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.sizeDelta = new Vector2(400, 80);
            text.font = _font;
            text.fontSize = 24;
            text.richText = false;
            _input.textViewport = (RectTransform)viewport.transform;
            _input.textComponent = text;
            _input.fontAsset = _font;
            _input.richText = false;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _label.titleInput = _input;
            root.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_canvas != null) Object.Destroy(_canvas);
            if (_font != null)
            {
                for (int i = 0; i < _font.atlasTextureCount; i++)
                    if (_font.atlasTextures[i] != null) Object.Destroy(_font.atlasTextures[i]);
                Object.Destroy(_font.material);
                Object.Destroy(_font);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Label_assignment_uses_native_text_without_notifying_runtime_listeners()
        {
            CreateInput();
            yield return null;
            int changes = 0;
            _input.onValueChanged.AddListener(_ => changes++);
            _label.title = "value";
            Assert.That(_input.text, Is.EqualTo("value"));
            Assert.That(_label.title, Is.EqualTo("value"));
            Assert.That(changes, Is.Zero);
            _input.SetTextWithoutNotify("native");
            Assert.That(_label.title, Is.EqualTo("native"));
            Assert.That(changes, Is.Zero);
            _input.text = "notified";
            Assert.That(changes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Character_limit_applies_to_native_key_events_and_not_loaded_values()
        {
            CreateInput();
            yield return null;
            _input.characterLimit = 16;
            _label.title = new string('x', 20);
            Assert.That(_input.text.Length, Is.EqualTo(20));
            _label.title = "";
            _input.stringPosition = 0;
            int changes = 0;
            _input.onValueChanged.AddListener(_ => changes++);
            foreach (char c in "abcdefghijklmnopqrst")
                _input.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            Assert.That(_input.text, Is.EqualTo("abcdefghijklmnop"));
            Assert.That(changes, Is.EqualTo(16));
            _input.ForceLabelUpdate();
            _input.textComponent.ForceMeshUpdate();
            _input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            _input.ProcessEvent(new Event { type = EventType.KeyDown, character = 'z' });
            Assert.That(_input.text, Is.EqualTo("abcdefghijklmnoz"));
            Assert.That(changes, Is.EqualTo(18));
            _input.readOnly = true;
            _input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            Assert.That(_input.text, Is.EqualTo("abcdefghijklmnoz"));
            Assert.That(changes, Is.EqualTo(18));
        }
    }
}
#endif
