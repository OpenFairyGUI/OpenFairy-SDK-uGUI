#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class AutoClearItemsTests
    {
        private GameObject _canvas;
        private static Type Fixture => Type.GetType("OpenFairy.UGUI.Tests.AutoClearItemsFixture, OpenFairy.UGUI.Tests.Editor", true);
        [TearDown] public void Cleanup()
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            Fixture.GetMethod("Delete").Invoke(null, null); _canvas = null;
        }
        [UnityTest] public IEnumerator Cleared_list_can_fill_and_reenable_without_losing_runtime_items() => Run(true);
        [UnityTest] public IEnumerator Unflagged_list_keeps_authored_items_until_explicitly_filled() => Run(false);
        private IEnumerator Run(bool clear)
        {
            try
            {
                _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var prefab = (GameObject)Fixture.GetMethod("Build").Invoke(null, new object[] { clear, "scroll" });
                var instance = Object.Instantiate(prefab, _canvas.transform, false);
                yield return null;
                var source = instance.GetComponent<ListSource>();
                Assert.That(source.count, Is.EqualTo(clear ? 0 : 2));
                Assert.That(source.content.childCount, Is.EqualTo(clear ? 0 : 2));
                source.Fill(3, (item, index) => item.name = "Runtime " + index);
                yield return null;
                Assert.That(source.count, Is.EqualTo(3));
                for (var i = 0; i < 3; i++) Assert.That(source.content.GetChild(i).name, Is.EqualTo("Runtime " + i));
                _canvas.SetActive(false); yield return null; _canvas.SetActive(true); yield return null;
                Assert.That(source.count, Is.EqualTo(3));
                source.Fill(0, (_, _) => Assert.Fail("An empty fill must not create an item."));
                Assert.That(source.count, Is.Zero);
                source.Fill(2, (item, index) => item.name = "Refill " + index);
                yield return null;
                Assert.That(source.count, Is.EqualTo(2));
                for (var i = 0; i < 2; i++) Assert.That(source.content.GetChild(i).name, Is.EqualTo("Refill " + i));
            }
            finally { Cleanup(); }
        }
    }
}
#endif
