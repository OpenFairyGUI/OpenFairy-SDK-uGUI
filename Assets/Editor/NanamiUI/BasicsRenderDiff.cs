using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FairyGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI.Editor
{
    public static class BasicsRenderDiff
    {
        private const string DocsDir = "Docs/RenderDiff";
        private const string PackageName = "Basics";
        private const string FairyPackage = "UI/Basics";
        private const string NanamiPrefabRoot = "Assets/UIProject/Assets/Basics";
        private const string SourceRoot = "UIProject/assets/Basics";
        private const string PendingKey = "NanamiUI.BasicsRenderDiff.Pending";
        private const string ExitPlayModeKey = "NanamiUI.BasicsRenderDiff.ExitPlayMode";

        private static int _pageIndex;
        private static bool _running;
        private static bool _pageReady;
        private static bool _exitWhenDone;
        private static UIPanel _fairyPanel;
        private static GComponent _fairyView;
        private static Camera _fairyCamera;

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
        };

        [MenuItem("Tools/NanamiUI/Capture Basics Render Diff")]
        public static void CaptureAll()
        {
            if (!EditorApplication.isPlaying)
            {
                _exitWhenDone = true;
                SessionState.SetBool(PendingKey, true);
                SessionState.SetBool(ExitPlayModeKey, true);
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
            SessionState.SetBool(PendingKey, false);
            EditorApplication.delayCall += StartCapture;
        }

        private static void StartCapture()
        {
            if (_running)
                return;

            Directory.CreateDirectory(DocsDir);
            UIConfig.defaultFont = "Microsoft YaHei";
            NanamiUI.Text.defaultFont = "Microsoft YaHei";
            if (UIPackage.GetByName(PackageName) == null)
                UIPackage.AddPackage(FairyPackage);
            PrepareFairyScene();

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
                StopCapture();
                Debug.Log($"NanamiUI Basics render diff saved to {DocsDir}.");
                if (_exitWhenDone || SessionState.GetBool(ExitPlayModeKey, false))
                {
                    _exitWhenDone = false;
                    SessionState.SetBool(ExitPlayModeKey, false);
                    EditorApplication.isPlaying = false;
                }
                return;
            }

            var page = Pages[_pageIndex++];
            var size = PageSize(page);
            if (!_pageReady)
            {
                PrepareFairyPage(page, size);
                _pageReady = true;
                _pageIndex--;
                return;
            }

            var fairy = CaptureFairy(page, size);
            var nanami = CaptureNanami(page, size);
            var diff = Diff(fairy, nanami);
            var name = SafeName(page.Name);
            Save(fairy, $"{DocsDir}/{name}_fairygui.png");
            Save(nanami, $"{DocsDir}/{name}_nanami.png");
            Save(diff, $"{DocsDir}/{name}_diff.png");
            UnityEngine.Object.DestroyImmediate(fairy);
            UnityEngine.Object.DestroyImmediate(nanami);
            UnityEngine.Object.DestroyImmediate(diff);
            _pageReady = false;
            Debug.Log($"Captured Basics render diff {_pageIndex}/{Pages.Length}: {page.Name}");
        }

        private static void StopCapture()
        {
            if (_fairyView != null)
            {
                _fairyView.Dispose();
                _fairyView = null;
            }
            _running = false;
            EditorApplication.update -= CaptureNext;
        }

        private static void PrepareFairyScene()
        {
            var stageCameras = UnityEngine.Object.FindObjectsByType<StageCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var stageCamera = stageCameras.FirstOrDefault(stage => stage.GetComponents<Component>().Any(component => component.GetType().Name == "UniversalAdditionalCameraData"))
                ?? stageCameras.First();
            stageCamera.gameObject.SetActive(true);
            stageCamera.enabled = true;
            _fairyCamera = stageCamera.GetComponent<Camera>();
            _fairyCamera.enabled = true;

            _fairyPanel = UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(panel => panel.packageName == PackageName);
            _fairyPanel.gameObject.SetActive(true);
            _fairyPanel.enabled = true;
            _fairyPanel.packageName = PackageName;
            _fairyPanel.componentName = string.Empty;
            _fairyPanel.CreateUI();
        }

        private static void PrepareFairyPage(Page page, Vector2Int size)
        {
            var stage = Stage.inst;
            var stageCamera = _fairyCamera.GetComponent<StageCamera>();
            var unitsPerPixel = StageCamera.DefaultCameraSize * 2 / size.y;
            stage.SetSize(size.x, size.y);
            stage.cachedTransform.localScale = new Vector3(unitsPerPixel, unitsPerPixel, unitsPerPixel);
            GRoot.inst.ApplyContentScaleFactor();
            stageCamera.unitsPerPixel = unitsPerPixel;
            _fairyCamera.orthographic = true;
            _fairyCamera.orthographicSize = StageCamera.DefaultCameraSize;
            _fairyCamera.aspect = (float)size.x / size.y;
            _fairyCamera.transform.position = new Vector3(_fairyCamera.orthographicSize * _fairyCamera.aspect, -_fairyCamera.orthographicSize, -10);
            _fairyCamera.clearFlags = CameraClearFlags.SolidColor;
            _fairyCamera.backgroundColor = Color.clear;
            _fairyCamera.cullingMask = 1 << LayerMask.NameToLayer(StageCamera.LayerName);

            if (_fairyView != null)
                _fairyView.Dispose();
            GRoot.inst.RemoveChildren(0, -1, true);
            var view = UIPackage.CreateObject(PackageName, page.Component).asCom;
            GRoot.inst.AddChild(view);
            view.SetXY(0, 0);
            view.EnsureBoundsCorrect();
            _fairyView = view;
        }

        private static Texture2D CaptureFairy(Page page, Vector2Int size)
        {
            RenderTexture rt = null;
            try
            {
                rt = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                _fairyCamera.targetTexture = rt;
                _fairyCamera.Render();
                return Read(rt);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FairyGUI capture failed for {page.Component}: {e.Message}");
                return Placeholder(size, new Color32(255, 230, 230, 255));
            }
            finally
            {
                if (_fairyCamera != null)
                    _fairyCamera.targetTexture = null;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D CaptureNanami(Page page, Vector2Int size)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{NanamiPrefabRoot}/{page.Component}.prefab");
            if (prefab == null)
                return Placeholder(size, new Color32(235, 235, 235, 255));

            var cameraObject = new GameObject("NanamiUI Capture Camera");
            var canvasObject = new GameObject("NanamiUI Capture Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            GameObject instance = null;
            RenderTexture rt = null;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = size.y * 0.5f;
                camera.transform.position = new Vector3(size.x * 0.5f, -size.y * 0.5f, -10);
                camera.nearClipPlane = -30;
                camera.farClipPlane = 30;

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1;

                var canvasRt = (RectTransform)canvasObject.transform;
                canvasRt.pivot = new Vector2(0, 1);
                canvasRt.sizeDelta = size;
                canvasRt.position = Vector3.zero;

                instance = UnityEngine.Object.Instantiate(prefab, canvasRt, false);
                var rtInstance = (RectTransform)instance.transform;
                rtInstance.anchorMin = rtInstance.anchorMax = rtInstance.pivot = new Vector2(0, 1);
                rtInstance.anchoredPosition = Vector2.zero;
                rtInstance.sizeDelta = size;
                rtInstance.localScale = Vector3.one;
                if (page.Component == "Main")
                    instance.transform.Find("btn_Back").gameObject.SetActive(true);

                Canvas.ForceUpdateCanvases();
                rt = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();
                return Read(rt);
            }
            finally
            {
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
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
                output.SetPixel(x, y, d <= 1f / 255f ? Color.white : new Color(1, 1 - d, 1 - d, 1));
            }
            output.Apply();
            return output;
        }

        private static Texture2D Read(RenderTexture rt)
        {
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = old;
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

        private static Vector2Int PageSize(Page page)
        {
            var xml = page.Component == "Main"
                ? $"{SourceRoot}/Main.xml"
                : $"{SourceRoot}/{page.Component}.xml";
            var parts = XDocument.Load(xml).Root.Attribute("size").Value.Split(',');
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        private static void Save(Texture2D texture, string path)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }

        private static string SafeName(string name) => name.Replace("&", "_").Replace("/", "_");

        private readonly struct Page
        {
            public readonly string Name;
            public readonly string Component;

            public Page(string name, string component)
            {
                Name = name;
                Component = component;
            }
        }
    }
}
