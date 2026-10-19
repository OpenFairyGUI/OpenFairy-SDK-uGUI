#if FAIRYGUI_TMPRO
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OpenFairy.UGUI.Editor
{
    public static class TmpTextMaterialBuilder
    {
        public static void ConfigureShadow(TextMeshProUGUI text, string assetPath, Color color, Vector2 offset)
        {
            text.fontSharedMaterial = BakeShadow(text.fontSharedMaterial, text.font, text.fontSize, assetPath, color, offset);
            text.UpdateMeshPadding();
        }

        public static Material BakeShadow(Material source, TMP_FontAsset font, float fontSize, string assetPath, Color color, Vector2 offset)
        {
            var preset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            var created = preset == null;
            if (created) preset = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(assetPath) };
            var before = preset.ComputeCRC();
            if (preset != source) preset.CopyPropertiesFromMaterial(source);
            preset.EnableKeyword("UNDERLAY_ON");
            preset.DisableKeyword("UNDERLAY_INNER");
            preset.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0);
            ShaderUtilities.UpdateShaderRatios(preset);
            var scale = preset.GetFloat(ShaderUtilities.ID_GradientScale) * preset.GetFloat(ShaderUtilities.ID_ScaleRatio_C)
                * fontSize / font.faceInfo.pointSize * font.faceInfo.scale;
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x / scale);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset.y / scale);
            ShaderUtilities.UpdateShaderRatios(preset);
            if (created) AssetDatabase.CreateAsset(preset, assetPath);
            else if (before != preset.ComputeCRC()) EditorUtility.SetDirty(preset);
            return preset;
        }
    }
}
#endif
