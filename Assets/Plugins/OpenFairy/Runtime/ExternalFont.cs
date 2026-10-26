using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace OpenFairy.UGUI
{
    // File fonts use TextCore's rasterizer; legacy Font.CharacterInfo rounds their metrics.
    internal sealed class ExternalFont
    {
        internal readonly struct Character
        {
            public readonly GlyphMetrics metrics;
            public readonly RectInt pixels;
            public Character(GlyphMetrics metrics, RectInt pixels)
            {
                this.metrics = metrics;
                this.pixels = pixels;
            }
        }

        private static readonly Dictionary<(Font, GlyphRenderMode), ExternalFont> Fonts = new();
        private readonly Font _font;
        private readonly GlyphRenderMode _mode;
        private readonly Dictionary<(int, char), Character> _characters = new();
        private int _x = 2, _y = 2, _rowHeight;
        public Texture2D texture { get; private set; }
        public int version { get; private set; }

        private ExternalFont(Font font, GlyphRenderMode mode)
        {
            _font = font;
            _mode = mode;
            texture = CreateTexture(1024);
        }

        public static ExternalFont Get(Font font, GlyphRenderMode mode)
        {
            var key = (font, mode);
            if (!Fonts.TryGetValue(key, out var value)) Fonts[key] = value = new ExternalFont(font, mode);
            return value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Clear()
        {
            foreach (var font in Fonts.Values) OwnedObject.Destroy(font.texture);
            Fonts.Clear();
        }

        private static Texture2D CreateTexture(int size)
        {
            var value = new Texture2D(size, size, TextureFormat.Alpha8, false, true)
            {
                name = "OpenFairy file font atlas",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            value.SetPixelData(new byte[size * size], 0);
            value.Apply(false);
            return value;
        }

        public bool TryGet(char character, int size, out Character value) => _characters.TryGetValue((size, character), out value);

        public void Request(string text, int size)
        {
            List<uint> missing = null;
            foreach (var ch in text)
                if (ch is not '\n' and not '\r' && !_characters.ContainsKey((size, ch)))
                {
                    missing ??= new List<uint>();
                    if (!missing.Contains(ch)) missing.Add(ch);
                }
            if (missing == null) return;

            // FontAsset exposes the engine's public rasterization API. Its temporary
            // per-size atlas is copied into the shared, unscaled multi-size atlas.
            var asset = FontAsset.CreateFontAsset(_font, size, 0, _mode,
                Mathf.Max(256, Mathf.NextPowerOfTwo(size + 4)), Mathf.Max(256, Mathf.NextPowerOfTwo(size + 4)), AtlasPopulationMode.Dynamic);
            asset.isMultiAtlasTexturesEnabled = true;
            try
            {
                asset.TryAddCharacters(missing.ToArray());
                foreach (var unicode in missing)
                {
#pragma warning disable CS0618 // Public glyph rectangles are still required by a custom uGUI mesh.
                    if (!asset.characterLookupTable.TryGetValue(unicode, out var character))
                    {
                        _characters[(size, (char)unicode)] = default;
                        continue;
                    }
#pragma warning restore CS0618
                    var glyph = character.glyph;
                    var rect = glyph.glyphRect;
                    var placement = new RectInt();
                    if (rect.width != 0 && rect.height != 0)
                    {
                        placement = Reserve(rect.width, rect.height);
                        var sourceTexture = asset.atlasTextures[glyph.atlasIndex];
                        var source = sourceTexture.GetPixelData<byte>(0);
                        var destination = texture.GetPixelData<byte>(0);
                        for (var row = 0; row < rect.height; row++)
                            for (var column = 0; column < rect.width; column++)
                                destination[(placement.y + row) * texture.width + placement.x + column] =
                                    source[(rect.y + row) * sourceTexture.width + rect.x + column];
                    }
                    _characters[(size, (char)unicode)] = new Character(glyph.metrics, placement);
                }
                texture.Apply(false);
            }
            finally
            {
                foreach (var atlas in asset.atlasTextures) OwnedObject.Destroy(atlas);
                OwnedObject.Destroy(asset.material);
                OwnedObject.Destroy(asset);
            }
        }

        private RectInt Reserve(int width, int height)
        {
            while (width + 4 > texture.width) Grow();
            if (_x + width + 2 > texture.width)
            {
                _x = 2;
                _y += _rowHeight + 2;
                _rowHeight = 0;
            }
            while (_y + height + 2 > texture.height) Grow();
            var result = new RectInt(_x, _y, width, height);
            _x += width + 2;
            _rowHeight = Mathf.Max(_rowHeight, height);
            return result;
        }

        private void Grow()
        {
            var old = texture;
            var size = old.width * 2;
            if (size > SystemInfo.maxTextureSize) throw new InvalidOperationException("File font atlas exceeds the device texture size.");
            texture = CreateTexture(size);
            var source = old.GetPixelData<byte>(0);
            var destination = texture.GetPixelData<byte>(0);
            for (var row = 0; row < old.height; row++)
                for (var column = 0; column < old.width; column++)
                    destination[row * size + column] = source[row * old.width + column];
            OwnedObject.Destroy(old);
            version++;
        }
    }
}
