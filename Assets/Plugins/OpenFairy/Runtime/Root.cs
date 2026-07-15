using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZLinq;

namespace OpenFairy.UGUI
{
    public enum PopupDirection
    {
        Auto,
        Up,
        Down,
    }

    // 顶层覆盖层 host（复刻 FairyGUI GRoot）：承载 window 与 popup，渲染在页面之上。设计坐标系（左上原点、y 向下），
    // 与页面共用；popup 定位复刻 Root.GetPoupPosition（down/flip-up/right-align）。
    // 外点关闭用一个透明 blocker（IPointerClickHandler）而非输入轮询——本工程用新 Input System，读旧版 UnityEngine.Input 会抛异常。
    public sealed class Root : MonoBehaviour
    {
        private static Root _inst;
        public static Root inst => _inst;

        public RectTransform rect;
        private readonly List<RectTransform> _popups = new();
        private readonly Dictionary<RectTransform, AutoResetUniTaskCompletionSource> _popupClosed = new();
        private readonly List<Window> _windows = new();
        private GameObject _blocker;
        private GameObject _modalLayer;

        public Color modalColor = new(0, 0, 0, 0.4f);

        public Vector2 size => rect.rect.size;

        // 把覆盖层建到 designRoot 所在画布上、与 designRoot 同坐标区，自带高 sortingOrder 的
        // Canvas + GraphicRaycaster 渲染在页面之上并复用场景 EventSystem。
        // 传 canvas 根（如 ComboBox 自举）时覆盖层挂其下、铺满其 rect；传设计根节点（胶水/测试）时挂到其父、与其同区。
        public static Root Create(RectTransform designRoot)
        {
            if (_inst != null)
                return _inst;
            var go = new GameObject("Root", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rt = (RectTransform)go.transform;
            var isCanvas = designRoot.GetComponent<Canvas>() != null;
            rt.SetParent(isCanvas ? designRoot : designRoot.parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = designRoot.rect.size;
            // canvas 根的 anchoredPosition 是屏幕派生值，覆盖层须归零贴其左上；设计根则与其同位。
            rt.anchoredPosition = isCanvas ? Vector2.zero : designRoot.anchoredPosition;
            rt.localScale = Vector3.one;
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
            _inst = go.AddComponent<Root>();
            _inst.rect = rt;
            return _inst;
        }

        // 对象在 Root 内居中（复刻 GObject.Center on GRoot）。
        public void Center(RectTransform obj)
        {
            obj.SetParent(rect, false);
            obj.anchorMin = obj.anchorMax = obj.pivot = new Vector2(0, 1);
            var s = obj.rect.size;
            var r = size;
            obj.anchoredPosition = new Vector2(Mathf.Round((r.x - s.x) / 2), -Mathf.Round((r.y - s.y) / 2));
        }

        public bool hasAnyPopup => _popups.Count > 0;

        public int activeWindowCount =>
            _windows.AsValueEnumerable().Count(w => w.contentPane != null && w.contentPane.gameObject.activeSelf);

        public UniTask ShowPopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            if (_popups.Contains(popup))
                HidePopup(popup); // 去重：已在栈里则先收起该 popup 以上
            popup.SetParent(rect, false);
            popup.gameObject.SetActive(true);
            EnsureBlocker();
            _blocker.SetActive(true);
            _blocker.transform.SetAsLastSibling();
            popup.SetAsLastSibling(); // popup 在 blocker 之上
            _popups.Add(popup);
            var closed = AutoResetUniTaskCompletionSource.Create();
            _popupClosed.Add(popup, closed);

            var pos = GetPopupPosition(popup, target, dir); // 设计坐标（y-down）
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0, 1);
            popup.anchoredPosition = new Vector2(pos.x, -pos.y);
            return closed.Task;
        }

        // 在指定设计坐标（y-down、左上原点）弹出：先把 popup 摆到该点，再走通用定位（含屏内翻转），
        // 复刻 FairyGUI target==null 时用 touchPosition 定位——供右键上下文菜单在指针处弹出。
        public UniTask ShowPopupAt(RectTransform popup, Vector2 designPos, PopupDirection dir = PopupDirection.Down)
        {
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0, 1);
            popup.anchoredPosition = new Vector2(designPos.x, -designPos.y);
            return ShowPopup(popup, null, dir);
        }

