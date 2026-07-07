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
            NanamiUI.Text.defaultFont = "Microsoft YaHei";
            _main = Array.Find(GetComponents<NanamiUI.Component>(), component => component.GetType().FullName == "UI.Basics.Main");
            _mainType = _main.GetType();
            _container = ((UnityEngine.Component)Get("m_container")).transform;

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
            if (name == "Button")
                SetupButtonDemo(go);
            else if (name == "Graph")
                PlayGraph(go);
            else if (name == "Depth")
                PlayDepth(go);
            else if (name == "Drag&Drop")
                PlayDragDrop(go);
        }

        // 复刻 FairyGUI BasicsMain.PlayDepth：固定物置 sortingOrder 100 + 可拖；两个按钮各建 order 0(红,靠后)/200(绿,靠前) 矩形。
        private void PlayDepth(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), c => c.GetType().FullName == "UI.Basics.Demo_Depth");
            var container = (NanamiUI.Component)Get(demo, "m_n22");
            var fixedShape = (NanamiUI.Shape)Get(container, "m_n0");
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
            var go = new GameObject("Demo_" + name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_container, false);
            var text = go.GetComponent<Text>();
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

        private static void SetupButtonDemo(GameObject go)
        {
            var demo = Array.Find(go.GetComponents<NanamiUI.Component>(), component => component.GetType().FullName == "UI.Basics.Demo_Button");
            BindGroup(demo, "m_RadioGroup", "m_n18", "m_n19", "m_n20");
            BindGroup(demo, "m_tab", "m_n23", "m_n24", "m_n25");
        }

        private static void BindGroup(NanamiUI.Component owner, string controllerField, params string[] buttonFields)
        {
            SetPage(owner, controllerField, "_0");
            for (var i = 0; i < buttonFields.Length; i++)
            {
                var index = i;
                var button = Get(owner, buttonFields[index]);
                SetSelected(button, index == 0);
                BindButton(button, () =>
                {
                    SetPage(owner, controllerField, "_" + index);
                    for (var j = 0; j < buttonFields.Length; j++)
                        SetSelected(Get(owner, buttonFields[j]), j == index);
                });
            }
        }

        private static object Get(object owner, string field) => owner.GetType().GetField(field).GetValue(owner);

        private static void BindButton(object button, UnityAction action)
        {
            var onClick = (UnityEvent)button.GetType().GetField("onClick").GetValue(button);
            onClick.AddListener(action);
        }

        private static void SetSelected(object button, bool selected)
        {
            button.GetType().GetField("selected").SetValue(button, selected);
            button.GetType().GetMethod("RefreshState").Invoke(button, null);
        }

        private static void SetPage(object owner, string fieldName, string page)
        {
            var field = owner.GetType().GetField(fieldName);
            var controller = field.GetValue(owner);
            var property = controller.GetType().GetProperty("page");
            property.SetValue(controller, Enum.Parse(property.PropertyType, page));
            field.SetValue(owner, controller);
        }

        private static void Place(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
