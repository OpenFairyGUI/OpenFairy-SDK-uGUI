using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI.TestSupport
{
    // 复刻 BasicsRenderDiff 的 NanamiUI 半边渲染：WorldSpace canvas + 禁用的手动 Render 相机，
    // 逐页实例化产物、播 t0、由调用方逐帧静置到末帧后截全分辨率图。只渲 NanamiUI，不涉及 FairyGUI。
    public sealed class NanamiPageRenderer
    {
        private static readonly Color Background = new Color32(0x36, 0x3C, 0x44, 0xFF);

        private Camera _camera;
        private GameObject _canvas;
        private RectTransform _canvasRt;
        private GameObject _instance;
        private Vector2Int _size;

        public void Setup()
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = 1f / 60f; // 两侧动效同步的关键：锁定每帧步进量
            Application.runInBackground = true;
            NanamiUI.Text.defaultFont = "Microsoft YaHei";

            _camera = new GameObject("NanamiUI Golden Camera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.cullingMask = 1; // 只渲染 Default 层
            _camera.orthographic = true;
            _camera.nearClipPlane = -30;
            _camera.farClipPlane = 30;
            _camera.enabled = false; // 只用于手动 Render

            _canvas = new GameObject("NanamiUI Golden Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1;
            _canvasRt = (RectTransform)_canvas.transform;
            _canvasRt.pivot = new Vector2(0, 1);
            _canvasRt.position = Vector3.zero;
        }

        public void LoadPage(ParityPage page)
        {
            _size = ParityCatalog.PageSize(page);
            _canvasRt.sizeDelta = _size;

            // PlayMode 测试程序集是全平台的，AssetDatabase 只在编辑器可用；golden 测试只在编辑器 play mode 跑。
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ParityCatalog.PrefabPath(page));
#else
            GameObject prefab = null;
#endif
            _instance = Object.Instantiate(prefab, _canvasRt, false);
            var rt = (RectTransform)_instance.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            foreach (var text in _instance.GetComponentsInChildren<NanamiUI.Text>(true))
                text.WarmUp();
            foreach (var transition in _instance.GetComponents<Transition>())
                if (transition.transitionName == "t0")
                    transition.Play();
        }

        public Texture2D Capture()
        {
            _camera.orthographicSize = _size.y * 0.5f;
            _camera.transform.position = new Vector3(_size.x * 0.5f, -_size.y * 0.5f, -10);
            Canvas.ForceUpdateCanvases();

            var rt = RenderTexture.GetTemporary(_size.x, _size.y, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = rt;
            _camera.Render();

            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(_size.x, _size.y, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, _size.x, _size.y), 0, 0);
            texture.Apply();
            RenderTexture.active = old;

            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return texture;
        }

        public void Unload()
        {
            if (_instance != null)
                Object.DestroyImmediate(_instance);
        }

        public void Teardown()
        {
            Time.captureDeltaTime = 0;
            if (_canvas != null)
                Object.DestroyImmediate(_canvas);
            if (_camera != null)
                Object.DestroyImmediate(_camera.gameObject);
        }
    }

    public static class GoldenImage
    {
        public static Texture2D Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        // 任一 RGB 通道差 > 2/255 记为差异像素，返回差异占比（与 BasicsRenderDiff 的 diff 口径一致）。
        public static float DiffRatio(Texture2D actual, Texture2D golden)
        {
            var w = Mathf.Min(actual.width, golden.width);
            var h = Mathf.Min(actual.height, golden.height);
            var pa = actual.GetPixels32();
            var pg = golden.GetPixels32();
            var diff = 0;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var ca = pa[y * actual.width + x];
                var cg = pg[y * golden.width + x];
                if (Mathf.Abs(ca.r - cg.r) > 2 || Mathf.Abs(ca.g - cg.g) > 2 || Mathf.Abs(ca.b - cg.b) > 2)
                    diff++;
            }
            return (float)diff / (w * h);
        }
    }
}
