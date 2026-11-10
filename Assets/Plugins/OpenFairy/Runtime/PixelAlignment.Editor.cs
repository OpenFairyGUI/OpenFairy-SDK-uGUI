#if UNITY_EDITOR
using UnityEngine;

namespace OpenFairy.UGUI
{
    public sealed partial class PixelAlignment
    {
        internal static void RestoreEditorPositions()
        {
            foreach (var bar in Resources.FindObjectsOfTypeAll<AuthoredScrollbar>())
                bar.RestoreEditorPreview();
        }
    }
}
#endif
