using System;
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
        public Texture2D texture;
        public Glyph[] glyphs;

        public bool TryGetGlyph(char ch, out Glyph glyph)
        {
            foreach (var candidate in glyphs)
                if (candidate.code == ch)
                {
                    glyph = candidate;
                    return true;
                }
            glyph = default;
            return false;
        }
    }
}
