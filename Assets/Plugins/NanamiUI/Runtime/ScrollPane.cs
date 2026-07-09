using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    // 运行时滚动（复刻 FairyGUI ScrollPane 的交互部分）：把 viewport（RectMask2D）里的内容包进一个 content 容器，
    // 拖动/滚轮/拖动滚动条/点轨道翻页时在内容边界内平移 content，越界时橡皮筋回弹，松手带惯性衰减。
    // Migrate 给 overflow=scroll 的根挂 ScrollPaneHost，运行时自挂本组件——转换后的滚动组件无需业务胶水即可滚动。
    public sealed class ScrollPane : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float Deceleration = 0.967f;   // 每 60fps 帧的速度衰减（同 FairyGUI）
        private const float SpringBack = 0.0333f;     // 越界回弹时间常数
        private const float WheelStep = 40f;          // 单档滚轮位移
        private const float BounceMargin = 0.25f;     // 越界拖动阻尼
        private const float MinGrip = 20f;            // grip 最短像素长（避免内容极长时 grip 消失）
        internal const string HitName = "__scrollHit"; // 透明射线面保留名（FairyGUI 不产出双下划线名，不会撞名）

        public bool bounceEffect = true;
        public bool mouseWheelEnabled = true;
        public UnityEvent onScroll = new();

        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _vtBar, _vtGrip, _hzBar, _hzGrip;
        private Vector2 _contentSize, _viewSize;
        private Vector2 _startContent, _startPointer;
        private Vector2 _velocity;
        private Vector2 _lastPos;
        private bool _dragging;

        // 给已烘焙的滚动根（有名为 "viewport" 的 RectMask2D 子节点）挂上运行时滚动。已挂则直接返回（幂等）。返回 null 表示不是滚动结构。
        public static ScrollPane Attach(RectTransform scrollRoot)
        {
            if (scrollRoot.GetComponent<ScrollPane>() is { } existing)
                return existing;
            var viewportTf = scrollRoot.Find("viewport");
            if (viewportTf == null || viewportTf.GetComponent<RectMask2D>() == null)
                return null;
            var viewport = (RectTransform)viewportTf;

            // 把 viewport 的现有子节点包进 content 容器（原点与 viewport 一致，故 anchoredPosition 不变）。
            // 无 ScrollPane 时（上面的早返回已挡住重入）content 必不存在，直接新建——不按名复用，避免 FairyGUI 元素恰名为
            // "content" 时被误当容器、其余子物体没被移进去。
            var content = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            for (var i = viewport.childCount - 1; i >= 0; i--)
                if (viewport.GetChild(i) != content)
                    viewport.GetChild(i).SetParent(content, false);

            var pane = scrollRoot.gameObject.AddComponent<ScrollPane>();
            pane._viewport = viewport;
            pane._content = content;
            pane._viewSize = viewport.rect.size;

            pane.FindBar(scrollRoot, "ScrollBar_VT", ref pane._vtBar, ref pane._vtGrip);
            pane.FindBar(scrollRoot, "ScrollBar_HZ", ref pane._hzBar, ref pane._hzGrip);
            if (pane._vtGrip != null)
                ScrollBarGrip.Bind(pane._vtGrip, pane, false);
            if (pane._hzGrip != null)
                ScrollBarGrip.Bind(pane._hzGrip, pane, true);
            ScrollBarTrack.Bind(pane._vtBar, pane._vtGrip, pane, false);
            ScrollBarTrack.Bind(pane._hzBar, pane._hzGrip, pane, true);
            pane.RefreshContent();
            return pane;
        }

        private void FindBar(RectTransform root, string namePrefix, ref RectTransform bar, ref RectTransform grip)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.StartsWith(namePrefix))
                {
                    bar = c.Find("bar") as RectTransform;
                    grip = c.Find("grip") as RectTransform;
                    return;
                }
            }
        }

        // 内容边界（y-down）：用 Relation.TopLeft 计入各子物体的 pivot/anchor，故内容用非 (0,1) 轴心也测得准（对齐 Migrate.ContentBounds）。
        private static Vector2 ContentBounds(RectTransform content)
        {
            var bounds = Vector2.zero;
            for (var i = 0; i < content.childCount; i++)
            {
                var rt = (RectTransform)content.GetChild(i);
                if (rt.name == HitName)
                    continue;
                var topLeft = Relation.TopLeft(rt);
                bounds = Vector2.Max(bounds, new Vector2(topLeft.x + rt.rect.width, rt.rect.height - topLeft.y));
            }
            return bounds;
        }

        private Vector2 MaxScroll => new(Mathf.Max(0, _contentSize.x - _viewSize.x), Mathf.Max(0, _contentSize.y - _viewSize.y));

        private static Vector2 ClampToBounds(Vector2 pos, Vector2 max) => new(Mathf.Clamp(pos.x, -max.x, 0), Mathf.Clamp(pos.y, 0, max.y));

        public void RefreshContent()
        {
            _contentSize = ContentBounds(_content);
            _content.sizeDelta = _contentSize;
            EnsureHit();
            UpdateGrips();
        }

        private void EnsureHit()
        {
            var hit = _content.Find(HitName) as RectTransform;
            if (hit == null)
            {
                var go = new GameObject(HitName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                hit = (RectTransform)go.transform;
                hit.SetParent(_content, false);
                go.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
            }
            hit.anchorMin = hit.anchorMax = hit.pivot = new Vector2(0, 1);
            hit.sizeDelta = Vector2.Max(_contentSize, _viewSize);
            hit.anchoredPosition = Vector2.zero;
            hit.SetAsFirstSibling();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            RefreshContent(); // 内容可能在 Attach 之后被 List.Fill 填充，起拖时重算滚动范围
            _dragging = true;
            _velocity = Vector2.zero;
            _startContent = _content.anchoredPosition;
            _lastPos = _startContent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.pressEventCamera, out _startPointer);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.pressEventCamera, out var local);
            var pos = _startContent + (local - _startPointer);
            var max = MaxScroll;
            if (bounceEffect)
            {
                pos.x = RubberBand(pos.x, -max.x, 0);
                pos.y = RubberBand(pos.y, 0, max.y);
            }
            else
                pos = ClampToBounds(pos, max);
            _velocity = (pos - _lastPos) / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            _lastPos = pos;
            _content.anchoredPosition = pos;
            UpdateGrips();
            onScroll.Invoke();
        }

        public void OnEndDrag(PointerEventData e) => _dragging = false;

        public void OnScroll(PointerEventData e)
        {
            if (!mouseWheelEnabled)
                return;
            RefreshContent(); // 内容可能在 Attach 后被 List.Fill 填充，滚轮前重算范围
            var max = MaxScroll;
            var pos = _content.anchoredPosition;
            if (max.y > 0)
                pos.y = Mathf.Clamp(pos.y - e.scrollDelta.y * WheelStep, 0, max.y);
            else if (max.x > 0)
                pos.x = Mathf.Clamp(pos.x + e.scrollDelta.y * WheelStep, -max.x, 0);
            _content.anchoredPosition = pos;
            _velocity = Vector2.zero;
            UpdateGrips();
            onScroll.Invoke();
        }

        // 拖动滚动条 grip：percent 为沿轨道的比例（0 顶/左），映射到内容位置。
        internal void SetPercent(bool horizontal, float percent)
        {
            RefreshContent();
            var max = MaxScroll;
            var pos = _content.anchoredPosition;
            percent = Mathf.Clamp01(percent);
            if (horizontal)
                pos.x = -percent * max.x;
            else
                pos.y = percent * max.y;
            _content.anchoredPosition = pos;
            _velocity = Vector2.zero;
            UpdateGrips();
            onScroll.Invoke();
        }

        // 点滚动条轨道空白处翻一页（复刻 GScrollBar 轨道点按 ScrollUp/Down(4)）：往点击一侧移动一个 view 高/宽。
        internal void PageScroll(bool horizontal, bool forward)
        {
            RefreshContent();
            var max = MaxScroll;
            var pos = _content.anchoredPosition;
            if (horizontal)
                pos.x = Mathf.Clamp(pos.x + (forward ? -_viewSize.x : _viewSize.x), -max.x, 0);
            else
                pos.y = Mathf.Clamp(pos.y + (forward ? _viewSize.y : -_viewSize.y), 0, max.y);
            _content.anchoredPosition = pos;
            _velocity = Vector2.zero;
            UpdateGrips();
            onScroll.Invoke();
        }

        // 把子节点滚到可见区（复刻 ScrollPane.ScrollToView 的简版）。
        public void ScrollToView(RectTransform target)
        {
            RefreshContent();
            var max = MaxScroll;
            var top = -target.anchoredPosition.y;
            var pos = _content.anchoredPosition;
            if (top < pos.y)
                pos.y = top;
            else if (top + target.rect.height > pos.y + _viewSize.y)
                pos.y = top + target.rect.height - _viewSize.y;
            pos.y = Mathf.Clamp(pos.y, 0, max.y);
            _content.anchoredPosition = pos;
            UpdateGrips();
        }

        private void Update()
        {
            if (_dragging)
                return;
            var pos = _content.anchoredPosition;
            var max = MaxScroll;
            var target = ClampToBounds(pos, max);

            if (pos != target)
            {
                // 越界：弹回边界（无视惯性）。
                pos = Vector2.Lerp(pos, target, Mathf.Clamp01(Time.unscaledDeltaTime / SpringBack));
                if ((pos - target).sqrMagnitude < 0.01f)
                    pos = target;
                _velocity = Vector2.zero;
            }
            else if (_velocity.sqrMagnitude > 1f)
            {
                pos += _velocity * Time.unscaledDeltaTime;
                _velocity *= Mathf.Pow(Deceleration, Time.unscaledDeltaTime * 60f);
                if (!bounceEffect)
                    pos = ClampToBounds(pos, max);
            }
            else
                return;

            _content.anchoredPosition = pos;
            UpdateGrips();
            onScroll.Invoke();
        }

        // 越界橡皮筋：超出 [min,max] 的部分按阻尼压缩。
        private static float RubberBand(float v, float min, float max)
        {
            if (v < min)
                return min - (min - v) * BounceMargin;
            if (v > max)
                return max + (v - max) * BounceMargin;
            return v;
        }

        // grip 长度按 view/content 比例随内容实时缩放（复刻 GScrollBar.SetDisplayPerc），位置按滚动比例。
        private void UpdateGrips()
        {
            UpdateGrip(_vtBar, _vtGrip, false);
            UpdateGrip(_hzBar, _hzGrip, true);
        }

        private void UpdateGrip(RectTransform bar, RectTransform grip, bool horizontal)
        {
            if (grip == null || bar == null)
                return;
            var barLen = horizontal ? bar.rect.width : bar.rect.height;
            var view = horizontal ? _viewSize.x : _viewSize.y;
            var content = horizontal ? _contentSize.x : _contentSize.y;
            if (content <= view)
                return; // 不可滚动：保留烘焙的 grip（本工程滚动条常显）
            var gripLen = Mathf.Max(MinGrip, Mathf.FloorToInt(Mathf.Min(1, view / Mathf.Max(content, 1)) * barLen));
            var percent = horizontal
                ? Mathf.Clamp01(-_content.anchoredPosition.x / (content - view))
                : Mathf.Clamp01(_content.anchoredPosition.y / (content - view));
            if (horizontal)
            {
                grip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gripLen);
                grip.anchoredPosition = new Vector2(Relation.TopLeft(bar).x + percent * (barLen - gripLen), grip.anchoredPosition.y);
            }
            else
            {
                grip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gripLen);
                grip.anchoredPosition = new Vector2(grip.anchoredPosition.x, Relation.TopLeft(bar).y - percent * (barLen - gripLen));
            }
        }
    }

    // 挂在滚动条 grip 上：拖动 grip 沿轨道移动，按比例驱动 ScrollPane。
    public sealed class ScrollBarGrip : UIBehaviour, IDragHandler
    {
        private ScrollPane _pane;
        private RectTransform _bar;
        private bool _horizontal;

        internal static void Bind(RectTransform grip, ScrollPane pane, bool horizontal)
        {
            if (grip.GetComponent<Graphic>() == null)
                return; // 无图形的 grip 收不到射线，不接拖动（烘焙的可见 grip 都带 Image）
            var g = grip.gameObject.GetComponent<ScrollBarGrip>() ?? grip.gameObject.AddComponent<ScrollBarGrip>();
            g._pane = pane;
            g._bar = grip.parent.Find("bar") as RectTransform ?? (RectTransform)grip.parent;
            g._horizontal = horizontal;
        }

        public void OnDrag(PointerEventData e)
        {
            var grip = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_bar, e.position, e.pressEventCamera, out var local);
            var percent = _horizontal
                ? (local.x - _bar.rect.xMin - grip.rect.width * 0.5f) / Mathf.Max(1, _bar.rect.width - grip.rect.width)
                : (_bar.rect.yMax - local.y - grip.rect.height * 0.5f) / Mathf.Max(1, _bar.rect.height - grip.rect.height);
            _pane.SetPercent(_horizontal, percent);
        }
    }

    // 挂在滚动条轨道（bar）上：点 grip 之外的轨道空白翻一页（复刻 GScrollBar 轨道点按）。
    public sealed class ScrollBarTrack : UIBehaviour, IPointerDownHandler
    {
        private ScrollPane _pane;
        private RectTransform _grip;
        private bool _horizontal;

        internal static void Bind(RectTransform bar, RectTransform grip, ScrollPane pane, bool horizontal)
        {
            if (bar == null || grip == null || bar.GetComponent<Graphic>() == null)
                return; // 轨道需可收射线（烘焙的 bar 带 Image）
            var t = bar.gameObject.GetComponent<ScrollBarTrack>() ?? bar.gameObject.AddComponent<ScrollBarTrack>();
            t._pane = pane;
            t._grip = grip;
            t._horizontal = horizontal;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_grip, e.position, e.pressEventCamera))
                return; // 点在 grip 上：交给 grip 拖动
            var bar = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bar, e.position, e.pressEventCamera, out var local);
            // grip 与 bar 常是同容器兄弟、坐标系不同（差一个箭头缩进），须换到 bar 本地再比较方向，否则阈值偏移。
            var gripLocal = (Vector2)bar.InverseTransformPoint(_grip.TransformPoint(_grip.rect.center));
            var forward = _horizontal ? local.x > gripLocal.x : local.y < gripLocal.y;
            _pane.PageScroll(_horizontal, forward);
        }
    }
}
