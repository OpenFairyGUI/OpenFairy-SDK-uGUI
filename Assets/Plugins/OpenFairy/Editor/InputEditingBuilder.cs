using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using NativeInput = TMPro.TMP_InputField;
#else
using NativeInput = UnityEngine.UI.InputField;
#endif

namespace OpenFairy.UGUI.Editor
{
    public static class InputEditingBuilder
    {
        public static void Bake(GameObject root)
        {
            var fields = root.GetComponentsInChildren<NativeInput>(true);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (!field.TryGetComponent(out InputEditing editing)) editing = field.gameObject.AddComponent<InputEditing>();
                editing.field = field;
                var navigation = field.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = fields[(i + fields.Length - 1) % fields.Length];
                navigation.selectOnDown = fields[(i + 1) % fields.Length];
                field.navigation = navigation;
            }
#if FAIRYGUI_TMPRO
            foreach (var label in root.GetComponentsInChildren<Label>(true))
                if (label.titleInput != null) label.titleEditing = label.titleInput.GetComponent<InputEditing>();
#else
            foreach (var input in root.GetComponentsInChildren<TextInput>(true))
                input.editing = input.field.GetComponent<InputEditing>();
#endif
        }
    }
}
