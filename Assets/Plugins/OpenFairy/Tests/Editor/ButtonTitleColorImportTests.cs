using System;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif
using Object = UnityEngine.Object;
using Schema = OpenFairy.UGUI.Editor.Schema;
using State = OpenFairy.UGUI.Tests.PointerOverrideButton.State;

namespace OpenFairy.UGUI.Tests
{
    public static class ButtonTitleColorFixture
    {
        public static readonly Color InstanceColor = new Color32(38, 179, 220, 255);
        public static readonly Color UpColor = Color.gray;
        private static string _folder;
        public static GameObject Build(bool explicitSlot, bool colorOverride, bool selected = false, bool hasGear = true)
        {
            Assert.That(_folder, Is.Null);
            var name = "__TitleColor_" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + name; AssetDatabase.CreateFolder("Assets", name);
            var root = new GameObject("Button", typeof(RectTransform)); root.SetActive(false);
            GameObject instance = null;
            try
            {
                var button = root.AddComponent<PointerOverrideButton>();
                var title = new GameObject("Title", typeof(RectTransform), typeof(TextComponent)); title.transform.SetParent(root.transform, false);
                var graphic = title.GetComponent<TextComponent>();
#if FAIRYGUI_TMPRO
                graphic.font = TMPro.TMP_Settings.defaultFontAsset;
#else
                graphic.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
                graphic.text = "Caption"; graphic.color = UpColor; button.titleText = graphic;
                if (hasGear)
                    button.controller.gears = new Gear<State>[] { new GearColor<State> {
                        target = title, pages = explicitSlot ? new[] { State.up, State.over, State.down } : new[] { State.over, State.down },
                        values = explicitSlot ? new[] { UpColor, Color.green, Color.red } : new[] { Color.green, Color.red },
                        strokeValues = explicitSlot ? new[] { Color.black, Color.black, Color.black } : new[] { Color.black, Color.black },
                        defaultValue = UpColor, defaultStroke = Color.black,
                    } };
                button.controller.Bake(State.up);
                var definition = PrefabUtility.SaveAsPrefabAsset(root, _folder + "/Definition.prefab", out var definitionSaved);
                Assert.That(definitionSaved, Is.True);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(definition);
                typeof(Migrate).GetMethod("ConfigureButton", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {
                    instance, new Schema.Extension { TitleColor = colorOverride ? "#26b3dc" : null, Checked = selected, Mode = selected ? ButtonMode.Check : ButtonMode.Common }, "fixture",
                });
                var asset = PrefabUtility.SaveAsPrefabAsset(instance, _folder + "/Instance.prefab", out var saved);
                Assert.That(saved, Is.True);
                var original = definition.GetComponent<PointerOverrideButton>();
                Assert.That(original.titleText.color, Is.EqualTo(UpColor));
                if (hasGear)
                {
                    var source = (GearColor<State>)original.controller.gears[0];
                    Assert.That(source.pages.Length, Is.EqualTo(explicitSlot ? 3 : 2));
                    Assert.That(source.defaultValue, Is.EqualTo(UpColor));
                    if (explicitSlot) Assert.That(source.values[0], Is.EqualTo(UpColor));
                }
                AssetDatabase.ImportAsset(_folder + "/Instance.prefab", ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(_folder + "/Instance.prefab");
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); Object.DestroyImmediate(root); }
        }
        public static void Delete() { if (_folder == null) return; AssetDatabase.DeleteAsset(_folder); _folder = null; }
    }

    public sealed class ButtonTitleColorImportTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Instance_color_updates_only_the_current_page_and_survives_prefab_reload(bool explicitSlot, bool colorOverride)
        {
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(ButtonTitleColorFixture.Build(explicitSlot, colorOverride));
                var button = instance.GetComponent<PointerOverrideButton>();
                var expected = colorOverride ? ButtonTitleColorFixture.InstanceColor : ButtonTitleColorFixture.UpColor;
                Assert.That(button.titleText.color, Is.EqualTo(expected));
                button.controller.page = State.over; Assert.That(button.titleText.color, Is.EqualTo(Color.green));
                button.controller.page = State.down; Assert.That(button.titleText.color, Is.EqualTo(Color.red));
                button.controller.page = State.up; Assert.That(button.titleText.color, Is.EqualTo(expected));
                button.controller.page = State.disabled; Assert.That(button.titleText.color, Is.EqualTo(ButtonTitleColorFixture.UpColor));
                button.controller.page = State.up; Assert.That(button.titleText.color, Is.EqualTo(expected));
                var gear = (GearColor<State>)button.controller.gears[0];
                Assert.That(gear.strokeValues.Length, Is.EqualTo(gear.pages.Length));
                Assert.That(gear.defaultValue, Is.EqualTo(ButtonTitleColorFixture.UpColor));
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); ButtonTitleColorFixture.Delete(); }
        }
        [Test] public void Plain_title_without_color_gear_receives_the_instance_color()
        {
            try { Assert.That(ButtonTitleColorFixture.Build(true, true, false, false).GetComponent<PointerOverrideButton>().titleText.color, Is.EqualTo(ButtonTitleColorFixture.InstanceColor)); }
            finally { ButtonTitleColorFixture.Delete(); }
        }
        [Test] public void Checked_instance_changes_state_after_recording_the_original_current_page()
        {
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(ButtonTitleColorFixture.Build(true, true, true));
                var button = instance.GetComponent<PointerOverrideButton>();
                Assert.That(button.titleText.color, Is.EqualTo(Color.red));
                button.selected = false; Assert.That(button.titleText.color, Is.EqualTo(ButtonTitleColorFixture.InstanceColor));
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); ButtonTitleColorFixture.Delete(); }
        }
    }
}
