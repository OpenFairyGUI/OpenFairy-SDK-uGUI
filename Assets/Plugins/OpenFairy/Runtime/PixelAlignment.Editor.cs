#if UNITY_EDITOR
using UnityEngine;

namespace OpenFairy.UGUI
{
    public sealed partial class PixelAlignment
    {
        [SerializeField] internal bool editorPositionDriven;
        private bool _previewing;

        internal static void RestoreEditorPositions()
        {
            foreach (var alignment in Resources.FindObjectsOfTypeAll<PixelAlignment>())
                alignment.Restore();
            foreach (var bar in Resources.FindObjectsOfTypeAll<AuthoredScrollbar>())
                bar.RestoreEditorPreview();
        }
    }
}
#endif
