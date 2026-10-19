using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class ButtonSoundImportTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        private static object Call(string name, params object[] args) =>
            typeof(Migrate).GetMethod(name, Static).Invoke(null, args);

        [Test]
        public void Schema_keeps_missing_sound_distinct_from_an_explicit_clear_and_optional_volume()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "<component size='100,40'><Button sound='ui://soundpkgclip' volume='0.25'/><displayList><component id='child'><Button sound=''/></component></displayList></component>");
                var xml = Schema.Component.Load(file);
                Assert.That(xml.Button.Sound, Is.EqualTo("ui://soundpkgclip"));
                Assert.That(xml.Button.SoundVolume, Is.EqualTo(.25f));
                Assert.That(xml.DisplayList[0].Button.Sound, Is.Empty);
                Assert.That(xml.DisplayList[0].Button.SoundVolume, Is.Null);
                File.WriteAllText(file, "<component size='100,40'><Button/></component>");
                Assert.That(Schema.Component.Load(file).Button.Sound, Is.Null);
            }
            finally { File.Delete(file); }
        }

        private static void Wave(string path)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 512);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(512);
            for (int i = 0; i < 256; i++) writer.Write((short)(Math.Sin(i * Math.PI / 8) * 2000));
        }

        [Test]
        public void Native_clip_is_baked_with_instance_inheritance_and_an_identical_import_keeps_its_identity()
        {
            var id = "__SoundTest_" + Guid.NewGuid().ToString("N");
            var source = "UIProject/assets/" + id;
            var output = "Assets/UIProject/Assets/" + id;
            var file = source + "/Click.wav";
            var target = output + "/Click.wav";
            var root = new GameObject("Button", typeof(RectTransform));
            var button = root.AddComponent<SoundImportButton>();
            var resourceType = typeof(Migrate).GetNestedType("Resource", BindingFlags.NonPublic);
            var resource = Activator.CreateInstance(resourceType, true);
            resourceType.GetField("File").SetValue(resource, file);
            resourceType.GetField("Type").SetValue(resource, Schema.ResourceKind.Sound);
            var resources = (IDictionary)typeof(Migrate).GetField("Resources", Static).GetValue(null);
            var sounds = (IDictionary)typeof(Migrate).GetField("Sounds", Static).GetValue(null);
            resources.Add(id, resource);
            try
            {
                Directory.CreateDirectory(source);
                Wave(file);
                var setting = new Schema.Extension { Sound = "ui://" + id, SoundVolume = .25f };
                Call("ConfigureButton", root, setting, null);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
                Assert.That(clip, Is.Not.Null);
                Assert.That(button.sound, Is.SameAs(clip));
                Assert.That(button.soundVolumeScale, Is.EqualTo(.25f));
                var guid = AssetDatabase.AssetPathToGUID(target);
                var metadata = File.ReadAllBytes(target + ".meta");
                sounds.Remove(resource);
                Call("ConfigureButton", root, setting, null);
                Assert.That(AssetDatabase.AssetPathToGUID(target), Is.EqualTo(guid));
                CollectionAssert.AreEqual(metadata, File.ReadAllBytes(target + ".meta"));
                Call("ConfigureButton", root, new Schema.Extension(), null);
                Assert.That(button.sound, Is.SameAs(clip));
                Assert.That(button.soundVolumeScale, Is.EqualTo(.25f));
                Call("ConfigureButton", root, new Schema.Extension { Sound = "" }, null);
                Assert.That(button.sound, Is.Null);
            }
            finally
            {
                resources.Remove(id);
                sounds.Remove(resource);
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(output);
                if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(source)) Directory.Delete(source);
            }
        }
    }
}