        // 屏幕点 → Root 设计坐标（y-down）。用于把指针位置传给 ShowPopupAt。
        public Vector2 ScreenToDesign(Vector2 screen, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out var local);
            return new Vector2(local.x, -local.y);
        }

        public UniTask TogglePopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            if (_popups.Contains(popup))
            {
                HidePopup(popup);
                return UniTask.CompletedTask;
            }
            return ShowPopup(popup, target, dir);
        }

        // popup==null：收起全部；否则收起该 popup 及其上层。
        public void HidePopup(RectTransform popup = null)
        {
            var from = popup == null ? 0 : _popups.IndexOf(popup);
            if (from < 0)
                return;
            for (var i = _popups.Count - 1; i >= from; i--)
            {
                var current = _popups[i];
                current.gameObject.SetActive(false);
                _popups.RemoveAt(i);
                if (_popups.Count == 0 && _blocker != null)
                    _blocker.SetActive(false);
                if (_popupClosed.Remove(current, out var closed))
                    closed.TrySetResult();
            }
        }

        private void EnsureBlocker()
        {
            if (_blocker != null)
                return;
            _blocker = new GameObject("PopupBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(PopupBlocker));
            var brt = (RectTransform)_blocker.transform;
            brt.SetParent(rect, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var img = _blocker.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0, 0, 0, 0); // 透明但可接收射线
            _blocker.SetActive(false);
        }

        // 复刻 Root.GetPoupPosition（FairyGUI 设计坐标、左上 y-down）。
        private Vector2 GetPopupPosition(RectTransform popup, RectTransform target, PopupDirection dir)
        {
            var popupSize = popup.rect.size;
            var root = size;
            Vector2 pos, targetSize;
            if (target != null)
            {
                pos = RootTopLeft(target);
                targetSize = target.rect.size;
            }
            else
            {
                pos = RootTopLeft(popup); // 无 target：用 popup 当前位置作起点（demo 右键在指针处，见 glue 直接定位）
                targetSize = Vector2.zero;
            }

            var xx = pos.x;
            if (xx + popupSize.x > root.x)
                xx = xx + targetSize.x - popupSize.x;
            var yy = pos.y + targetSize.y;
            if ((dir == PopupDirection.Auto && yy + popupSize.y > root.y) || dir == PopupDirection.Up)
            {
                yy = pos.y - popupSize.y - 1;
                if (yy < 0)
                {
                    yy = 0;
                    xx += targetSize.x / 2;
                }
            }
            return new Vector2(Mathf.Round(xx), Mathf.Round(yy));
        }

        private readonly Vector3[] _corners = new Vector3[4];

        // 节点左上角在 Root 设计坐标（y-down）里的位置。
        public Vector2 RootTopLeft(RectTransform node)
        {
            node.GetWorldCorners(_corners); // [1] = top-left（world）
            var local = rect.InverseTransformPoint(_corners[1]); // Root 局部（pivot 0,1 → 原点在左上、y 向上）
            return new Vector2(local.x, -local.y);
        }

        // ---- Window 托管（复刻 Root.ShowWindow/HideWindowImmediately/BringToFront）----
        public void ShowWindow(Window win)
        {
            if (!_windows.Contains(win))
                _windows.Add(win);
            win.EnsureInited(rect);
            win.contentPane.SetParent(rect, false);
            win.contentPane.SetAsLastSibling();
            // 复刻 GRoot.ShowWindow：已有模态层时，非模态窗插到模态层之下（不越过模态窗）。
            if (!win.modal && _modalLayer != null && _modalLayer.activeSelf)
                win.contentPane.SetSiblingIndex(_modalLayer.transform.GetSiblingIndex());
            win.contentPane.gameObject.SetActive(true);
            win.DoShow();
            RefreshModal();
        }

        public void HideWindowImmediately(Window win)
        {
            _windows.Remove(win);
            if (win.contentPane != null)
                win.contentPane.gameObject.SetActive(false);
            RefreshModal();
        }

        public void BringToFront(Window win)
        {
            if (win.contentPane != null)
                win.contentPane.SetAsLastSibling();
            RefreshModal();
        }

        public bool hasModalWindow => TopModalWindow() != null;

        // 按显示序（兄弟序）从上往下找最上层激活的模态窗（复刻 GRoot.AdjustModalLayer 的扫描方向）。
        private Window TopModalWindow()
        {
            for (var i = rect.childCount - 1; i >= 0; i--)
            {
                var child = rect.GetChild(i);
                foreach (var w in _windows)
                    if (w.modal && w.contentPane == child && child.gameObject.activeSelf)
                        return w;
            }
            return null;
        }

        // 复刻 GRoot.AdjustModalLayer：模态层插到最上层模态窗之下，不改变窗口自身顺序；无模态窗则收起。
        private void RefreshModal()
        {
            var top = TopModalWindow();
            if (top == null)
            {
                if (_modalLayer != null)
                    _modalLayer.SetActive(false);
                return;
            }
            if (_modalLayer == null)
            {
                _modalLayer = new GameObject("ModalLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                var mrt = (RectTransform)_modalLayer.transform;
                mrt.SetParent(rect, false);
                mrt.anchorMin = Vector2.zero;
                mrt.anchorMax = Vector2.one;
                mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            }
            _modalLayer.GetComponent<UnityEngine.UI.Image>().color = modalColor; // 可接收射线，拦截下层
            _modalLayer.SetActive(true);
            _modalLayer.transform.SetAsLastSibling(); // 先移到末尾，再插到模态窗位（窗口序号不受移除影响）
            _modalLayer.transform.SetSiblingIndex(top.contentPane.GetSiblingIndex());
        }
    }

    // 透明全屏 blocker：点它（= 点在 popup 之外）即收起全部 popup（复刻 FairyGUI 外点关闭，避开新 Input System）。
    public sealed class PopupBlocker : UIBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => Root.inst.HidePopup();
    }
}
