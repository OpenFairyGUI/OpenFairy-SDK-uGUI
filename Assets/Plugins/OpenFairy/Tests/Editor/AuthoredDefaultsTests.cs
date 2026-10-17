using System;
using System.Reflection;
using NUnit.Framework;
using OpenFairy.UGUI.Editor;
using UnityEngine;
using UnityEngine.TestTools;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    public class AuthoredDefaultsTests
    {
        private enum Page { First, Second }

        [Test]
        public void Default_only_look_gear_applies_to_every_controller_page()
        {
            var target = new GameObject("Item", typeof(RectTransform));
            try
            {
                var gear = new GearLook<Page> { target = target, pages = Array.Empty<Page>() };
                typeof(Migrate).GetMethod("ConfigureGearLook", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { typeof(GearLook<Page>), gear,
                        new Schema.Gear { Default = "0.4,25,0,0" }, target });
                foreach (var page in new[] { Page.First, Page.Second })
                {
                    gear.Apply(page);
                    Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.4f));
                    Assert.That(target.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                    Assert.That(Mathf.DeltaAngle(target.transform.localEulerAngles.z, -25), Is.Zero.Within(0.001f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Empty_authored_icon_clears_the_sprite_without_a_resource_lookup(string icon)
        {
            var sprite = typeof(Migrate).GetMethod("LoadSprite", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { icon, "package" });
            Assert.That(sprite, Is.Null);
        }

        [Test]
        public void Deleted_optional_icon_is_reported_and_left_empty()
        {
            LogAssert.Expect(LogType.Warning, "Unresolved FairyGUI icon: ui://missingIcon");
            var sprite = typeof(Migrate).GetMethod("LoadSprite", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "ui://missingIcon", "package" });
            Assert.That(sprite, Is.Null);
        }
    }
}
