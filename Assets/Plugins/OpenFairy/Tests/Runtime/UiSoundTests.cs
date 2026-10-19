using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public enum SoundTestPage { up, down }
    public sealed class SoundTestButton : NamedStateButton<SoundTestPage> { }
    public sealed class SoundTestCombo : NamedStateCombo<SoundTestPage> { }

    public class UiSoundTests
    {
        private GameObject _canvas, _events;
        private AudioClip _clip;
        private Root _root;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _events = new GameObject("Events", typeof(EventSystem));
            if (!System.Array.Exists(Object.FindObjectsByType<AudioListener>(), listener => listener.isActiveAndEnabled))
                _events.AddComponent<AudioListener>();
            _root = Root.Create((RectTransform)_canvas.transform);
            _clip = AudioClip.Create("Click", 44100, 1, 44100, false);
            var samples = new float[44100];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 2 * Mathf.PI * 440 / 44100) * .1f;
            _clip.SetData(samples, 0);
        }

        private T Make<T>() where T : ButtonBase
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            var button = go.AddComponent<T>();
            button.sound = _clip;
            button.soundVolumeScale = .25f;
            return button;
        }

        private PointerEventData Pointer(PointerEventData.InputButton button = PointerEventData.InputButton.Left) =>
            new(_events.GetComponent<EventSystem>()) { button = button };

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(_canvas);
            Object.Destroy(_events);
            Object.Destroy(_clip);
            yield return null;
            Assert.That(Root.inst, Is.Null);
        }

        [UnityTest]
        public IEnumerator Clicks_use_one_native_source_before_business_events_and_preserve_host_controls()
        {
            var button = Make<SoundTestButton>();
            var source = _root.soundSource;
            source.volume = .4f;
            source.mute = true;
            int clicks = 0;
            button.onClick.AddListener(() => {
                source.Stop();
                clicks++;
            });
            button.OnPointerClick(Pointer());
            yield return null;
            Assert.That(source.isPlaying, Is.False, "A business listener can stop the sound started before onClick.");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicks++);
            button.OnPointerClick(Pointer());
            button.OnPointerClick(Pointer());
            yield return null;
            Assert.That(clicks, Is.EqualTo(3));
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(.4f));
            Assert.That(source.mute, Is.True);
            Assert.That(source.playOnAwake, Is.False);
            Assert.That(source.spatialBlend, Is.Zero);
            Assert.That(_root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
            Assert.That(_root.soundSource, Is.SameAs(source));
        }

        [UnityTest]
        public IEnumerator Right_click_and_missing_clips_are_silent_while_grayed_buttons_still_click()
        {
            var button = Make<SoundTestButton>();
            button.OnPointerClick(Pointer(PointerEventData.InputButton.Right));
            Assert.That(_root.soundSource.isPlaying, Is.False);
            button.sound = null;
            button.OnPointerClick(Pointer());
            Assert.That(_root.soundSource.isPlaying, Is.False);
            button.sound = _clip;
            button.grayed = true;
            button.OnPointerClick(Pointer());
            yield return null;
            Assert.That(_root.soundSource.isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator Combo_buttons_use_the_same_sound_source_without_requiring_a_dropdown()
        {
            var combo = Make<SoundTestCombo>();
            combo.OnPointerClick(Pointer(PointerEventData.InputButton.Right));
            Assert.That(_root.soundSource.isPlaying, Is.False);
            combo.OnPointerClick(Pointer());
            yield return null;
            Assert.That(_root.soundSource.isPlaying, Is.True);
            Assert.That(_root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
        }
        private Transition MakeTransition()
        {
            var go = new GameObject("Transition", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_canvas.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var transition = go.AddComponent<Transition>();
            transition.audioSource = source;
            transition.items = new[] { new TransitionItem { type = TransitionItemType.Sound, sound = _clip, volume = .25f } };
            go.SetActive(true);
            Assert.That(source.isPlaying, Is.False, "The configured fixture must be silent before evaluating its transition.");
            return transition;
        }

        [UnityTest]
        public IEnumerator Transition_sound_uses_the_root_source_and_preserves_host_controls()
        {
            var transition = MakeTransition();
            transition.audioSource.enabled = false;
            var source = _root.soundSource;
            source.volume = .4f;
            source.mute = true;
            source.pitch = .75f;
            transition.Play().Forget();
            Assert.That(source.isPlaying, Is.True);
            Assert.That(transition.audioSource.isPlaying, Is.False);
            Assert.That(source.volume, Is.EqualTo(.4f));
            Assert.That(source.mute, Is.True);
            Assert.That(source.pitch, Is.EqualTo(.75f));
            Assert.That(_root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Hidden_transition_sound_is_consumed_without_replaying_on_activation()
        {
            var transition = MakeTransition();
            transition.gameObject.SetActive(false);
            transition.Play().Forget();
            Assert.That(_root.soundSource.isPlaying, Is.False);
            Assert.That(transition.audioSource.isPlaying, Is.False);
            transition.gameObject.SetActive(true);
            yield return null;
            Assert.That(_root.soundSource.isPlaying, Is.False);
            transition.Play().Forget();
            Assert.That(_root.soundSource.isPlaying, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Disabled_transition_sound_is_silent_while_other_tracks_still_apply()
        {
            var transition = MakeTransition();
            transition.enabled = false;
            transition.items = new[] {
                transition.items[0],
                new TransitionItem { type = TransitionItemType.Size, start = new[] { 123f, 45f } },
            };
            transition.Play().Forget();
            Assert.That(((RectTransform)transition.transform).rect.size, Is.EqualTo(new Vector2(123, 45)));
            Assert.That(_root.soundSource.isPlaying, Is.False);
            Assert.That(transition.audioSource.isPlaying, Is.False);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Disabled_root_sound_does_not_fall_back_to_an_unrouted_source()
        {
            var transition = MakeTransition();
            _root.soundSource.enabled = false;
            transition.Play().Forget();
            Assert.That(_root.soundSource.isPlaying, Is.False);
            Assert.That(transition.audioSource.isPlaying, Is.False);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Standalone_transition_uses_only_its_enabled_baked_source()
        {
            Object.Destroy(_root.gameObject);
            yield return null;
            Assert.That(Root.inst, Is.Null);
            var transition = MakeTransition();
            transition.audioSource.enabled = false;
            transition.Play().Forget();
            Assert.That(transition.audioSource.isPlaying, Is.False);
            transition.audioSource.enabled = true;
            transition.Play().Forget();
            Assert.That(transition.audioSource.isPlaying, Is.True);
            Assert.That(Root.inst, Is.Null, "Playing a standalone transition must not create a Root.");
            LogAssert.NoUnexpectedReceived();
        }

    }
}
