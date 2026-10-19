#if !FAIRYGUI_TMPRO
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class NativeInputSubmitTests
    {
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        [SetUp]
        public void Setup()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            AssetDatabase.DeleteAsset("Assets/__OpenFairyInputSubmit");
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
        }

        [UnityTest] public IEnumerator Saved_single_line_input_relays_native_submit_but_not_focus_or_blur() => RoundTrip(true);
        [UnityTest] public IEnumerator Saved_multiline_input_keeps_submit_separate_from_single_line_events() => RoundTrip(false);

        private static IEnumerator RoundTrip(bool singleLine)
        {
            const string folder = "Assets/__OpenFairyInputSubmit";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var root = new GameObject("Input", typeof(RectTransform), typeof(Canvas), typeof(TextField));
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var text = root.GetComponent<TextField>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            typeof(Migrate).GetMethod("ConfigureInput", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { root, text, new OpenFairy.UGUI.Editor.Schema.Display { SingleLine = singleLine, Text = "Initial" } });
            var path = folder + "/Input.prefab";
            GameObject clone = null;
            GameObject events = null;
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                Assert.That(saved, Is.True);
                Object.DestroyImmediate(root);
                root = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                yield return new EnterPlayMode(false);
                clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                clone.SetActive(true);
                events = new GameObject("Events", typeof(EventSystem));
                yield return null;
                var input = clone.GetComponent<TextInput>();
                Assert.That(input.field.onSubmit.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(input.field.onSubmit.GetPersistentTarget(0), Is.SameAs(input.submit));
                var calls = 0;
                string value = null;
                input.onSubmit.AddListener(text => { calls++; value = text; });
                ExecuteEvents.Execute(clone, new BaseEventData(events.GetComponent<EventSystem>()), ExecuteEvents.submitHandler);
                yield return null;
                Assert.That(calls, Is.Zero, "Navigation submit activates editing; it is not a text submission.");
                input.field.onEndEdit.Invoke("Blurred");
                Assert.That(calls, Is.Zero);
                input.field.onSubmit.Invoke("Typed");
                Assert.That(calls, Is.EqualTo(singleLine ? 1 : 0));
                Assert.That(value, Is.EqualTo(singleLine ? "Typed" : null));
            }
            finally
            {
                if (events != null) Object.DestroyImmediate(events);
                if (clone != null) Object.DestroyImmediate(clone);
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
