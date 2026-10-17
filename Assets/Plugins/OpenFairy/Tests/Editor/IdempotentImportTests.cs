using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public sealed class ImportWitness : AssetPostprocessor
    {
        internal static string target;
        internal static int imported;
        private static void OnPostprocessAllAssets(string[] imports, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imports) if (path == target) imported++;
        }
    }

    public class IdempotentImportTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static object Call(string name, params object[] args) => typeof(Migrate).GetMethod(name, Static).Invoke(null, args);

        [Test]
        public void Byte_identical_outputs_keep_their_file_and_metadata_untouched()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var file = Path.Combine(root, "asset.bin");
            try
            {
                Assert.That(Call("WriteFileIfChanged", file, new byte[] { 1, 2, 3 }), Is.True);
                File.WriteAllText(file + ".meta", "existing identity");
                var timestamp = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(file, timestamp);
                Assert.That(Call("WriteFileIfChanged", file, new byte[] { 1, 2, 3 }), Is.False);
                Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(timestamp));
                Assert.That(File.ReadAllText(file + ".meta"), Is.EqualTo("existing identity"));
                Assert.That(Call("WriteFileIfChanged", file, new byte[] { 4, 5 }), Is.True);
                CollectionAssert.AreEqual(new byte[] { 4, 5 }, File.ReadAllBytes(file));
            }
            finally { Directory.Delete(root, true); }
        }

        private static object Resource(string file)
        {
            var type = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var resource = Activator.CreateInstance(type, true);
            type.GetField("File").SetValue(resource, file);
            return resource;
        }
        private static void Forget(string cache, object resource) =>
            ((IDictionary)typeof(Migrate).GetField(cache, Static).GetValue(null)).Remove(resource);

        [TestCase(true)]
        [TestCase(false)]
        public void Authored_tile_trim_and_nine_slice_survive_an_identical_second_import(bool tile)
        {
            var name = "__ImportTest_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + name;
            var output = "Assets/UIProject/Assets/" + name;
            var file = source + "/Image.png";
            var target = output + "/Image.png";
            var texture = new Texture2D(16, 12, TextureFormat.RGBA32, false);
            var resource = Resource(file);
            var type = resource.GetType();
            type.GetField("Type").SetValue(resource, Schema.ResourceKind.Image);
            type.GetField("Scale").SetValue(resource, tile ? Schema.ImageScale.Tile : Schema.ImageScale.NineGrid);
            if (!tile) type.GetField("Scale9Grid").SetValue(resource, "1,2,8,6");
            try
            {
                var pixels = new Color32[16 * 12];
                for (var y = 3; y < 9; y++) for (var x = 2; x < 10; x++) pixels[y * 16 + x] = new Color32(255, 0, 0, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                Directory.CreateDirectory(source);
                File.WriteAllBytes(file, texture.EncodeToPNG());
                ImportWitness.target = target;
                Call("ImportImage", resource);
                var guid = AssetDatabase.AssetPathToGUID(target);
                var bytes = File.ReadAllBytes(target);
                ImportWitness.imported = 0;
                Call("ImportImage", resource);
                Assert.That(ImportWitness.imported, Is.Zero);
                Assert.That(AssetDatabase.AssetPathToGUID(target), Is.EqualTo(guid));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(target));
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.GetSourceTextureWidthAndHeight(out var width, out var height);
                Assert.That(new Vector2(width, height), Is.EqualTo(tile ? new Vector2(8, 6) : new Vector2(16, 12)));
                Assert.That(importer.wrapMode, Is.EqualTo(tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp));
                Assert.That(importer.spriteBorder, Is.EqualTo(tile ? Vector4.zero : new Vector4(1, 4, 7, 2)));
            }
            finally
            {
                ImportWitness.target = null;
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }

        [Test]
        public void Sprite_rerun_imports_nothing_and_only_changed_settings_reimport_preserving_guid()
        {
            var folder = "Assets/__ImportTest_" + Guid.NewGuid().ToString("N");
            var path = folder + "/Sprite.png";
            var texture = new Texture2D(32, 24, TextureFormat.RGBA32, false);
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                ImportWitness.target = path;
                Call("ImportSprite", path, false, new Vector4(2, 3, 4, 5), true);
                var guid = AssetDatabase.AssetPathToGUID(path);
                var meta = File.ReadAllBytes(path + ".meta");
                ImportWitness.imported = 0;
                Call("ImportSprite", path, false, new Vector4(2, 3, 4, 5), false);
                Assert.That(ImportWitness.imported, Is.Zero);
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
                Call("ImportSprite", path, true, Vector4.zero, false);
                Assert.That(ImportWitness.imported, Is.EqualTo(1));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                ImportWitness.imported = 0;
                Call("ImportSprite", path, true, Vector4.zero, false);
                Assert.That(ImportWitness.imported, Is.Zero);
            }
            finally
            {
                ImportWitness.target = null;
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Audio_rerun_after_cache_reset_does_not_import_or_change_guid()
        {
            var name = "__ImportTest_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + name;
            var output = "Assets/UIProject/Assets/" + name;
            var file = source + "/Tone.wav";
            var target = output + "/Tone.wav";
            var resource = Resource(file);
            try
            {
                Directory.CreateDirectory(source);
                using (var writer = new BinaryWriter(File.Create(file)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(38);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(8000);
                    writer.Write((short)1); writer.Write((short)8);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(2); writer.Write((ushort)0x8080);
                }
                ImportWitness.target = target;
                Assert.That(Call("ImportSound", resource), Is.Not.Null);
                var guid = AssetDatabase.AssetPathToGUID(target);
                Forget("Sounds", resource);
                ImportWitness.imported = 0;
                Assert.That(Call("ImportSound", resource), Is.Not.Null);
                Assert.That(ImportWitness.imported, Is.Zero);
                Assert.That(AssetDatabase.AssetPathToGUID(target), Is.EqualTo(guid));
            }
            finally
            {
                ImportWitness.target = null;
                Forget("Sounds", resource);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }

        [Test]
        public void Bitmap_font_rerun_keeps_texture_guid_and_does_not_dirty_identical_glyphs()
        {
            var name = "__ImportTest_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + name;
            var output = "Assets/UIProject/Assets/" + name;
            var texture = new Texture2D(32, 24, TextureFormat.RGBA32, false);
            var font = Resource(source + "/Font.fnt");
            var atlas = Resource(source + "/Atlas.png");
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            font.GetType().GetField("PackageId").SetValue(font, name);
            font.GetType().GetField("Texture").SetValue(font, "atlas");
            resources.Add(name + "atlas", atlas);
            try
            {
                Directory.CreateDirectory(source);
                File.WriteAllBytes(source + "/Atlas.png", texture.EncodeToPNG());
                File.WriteAllText(source + "/Font.fnt", "info size=16\ncommon scaleW=32 scaleH=24\nchar id=65 x=0 y=0 width=8 height=12 xoffset=1 yoffset=2 xadvance=9\n");
                ImportWitness.target = output + "/Atlas.png";
                var asset = (BitmapFont)Call("ImportFont", font);
                AssetDatabase.SaveAssetIfDirty(asset);
                var guid = AssetDatabase.AssetPathToGUID(output + "/Font.asset");
                var textureGuid = AssetDatabase.AssetPathToGUID(ImportWitness.target);
                Forget("FontAssets", font);
                ImportWitness.imported = 0;
                Assert.That(Call("ImportFont", font), Is.SameAs(asset));
                Assert.That(ImportWitness.imported, Is.Zero);
                Assert.That(EditorUtility.IsDirty(asset), Is.False);
                Assert.That(AssetDatabase.AssetPathToGUID(output + "/Font.asset"), Is.EqualTo(guid));
                Assert.That(AssetDatabase.AssetPathToGUID(ImportWitness.target), Is.EqualTo(textureGuid));
                Assert.That(asset.glyphs[0].code, Is.EqualTo(65));
                Assert.That(asset.glyphs[0].advance, Is.EqualTo(9));
            }
            finally
            {
                ImportWitness.target = null;
                resources.Remove(name + "atlas");
                Forget("FontAssets", font);
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(output);
                Directory.Delete(source, true);
            }
        }
    }
}
