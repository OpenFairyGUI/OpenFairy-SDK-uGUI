#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using State = OpenFairy.UGUI.Tests.PointerOverrideButton.State;

namespace OpenFairy.UGUI.Tests
{
    public sealed class ButtonTitleColorTests
    {
        private GameObject _canvas;
        private static Type Fixture => Type.GetType("OpenFairy.UGUI.Tests.ButtonTitleColorFixture, OpenFairy.UGUI.Tests.Editor", true);
        [TearDown] public void Cleanup()
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            Fixture.GetMethod("Delete").Invoke(null, null); _canvas = null;
        }
        [UnityTest] public IEnumerator Saved_explicit_page_color_survives_state_and_enable_changes() => Run(true);
        [UnityTest] public IEnumerator Saved_default_page_color_survives_state_and_enable_changes() => Run(false);
        private IEnumerator Run(bool explicitSlot)
        {
            try
            {
                _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var prefab = (GameObject)Fixture.GetMethod("Build").Invoke(null, new object[] { explicitSlot, true, false, true });
                var instance = Object.Instantiate(prefab, _canvas.transform, false); instance.SetActive(true);
                yield return null;
                var button = instance.GetComponent<PointerOverrideButton>(); var expected = new Color32(38, 179, 220, 255);
                Assert.That(button.titleText.color, Is.EqualTo((Color)expected));
                button.controller.page = State.over; yield return null; Assert.That(button.titleText.color, Is.EqualTo(Color.green));
                button.controller.page = State.down; yield return null; Assert.That(button.titleText.color, Is.EqualTo(Color.red));
                button.RefreshState(); yield return null; Assert.That(button.titleText.color, Is.EqualTo((Color)expected));
                _canvas.SetActive(false); yield return null; _canvas.SetActive(true); yield return null;
                Assert.That(button.titleText.color, Is.EqualTo((Color)expected));
            }
            finally { Cleanup(); }
        }
    }
}
#endif
