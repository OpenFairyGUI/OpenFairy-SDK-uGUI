using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using NativeInput = TMPro.TMP_InputField;
#else
using NativeInput = UnityEngine.UI.InputField;
#endif

namespace OpenFairy.UGUI.Tests
{
    public class InputEditingImportTests
    {
        [Test]
        public void Builder_bakes_native_navigation_and_programmatic_binding_idempotently()
        {
            var root = new GameObject("Scope", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var label = root.AddComponent<Label>();
                var first = new GameObject("First", typeof(RectTransform), typeof(NativeInput));
                first.transform.SetParent(root.transform, false);
                var last = new GameObject("Last", typeof(RectTransform), typeof(NativeInput));
                last.transform.SetParent(root.transform, false);
                var fields = root.GetComponentsInChildren<NativeInput>(true);
                var serialized = new SerializedObject(label);
#if FAIRYGUI_TMPRO
                serialized.FindProperty("titleInput").objectReferenceValue = fields[0];
#else
                var wrapper = first.AddComponent<TextInput>();
                wrapper.field = fields[0];
                serialized.FindProperty("titleInput").objectReferenceValue = wrapper;
#endif
                serialized.ApplyModifiedPropertiesWithoutUndo();
                InputEditingBuilder.Bake(root);
                var editing = first.GetComponent<InputEditing>();
                Assert.That(editing.field, Is.SameAs(fields[0]));
                Assert.That(fields[0].navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(fields[0].FindSelectableOnDown(), Is.SameAs(fields[1]));
                Assert.That(fields[1].FindSelectableOnDown(), Is.SameAs(fields[0]));
                Assert.That(fields[0].FindSelectableOnUp(), Is.SameAs(fields[1]));
#if FAIRYGUI_TMPRO
                serialized.Update();
                Assert.That(serialized.FindProperty("titleEditing").objectReferenceValue, Is.SameAs(editing));
#else
                Assert.That(wrapper.editing, Is.SameAs(editing));
#endif
                InputEditingBuilder.Bake(root);
                Assert.That(first.GetComponent<InputEditing>(), Is.SameAs(editing));
                Assert.That(root.GetComponentsInChildren<InputEditing>(true).Length, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
