using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;
#if FAIRYGUI_TMPRO
using TMPro;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class InputImportTests
    {
        private static void Invoke(string method, params object[] args) => typeof(Migrate)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        [Test]
        public void Label_input_overrides_distinguish_missing_false_and_zero()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "<component><displayList><component><Label/></component><component><Label password='false' maxLength='0' prompt=''/></component><component><Label password='true' maxLength='16' prompt='[color=#666666]Name[/color]'/></component></displayList></component>");
                var elements = Schema.Component.Load(path).DisplayList;
                Assert.That(elements[0].Label.Password, Is.Null);
                Assert.That(elements[0].Label.MaxLength, Is.Null);
                Assert.That(elements[0].Label.Prompt, Is.Null);
                Assert.That(elements[1].Label.Password, Is.False);
                Assert.That(elements[1].Label.MaxLength, Is.Zero);
                Assert.That(elements[1].Label.Prompt, Is.Empty);
                Assert.That(elements[2].Label.Password, Is.True);
                Assert.That(elements[2].Label.MaxLength, Is.EqualTo(16));
            }
            finally { File.Delete(path); }
        }

#if FAIRYGUI_TMPRO
        private GameObject _canvas;
        private GameObject _root;
        private TMP_InputField _input;
        private TMP_FontAsset _font;

        private void InputLabel()
        {
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            _font = TMP_FontAsset.CreateFontAsset(TMP_Settings.defaultFontAsset.sourceFontFile);
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.SetActive(false);
            _root = new GameObject("Label", typeof(RectTransform), typeof(Label));
            _root.transform.SetParent(_canvas.transform, false);
            var title = new GameObject("title", typeof(RectTransform), typeof(TMP_InputField));
            title.transform.SetParent(_root.transform, false);
            var viewport = new GameObject("Text Area", typeof(RectTransform));
            viewport.transform.SetParent(title.transform, false);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(400, 80);
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(viewport.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.rectTransform.sizeDelta = new Vector2(400, 80);
            text.font = _font;
            text.fontSize = 24;
            text.richText = false;
            _input = title.GetComponent<TMP_InputField>();
            _input.textViewport = (RectTransform)viewport.transform;
            _input.textComponent = text;
            _input.fontAsset = _font;
            _input.richText = false;
            Invoke("BindLabelTitle", _root.GetComponent<Label>(), title.transform);
            _canvas.SetActive(true);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas);
            if (_font != null)
            {
                for (int i = 0; i < _font.atlasTextureCount; i++)
                    if (_font.atlasTextures[i] != null) UnityEngine.Object.DestroyImmediate(_font.atlasTextures[i]);
                UnityEngine.Object.DestroyImmediate(_font.material);
                UnityEngine.Object.DestroyImmediate(_font);
            }
        }

        [Test]
        public void Input_label_binding_reads_and_writes_the_native_value()
        {
            InputLabel();
            var label = _root.GetComponent<Label>();
            Assert.That(new SerializedObject(label).FindProperty("titleInput").objectReferenceValue, Is.SameAs(_input));
            label.title = "value";
            Assert.That(_input.text, Is.EqualTo("value"));
            Assert.That(label.title, Is.EqualTo("value"));
            _input.SetTextWithoutNotify("native");
            Assert.That(label.title, Is.EqualTo("native"));
        }

        [Test]
        public void Instance_overrides_bake_password_length_and_prompt_on_the_native_field()
        {
            InputLabel();
            Invoke("ConfigureLabel", _root, new Schema.Extension
            {
                Title = new string('a', 20), Password = true, MaxLength = 16,
                Prompt = "[color=#666666]Name[/color]",
            }, "");
            Assert.That(_input.characterLimit, Is.EqualTo(16));
            Assert.That(_input.text, Is.EqualTo(new string('a', 20)),
                "Loading a value does not apply the native user-input limit.");
            Assert.That(_input.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));
            Assert.That(_input.placeholder, Is.Not.Null);
            var originalPrompt = _input.placeholder;
            Invoke("ConfigureLabel", _root, new Schema.Extension(), "");
            Assert.That(_input.characterLimit, Is.EqualTo(16));
            Assert.That(_input.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));
            Invoke("ConfigureLabel", _root, new Schema.Extension { Password = false, MaxLength = 0, Prompt = "" }, "");
            Assert.That(_input.characterLimit, Is.Zero);
            Assert.That(_input.contentType, Is.EqualTo(TMP_InputField.ContentType.Standard));
            Assert.That(_input.placeholder, Is.SameAs(originalPrompt));
            Assert.That(((TMP_Text)_input.placeholder).text, Is.Empty);
            Assert.That(_input.richText, Is.False);
        }

        [Test]
        public void Prompt_ubb_is_parsed_separately_and_user_input_stays_plain()
        {
            InputLabel();
            Invoke("ConfigureTmpPrompt", _input, "[color=#666666]Type here[/color]");
            var placeholder = (TMP_Text)_input.placeholder;
            Assert.That(placeholder.isActiveAndEnabled, Is.True);
            Canvas.ForceUpdateCanvases();
            placeholder.ForceMeshUpdate();
            Assert.That(placeholder.GetParsedText(), Is.EqualTo("Type here"));
            Assert.That(placeholder.raycastTarget, Is.False);
            Assert.That(placeholder.richText, Is.True);
            Assert.That(placeholder.textInfo.characterInfo[0].color.r, Is.EqualTo(0x66));
            Assert.That(placeholder.fontStyle, Is.EqualTo(_input.textComponent.fontStyle));
            _input.SetTextWithoutNotify("<b>X</b>");
            Assert.That(_input.text, Is.EqualTo("<b>X</b>"));
            Assert.That(_input.richText, Is.False);
            Assert.That(_input.textComponent.richText, Is.False);
        }
#endif
    }
}
