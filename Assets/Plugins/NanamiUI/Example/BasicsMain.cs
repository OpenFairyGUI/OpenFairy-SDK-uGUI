using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI.Example
{
    public class BasicsMain : MonoBehaviour
    {
        public string[] demoNames;
        public GameObject[] demoPrefabs;
        public Sprite changeSprite; // Demo_Graph trapezoid 运行时贴图（change.png）
        public GameObject windowAPrefab;
        public GameObject windowBPrefab;
        public GameObject popupMenuPrefab;
        public GameObject popupItemPrefab;
        public GameObject popupComPrefab;

        private NanamiUI.Window _winA, _winB;
        private NanamiUI.PopupMenu _pm;
        private GameObject _popupCom;

        private static readonly (string Name, string Field)[] Buttons =
        {
            ("Button", "m_btn_Button"),
            ("Image", "m_btn_Image"),
            ("Graph", "m_btn_Graph"),
            ("MovieClip", "m_btn_MovieClip"),
            ("Depth", "m_btn_Depth"),
            ("Loader", "m_btn_Loader"),
            ("List", "m_btn_List"),
            ("ProgressBar", "m_btn_ProgressBar"),
            ("Slider", "m_btn_Slider"),
            ("ComboBox", "m_btn_ComboBox"),
            ("Clip&Scroll", "m_btn_Clip_Scroll"),
            ("Controller", "m_btn_Controller"),
            ("Relation", "m_btn_Relation"),
            ("Label", "m_btn_Label"),
            ("Popup", "m_btn_Popup"),
            ("Window", "m_btn_Window"),
            ("Drag&Drop", "m_btn_Drag_Drop"),
            ("Component", "m_btn_Component"),
            ("Grid", "m_btn_Grid"),
            ("Text", "m_btn_Text"),
        };

        private NanamiUI.Component _main;
        private Type _mainType;
        private Transform _container;

        private void Awake()
        {
            NanamiUI.TextField.defaultFont = "Microsoft YaHei";
            _main = Array.Find(GetComponents<NanamiUI.Component>(), component => component.GetType().FullName == "UI.Basics.Main");
            _mainType = _main.GetType();
            _container = ((UnityEngine.Component)Get("m_container")).transform;
            NanamiUI.Root.Create((RectTransform)_main.transform); // 顶层覆盖层：承载 window/popup

            Bind("m_btn_Back", Back);
            foreach (var (name, field) in Buttons)
            {
                var demoName = name;
                Bind(field, () => Run(demoName));
            }

            Back();
        }

        private void Run(string name)
        {
            SetPage("_1");
            SetBackVisible(true);
            Show(name);
        }

        private void Back()
        {
            SetPage("_0");
            SetBackVisible(false);
        }

        private void Show(string name)
        {
            for (var i = _container.childCount - 1; i >= 0; i--)
                Destroy(_container.GetChild(i).gameObject);

            var prefab = Prefab(name);
            var go = prefab != null ? Instantiate(prefab, _container, false) : Placeholder(name);
            Place((RectTransform)go.transform);
            // Button demo 的 tab/radio 组现由烘焙的关联控制器（<Button controller=..>）运行时自处理，无需胶水。
            if (name == "Graph")
                PlayGraph(go);
            else if (name == "Depth")
                PlayDepth(go);
            else if (name == "Drag&Drop")
                PlayDragDrop(go);
            else if (name == "Window")
                PlayWindow(go);
            else if (name == "Popup")
                PlayPopup(go);
            else if (name == "ProgressBar")
                StartCoroutine(PlayProgressBar(go));
            else if (name == "Text")
                PlayText(go);
            else if (name == "Grid")
                PlayGrid(go);
            // ComboBox 现由烘焙的 NanamiUI.ComboBox<T> 运行时自处理（点击弹下拉、选项设标题），无需胶水。
        }

        // 复刻 FairyGUI PlayGrid：把两个列表用平台名+随机数据填满；滚动由烘焙的 ScrollPaneHost 自挂。
        private void PlayGrid(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Grid");
            var names = System.Enum.GetNames(typeof(RuntimePlatform));
            var colors = new[] { Color.yellow, Color.red, Color.white, Color.cyan };

            NanamiUI.List.Fill((RectTransform)((UnityEngine.Component)Get(demo, "m_list1")).transform, names.Length, (item, i) =>
            {
                var comp = ItemComp(item, "UI.Basics.GridItem");
                SetText(comp, "m_t0", (i + 1).ToString());
                SetText(comp, "m_t1", names[i]);
                if (Get(comp, "m_t2") is NanamiUI.TextField t2)
                    t2.color = colors[UnityEngine.Random.Range(0, colors.Length)];
            });
            NanamiUI.List.Fill((RectTransform)((UnityEngine.Component)Get(demo, "m_list2")).transform, names.Length, (item, i) =>
            {
                var comp = ItemComp(item, "UI.Basics.GridItem2");
                SetText(comp, "m_t1", names[i]);
                SetText(comp, "m_t3", UnityEngine.Random.Range(0, 10000).ToString());
            });
        }

        private static object ItemComp(GameObject item, string fullName) =>
            Array.Find(item.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == fullName);

        private static void SetText(object comp, string field, string value)
        {
            if (Get(comp, field) is NanamiUI.TextField text)
                text.text = value;
        }

        // 复刻 FairyGUI PlayProgressBar：每帧把每个进度条 value +1、越过 max 回 0（FairyGUI 用 0.001s Timer ≈ 每帧）。
        private System.Collections.IEnumerator PlayProgressBar(GameObject go)
        {
            var bars = go.GetComponentsInChildren<NanamiUI.ProgressBar>();
            while (go)
            {
                foreach (var bar in bars)
                    if (bar)
                    {
                        bar.value = bar.value + 1 > bar.max ? 0 : bar.value + 1;
                    }
                yield return null;
            }
        }

        // 复刻 FairyGUI PlayText：点富文本链接改写其内容；点 n25 把 n22 文本拷到 n24。
        private void PlayText(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Text");
            if (Get(demo, "m_n12") is NanamiUI.TextField rich)
                rich.onClickLink = href =>
                    rich.text = $"[img]ui://Basics/pet[/img][color=#FF0000]You click the link[/color]：{href}";
            var n25 = (UnityEngine.Component)Get(demo, "m_n25");
            BindButton(n25, () =>
            {
                var n22 = (NanamiUI.TextInput)Get(demo, "m_n22"); // n22 是输入框
                var n24 = (NanamiUI.TextField)Get(demo, "m_n24");
                n24.text = n22.text;
            });
        }

        // 复刻 FairyGUI PlayWindow：两个按钮各开一个单例 window（A 无动效居中，B 缩放进出 + 播 t1）。
        private void PlayWindow(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Window");
            BindButton(Get(demo, "m_n0"), () => (_winA ??= new NanamiWindow1 { prefab = windowAPrefab }).Show());
            BindButton(Get(demo, "m_n1"), () => (_winB ??= new NanamiWindow2 { prefab = windowBPrefab }).Show());
        }

        // 复刻 FairyGUI PlayPopup：n0 在按钮下方弹菜单；n1 居中弹一个组件。
        private void PlayPopup(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Popup");
            if (_pm == null)
            {
                _pm = new NanamiUI.PopupMenu(popupMenuPrefab, popupItemPrefab);
                for (var i = 1; i <= 4; i++)
                {
                    var n = i;
                    _pm.AddItem("Item " + n, () => Debug.Log("click Item " + n));
                }
            }
            var n0 = (UnityEngine.Component)Get(demo, "m_n0");
            BindButton(n0, () => _pm.Show((RectTransform)n0.transform, NanamiUI.PopupDirection.Down));
            BindButton(Get(demo, "m_n1"), () =>
            {
                if (_popupCom == null)
                {
                    _popupCom = Instantiate(popupComPrefab);
                    NanamiUI.Root.inst.Center((RectTransform)_popupCom.transform);
                }
                NanamiUI.Root.inst.ShowPopup((RectTransform)_popupCom.transform);
            });
        }

        // 复刻 FairyGUI BasicsMain.PlayDepth：固定物置 sortingOrder 100 + 可拖；两个按钮各建 order 0(红,靠后)/200(绿,靠前) 矩形。
        private void PlayDepth(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Depth");
            var container = (NanamiUI.Component)Get(demo, "m_n22");
            var fixedShape = (NanamiUI.Graph)Get(container, "m_n0");
            var containerRt = (RectTransform)((UnityEngine.Component)container).transform;
            var fixedRt = (RectTransform)fixedShape.transform;
            for (var i = containerRt.childCount - 1; i >= 0; i--)
                if (containerRt.GetChild(i) != fixedRt)
                    Destroy(containerRt.GetChild(i).gameObject);
            NanamiUI.Depth.SetSortingOrder(fixedRt, 100);
            fixedShape.gameObject.AddComponent<NanamiUI.Draggable>();
            fixedShape.raycastTarget = true;
            var startPos = new Vector2(fixedRt.anchoredPosition.x, -fixedRt.anchoredPosition.y); // fairy xy
            BindButton(Get(demo, "m_btn0"), () => { startPos += new Vector2(10, 10); NanamiUI.Depth.CreateRect(containerRt, startPos, 150, 150, 1, Color.black, Color.red, 0); });
            BindButton(Get(demo, "m_btn1"), () => { startPos += new Vector2(10, 10); NanamiUI.Depth.CreateRect(containerRt, startPos, 150, 150, 1, Color.black, Color.green, 200); });
        }

        // 复刻 FairyGUI BasicsMain.PlayDragDrop：a 自由拖；b 拖时改走 DragDropManager 的 agent；c 是落点(收到 icon)；d 限定在 n7 范围内拖。
        private void PlayDragDrop(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Drag_Drop");
            var a = (UnityEngine.Component)Get(demo, "m_a");
            var b = (UnityEngine.Component)Get(demo, "m_b");
            var c = (UnityEngine.Component)Get(demo, "m_c");
            var d = (UnityEngine.Component)Get(demo, "m_d");
            var n7 = (UnityEngine.Component)Get(demo, "m_n7");
            var rootCanvas = _container.GetComponentInParent<Canvas>();
            var raycaster = rootCanvas.GetComponent<GraphicRaycaster>();

            a.gameObject.AddComponent<NanamiUI.Draggable>();

            var bIcon = (Sprite)b.GetType().GetProperty("Icon").GetValue(b);
            var bDrag = b.gameObject.AddComponent<NanamiUI.Draggable>();
            bDrag.onDragStart = e =>
            {
                NanamiUI.DragDropManager.inst.StartDrag(rootCanvas, raycaster, bIcon, bIcon, e);
                return true; // PreventDefault：b 本体不动，改拖 agent
            };

            c.GetType().GetProperty("Icon").SetValue(c, null);
            c.gameObject.AddComponent<NanamiUI.DropTarget>().onDrop = data => c.GetType().GetProperty("Icon").SetValue(c, (Sprite)data);

            var dDrag = d.gameObject.AddComponent<NanamiUI.Draggable>();
            var n7Rt = (RectTransform)n7.transform;
            dDrag.dragBounds = new Rect(n7Rt.anchoredPosition.x, -n7Rt.anchoredPosition.y, n7Rt.rect.width, n7Rt.rect.height);
        }

        // 复刻 FairyGUI BasicsMain.PlayGraph：把静态烘焙的图形在运行时改成扇形/带贴图梯形/三条折线，
        // 其中 line 的 fillEnd 5 秒线性扫入（设置逻辑抽到 GraphDemo，与截图对比工具共用）。
        private void PlayGraph(GameObject go) => StartCoroutine(FillTween(GraphDemo.Setup(go, changeSprite)));

        private static System.Collections.IEnumerator FillTween(Line line)
        {
            var t = 0f;
            while (t < 5f && line)
            {
                t += Time.deltaTime;
                line.fillEnd = Mathf.Clamp01(t / 5f);
                line.SetVerticesDirty();
                yield return null;
            }
            if (line)
            {
                line.fillEnd = 1;
                line.SetVerticesDirty();
            }
        }

        private GameObject Prefab(string name)
        {
            for (var i = 0; i < demoNames.Length; i++)
                if (demoNames[i] == name)
                    return demoPrefabs[i];
            return null;
        }

        private GameObject Placeholder(string name)
        {
            var go = new GameObject("Demo_" + name, typeof(RectTransform), typeof(TextField));
            go.transform.SetParent(_container, false);
            var text = go.GetComponent<TextField>();
            text.text = "Not implemented: " + name;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1136, 570);
            return go;
        }

        private void SetPage(string page)
        {
            var field = _mainType.GetField("m_c1");
            var controller = field.GetValue(_main);
            var property = controller.GetType().GetProperty("page");
            property.SetValue(controller, Enum.Parse(property.PropertyType, page));
            field.SetValue(_main, controller);
        }

        private void SetBackVisible(bool visible) =>
            ((UnityEngine.Component)Get("m_btn_Back")).gameObject.SetActive(visible);

        private void Bind(string field, UnityAction action)
        {
            BindButton(Get(field), action);
        }

        private object Get(string field) => _mainType.GetField(field).GetValue(_main);

        private static object Get(object owner, string field) => owner.GetType().GetField(field).GetValue(owner);

        private static void BindButton(object button, UnityAction action)
        {
            var onClick = (UnityEvent)button.GetType().GetField("onClick").GetValue(button);
            onClick.AddListener(action);
        }

        private static void Place(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
