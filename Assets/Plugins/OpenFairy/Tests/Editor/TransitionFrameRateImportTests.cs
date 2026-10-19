using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using OpenFairy.UGUI.Editor;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Tests
{
    // Editor test candidate. Invoke the real importer, then the real runtime timeline.
    public class TransitionFrameRateImportTests
    {
        [TestCase(24)]
        [TestCase(30)]
        [TestCase(60)]
        public void Authored_rate_controls_tween_instant_and_hook_times(int rate)
        {
            string file = Path.GetTempFileName();
            var root = new GameObject("Timeline", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                File.WriteAllText(file, $@"<component size='100,100'><transition name='Fade' frameRate='{rate}' autoPlayDelay='0.25'>
<item time='{rate}' type='Alpha' tween='true' duration='{rate * 2}' ease='Linear' label='Begin' label2='End' startValue='0' endValue='1'/>
<item time='{rate * 3}' type='Visible' value='true' label='Shown'/>
</transition></component>");
                var xml = Schema.Component.Load(file);
                var method = typeof(Migrate).GetMethod("BuildTransitions", BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, new object[] { root, xml, new Dictionary<string, (Schema.Display, GameObject)>(), "fixture" });
                var timeline = root.GetComponent<Transition>();
                Assert.AreEqual(rate, xml.Transitions[0].FrameRate);
                Assert.AreEqual(.25f, timeline.autoPlayDelay); // Authored delay is seconds.
                Assert.AreEqual(1f, timeline.items[0].time);
                Assert.AreEqual(2f, timeline.items[0].duration);
                Assert.AreEqual(3f, timeline.items[1].time);
                int begin = 0, end = 0, shown = 0;
                timeline.SetHook(0, false, () => begin++);
                timeline.SetHook(0, true, () => end++);
                timeline.SetHook(1, false, () => shown++);
                // Reflection avoids adding a UniTask compile reference to the Editor-test assembly.
                typeof(Transition).GetMethod("Play").Invoke(timeline, new object[] { 1, 0f });
                timeline.Step(.5f);
                Assert.AreEqual(0, begin);
                timeline.Step(.5f);
                Assert.AreEqual(1, begin);
                Assert.AreEqual(0f, root.GetComponent<CanvasGroup>().alpha);
                timeline.Step(1f);
                Assert.AreEqual(.5f, root.GetComponent<CanvasGroup>().alpha);
                Assert.AreEqual(0, end);
                Assert.AreEqual(0, shown);
                timeline.Step(1f);
                Assert.AreEqual(1f, root.GetComponent<CanvasGroup>().alpha);
                Assert.AreEqual(1, end);
                Assert.AreEqual(1, shown);
                Assert.IsFalse(timeline.playing);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                File.Delete(file);
            }
        }

        [Test]
        public void Default_rate_is_24_and_value_units_are_preserved()
        {
            string file = Path.GetTempFileName();
            var root = new GameObject("Timeline", typeof(RectTransform));
            try
            {
                File.WriteAllText(file, @"<component size='100,100'><transition name='Values' autoPlayDelay='0.25'>
<item time='24' type='Shake' value='2,0.3'/>
<item time='48' type='Animation' value='15,p'/>
</transition></component>");
                var xml = Schema.Component.Load(file);
                typeof(Migrate).GetMethod("BuildTransitions", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { root, xml, new Dictionary<string, (Schema.Display, GameObject)>(), "fixture" });
                Assert.AreEqual(24, xml.Transitions[0].FrameRate);
                var timeline = root.GetComponent<Transition>();
                Assert.AreEqual(1, timeline.items[0].time);
                CollectionAssert.AreEqual(new[] { 2f, .3f }, timeline.items[0].start); // amplitude, seconds
                Assert.AreEqual(2, timeline.items[1].time);
                CollectionAssert.AreEqual(new[] { 15f, 1f }, timeline.items[1].start); // frame index, playing
                Assert.AreEqual(.25f, timeline.autoPlayDelay);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                File.Delete(file);
            }
        }
    }
}
