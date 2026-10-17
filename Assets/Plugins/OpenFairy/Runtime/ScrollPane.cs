using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public sealed class ScrollPane : ScrollRect, IPointerEnterHandler, IPointerExitHandler
    {
        public bool mouseWheelEnabled = true;
        public bool touchEffect = true;
        public bool snapToItem;
        public bool pageMode;
        public ListSource source;
        public Component pageOwner;
        public int pageController = -1;
        public int pageCount;
        public CanvasGroup[] autoHideBars = System.Array.Empty<CanvasGroup>();
        public UnityEvent onScroll = new();

        private bool _loop, _dragging, _updating, _notifying, _autoAxes, _needsSnap;
        private Vector2? _explicitSize;
        private Vector2 _previousPosition, _notifiedPosition, _notifiedSize, _notifiedView;
        private Vector2 _scrollStart, _scrollTarget;
        private Tween _scrollTween;
        private bool _hovered, _barDragging;
        private MovementType _boundedMovement = MovementType.Elastic;

        public float scrollStep { get => scrollSensitivity / 2; set => scrollSensitivity = value * 2; }
        public bool bounceEffect
        {
            get => movementType == MovementType.Elastic;
            set
            {
                _boundedMovement = value ? MovementType.Elastic : MovementType.Clamped;
                movementType = _loop ? MovementType.Unrestricted : _boundedMovement;
            }
        }
        internal bool loopHorizontal
        {
            get => _loop;
            set
            {
                if (_loop == value) return;
                if (value) _boundedMovement = movementType;
                _loop = value;
                if (value)
                {
                    horizontal = true;
                    vertical = false;
                    movementType = MovementType.Unrestricted;
                }
                else movementType = _boundedMovement;
            }
        }

        public Vector2 viewportSize => viewport.rect.size;
        public Vector2 contentSize => content.rect.size;
        public Vector2 maxScroll => Vector2.Max(Vector2.zero, contentSize - viewportSize);
        public Vector2 position => new(-content.anchoredPosition.x, content.anchoredPosition.y);
        public float percX { get => maxScroll.x > 0 ? Mathf.Clamp01(position.x / maxScroll.x) : 0; set => SetPosition(new(value * maxScroll.x, position.y)); }
        public float percY { get => maxScroll.y > 0 ? Mathf.Clamp01(position.y / maxScroll.y) : 0; set => SetPosition(new(position.x, value * maxScroll.y)); }
        internal bool updatingPage => _notifying;

        public void OnPointerEnter(PointerEventData data) { _hovered = true; ShowBars(); }
        public void OnPointerExit(PointerEventData data) { _hovered = false; ShowBars(); }
        internal void SetBarDragging(bool dragging)
        {
            if (_barDragging && !dragging) _needsSnap = snapToItem || pageMode;
            _barDragging = dragging;
            ShowBars();
        }
        private void ShowBars()
        {
            var show = _hovered || _dragging || _barDragging;
            foreach (var group in autoHideBars)
            {
                group.alpha = show ? 1 : 0;
                group.interactable = group.blocksRaycasts = show;
            }
        }

        // Only programmatically assembled views need this path. Imported views are fully baked.
        public static ScrollPane Attach(RectTransform root)
        {
            if (root.TryGetComponent<ScrollPane>(out var existing)) return existing;
            if (root.Find("viewport") is not RectTransform viewport || !viewport.TryGetComponent<RectMask2D>(out _)) return null;
            var content = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            while (viewport.GetChild(0) != content) viewport.GetChild(0).SetParent(content, false);
            if (!viewport.TryGetComponent<Graphic>(out _)) viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var pane = root.gameObject.AddComponent<ScrollPane>();
            pane.viewport = viewport;
            pane.content = content;
            pane._autoAxes = true;
            pane.scrollStep = 25;
            pane.decelerationRate = Mathf.Pow(.967f, 60);
            if (root.TryGetComponent<ListSource>(out var source))
            {
                pane.source = source;
                source.content = content;
                source.scrollPane = pane;
            }
            pane.RefreshContent();
            return pane;
        }

        public void SetContentSize(Vector2 size)
        {
            _explicitSize = size;
            RefreshContent();
        }

        public void RefreshContent()
        {
            var size = Vector2.zero;
            if (_explicitSize is { } explicitSize) size = explicitSize;
            else
                foreach (RectTransform child in content)
                {
                    var corner = Relation.TopLeft(child);
                    size = Vector2.Max(size, new(corner.x + child.rect.width, child.rect.height - corner.y));
                }
            content.sizeDelta = size;
            if (_autoAxes && !_loop)
            {
                horizontal = size.x > viewportSize.x;
                vertical = size.y > viewportSize.y;
            }
            UpdateBounds();
        }

        public void SetPosition(Vector2 value, bool animated = false)
        {
            StopMovement();
            if (!_loop) value.x = Mathf.Clamp(value.x, 0, maxScroll.x);
            value.y = Mathf.Clamp(value.y, 0, maxScroll.y);
            var target = new Vector2(-value.x, value.y);
            if (animated && Application.isPlaying)
            {
                _scrollStart = content.anchoredPosition;
                _scrollTarget = target;
                var progress = 0f;
                _scrollTween = DOTween.To(() => progress, t => {
                    progress = t;
                    MoveTo(Vector2.LerpUnclamped(_scrollStart, _scrollTarget, t));
                }, 1, .3f).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() => _scrollTween = null);
            }
            else
            {
                MoveTo(target);
            }
        }

        private void MoveTo(Vector2 value)
        {
            content.anchoredPosition = value;
            UpdateBounds();
            Notify();
        }

        internal void Rebase(Vector2 shift)
        {
            var delta = new Vector2(shift.x, -shift.y);
            var current = content.anchoredPosition + delta;
            m_ContentStartPosition += delta;
            _scrollStart += delta;
            _scrollTarget += delta;
            // Refresh the native velocity baseline as well as the drag origin.
            // Outside its LateUpdate, retain the previous sample rather than sampling this frame's drag early.
            content.anchoredPosition = _updating ? current : _previousPosition + delta;
            UpdateBounds();
            UpdatePrevData();
            content.anchoredPosition = current;
            UpdateBounds();
            _previousPosition += delta;
        }

        public void ScrollToView(RectTransform target)
        {
            RefreshContent();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, target);
            var start = new Vector2(bounds.min.x - content.rect.xMin, content.rect.yMax - bounds.max.y);
            var value = position;
            for (var axis = 0; axis < 2; axis++)
                if (start[axis] < value[axis]) value[axis] = start[axis];
                else if (start[axis] + bounds.size[axis] > value[axis] + viewportSize[axis])
                    value[axis] = start[axis] + bounds.size[axis] - viewportSize[axis];
            SetPosition(value);
        }

        internal void Step(bool alongX, bool forward, float steps)
        {
            var value = position;
            var axis = alongX ? 0 : 1;
            value[axis] += (forward ? 1 : -1) * steps * (pageMode ? viewportSize[axis] : scrollStep);
            SetPosition(value);
            _needsSnap = snapToItem || pageMode;
        }

        public void SetPage(int page, bool animated = false)
        {
            var value = position;
            var axis = horizontal ? 0 : 1;
            value[axis] = page * viewportSize[axis];
            SetPosition(value, animated);
        }

        public override void StopMovement()
        {
            base.StopMovement();
            _scrollTween?.Kill();
            _scrollTween = null;
            _needsSnap = false;
        }

        public override void OnBeginDrag(PointerEventData data)
        {
            if (!touchEffect || data.button != PointerEventData.InputButton.Left || !IsActive()) return;
            StopMovement();
            RefreshContent();
            _dragging = true;
            ShowBars();
            base.OnBeginDrag(data);
        }

        public override void OnInitializePotentialDrag(PointerEventData data) { if (touchEffect) base.OnInitializePotentialDrag(data); }

        public override void OnDrag(PointerEventData data) { if (touchEffect) base.OnDrag(data); }
        public override void OnEndDrag(PointerEventData data)
        {
            if (!_dragging || data.button != PointerEventData.InputButton.Left) return;
            base.OnEndDrag(data);
            _dragging = false;
            ShowBars();
            _needsSnap = snapToItem || pageMode;
        }
        public override void OnScroll(PointerEventData data)
        {
            if (!mouseWheelEnabled) return;
            StopMovement();
            RefreshContent();
            base.OnScroll(data);
            Notify();
            _needsSnap = snapToItem || pageMode;
        }

        protected override void SetNormalizedPosition(float value, int axis)
        {
            StopMovement();
            base.SetNormalizedPosition(value, axis);
            Notify();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            onValueChanged.AddListener(OnChanged);
            _previousPosition = content != null ? content.anchoredPosition : Vector2.zero;
            ShowBars();
        }
        protected override void OnDisable()
        {
            onValueChanged.RemoveListener(OnChanged);
            StopMovement();
            _dragging = false;
            _hovered = _barDragging = false;
            ShowBars();
            base.OnDisable();
        }

        protected override void LateUpdate()
        {
            if (content == null) return;
            _updating = true;
            try { base.LateUpdate(); }
            finally { _updating = false; }
            _previousPosition = content.anchoredPosition;
            if (!_needsSnap || _dragging || velocity.sqrMagnitude > 1) return;
            var value = position;
            if (!_loop && (value.x < -.01f || value.x > maxScroll.x + .01f)
                || value.y < -.01f || value.y > maxScroll.y + .01f) return;
            if (pageMode)
            {
                var axis = horizontal ? 0 : 1;
                value[axis] = Mathf.Round(value[axis] / viewportSize[axis]) * viewportSize[axis];
            }
            else if (source != null) value = source.SnapPosition(value);
            SetPosition(value, true);
        }

        private void OnChanged(Vector2 _) => Notify();
        private void Notify()
        {
            if (_notifying || content == null) return;
            if (content.anchoredPosition == _notifiedPosition && contentSize == _notifiedSize && viewportSize == _notifiedView) return;
            _notifying = true;
            try
            {
                onScroll.Invoke();
                if (Application.isPlaying && pageOwner != null && !pageOwner.IsControllerChanging(pageController))
                {
                    var pagePosition = _scrollTween != null ? new Vector2(-_scrollTarget.x, _scrollTarget.y) : position;
                    pageOwner.SetControllerPage(pageController, Mathf.Clamp(Mathf.RoundToInt((horizontal ? pagePosition.x : pagePosition.y) / (horizontal ? viewportSize.x : viewportSize.y)), 0, pageCount - 1));
                }
            }
            finally
            {
                _notifiedPosition = content.anchoredPosition;
                _notifiedSize = contentSize;
                _notifiedView = viewportSize;
                _notifying = false;
            }
        }
    }
}
