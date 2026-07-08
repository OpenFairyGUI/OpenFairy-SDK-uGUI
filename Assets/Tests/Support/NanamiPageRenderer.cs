using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private GameObject _eventSystem;
        private Vector2Int _size;

        public GameObject Instance => _instance;
        public Camera Camera => _camera;
        public RectTransform CanvasRt => _canvasRt;
        public GraphicRaycaster Raycaster => _canvas.GetComponent<GraphicRaycaster>();

        public void Setup()
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = 1f / 60f; // 两侧动效同步的关键：锁定每帧步进量
            Application.runInBackground = true;
            NanamiUI.TextField.defaultFont = "Microsoft YaHei";

            _camera = new GameObject("NanamiUI Golden Camera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.cullingMask = 1; // 只渲染 Default 层
            _camera.orthographic = true;
            _camera.nearClipPlane = -30;
            _camera.farClipPlane = 30;
            _camera.enabled = false; // 只用于手动 Render

            // GraphicRaycaster 供交互驱动取相机（其 eventCamera = 画布 worldCamera），静态截图不用它。
            _canvas = new GameObject("NanamiUI Golden Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1;
            _canvasRt = (RectTransform)_canvas.transform;
            _canvasRt.pivot = new Vector2(0, 1);
            _canvasRt.position = Vector3.zero;

            // PlayMode 测试跑在空场景，需自带 EventSystem 令 EventSystem.current 非空（GraphicRaycaster.Raycast/drop 命中要用）。
            // 不加 InputModule：StandaloneInputModule 读旧版 UnityEngine.Input，本工程用新 Input System 会抛异常；
            // 测试都直接调 handler，无需输入模块驱动。
            if (EventSystem.current == null)
                _eventSystem = new GameObject("NanamiUI EventSystem", typeof(EventSystem));
        }

        public void LoadPage(ParityPage page)
        {
            LoadComponent(page.Package, page.Component);
            if (_instance == null)
                return;
            foreach (var transition in _instance.GetComponents<Transition>())
                if (transition.transitionName == "t0")
                    transition.Play();
        }

        // 实例化烘焙态 prefab（不播 t0）。交互测试在此基础上驱动目标；null（prefab 缺失）由调用方判失败。
        public void LoadComponent(string package, string component)
        {
            _size = ParityCatalog.PageSize(package, component);
            _canvasRt.sizeDelta = _size;

            // PlayMode 测试程序集是全平台的，AssetDatabase 只在编辑器可用；golden 测试只在编辑器 play mode 跑。
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ParityCatalog.PrefabPath(package, component));
#else
            GameObject prefab = null;
#endif
            if (prefab == null)
            {
                _instance = null;
                return;
            }
            _instance = Object.Instantiate(prefab, _canvasRt, false);
            var rt = (RectTransform)_instance.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            foreach (var text in _instance.GetComponentsInChildren<NanamiUI.TextField>(true))
                text.WarmUp();
        }

        // 无 prefab 的独立测试用：设定画布尺寸并摆好相机（供 Depth/Drag 这类自建物体的坐标换算）。
        public void Configure(int width, int height)
        {
            _size = new Vector2Int(width, height);
            _canvasRt.sizeDelta = _size;
            PlaceCamera();
        }

        // 相机摆位：正交、覆盖整页，画布左上角落在 world 原点（与快照/截图坐标约定一致）。
        public void PlaceCamera()
        {
            _camera.orthographicSize = _size.y * 0.5f;
            _camera.transform.position = new Vector3(_size.x * 0.5f, -_size.y * 0.5f, -10);
        }

        public Texture2D Capture()
        {
            PlaceCamera();
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
            {
                Object.Destroy(_instance);
                _instance = null;
            }
        }

        public void Teardown()
        {
            Time.captureDeltaTime = 0;
            if (_canvas != null)
            {
                Object.Destroy(_canvas);
                _canvas = null;
                _canvasRt = null;
            }
            if (_camera != null)
            {
                Object.Destroy(_camera.gameObject);
                _camera = null;
            }
            if (_eventSystem != null)
            {
                Object.Destroy(_eventSystem);
                _eventSystem = null;
            }
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
