using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenFairy.UGUI.Tests
{
    public class TextureBlurTests
    {
        [TestCase(TextureAlphaMode.Straight)]
        [TestCase(TextureAlphaMode.Premultiplied)]
        public void Gaussian_preserves_energy_and_transparent_color(TextureAlphaMode alphaMode)
        {
            const int size = 17;
            var texture = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var source = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var result = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var read = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var material = new Material(Shader.Find("OpenFairy/Texture Blur"));
            var active = RenderTexture.active;
            try
            {
                Assert.That(material.shader.isSupported, Is.True);
                var pixels = Enumerable.Repeat(alphaMode == TextureAlphaMode.Straight ? new Color(0, 0, 1, 0) : Color.clear, size * size).ToArray();
                pixels[size * (size / 2) + size / 2] = Color.red;
                texture.SetPixels(pixels);
                texture.Apply();
                Graphics.Blit(texture, source);
                var beforeBlur = RenderTexture.active;
                TextureBlur.Apply(source, result, material, 1, alphaMode);
                Assert.That(RenderTexture.active, Is.SameAs(beforeBlur));
                RenderTexture.active = result;
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                pixels = read.GetPixels();
                Assert.That(pixels.Sum(pixel => pixel.a), Is.EqualTo(1).Within(0.002));
                Assert.That(pixels.Max(pixel => pixel.a), Is.InRange(0.04, 0.06));
                foreach (var pixel in pixels.Where(pixel => pixel.a > 0.0001))
                {
                    Assert.That(pixel.g, Is.Zero.Within(0.001));
                    Assert.That(pixel.b, Is.Zero.Within(0.001));
                    Assert.That(pixel.r, Is.EqualTo(alphaMode == TextureAlphaMode.Straight ? 1 : pixel.a).Within(0.001));
                }
                TextureBlur.Apply(source, source, material, 1, alphaMode);
                RenderTexture.active = source;
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                var inPlace = read.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    Assert.That(Vector4.Distance(inPlace[i], pixels[i]), Is.LessThan(0.001));
            }
            finally
            {
                RenderTexture.active = active;
                source.Release();
                result.Release();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(result);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(material);
            }
        }
    }
}
