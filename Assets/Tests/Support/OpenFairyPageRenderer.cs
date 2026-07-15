using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI.TestSupport
{
    // 复刻 BasicsRenderDiff 的 OpenFairy.UGUI 半边渲染：WorldSpace canvas + 禁用的手动 Render 相机，
    // 逐页实例化产物、播 t0、由调用方逐帧静置到末帧后截全分辨率图。只渲 OpenFairy.UGUI，不涉及 FairyGUI。
    public sealed class OpenFairyPageRenderer
    {
        private static readonly Color Background = new Color32(0x36, 0x3C, 0x44, 0xFF);

        private Camera _camera;
        private GameObject _canvas;
        private RectTransform _canvasRt;
        private GameObject _instance;
        private GameObject _eventSystem;
        private Vector2Int _size;
        private Font _runtimeFont;

        public GameObject Instance => _instance;
        public Camera Camera => _camera;
        public RectTransform CanvasRt => _canvasRt;
        public GraphicRaycaster Raycaster => _canvas.GetComponent<GraphicRaycaster>();

        public void Setup()
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = 1f / 60f; // 两侧动效同步的关键：锁定每帧步进量
            Application.runInBackground = true;

            _camera = new GameObject("OpenFairy.UGUI Golden Camera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.cullingMask = 1; // 只渲染 Default 层
            _camera.orthographic = true;
            _camera.nearClipPlane = -30;
            _camera.farClipPlane = 30;
            _camera.enabled = false; // 只用于手动 Render

            // GraphicRaycaster 供交互驱动取相机（其 eventCamera = 画布 worldCamera），静态截图不用它。
            _canvas = new GameObject("OpenFairy.UGUI Golden Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
                _eventSystem = new GameObject("OpenFairy.UGUI EventSystem", typeof(EventSystem));
        }

        public void LoadPage(ParityPage page)
        {
            LoadComponent(page.Package, page.Component);
            if (_instance == null)
                return;
            foreach (var transition in _instance.GetComponents<Transition>())
                if (transition.transitionName == "t0")
                    transition.Play().Forget();
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
            // 复刻用户脚本的运行时字体替换（Assets/Scripts 的 DemoFont，TestSupport 引用不到 Assembly-CSharp 故内联）：
            // 与 FairyGUI 参照侧的 UIConfig.defaultFont = "Microsoft YaHei" 同字体，否则像素对比全崩。
            foreach (var text in _instance.GetComponentsInChildren<OpenFairy.UGUI.TextField>(true))
            {
                if (text.fontNames == "Heiti SC Medium")
                {
                    text.fontNames = "Microsoft YaHei";
                    text.font = _runtimeFont = _runtimeFont ? _runtimeFont : Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
                }
                text.WarmUp();
            }
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
            // ARGB32 与 GoldenImage.Load（LoadImage 解 PNG 的输出格式）一致，ImageAssert 要求两侧格式相同。
            var texture = new Texture2D(_size.x, _size.y, TextureFormat.ARGB32, false);
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
}
