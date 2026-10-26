using UnityEditor;

namespace OpenFairy.UGUI.Editor
{
    [InitializeOnLoad]
    internal static class ExternalFontLifetime
    {
        static ExternalFontLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ExternalFont.Clear;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.ExitingEditMode)
                    ExternalFont.Clear();
            };
            EditorApplication.quitting += ExternalFont.Clear;
        }
    }
}
