using System.Collections;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public sealed class GearClockTests
    {
        private enum State { Start, End }
        private enum Kind { Position, Size, Look, Color }

        [UnityTest] public IEnumerator Position_uses_the_engine_clock_with_manual_defaults() => Run(Kind.Position, false);
        [UnityTest] public IEnumerator Size_uses_the_engine_clock_with_manual_defaults() => Run(Kind.Size, false);
        [UnityTest] public IEnumerator Look_uses_the_engine_clock_with_manual_defaults() => Run(Kind.Look, false);
        [UnityTest] public IEnumerator Color_uses_the_engine_clock_with_manual_defaults() => Run(Kind.Color, false);
        [UnityTest] public IEnumerator Position_preserves_engine_pause_and_resume() => Run(Kind.Position, true);
        [UnityTest] public IEnumerator Size_preserves_engine_pause_and_resume() => Run(Kind.Size, true);
        [UnityTest] public IEnumerator Look_preserves_engine_pause_and_resume() => Run(Kind.Look, true);
        [UnityTest] public IEnumerator Color_preserves_engine_pause_and_resume() => Run(Kind.Color, true);

        private static IEnumerator Run(Kind kind, bool paused)
        {
            DOTween.Init();
            var previousUpdate = DOTween.defaultUpdateType;
            var previousTimeScale = Time.timeScale;
            var target = new GameObject("Gear", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            Gear<State> gear = kind switch
            {
                Kind.Position => new GearXY<State> { defaultValue = Vector2.zero, values = new[] { new Vector2(80, -40) } },
                Kind.Size => new GearSize<State> { defaultSize = new Vector2(40, 20), defaultScale = Vector2.one, sizes = new[] { new Vector2(80, 60) }, scales = new[] { new Vector2(2, 3) } },
                Kind.Look => new GearLook<State> { defaultAlpha = 1, alphas = new[] { .25f }, rotations = new[] { 45f }, grayed = new[] { false } },
                _ => new GearColor<State> { defaultValue = Color.white, values = new[] { Color.black } },
            };
            try
            {
                DOTween.defaultUpdateType = UpdateType.Manual;
                Time.timeScale = paused ? 0 : 1;
                gear.target = target;
                gear.pages = new[] { State.End };
                gear.tween = true;
                gear.duration = .05f;
                gear.Apply(State.Start);
                gear.Apply(State.End, true);
                yield return new WaitForSecondsRealtime(.15f);
                if (paused)
                {
                    AssertValue(kind, target, false);
                    Time.timeScale = 1;
                    yield return new WaitForSecondsRealtime(.15f);
                }
                AssertValue(kind, target, true);
                Assert.That(DOTween.defaultUpdateType, Is.EqualTo(UpdateType.Manual));
            }
            finally
            {
                gear.Apply(State.Start);
                Object.DestroyImmediate(target);
                Time.timeScale = previousTimeScale;
                DOTween.defaultUpdateType = previousUpdate;
            }
        }

        private static void AssertValue(Kind kind, GameObject target, bool end)
        {
            var rect = (RectTransform)target.transform;
            switch (kind)
            {
                case Kind.Position:
                    Assert.That(rect.anchoredPosition, Is.EqualTo(end ? new Vector2(80, -40) : Vector2.zero));
                    break;
                case Kind.Size:
                    Assert.That(Vector2.Distance(rect.rect.size, end ? new Vector2(80, 60) : new Vector2(40, 20)), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(rect.localScale, end ? new Vector3(2, 3, 1) : Vector3.one), Is.LessThan(.001f));
                    break;
                case Kind.Look:
                    Assert.That(target.GetComponent<CanvasGroup>().alpha, Is.EqualTo(end ? .25f : 1));
                    Assert.That(Mathf.DeltaAngle(rect.localEulerAngles.z, end ? -45 : 0), Is.EqualTo(0).Within(.001f));
                    break;
                case Kind.Color:
                    Assert.That(target.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(end ? Color.black : Color.white));
                    break;
            }
        }
    }
}
