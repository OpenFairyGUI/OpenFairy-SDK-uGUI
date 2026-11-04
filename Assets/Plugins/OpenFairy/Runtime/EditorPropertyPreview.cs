#if UNITY_EDITOR
using System;
using UnityEngine;

namespace OpenFairy.UGUI
{
    // Native preview snapshots serialize the original property while displaying a temporary value.
    // Bind the internal editor API once. Player builds contain neither reflection nor snapshots.
    internal static class EditorPropertyPreview
    {
        private static readonly Type Manager = typeof(RectTransform).Assembly.GetType("UnityEngine.DrivenPropertyManager");
        internal static readonly Action<UnityEngine.Object, UnityEngine.Object, string> Begin =
            (Action<UnityEngine.Object, UnityEngine.Object, string>)Manager.GetMethod("TryRegisterProperty")
                .CreateDelegate(typeof(Action<UnityEngine.Object, UnityEngine.Object, string>));
        internal static readonly Action<UnityEngine.Object> End =
            (Action<UnityEngine.Object>)Manager.GetMethod("UnregisterProperties")
                .CreateDelegate(typeof(Action<UnityEngine.Object>));
    }
}
#endif
