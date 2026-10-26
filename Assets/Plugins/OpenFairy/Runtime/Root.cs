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
    // PopupInputModule checks outside presses before native event dispatch without consuming the click.
    public sealed partial class Root : UIBehaviour
    {
        private static Root _inst;
        public static Root inst => _inst;
        [SerializeField] private AudioSource _soundSource;
        [SerializeField] private RootSizeBinding _sizeBinding;
        public AudioSource soundSource => _soundSource;

        public RectTransform rect;
        private readonly List<Window> _windows = new();
        private GameObject _modalLayer;
        private Label _tooltipTemplate;
        private Label _tooltipView;
        private Tooltip _tooltipOwner;
        private PointerEventData _tooltipPointer;
        private Vector2 _tooltipPosition;
        private string _tooltipText;
        private float _tooltipTime;
        private bool _tooltipPending;

        public Color modalColor = new(0, 0, 0, 0.4f);

        public Vector2 size => rect.rect.size;

        protected override void Awake()
        {
            base.Awake();
            _inst = this;
        }

        // 把覆盖层建到 designRoot 所在画布上、与 designRoot 同坐标区，自带高 sortingOrder 的
        // Canvas + GraphicRaycaster 渲染在页面之上并复用场景 EventSystem。
        // 传 canvas 根（如 ComboBox 自举）时覆盖层挂其下、铺满其 rect；传设计根节点（胶水/测试）时挂到其父、与其同区。
        public static Root Create(RectTransform designRoot)
        {
            if (_inst != null)
                return _inst;
            var go = new GameObject("Root", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(AudioSource), typeof(LayoutElement));
            go.layer = designRoot.gameObject.layer;
            var rt = (RectTransform)go.transform;
            var isCanvas = designRoot.GetComponent<Canvas>() != null;
            rt.SetParent(isCanvas ? designRoot : designRoot.parent, false);
            rt.pivot = new Vector2(0, 1);
            rt.localScale = Vector3.one;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var canvas = go.GetComponent<Canvas>();
            if (designRoot.GetComponentInParent<Canvas>() is { } parentCanvas)
                canvas.vertexColorAlwaysGammaSpace = parentCanvas.rootCanvas.vertexColorAlwaysGammaSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10000;
            _inst = go.AddComponent<Root>();
            _inst.rect = rt;
            _inst._soundSource = go.GetComponent<AudioSource>();
            _inst._soundSource.playOnAwake = false;
            if (isCanvas)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                if (!designRoot.TryGetComponent(out RootSizeBinding binding))
                    binding = designRoot.gameObject.AddComponent<RootSizeBinding>();
                _inst._sizeBinding = binding;
                binding.Bind(_inst);
            }
            return _inst;
        }

        // 烘焙 prefab 无需业务胶水：首次需要覆盖层时建到所在画布根上（复刻 GRoot.inst 惰性自建）。
        internal static Root Ensure(UnityEngine.Component context) =>
            _inst != null ? _inst : Create((RectTransform)context.GetComponentInParent<Canvas>().rootCanvas.transform);

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
            return new Vector2(local.x - rect.rect.xMin, rect.rect.yMax - local.y);
        }

        // Positions use the same top-left design coordinates as popups.
        public void ShowTooltip(Label template, string message, Vector2 position, float delay = .1f)
        {
            HideTooltip();
            if (template == null || string.IsNullOrEmpty(message))
                return;
            if (_tooltipTemplate != template)
            {
                if (_tooltipView != null)
                    Destroy(_tooltipView.gameObject);
                _tooltipTemplate = template;
                _tooltipView = Instantiate(template, rect);
                var group = _tooltipView.GetComponent<CanvasGroup>();
                if (group == null) group = _tooltipView.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                foreach (var graphic in _tooltipView.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                _tooltipView.gameObject.SetActive(false);
            }
            _tooltipText = message;
            _tooltipPosition = position;
            _tooltipTime = Time.unscaledTime + delay;
            _tooltipPending = true;
            if (delay <= 0) PresentTooltip();
        }

        internal void ShowTooltip(Tooltip owner, PointerEventData pointer)
        {
            ShowTooltip(owner.template, owner.text, TooltipPosition(pointer), owner.delay);
            _tooltipOwner = owner;
            _tooltipPointer = pointer;
        }

        private Vector2 TooltipPosition(PointerEventData pointer) =>
            ScreenToDesign(pointer.position + new Vector2(10, -20), pointer.enterEventCamera);

        internal void RefreshTooltip(Tooltip owner)
        {
            if (_tooltipOwner != owner) return;
            if (string.IsNullOrEmpty(owner.text)) { HideTooltip(); return; }
            _tooltipText = owner.text;
            if (!_tooltipPending) PresentTooltip();
        }

        internal void HideTooltip(Tooltip owner)
        {
            if (_tooltipOwner == owner) HideTooltip();
        }

        public void HideTooltip()
        {
            _tooltipPending = false;
            _tooltipOwner = null;
            _tooltipPointer = null;
            if (_tooltipView != null) _tooltipView.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Observe the existing event data without adding a pointer-down handler
            // that would intercept a parent button or scroll view's input.
            if (_tooltipPointer != null && (_tooltipPointer.eligibleForClick || _tooltipPointer.pointerPress != null || _tooltipPointer.dragging))
                HideTooltip();
            if (_tooltipPending && Time.unscaledTime >= _tooltipTime)
            {
                if (_tooltipPointer != null) _tooltipPosition = TooltipPosition(_tooltipPointer);
                PresentTooltip();
            }
        }

        private void PresentTooltip()
        {
            _tooltipPending = false;
            _tooltipView.gameObject.SetActive(true);
            _tooltipView.transform.SetAsLastSibling();
            _tooltipView.title = _tooltipText;
            _tooltipView.titleText.RefreshText();
            var rt = (RectTransform)_tooltipView.transform;
            // A Label is not a layout controller. Rebuild the title's own layout
            // root rather than asking LayoutRebuilder to traverse an unrelated root.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipView.titleText.rectTransform);
            foreach (var relation in _tooltipView.GetComponentsInChildren<Relation>(true))
                relation.Sync();
            var position = _tooltipPosition;
            if (position.x + rt.rect.width > size.x) position.x -= rt.rect.width;
            if (position.y + rt.rect.height > size.y) position.y -= rt.rect.height + 1;
            position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, size.x - rt.rect.width));
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, size.y - rt.rect.height));
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(Mathf.Round(position.x), -Mathf.Round(position.y));
        }

        protected override void OnDisable()
        {
            HideTooltip();
            CancelPopups();
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            if (_sizeBinding != null) _sizeBinding.Unbind(this);
            if (_inst == this) _inst = null;
            base.OnDestroy();
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
            var local = rect.InverseTransformPoint(_corners[1]); // Root local coordinates; convert from its native pivot to the top-left origin
            return new Vector2(local.x - rect.rect.xMin, rect.rect.yMax - local.y);
        }

        public bool hasModalWindow => TopModalWindow() != null;

        // 按显示序（兄弟序）从上往下找最上层激活的模态窗（复刻 GRoot.AdjustModalLayer 的扫描方向）。
        private Window TopModalWindow()
        {
            Window top = null;
            foreach (var window in _windows)
                if (window.modal && window.gameObject.activeSelf &&
                    (top == null || window.contentPane.GetSiblingIndex() > top.contentPane.GetSiblingIndex()))
                    top = window;
            return top;
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

}
