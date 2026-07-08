using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    public enum PopupDirection
    {
        Auto,
        Up,
        Down,
    }

    // 顶层覆盖层 host（复刻 FairyGUI GRoot）：承载 window 与 popup，渲染在页面之上。设计坐标系（左上原点、y 向下），
    // 与页面共用；popup 定位复刻 GRoot.GetPoupPosition（down/flip-up/right-align）。
    // 外点关闭用一个透明 blocker（IPointerClickHandler）而非输入轮询——本工程用新 Input System，读旧版 UnityEngine.Input 会抛异常。
    public sealed class GRoot : MonoBehaviour
    {
        private static GRoot _inst;
        public static GRoot inst => _inst;

        public RectTransform rect;
        private readonly List<RectTransform> _popups = new();
        private readonly Dictionary<RectTransform, System.Action> _onClose = new();
        private readonly List<Window> _windows = new();
        private GameObject _blocker;

        public Vector2 Size => rect.rect.size;

        // 测试 seam：把覆盖层建到 designRoot 所在画布上、与 designRoot 同坐标区（1136×640 设计尺寸），
        // 自带高 sortingOrder 的 Canvas + GraphicRaycaster 渲染在页面之上并复用场景 EventSystem。
        public static GRoot Create(RectTransform designRoot)
        {
            if (_inst != null)
                return _inst;
            var go = new GameObject("GRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rt = (RectTransform)go.transform;
            rt.SetParent(designRoot.parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = designRoot.rect.size;
            rt.anchoredPosition = designRoot.anchoredPosition;
            rt.localScale = Vector3.one;
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
            _inst = go.AddComponent<GRoot>();
            _inst.rect = rt;
            return _inst;
        }

        // 对象在 GRoot 内居中（复刻 GObject.Center on GRoot）。
        public void Center(RectTransform obj)
        {
            obj.SetParent(rect, false);
            obj.anchorMin = obj.anchorMax = obj.pivot = new Vector2(0, 1);
            var s = obj.rect.size;
            var r = Size;
            obj.anchoredPosition = new Vector2(Mathf.Round((r.x - s.x) / 2), -Mathf.Round((r.y - s.y) / 2));
        }

        public bool HasAnyPopup => _popups.Count > 0;

        public int ActiveWindowCount
        {
            get
            {
                var n = 0;
                foreach (var w in _windows)
                    if (w.Root != null && w.Root.gameObject.activeSelf)
                        n++;
                return n;
            }
        }

        public void ShowPopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto, System.Action onClose = null)
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
            if (onClose != null)
                _onClose[popup] = onClose;

            var pos = GetPopupPosition(popup, target, dir); // 设计坐标（y-down）
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0, 1);
            popup.anchoredPosition = new Vector2(pos.x, -pos.y);
        }

        public void TogglePopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            if (_popups.Contains(popup))
                HidePopup(popup);
            else
                ShowPopup(popup, target, dir);
        }

        // popup==null：收起全部；否则收起该 popup 及其上层。
        public void HidePopup(RectTransform popup = null)
        {
            var from = popup == null ? 0 : _popups.IndexOf(popup);
            if (from < 0)
                return;
            for (var i = _popups.Count - 1; i >= from; i--)
            {
                _popups[i].gameObject.SetActive(false);
                if (_onClose.Remove(_popups[i], out var onClose))
                    onClose();
                _popups.RemoveAt(i);
            }
            if (_popups.Count == 0 && _blocker != null)
                _blocker.SetActive(false);
        }

        private void EnsureBlocker()
        {
            if (_blocker != null)
                return;
            _blocker = new GameObject("PopupBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PopupBlocker));
            var brt = (RectTransform)_blocker.transform;
            brt.SetParent(rect, false);
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            var img = _blocker.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // 透明但可接收射线
            _blocker.SetActive(false);
        }

        // 复刻 GRoot.GetPoupPosition（FairyGUI 设计坐标、左上 y-down）。
        private Vector2 GetPopupPosition(RectTransform popup, RectTransform target, PopupDirection dir)
        {
            var popupSize = popup.rect.size;
            var root = Size;
            Vector2 pos, size;
            if (target != null)
            {
                pos = RootTopLeft(target);
                size = target.rect.size;
            }
            else
            {
                pos = RootTopLeft(popup); // 无 target：用 popup 当前位置作起点（demo 右键在指针处，见 glue 直接定位）
                size = Vector2.zero;
            }

            var xx = pos.x;
            if (xx + popupSize.x > root.x)
                xx = xx + size.x - popupSize.x;
            var yy = pos.y + size.y;
            if ((dir == PopupDirection.Auto && yy + popupSize.y > root.y) || dir == PopupDirection.Up)
            {
                yy = pos.y - popupSize.y - 1;
                if (yy < 0)
                {
                    yy = 0;
                    xx += size.x / 2;
                }
            }
            return new Vector2(Mathf.Round(xx), Mathf.Round(yy));
        }

        private readonly Vector3[] _corners = new Vector3[4];

        // 节点左上角在 GRoot 设计坐标（y-down）里的位置。
        public Vector2 RootTopLeft(RectTransform node)
        {
            node.GetWorldCorners(_corners); // [1] = top-left（world）
            var local = rect.InverseTransformPoint(_corners[1]); // GRoot 局部（pivot 0,1 → 原点在左上、y 向上）
            return new Vector2(local.x, -local.y);
        }

        // ---- Window 托管（复刻 GRoot.ShowWindow/HideWindowImmediately/BringToFront）----
        public void ShowWindow(Window win)
        {
            if (!_windows.Contains(win))
                _windows.Add(win);
            win.EnsureInited(rect);
            win.Root.SetParent(rect, false);
            win.Root.SetAsLastSibling();
            win.Root.gameObject.SetActive(true);
            win.DoShow();
        }

        public void HideWindowImmediately(Window win)
        {
            _windows.Remove(win);
            if (win.Root != null)
                win.Root.gameObject.SetActive(false);
        }

        public void BringToFront(Window win)
        {
            if (win.Root != null)
                win.Root.SetAsLastSibling();
        }
    }

    // 透明全屏 blocker：点它（= 点在 popup 之外）即收起全部 popup（复刻 FairyGUI 外点关闭，避开新 Input System）。
    public sealed class PopupBlocker : UIBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => GRoot.inst.HidePopup();
    }
}
