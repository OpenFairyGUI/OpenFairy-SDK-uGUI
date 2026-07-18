using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;
using FairyGUI;
using OpenFairy.UGUI.TestSupport;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;

namespace OpenFairy.UGUI.Editor
{
    // 截图流程：每页 FairyGUI 与 OpenFairy.UGUI 同帧创建、同帧起播 t0，之后用 Time.captureDeltaTime
    // 把两侧的 deltaTime/unscaledDeltaTime 统一锁成 FixedDeltaTime，逐帧同步推进。
    // 在一组采样时刻各截一张缩略图拼成胶片条（上 FairyGUI / 中 OpenFairy.UGUI / 下 diff），
    // 用来确认动效的轨迹与相位一致；末帧再全分辨率输出三联图做静态精度对比。
    public static class BasicsRenderDiff
    {
        private const string DocsDir = "Docs/RenderDiff";
        private const string PendingKey = "OpenFairy.UGUI.BasicsRenderDiff.Pending";
        private const string ExitPlayModeKey = "OpenFairy.UGUI.BasicsRenderDiff.ExitPlayMode";
        private const string GoldenKey = "OpenFairy.UGUI.BasicsRenderDiff.Golden";

        // 统一步进：captureDeltaTime 让每一帧 game time 恰好前进这么多，两侧动效同相位。
        private const float FixedDeltaTime = 1f / 60f;
        private const int ThumbWidth = 240;

        // 胶片条采样时刻（秒）：前密后疏，覆盖缓动初段的快速变化与末段的静止。
        private static readonly float[] SampleTimes =
            { 0f, 0.067f, 0.133f, 0.25f, 0.4f, 0.6f, 0.85f, 1.15f, 1.5f, 2f, 2.6f, 3.3f };

        // 不透明底色（场景背景 #363C44）：FairyGUI 文字 shader 是直通 alpha 混合、uGUI 是预乘，
        // 透明背景会导致两侧 alpha 通道不同（视觉上其实一致），垫实底后 diff 才反映真实视觉。
        private static readonly Color Background = new Color32(0x36, 0x3C, 0x44, 0xFF);

        private static int _pageIndex;
        private static int _sampleIndex;
        private static bool _running;
        private static bool _pageReady;
        private static bool _exitWhenDone;
        private static bool _writeGolden;
        private static float _pageStartTime;
        private static Texture2D[] _fairyThumbs;
        private static Texture2D[] _openFairyThumbs;
        private static float _maxError;
        private static UIPanel _fairyPanel;
        private static GComponent _fairyView;
        private static Camera _fairyCamera;
        private static Camera _openFairyCamera;
        private static GameObject _openFairyCanvas;
        private static GameObject _openFairyInstance;
        private static OpenFairy.UGUI.Line _openFairyGraphLine;
        private static NGraphics _fairyGraphLine;
        private static Sprite _changeSprite;

        private static readonly Page[] Pages =
        {
            new("Main", "Main"),
            new("Button", "Demo_Button"),
            new("Image", "Demo_Image"),
            new("Graph", "Demo_Graph"),
            new("MovieClip", "Demo_MovieClip"),
            new("Depth", "Demo_Depth"),
            new("Loader", "Demo_Loader"),
            new("List", "Demo_List"),
            new("ProgressBar", "Demo_ProgressBar"),
            new("Slider", "Demo_Slider"),
            new("ComboBox", "Demo_ComboBox"),
            new("Clip&Scroll", "Demo_Clip&Scroll"),
            new("Controller", "Demo_Controller"),
            new("Relation", "Demo_Relation"),
            new("Label", "Demo_Label"),
            new("Popup", "Demo_Popup"),
            new("Window", "Demo_Window"),
            new("Drag&Drop", "Demo_Drag&Drop"),
            new("Component", "Demo_Component"),
            new("Grid", "Demo_Grid"),
            new("Text", "Demo_Text"),
            new("Transition_BOSS", "BOSS", "Transition"),
            new("Transition_BOSS_SKILL", "BOSS_SKILL", "Transition"),
            new("Transition_TRAP", "TRAP", "Transition"),
            new("Transition_GoodHit", "GoodHit", "Transition"),
            new("Transition_PowerUp", "PowerUp", "Transition"),
            new("Transition_PathDemo", "PathDemo", "Transition"),
        };

