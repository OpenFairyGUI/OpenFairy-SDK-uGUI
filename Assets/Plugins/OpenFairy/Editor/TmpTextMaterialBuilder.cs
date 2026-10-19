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

        public static void ConfigureStroke(TextMeshProUGUI text, string assetPath, Color color, float size)
        {
            text.fontSharedMaterial = BakeStroke(text.fontSharedMaterial, text.font, text.fontSize, assetPath, color, size);
            text.UpdateMeshPadding();
        }

        public static Material BakeShadow(Material source, TMP_FontAsset font, float fontSize, string assetPath, Color color, Vector2 offset)
        {
            var preset = Preset(source, assetPath, out var created, out var before);
            preset.EnableKeyword("UNDERLAY_ON");
            preset.DisableKeyword("UNDERLAY_INNER");
            preset.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0);
            preset.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0);
            ShaderUtilities.UpdateShaderRatios(preset);
            var scale = PixelsPerUnit(preset, font, fontSize, ShaderUtilities.ID_ScaleRatio_C);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x / scale);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset.y / scale);
            ShaderUtilities.UpdateShaderRatios(preset);
            Save(preset, assetPath, created, before);
            return preset;
        }

        // FairyGUI 描边在字形背后向四周扩出 size 像素、不侵入字面。SDF 描边以字形边缘为中心各占一半，
        // 因此按同一宽度外扩字面（_FaceDilate），描边内沿落回原边缘，外沿外扩 2 × 宽度。
        public static Material BakeStroke(Material source, TMP_FontAsset font, float fontSize, string assetPath, Color color, float size)
        {
            var preset = Preset(source, assetPath, out var created, out var before);
            preset.EnableKeyword(ShaderUtilities.Keyword_Outline);
            preset.SetColor(ShaderUtilities.ID_OutlineColor, color);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);
            preset.SetFloat(ShaderUtilities.ID_FaceDilate, 0);
            ShaderUtilities.UpdateShaderRatios(preset);
            var width = size / (2 * PixelsPerUnit(preset, font, fontSize, ShaderUtilities.ID_ScaleRatio_A));
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            preset.SetFloat(ShaderUtilities.ID_FaceDilate, width);
            ShaderUtilities.UpdateShaderRatios(preset);
            Save(preset, assetPath, created, before);
            return preset;
        }

        // 材质距离单位对应的界面像素：图集距离 × 缩放比 × 字号/采样点数。
        private static float PixelsPerUnit(Material preset, TMP_FontAsset font, float fontSize, int scaleRatio) =>
            preset.GetFloat(ShaderUtilities.ID_GradientScale) * preset.GetFloat(scaleRatio)
            * fontSize / font.faceInfo.pointSize * font.faceInfo.scale;

        private static Material Preset(Material source, string assetPath, out bool created, out int before)
        {
            var preset = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            created = preset == null;
            if (created) preset = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(assetPath) };
            before = preset.ComputeCRC();
            if (preset != source) preset.CopyPropertiesFromMaterial(source);
            return preset;
        }

        private static void Save(Material preset, string assetPath, bool created, int before)
        {
            if (created) AssetDatabase.CreateAsset(preset, assetPath);
            else if (before != preset.ComputeCRC()) EditorUtility.SetDirty(preset);
        }
    }
}
#endif
