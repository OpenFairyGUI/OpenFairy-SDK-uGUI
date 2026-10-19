using UnityEngine;

namespace OpenFairy.UGUI
{
    public enum TextureAlphaMode
    {
        Straight,
        Premultiplied,
    }

    // Capture and presentation own their textures; this operation only filters pixels.
    public static class TextureBlur
    {
        private static readonly int Direction = Shader.PropertyToID("_Direction");
        private static readonly int Unpremultiply = Shader.PropertyToID("_Unpremultiply");

        public static void Apply(RenderTexture source, RenderTexture destination, Material material,
            float radius, TextureAlphaMode alphaMode = TextureAlphaMode.Straight)
        {
            var active = RenderTexture.active;
            if (radius <= 0)
            {
                try
                {
                    if (source != destination)
                        Graphics.Blit(source, destination);
                }
                finally { RenderTexture.active = active; }
                return;
            }

            var descriptor = source.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            var horizontal = RenderTexture.GetTemporary(descriptor);
            RenderTexture premultiplied = null;
            try
            {
                horizontal.filterMode = FilterMode.Bilinear;
                horizontal.wrapMode = TextureWrapMode.Clamp;
                var input = source;
                if (alphaMode == TextureAlphaMode.Straight)
                {
                    premultiplied = RenderTexture.GetTemporary(descriptor);
                    premultiplied.filterMode = FilterMode.Bilinear;
                    premultiplied.wrapMode = TextureWrapMode.Clamp;
                    // Convert before filtering: hidden RGB in transparent pixels must not bleed.
                    Graphics.Blit(source, premultiplied, material, 0);
                    input = premultiplied;
                }

                material.SetVector(Direction, new Vector4(radius / source.width, 0, 0, 0));
                material.SetFloat(Unpremultiply, 0);
                Graphics.Blit(input, horizontal, material, 1);
                material.SetVector(Direction, new Vector4(0, radius / source.height, 0, 0));
                material.SetFloat(Unpremultiply, alphaMode == TextureAlphaMode.Straight ? 1 : 0);
                Graphics.Blit(horizontal, destination, material, 1);
            }
            finally
            {
                RenderTexture.active = active;
                if (premultiplied != null)
                    RenderTexture.ReleaseTemporary(premultiplied);
                RenderTexture.ReleaseTemporary(horizontal);
            }
        }
    }
}