        // 直接生成 golden：跑同一套播放式截图，额外把静态页的 FairyGUI 末帧写进 ReferenceImages（取代旧的两步 Capture+Promote）。
        [MenuItem("Tools/OpenFairy/Generate Golden References")]
        public static void GenerateGolden()
        {
            _writeGolden = true;
            if (!EditorApplication.isPlaying)
            {
                _exitWhenDone = true;
                SessionState.SetBool(PendingKey, true);
                SessionState.SetBool(ExitPlayModeKey, true);
                SessionState.SetBool(GoldenKey, true);
                EditorApplication.EnterPlaymode();
                return;
            }
            StartCapture();
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;

            _exitWhenDone = SessionState.GetBool(ExitPlayModeKey, false);
            _writeGolden = SessionState.GetBool(GoldenKey, false);
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(GoldenKey, false);
            EditorApplication.delayCall += StartCapture;
        }

        private static void StartCapture()
        {
            if (_running)
                return;

            Directory.CreateDirectory(DocsDir);
            if (_writeGolden)
                Directory.CreateDirectory(ParityCatalog.ReferenceDir);
            Application.runInBackground = true; // 编辑器失焦时播放循环会冻结，截图必须后台可跑
            Time.timeScale = 1;
            Time.captureDeltaTime = FixedDeltaTime; // 两侧动效同步的关键：锁定每帧步进量
            UIConfig.defaultFont = "Microsoft YaHei";
            // 复刻 BasicsMain 的配置：FairyGUI 只有在这里配了滚动条资源才会创建滚动条。
            // 不配则 FairyGUI 侧无滚动条、viewport 用满宽，与已烤进滚动条的 OpenFairy.UGUI 产物产生假 diff。
            UIConfig.verticalScrollBar = "ui://Basics/ScrollBar_VT";
            UIConfig.horizontalScrollBar = "ui://Basics/ScrollBar_HZ";
            foreach (var package in Pages.AsValueEnumerable().Select(page => page.Package).Distinct())
                if (UIPackage.GetByName(package) == null)
                    UIPackage.AddPackage($"UI/{package}");
            PrepareFairyScene();
            PrepareOpenFairyScene();
            _changeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIProject/Assets/Basics/images/change.png");

            _pageIndex = 0;
            _pageReady = false;
            _running = true;
            EditorApplication.update -= CaptureNext;
            EditorApplication.update += CaptureNext;
        }

        private static void CaptureNext()
        {
            if (!EditorApplication.isPlaying)
            {
                StopCapture();
                return;
            }

            if (_pageIndex >= Pages.Length)
            {
                if (_writeGolden)
                    DumpInteractionGoldens();
                StopCapture();
                Debug.Log($"OpenFairy.UGUI Basics render diff saved to {DocsDir}.");
                if (_exitWhenDone || SessionState.GetBool(ExitPlayModeKey, false))
                {
                    _exitWhenDone = false;
                    SessionState.SetBool(ExitPlayModeKey, false);
                    EditorApplication.isPlaying = false;
                }
                return;
            }

            var page = Pages[_pageIndex];
            var size = PageSize(page);
            if (!_pageReady)
            {
                PrepareFairyPage(page, size);
                PrepareOpenFairyPage(page, size);
                _pageStartTime = Time.time;
                _sampleIndex = 0;
                _maxError = 0;
                _fairyThumbs = new Texture2D[SampleTimes.Length];
                _openFairyThumbs = new Texture2D[SampleTimes.Length];
                _pageReady = true;
                return;
            }

            if (_sampleIndex < SampleTimes.Length)
            {
                if (Time.time - _pageStartTime < SampleTimes[_sampleIndex])
                    return;
                DriveGraph(Time.time - _pageStartTime);
                CaptureSample(page, size);
                _sampleIndex++;
                return;
            }

            var strip = BuildStrip();
            Save(strip, $"{DocsDir}/{SafeName(page.Name)}_strip.png");
            UnityEngine.Object.DestroyImmediate(strip);
            foreach (var texture in _fairyThumbs.AsValueEnumerable().Concat(_openFairyThumbs))
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            if (_openFairyInstance != null)
                UnityEngine.Object.Destroy(_openFairyInstance);
            _pageReady = false;
            _pageIndex++;
            Debug.Log($"Captured Basics render diff {_pageIndex}/{Pages.Length}: {page.Name} (max diff {_maxError * 255:F1}/255)");
        }

