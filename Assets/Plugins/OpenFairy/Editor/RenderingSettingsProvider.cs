using UnityEditor;
using UnityEngine;

namespace OpenFairy.UGUI.Editor
{
    internal static class RenderingSettingsProvider
    {
        internal static string GammaConfigurationError()
        {
            if (!MigrationSettings.instance.gammaRendering || QualitySettings.activeColorSpace != ColorSpace.Linear) return null;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
                || System.Type.GetType("OpenFairy.UGUI.GammaUIRendererFeature, OpenFairy.UGUI.URP") == null)
                return "Gamma UI in a Linear project currently requires Unity 6.6 and URP 17.6 or later with the Universal Renderer.";
            var api = SystemInfo.graphicsDeviceType;
            if (api != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 && api != UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
                return "Gamma UI in a Linear project currently supports Direct3D 11 and 12 only.";
            return null;
        }

        [SettingsProvider]
        private static SettingsProvider Create() => new("Project/OpenFairy", SettingsScope.Project)
        {
            label = "OpenFairy",
            guiHandler = _ =>
            {
                var settings = MigrationSettings.instance;
                EditorGUI.BeginChangeCheck();
                var gamma = EditorGUILayout.Toggle(new GUIContent("Gamma UI Rendering", "Use Gamma UI colors and blending in a Linear URP project. Run Migrate after changing this option."), settings.gammaRendering);
                if (EditorGUI.EndChangeCheck()) { settings.gammaRendering = gamma; settings.Save(); }
                EditorGUILayout.HelpBox("Migrate prepares Gamma UI textures and materials. In a Linear project, also add and enable OpenFairy Gamma UI on your Universal Renderer Data. Requires Unity 6.6 / URP 17.6+ / Direct3D 11 or 12 / SDR / Screen Space Overlay. No UI compositing render texture is created. See the package README for setup and material requirements.", MessageType.Info);
                var error = GammaConfigurationError();
                if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (GUILayout.Button("Apply / Migrate UI")) Migrate.Execute();
            },
        };
    }
}
