using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace OpenFairy.UGUI.Tests
{
    public class AutomaticFilterPlaySessionTests
    {
        private readonly HashSet<int> _renderedFrames = new();
        private readonly List<(int frame, int captures)> _cameraCompletions = new();
        private GameObject _root;
        private RectTransform _source;
        private Camera _camera;
        private Material _material;
        private int _sessionStartCapture;
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        // Native Play transitions can replace the managed MonoBehaviour wrapper.
        private AutomaticFilterProbe Filter => _source.GetComponent<AutomaticFilterProbe>();

        private int CapturesInFrame(int frame)
        {
            var filter = Filter;
            int count = 0;
            for (int i = _sessionStartCapture; i < filter.frames.Count; i++)
                if (filter.frames[i] == frame) count++;
            return count;
        }

        private void CameraEnd(Camera camera)
        {
            if (Application.isPlaying && camera == _camera)
            {
                _renderedFrames.Add(Time.frameCount);
                _cameraCompletions.Add((Time.frameCount, CapturesInFrame(Time.frameCount)));
            }
        }

        private void PipelineCameraEnd(ScriptableRenderContext context, Camera camera) => CameraEnd(camera);

        [SetUp]
        public void Setup()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            _root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _root.layer = 30;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(128, 128);
            _root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var cameraObject = new GameObject("UI Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.transform.localPosition = new Vector3(0, 0, -100);
            _camera.orthographic = true;
            _camera.orthographicSize = 64;
            _camera.cullingMask = 1 << 30;
            _root.GetComponent<Canvas>().worldCamera = _camera;
            // The fixture owns the material across native component restoration on Exit.
            _material = new Material(Shader.Find("OpenFairy/UI Premultiplied"));
            _source = (RectTransform)AutomaticFilterProbe.Create(_root.transform, new Vector2(48, 48), _material).transform;
            var art = new GameObject("Art", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            art.layer = 30;
            art.transform.SetParent(_source, false);
            ((RectTransform)art.transform).sizeDelta = new Vector2(48, 48);
            art.GetComponent<UnityEngine.UI.Image>().color = Color.red;
            Camera.onPostRender += CameraEnd;
            RenderPipelineManager.endCameraRendering += PipelineCameraEnd;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (Application.isPlaying) yield return new ExitPlayMode();
            }
            finally
            {
                Camera.onPostRender -= CameraEnd;
                RenderPipelineManager.endCameraRendering -= PipelineCameraEnd;
                Object.DestroyImmediate(_root);
                Object.DestroyImmediate(_material);
                EditorSettings.enterPlayModeOptions = _options;
                EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            }
        }

        [UnityTest]
        public IEnumerator Existing_enabled_component_captures_once_after_each_no_reload_Play_entry()
        {
            for (int session = 0; session < 2; session++)
            {
                _sessionStartCapture = Filter.frames.Count;
                _renderedFrames.Clear();
                _cameraCompletions.Clear();
                yield return new EnterPlayMode(false);
                double deadline = EditorApplication.timeSinceStartup + 5;
                while (_renderedFrames.Count < 2 && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(_renderedFrames.Count, Is.GreaterThanOrEqualTo(2), "Two actual Player frames must render.");
                foreach (var completion in _cameraCompletions)
                    Assert.That(completion.captures, Is.EqualTo(1),
                        $"Session {session}, frame {completion.frame}: capture must precede its camera render exactly once.");
                var filter = Filter;
                for (int i = _sessionStartCapture; i < filter.frames.Count; i++)
                    Assert.That(CapturesInFrame(filter.frames[i]), Is.EqualTo(1), "No stale subscription may duplicate a capture.");
                Assert.That(filter.frames.Count, Is.GreaterThan(_sessionStartCapture));
                Assert.That(filter.pixel.r, Is.GreaterThan(.98f));
                Assert.That(filter.presentationMaterial == _material, Is.True);
                yield return new ExitPlayMode();
                Assert.That(Filter.texture, Is.Null);
                Assert.That(Filter.isPresenting, Is.False);
                Assert.That(_material != null && Filter.presentationMaterial == _material, Is.True);
            }
        }
    }
}