        private static void CaptureSample(Page page, Vector2Int size)
        {
            var thumb = ThumbSize(size);
            _fairyThumbs[_sampleIndex] = CaptureFairy(page, size, thumb);
            _openFairyThumbs[_sampleIndex] = CaptureOpenFairy(page, size, thumb);

            if (_sampleIndex < SampleTimes.Length - 1)
                return;

            // 末帧（已静止）额外全分辨率输出三联图，保留既有静态精度对比。
            var fairy = CaptureFairy(page, size, size);
            var openFairy = CaptureOpenFairy(page, size, size);
            var diff = Diff(fairy, openFairy);
            var name = SafeName(page.Name);
            Save(fairy, $"{DocsDir}/{name}_fairygui.png");
            if (_writeGolden && ParityCatalog.StaticPages.AsValueEnumerable().Any(p => p.Name == page.Name))
                Save(fairy, $"{ParityCatalog.ReferenceDir}/{name}.png");
            Save(openFairy, $"{DocsDir}/{name}_openfairy.png");
            Save(diff, $"{DocsDir}/{name}_diff.png");
            UnityEngine.Object.DestroyImmediate(fairy);
            UnityEngine.Object.DestroyImmediate(openFairy);
            UnityEngine.Object.DestroyImmediate(diff);
        }

        private static void StopCapture()
        {
            Time.captureDeltaTime = 0;
            if (_fairyView != null)
            {
                _fairyView.Dispose();
                _fairyView = null;
            }
            if (_openFairyInstance != null)
                UnityEngine.Object.Destroy(_openFairyInstance);
            if (_openFairyCanvas != null)
                UnityEngine.Object.Destroy(_openFairyCanvas);
            if (_openFairyCamera != null)
                UnityEngine.Object.Destroy(_openFairyCamera.gameObject);
            _running = false;
            EditorApplication.update -= CaptureNext;
            if (_writeGolden)
                AssetDatabase.Refresh();
            _writeGolden = false;
        }

        private static void PrepareFairyScene()
        {
            var stageCameras = UnityEngine.Object.FindObjectsByType<StageCamera>(FindObjectsInactive.Include);
            var stageCamera = stageCameras.AsValueEnumerable().FirstOrDefault(stage => stage.GetComponents<Component>().AsValueEnumerable().Any(component => component.GetType().Name == "UniversalAdditionalCameraData"))
                ?? stageCameras.AsValueEnumerable().First();
            stageCamera.gameObject.SetActive(true);
            stageCamera.enabled = true;
            _fairyCamera = stageCamera.GetComponent<Camera>();
            _fairyCamera.enabled = true;

            _fairyPanel = UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Include).AsValueEnumerable()
                .First(panel => panel.packageName == "Basics");
            _fairyPanel.gameObject.SetActive(true);
            _fairyPanel.enabled = true;
            _fairyPanel.componentName = string.Empty;
            _fairyPanel.CreateUI();
        }

