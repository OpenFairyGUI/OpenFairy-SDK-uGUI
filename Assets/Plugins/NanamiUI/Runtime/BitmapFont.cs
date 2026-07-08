using System;
using System.Collections.Generic;
using UnityEngine;

namespace NanamiUI
{
    public class BitmapFont : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public int code;
            public Rect uv;
            public float x;
            public float y;
            public float width;
            public float height;
            public int advance;
            public int lineHeight;
        }

        public int size;
        public bool canTint; // 标准 BMFont（白字图集）可着色；UIBuilder 逐字图片字体保留原色
        public Texture2D texture;
        public Glyph[] glyphs;

        [NonSerialized] private Dictionary<int, Glyph> _lookup;

        public bool TryGetGlyph(char ch, out Glyph glyph)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<int, Glyph>(glyphs.Length);
                foreach (var candidate in glyphs)
                    _lookup[candidate.code] = candidate;
            }
            return _lookup.TryGetValue(ch, out glyph);
        }
    }
}
