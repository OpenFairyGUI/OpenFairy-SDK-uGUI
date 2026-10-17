#if FAIRYGUI_TMPRO
using System;
using System.IO;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public class TmpSpriteAssetBuilderTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            string name = "TmpSpriteAssetTests_" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + name;
            AssetDatabase.CreateFolder("Assets", name);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_folder);

        private Sprite MakeSprite(string name, int width, int height)
        {
            string path = _folder + "/" + name + ".png";
            var texture = new Texture2D(width, height);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [Test]
        public void BakingReusesSourceTexturesAndPreservesRootGuidAcrossRebuilds()
        {
            var first = MakeSprite("first", 8, 4);
            var second = MakeSprite("second", 4, 8);
            string sourcePath = AssetDatabase.GetAssetPath(first);
            byte[] source = File.ReadAllBytes(sourcePath);
            string output = _folder + "/Sprites.asset";
            var root = TmpSpriteAssetBuilder.Bake(output, new[]
            {
                new TmpSpriteAssetBuilder.Entry("wide", first),
                new TmpSpriteAssetBuilder.Entry("tall", second),
            });
            string guid = AssetDatabase.AssetPathToGUID(output);
            Assert.That(root.version, Is.EqualTo("1.1.0"));
            Assert.That(root.spriteSheet, Is.SameAs(first.texture));
            Assert.That(root.fallbackSpriteAssets.Count, Is.EqualTo(1));
            var fallback = root.fallbackSpriteAssets[0];
            Assert.That(fallback.spriteSheet, Is.SameAs(second.texture));
            Assert.That(root.spriteGlyphTable[0].sprite, Is.SameAs(first));
            Assert.That(fallback.spriteGlyphTable[0].sprite, Is.SameAs(second));
            Assert.That(root.spriteGlyphTable[0].metrics.width, Is.EqualTo(2));
            Assert.That(root.spriteGlyphTable[0].metrics.height, Is.EqualTo(1));
            Assert.That(root.material.mainTexture, Is.SameAs(first.texture));
            CollectionAssert.AreEqual(source, File.ReadAllBytes(sourcePath));

            var rebuilt = TmpSpriteAssetBuilder.Bake(output, new[] { new TmpSpriteAssetBuilder.Entry("wide", first) });
            Assert.That(AssetDatabase.AssetPathToGUID(output), Is.EqualTo(guid));
            Assert.That(rebuilt, Is.SameAs(root));
            Assert.That(rebuilt.fallbackSpriteAssets, Is.Empty);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(output).Length, Is.EqualTo(2));
            CollectionAssert.AreEqual(source, File.ReadAllBytes(sourcePath));
        }

        [Test]
        public void ImportResolvesImageReferencesOnceAndBakesOnlyNativeTags()
        {
            var wide = MakeSprite("wide", 8, 4);
            var tall = MakeSprite("tall", 4, 8);
            const string source = "[img]ui://fixture/wide[/img]<img src=ui://fixture/tall width=12 height=18 />"
                + "[img]ui://fixture/wide[/img]<noparse>[img]ui://literal[/img]</noparse>";
            int calls = 0;
            string text = TmpSpriteAssetBuilder.BakeInlineImages(_folder + "/Inline.asset", source, true, true,
                name => { calls++; return name == "ui://fixture/wide" ? wide : tall; }, out var asset);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(asset, Is.Not.Null);
            Assert.That(text, Does.Contain("<sprite name=\"inline_0\">"));
            Assert.That(text, Does.Contain("<sprite name=\"inline_1\">"));
            Assert.That(text, Does.Contain("<size=18>"));
            Assert.That(text, Does.Not.Contain("ui://fixture/"));
            Assert.That(text, Does.Contain("<noparse>[img]ui://literal[/img]</noparse>"));
            const string plain = "No inline image";
            Assert.That(TmpSpriteAssetBuilder.BakeInlineImages(_folder + "/Unused.asset", plain, true, true,
                _ => throw new InvalidOperationException(), out var unused), Is.EqualTo(plain));
            Assert.That(unused, Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/Unused.asset"), Is.Null);
        }
    }
}
#endif