        private static void PrepareOpenFairyScene()
        {
            var cameraObject = new GameObject("OpenFairy.UGUI Capture Camera");
            _openFairyCamera = cameraObject.AddComponent<Camera>();
            _openFairyCamera.clearFlags = CameraClearFlags.SolidColor;
            _openFairyCamera.backgroundColor = Background;
            _openFairyCamera.cullingMask = 1; // 只渲染 Default 层，隔离场景里的 FairyGUI 舞台
            _openFairyCamera.orthographic = true;
            _openFairyCamera.nearClipPlane = -30;
            _openFairyCamera.farClipPlane = 30;
            _openFairyCamera.enabled = false; // 只用于手动 Render

            _openFairyCanvas = new GameObject("OpenFairy.UGUI Capture Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _openFairyCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _openFairyCamera;
            var scaler = _openFairyCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1;
            var canvasRt = (RectTransform)_openFairyCanvas.transform;
            canvasRt.pivot = new Vector2(0, 1);
            canvasRt.position = Vector3.zero;
        }

        private static void PrepareFairyPage(Page page, Vector2Int size)
        {
            var stage = Stage.inst;
            var stageCamera = _fairyCamera.GetComponent<StageCamera>();
            var unitsPerPixel = StageCamera.DefaultCameraSize * 2 / size.y;
            stage.SetSize(size.x, size.y);
            stage.cachedTransform.localScale = new Vector3(unitsPerPixel, unitsPerPixel, unitsPerPixel);
            FairyGUI.GRoot.inst.ApplyContentScaleFactor();
            stageCamera.unitsPerPixel = unitsPerPixel;
            _fairyCamera.orthographic = true;
            _fairyCamera.orthographicSize = StageCamera.DefaultCameraSize;
            _fairyCamera.aspect = (float)size.x / size.y;
            _fairyCamera.transform.position = new Vector3(_fairyCamera.orthographicSize * _fairyCamera.aspect, -_fairyCamera.orthographicSize, -10);
            _fairyCamera.clearFlags = CameraClearFlags.SolidColor;
            _fairyCamera.backgroundColor = Background;
            _fairyCamera.cullingMask = 1 << LayerMask.NameToLayer(StageCamera.LayerName);

            if (_fairyView != null)
                _fairyView.Dispose();
            FairyGUI.GRoot.inst.RemoveChildren(0, -1, true);
            var view = UIPackage.CreateObject(page.Package, page.Component).asCom;
            FairyGUI.GRoot.inst.AddChild(view);
            view.SetXY(0, 0);
            view.EnsureBoundsCorrect();
            if (view.GetTransition("t0") is { } transition)
            {
                // Time.captureDeltaTime 只锁 Time.deltaTime，不锁 unscaledDeltaTime；FairyGUI 的 GTweener
                // 默认 ignoreEngineTimeScale=true 用 unscaledDeltaTime，会按真实墙钟推进而与 OpenFairy.UGUI 脱同步。
                // 关掉它，让 tweener 改用被锁定的 deltaTime，两侧才能逐帧同相位。
                transition.ignoreEngineTimeScale = false;
                transition.Play();
            }
            _fairyView = view;
            _fairyGraphLine = null;
            if (page.Name == "Graph")
                FairyPlayGraph(view);
        }

        private static void PrepareOpenFairyPage(Page page, Vector2Int size)
        {
            var canvasRt = (RectTransform)_openFairyCanvas.transform;
            canvasRt.sizeDelta = new Vector2(size.x, size.y);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/UIProject/Assets/{page.Package}/{page.Component}.prefab");
            if (prefab == null)
            {
                _openFairyInstance = null;
                return;
            }
            _openFairyInstance = UnityEngine.Object.Instantiate(prefab, canvasRt, false);
            var rt = (RectTransform)_openFairyInstance.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            if (page.Component == "Main")
                _openFairyInstance.transform.Find("btn_Back").gameObject.SetActive(true);
            OpenFairy.UGUI.Example.DemoFont.Apply(_openFairyInstance); // 与 FairyGUI 侧 UIConfig.defaultFont 同字体
            foreach (var text in _openFairyInstance.GetComponentsInChildren<OpenFairy.UGUI.TextField>(true))
                text.WarmUp();
            if (_openFairyInstance.GetComponents<Transition>().AsValueEnumerable().FirstOrDefault(transition => transition.transitionName == "t0") is { } transition)
                transition.Play().Forget();
            _openFairyGraphLine = page.Component == "Demo_Graph"
                ? OpenFairy.UGUI.Example.GraphDemo.Setup(_openFairyInstance, _changeSprite)
                : null;
        }

        // Demo_Graph 的 line 由 fillEnd 5s 线性扫入，两侧按同一 elapsed 驱动以保持同相位。
        private static void DriveGraph(float elapsed)
        {
            var f = Mathf.Clamp01(elapsed / 5f);
            if (_openFairyGraphLine != null)
            {
                _openFairyGraphLine.fillEnd = f;
                _openFairyGraphLine.SetVerticesDirty();
            }
            if (_fairyGraphLine != null)
            {
                _fairyGraphLine.GetMeshFactory<LineMesh>().fillEnd = f;
                _fairyGraphLine.SetMeshDirty();
                // FairyGUI 的网格在 Stage 更新时才惰性重建，而 OpenFairy.UGUI 侧被 Canvas.ForceUpdateCanvases 立即重建，
                // 不强制重建会导致 FairyGUI 参照滞后一个采样，line 的 fillEnd 相位对不齐。
                _fairyGraphLine.UpdateMesh();
            }
        }

        // 移植 FairyGUI BasicsMain.PlayGraph：把 FairyGUI 参照侧的图形改成扇形/带贴图梯形/三条折线。
        private static void FairyPlayGraph(GComponent obj)
        {
            var pie = obj.GetChild("pie").asGraph.shape;
            var ellipse = pie.graphics.GetMeshFactory<EllipseMesh>();
            ellipse.startDegree = 30;
            ellipse.endDegreee = 300;
            pie.graphics.SetMeshDirty();

            var trap = obj.GetChild("trapezoid").asGraph.shape;
            var trapezoid = trap.graphics.GetMeshFactory<PolygonMesh>();
            trapezoid.usePercentPositions = true;
            trapezoid.points.Clear();
            trapezoid.points.Add(new Vector2(0f, 1f));
            trapezoid.points.Add(new Vector2(0.3f, 0));
            trapezoid.points.Add(new Vector2(0.7f, 0));
            trapezoid.points.Add(new Vector2(1f, 1f));
            trapezoid.texcoords.Clear();
            trapezoid.texcoords.AddRange(VertexBuffer.NormalizedUV);
            trap.graphics.SetMeshDirty();
            trap.graphics.texture = (NTexture)UIPackage.GetItemAsset("Basics", "change");

            var lineShape = obj.GetChild("line").asGraph.shape;
            var line = lineShape.graphics.GetMeshFactory<LineMesh>();
            line.lineWidthCurve = AnimationCurve.Linear(0, 25, 1, 10);
            line.roundEdge = true;
            line.gradient = OpenFairy.UGUI.Example.GraphDemo.LineGradient();
            line.path.Create(new[]
            {
                new GPathPoint(new Vector3(0, 120, 0)), new GPathPoint(new Vector3(20, 120, 0)),
                new GPathPoint(new Vector3(100, 100, 0)), new GPathPoint(new Vector3(180, 30, 0)),
                new GPathPoint(new Vector3(100, 0, 0)), new GPathPoint(new Vector3(20, 30, 0)),
                new GPathPoint(new Vector3(100, 100, 0)), new GPathPoint(new Vector3(180, 120, 0)),
                new GPathPoint(new Vector3(200, 120, 0)),
            });
            line.fillEnd = 0;
            lineShape.graphics.SetMeshDirty();
            _fairyGraphLine = lineShape.graphics;

            var line2Shape = obj.GetChild("line2").asGraph.shape;
            var line2 = line2Shape.graphics.GetMeshFactory<LineMesh>();
            line2.lineWidth = 3;
            line2.roundEdge = true;
            line2.path.Create(new[]
            {
                new GPathPoint(new Vector3(0, 120, 0), GPathPoint.CurveType.Straight),
                new GPathPoint(new Vector3(60, 30, 0), GPathPoint.CurveType.Straight),
                new GPathPoint(new Vector3(80, 90, 0), GPathPoint.CurveType.Straight),
                new GPathPoint(new Vector3(140, 30, 0), GPathPoint.CurveType.Straight),
                new GPathPoint(new Vector3(160, 90, 0), GPathPoint.CurveType.Straight),
                new GPathPoint(new Vector3(220, 30, 0), GPathPoint.CurveType.Straight),
            });
            line2Shape.graphics.SetMeshDirty();

            var image = obj.GetChild("line3");
            var line3 = image.displayObject.graphics.GetMeshFactory<LineMesh>();
            line3.lineWidth = 30;
            line3.roundEdge = false;
            line3.path.Create(new[]
            {
                new GPathPoint(new Vector3(0, 30, 0), new Vector3(50, -30), new Vector3(150, -50)),
                new GPathPoint(new Vector3(200, 30, 0), new Vector3(300, 130)),
                new GPathPoint(new Vector3(400, 30, 0)),
            });
            image.displayObject.graphics.SetMeshDirty();
        }

        private static Texture2D CaptureFairy(Page page, Vector2Int size, Vector2Int res)
        {
            UnityEngine.RenderTexture rt = null;
            try
            {
                rt = UnityEngine.RenderTexture.GetTemporary(res.x, res.y, 24, RenderTextureFormat.ARGB32);
                _fairyCamera.targetTexture = rt;
                _fairyCamera.Render();
                return Read(rt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FairyGUI capture failed for {page.Component}: {e.Message}");
                return Placeholder(res, new Color32(255, 230, 230, 255));
            }
            finally
            {
                if (_fairyCamera != null)
                    _fairyCamera.targetTexture = null;
                if (rt != null)
                    UnityEngine.RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D CaptureOpenFairy(Page page, Vector2Int size, Vector2Int res)
        {
            if (_openFairyInstance == null)
                return Placeholder(res, new Color32(235, 235, 235, 255));

            UnityEngine.RenderTexture rt = null;
            try
            {
                _openFairyCamera.orthographicSize = size.y * 0.5f;
                _openFairyCamera.transform.position = new Vector3(size.x * 0.5f, -size.y * 0.5f, -10);
                Canvas.ForceUpdateCanvases();
                rt = UnityEngine.RenderTexture.GetTemporary(res.x, res.y, 24, RenderTextureFormat.ARGB32);
                _openFairyCamera.targetTexture = rt;
                _openFairyCamera.Render();
                return Read(rt);
            }
            finally
            {
                if (rt != null)
                    UnityEngine.RenderTexture.ReleaseTemporary(rt);
                _openFairyCamera.targetTexture = null;
            }
        }

        // 三行（FairyGUI / OpenFairy.UGUI / diff）× 采样列拼成一张胶片条。
        private static Texture2D BuildStrip()
        {
            var cols = SampleTimes.Length;
            var w = _fairyThumbs[0].width;
            var h = _fairyThumbs[0].height;
            var strip = new Texture2D(w * cols, h * 3, TextureFormat.RGBA32, false);
            for (var c = 0; c < cols; c++)
            {
                var diff = Diff(_fairyThumbs[c], _openFairyThumbs[c]);
                strip.SetPixels(c * w, h * 2, w, h, _fairyThumbs[c].GetPixels());
                strip.SetPixels(c * w, h, w, h, _openFairyThumbs[c].GetPixels());
                strip.SetPixels(c * w, 0, w, h, diff.GetPixels());
                UnityEngine.Object.DestroyImmediate(diff);
            }
            strip.Apply();
            return strip;
        }

        private static Texture2D Diff(Texture2D a, Texture2D b)
        {
            var width = Math.Max(a.width, b.width);
            var height = Math.Max(a.height, b.height);
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var ca = x < a.width && y < a.height ? a.GetPixel(x, y) : Color.clear;
                var cb = x < b.width && y < b.height ? b.GetPixel(x, y) : Color.clear;
                var d = Mathf.Max(Mathf.Abs(ca.r - cb.r), Mathf.Abs(ca.g - cb.g), Mathf.Abs(ca.b - cb.b), Mathf.Abs(ca.a - cb.a));
                if (d > _maxError)
                    _maxError = d;
                output.SetPixel(x, y, d <= 1f / 255f ? Color.white : new Color(1, 1 - d, 1 - d, 1));
            }
            output.Apply();
            return output;
        }

        private static Texture2D Read(UnityEngine.RenderTexture rt)
        {
            var old = UnityEngine.RenderTexture.active;
            UnityEngine.RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            UnityEngine.RenderTexture.active = old;
            return texture;
        }

        private static Texture2D Placeholder(Vector2Int size, Color32 color)
        {
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            for (var y = 0; y < size.y; y++)
            for (var x = 0; x < size.x; x++)
            {
                var border = x < 4 || y < 4 || x >= size.x - 4 || y >= size.y - 4;
                var stripe = (x + y) % 48 < 24;
                texture.SetPixel(x, y, border || stripe ? color : Color.white);
            }
            texture.Apply();
            return texture;
        }

        private static Vector2Int ThumbSize(Vector2Int size)
        {
            var h = Mathf.Max(1, Mathf.RoundToInt(ThumbWidth * (float)size.y / size.x));
            return new Vector2Int(ThumbWidth, h);
        }

        private static Vector2Int PageSize(Page page)
        {
            var xml = $"UIProject/assets/{page.Package}/{page.Component}.xml";
            var parts = XDocument.Load(xml).Root.Attribute("size").Value.Split(',');
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        private static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }

        private static string SafeName(string name) => name.Replace("&", "_").Replace("/", "_");

        // 交互几何参照：对每个 case 建 FairyGUI 视图、驱动到该态、快照目标子树几何写 JSON。
        // 与静态 golden 同一趟 Generate 生成；FairyGUI 是规格，OpenFairy.UGUI 侧测试时现算再比。
        // 首批交互（slider value / checkbox 显隐）为瞬时终态，故同步一帧完成即可，无需推帧。
        private static void DumpInteractionGoldens()
        {
            foreach (var c in ParityCatalog.Interactions)
            {
                if (UIPackage.GetByName(c.Package) == null)
                    UIPackage.AddPackage($"UI/{c.Package}");
                var view = UIPackage.CreateObject(c.Package, c.Component).asCom;
                FairyGUI.GRoot.inst.AddChild(view);
                view.SetXY(0, 0);
                view.EnsureBoundsCorrect();
                var target = view.GetChild(c.Target);
                DriveFairy(target, c.Action, c.Param);
                var snapshot = FromFairy(target);
                snapshot.Save(ParityCatalog.GeometryPath(c));
                view.Dispose();
            }
            Debug.Log($"Wrote {ParityCatalog.Interactions.Length} interaction geometry references to {ParityCatalog.ReferenceDir}.");
        }

        private static void DriveFairy(GObject target, ParityCatalog.ActionKind action, float param)
        {
            switch (action)
            {
                case ParityCatalog.ActionKind.SliderValue:
                    target.asSlider.value = param;
                    break;
                case ParityCatalog.ActionKind.ButtonSelected:
                    target.asButton.selected = true;
                    break;
                case ParityCatalog.ActionKind.ButtonDown:
                    ((GComponent)target).GetController("button").selectedPage = "down";
                    break;
            }
        }

        // 手动累加 local x/y/width/height 组合几何：不经 LocalToGlobal/LocalToRoot，故与 stage/GRoot/UIContentScaler
        // 的缩放无关，得到纯设计像素（相对目标左上、y 向下），与 OpenFairy.UGUI 烘焙的 1:1 px 布局同口径。
        private static GeometrySnapshot FromFairy(GObject target)
        {
            var snapshot = new GeometrySnapshot();
            WalkFairy(target, target, 0, 0, snapshot.nodes);
            return snapshot;
        }

        private static void WalkFairy(GObject node, GObject root, float parentX, float parentY, List<GeometrySnapshot.Node> nodes)
        {
            var absX = node == root ? 0 : parentX + node.x;
            var absY = node == root ? 0 : parentY + node.y;
            nodes.Add(new GeometrySnapshot.Node
            {
                path = FairyPath(node, root),
                x = absX,
                y = absY,
                w = node.width,
                h = node.height,
                // GObject.visible 是用户标志、恒为 true；gearDisplay 隐藏是把 displayObject 移出容器（parent==null，
                // 见 GComponent.ChildStateChanged）。故有效可见 = displayObject 在容器里且自身可见，对齐 OpenFairy.UGUI 的 activeSelf。
                active = node.displayObject != null && node.displayObject.parent != null && node.displayObject.visible,
                text = node is GTextField tf ? tf.text ?? "" : "", // 只取真正文本节点，与 OpenFairy.UGUI 侧仅 Text 组件对齐
            });
            if (node is GComponent com)
                for (var i = 0; i < com.numChildren; i++)
                    WalkFairy(com.GetChildAt(i), root, absX, absY, nodes);
        }

        private static string FairyPath(GObject node, GObject root)
        {
            if (node == root)
                return "";
            var path = node.name;
            for (var p = node.parent; p != null && p != root; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        private readonly struct Page
        {
            public readonly string Name;
            public readonly string Component;
            public readonly string Package;

            public Page(string name, string component, string package = "Basics")
            {
                Name = name;
                Component = component;
                Package = package;
            }
        }
    }
}
