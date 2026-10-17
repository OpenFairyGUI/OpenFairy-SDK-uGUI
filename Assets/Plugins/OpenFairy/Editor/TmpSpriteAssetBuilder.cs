#if FAIRYGUI_TMPRO
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Editor
{
    // Owns the generated asset file; source sprites and textures remain unchanged.
    public static class TmpSpriteAssetBuilder
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly Sprite Sprite;
            public Entry(string name, Sprite sprite) { Name = name; Sprite = sprite; }
        }

        private static readonly Regex Images = new(
            @"(?<literal><noparse>.*?(?:</noparse>|$))|(?<!\\)\[img\](?<source>.*?)\[/img\]|<img\b(?<attributes>[^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex Attributes = new(
            @"(?<name>[\w-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static TMP_SpriteAsset Bake(string assetPath, IReadOnlyList<Entry> entries)
        {
            if (entries.Count == 0)
                throw new ArgumentException("At least one sprite is required.", nameof(entries));
            var textures = new List<Texture2D>();
            var groups = new List<List<Entry>>();
            var hashes = new HashSet<int>();
            foreach (var entry in entries)
            {
                TmpTextMarkup.SpriteTag(entry.Name);
                if (!hashes.Add(TMP_TextUtilities.GetHashCode(entry.Name)))
                    throw new ArgumentException($"Duplicate native sprite name hash: '{entry.Name}'.", nameof(entries));
                var texture = SpriteUtility.GetSpriteTexture(entry.Sprite, false);
                int index = textures.IndexOf(texture);
                if (index < 0)
                {
                    index = textures.Count;
                    textures.Add(texture);
                    groups.Add(new List<Entry>());
                }
                groups[index].Add(entry);
            }
            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
                throw new InvalidOperationException("The native TextMeshPro/Sprite shader is required.");
            var root = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
            if (root == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    throw new ArgumentException("The output path already contains a different asset type.", nameof(assetPath));
                root = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(root, assetPath);
            }
            else
                foreach (var child in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    if (child != root)
                        Object.DestroyImmediate(child, true);
            root.name = Path.GetFileNameWithoutExtension(assetPath);
            root.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            for (int i = 0; i < groups.Count; i++)
            {
                var asset = i == 0 ? root : ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                if (i > 0)
                {
                    asset.name = root.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(asset, root);
                    root.fallbackSpriteAssets.Add(asset);
                }
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("m_Version").stringValue = "1.1.0";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                asset.faceInfo = new FaceInfo { pointSize = 1, scale = 1, ascentLine = 0.8f, descentLine = -0.2f, lineHeight = 1 };
                asset.spriteSheet = textures[i];
                asset.spriteGlyphTable.Clear();
                asset.spriteCharacterTable.Clear();
                foreach (var entry in groups[i])
                {
                    float aspect = entry.Sprite.rect.width / entry.Sprite.rect.height;
                    var glyph = new TMP_SpriteGlyph((uint)asset.spriteGlyphTable.Count,
                        new GlyphMetrics(aspect, 1, 0, 0.8f, aspect), new GlyphRect(entry.Sprite.rect), 1, 0, entry.Sprite);
                    asset.spriteGlyphTable.Add(glyph);
                    asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = entry.Name, scale = 1 });
                }
                asset.material = new Material(shader) { name = asset.name + " Material", mainTexture = textures[i] };
                asset.materialHashCode = 0;
                AssetDatabase.AddObjectToAsset(asset.material, root);
                asset.UpdateLookupTables();
                EditorUtility.SetDirty(asset);
            }
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssetIfDirty(root);
            return root;
        }

        // Resource resolution exists only in the importer-supplied callback.
        public static string BakeInlineImages(string assetPath, string text, bool ubb, bool html,
            Func<string, Sprite> resolve, out TMP_SpriteAsset spriteAsset)
        {
            var entries = new List<Entry>();
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Images.Matches(text ?? ""))
            {
                string source = Source(match, ubb, html);
                if (source == null || names.ContainsKey(source))
                    continue;
                var sprite = resolve(source);
                if (sprite == null)
                    throw new InvalidOperationException($"Inline image '{source}' did not resolve to a Sprite.");
                string name = "inline_" + entries.Count;
                entries.Add(new Entry(name, sprite));
                names.Add(source, name);
            }
            if (entries.Count == 0)
            {
                spriteAsset = null;
                return text;
            }
            spriteAsset = Bake(assetPath, entries);
            var baked = spriteAsset;
            return Images.Replace(text, match =>
            {
                string source = Source(match, ubb, html);
                if (source == null)
                    return match.Value;
                string name = names[source];
                var sprite = entries.Find(entry => entry.Name == name).Sprite;
                string attributes = match.Groups["attributes"].Value;
                float width = Dimension(attributes, "width") ?? sprite.rect.width;
                float height = Dimension(attributes, "height") ?? sprite.rect.height;
                return TmpTextMarkup.SpriteTag(baked, name, width, height, Dimension(attributes, "baseline") ?? 0.8f);
            });
        }

        private static string Source(Match match, bool ubb, bool html)
        {
            if (match.Groups["literal"].Success)
                return null;
            if (match.Groups["source"].Success)
                return ubb ? match.Groups["source"].Value.Trim() : null;
            return html ? Attribute(match.Groups["attributes"].Value, "src") : null;
        }

        private static string Attribute(string attributes, string name)
        {
            foreach (Match match in Attributes.Matches(attributes))
                if (match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return WebUtility.HtmlDecode(match.Groups["value"].Value);
            return null;
        }

        private static float? Dimension(string attributes, string name) => Attribute(attributes, name) is { } value
            ? float.Parse(value, CultureInfo.InvariantCulture) : null;
    }
}
#endif
