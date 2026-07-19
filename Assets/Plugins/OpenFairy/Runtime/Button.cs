using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    public enum ButtonMode
    {
        Common,
        Check,
        Radio,
    }

    // 非泛型按钮基类（复刻 FairyGUI GButton 面）：让 Root/Window/PopupMenu/Migrate 不必知道 T 就能设标题/图标/选中态。
    // 关联控制器（FairyGUI relatedController/relatedPageId）由 Migrate 从 <Button controller=.. page=..> 烘焙；
    // 换页→选中态的反向同步由烘焙进控制器的 GearButton 完成（复刻 GButton.HandleControllerChanged）。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick = new();
        public UnityEvent onChanged = new(); // 复刻 GButton.onChanged：仅用户点击翻转 Check/Radio 时发

        public ButtonMode mode;
        public string selectedTitle;
        public bool changeStateOnClick = true; // 复刻 GButton：列表项交给 ListSelection 管选择时置 false，禁本体自翻 selected

        // 烘焙接线（Migrate 写入），不属用户 API 面。
        [SerializeField] internal TextField titleText;
        [SerializeField] internal Loader iconLoader;
        [SerializeField] internal Component relatedOwner;
        [SerializeField] internal int relatedController = -1;
        [SerializeField] internal int relatedPage = -1;
        [SerializeField] internal bool _selected; // Migrate 烘焙直写（绕过 setter 的关联控制器逻辑）
        [SerializeField] internal bool _grayed;

        [SerializeField]
        private string _title;

        public string title
        {
            get => _title;
            set
            {
                _title = value;
                RefreshTitle();
            }
        }

        public Sprite icon
        {
            get => iconLoader != null ? iconLoader.sprite : null;
            set
            {
                if (iconLoader == null)
                    return;
                iconLoader.sprite = value;
                iconLoader.enabled = value != null;
            }
        }

        // 复刻 GButton.selected 的 setter：Common 忽略选中态、同值早返回；
        // 选中把关联控制器设到本页，Check 取消且当前正在本页则回对页（2 页控制器的 oppositePageId）。
        public bool selected
        {
            get => _selected;
            set
            {
                if (mode == ButtonMode.Common || _selected == value)
                    return;
                _selected = value;
                RefreshState();
                if (HasRelatedController && Application.isPlaying) // 烘焙期选中态由控制器初始页经 GearButton 驱动，不反向写页
                {
                    if (_selected)
                        ControllerBinding.SetPage(relatedOwner, relatedController, relatedPage);
                    else if (mode == ButtonMode.Check && ControllerBinding.GetPage(relatedOwner, relatedController) == relatedPage)
                        ControllerBinding.SetPage(relatedOwner, relatedController, relatedPage == 0 ? 1 : 0);
                }
            }
        }

        // 复刻 GButton.grayed（HandleGrayedChanged）：置灰后进 disabled/selectedDisabled 页并拦截交互（守卫在各 handler 里）。
        public bool grayed
        {
            get => _grayed;
            set
            {
                _grayed = value;
                RefreshState();
            }
        }

        internal bool HasRelatedController =>
            relatedOwner != null && relatedController >= 0 && relatedPage >= 0;

        public abstract void RefreshState();

        protected void RefreshTitle()
        {
            if (titleText == null)
                return;
            _title ??= titleText.text;
            titleText.text = selected && !string.IsNullOrEmpty(selectedTitle) ? selectedTitle : _title;
        }
    }

    public abstract class Button<T> : ButtonBase, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler where T : struct, Enum
    {
        protected enum VisualState
        {
            Up,
            Down,
            Over,
            SelectedOver,
            Disabled,
            SelectedDisabled,
        }

        private static class StatePages
        {
            public static readonly T Up;
            public static readonly T Down;
            public static readonly T Over;
            public static readonly T SelectedOver;
            public static readonly T Disabled;
            public static readonly T SelectedDisabled;
            public static readonly bool HasUp;
            public static readonly bool HasDown;
            public static readonly bool HasOver;
            public static readonly bool HasSelectedOver;
            public static readonly bool HasDisabled;
            public static readonly bool HasSelectedDisabled;

            static StatePages()
            {
                HasUp = Enum.TryParse("up", out Up);
                HasDown = Enum.TryParse("down", out Down);
                HasOver = Enum.TryParse("over", out Over);
                HasSelectedOver = Enum.TryParse("selectedOver", out SelectedOver);
                HasDisabled = Enum.TryParse("disabled", out Disabled);
                HasSelectedDisabled = Enum.TryParse("selectedDisabled", out SelectedDisabled);
            }
        }

        public Controller<T> controller;

        private bool _down, _over;

        // grayed 只拦视觉/动作，指针状态照常维护——否则灰显期间移入移出后 _down/_over 残留，恢复时状态画错。
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) // 复刻 GButton.__touchBegin：仅左键
                return;
            _down = true;
            if (!grayed && mode == ButtonMode.Common) // 仅 Common 按下进 down 态，Check/Radio 按住不变
                SetState(VisualState.Down);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            _down = false;
            if (!grayed && mode == ButtonMode.Common)
                RefreshState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            if (!grayed && !_down)
                RefreshState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            if (!grayed && !_down)
                RefreshState();
        }

        // 复刻 GButton.__click：Check/Radio 经 Selected setter 翻选并驱动关联控制器；Common 直接换页。
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || grayed)
                return;
            if (mode == ButtonMode.Check)
            {
                if (changeStateOnClick)
                {
                    selected = !selected;
                    onChanged.Invoke();
                }
            }
            else if (mode == ButtonMode.Radio)
            {
                if (changeStateOnClick && !selected)
                {
                    if (!HasRelatedController)
                        DeselectSiblings(); // 无关联控制器的 radio 组回退：同父兄弟互斥
                    selected = true;
                    onChanged.Invoke();
                }
            }
            else if (HasRelatedController)
                ControllerBinding.SetPage(relatedOwner, relatedController, relatedPage);
            onClick.Invoke();
        }

        private void DeselectSiblings()
        {
            var parent = transform.parent;
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).GetComponent<ButtonBase>() is { } sibling
                    && !ReferenceEquals(sibling, this) && sibling.mode == ButtonMode.Radio && sibling.selected)
                    sibling.selected = false;
        }

        public override void RefreshState()
        {
            RefreshTitle();
            // 复刻 GButton.SetCurrentState：按 grayed/selected/over 择页，不含瞬时按下态（按下由 OnPointerDown 单独处理）。
            if (grayed && selected && SetState(VisualState.SelectedDisabled))
                return;
            if (grayed && SetState(VisualState.Disabled))
                return;
            if (selected)
            {
                if (_over)
                {
                    if (!SetState(VisualState.SelectedOver))
                        SetState(VisualState.Down);
                }
                else
                    SetState(VisualState.Down);
            }
            else
                SetState(_over ? VisualState.Over : VisualState.Up);
        }

        protected bool SetState(VisualState state)
        {
            var (exists, page) = state switch
            {
                VisualState.Up => (StatePages.HasUp, StatePages.Up),
                VisualState.Down => (StatePages.HasDown, StatePages.Down),
                VisualState.Over => (StatePages.HasOver, StatePages.Over),
                VisualState.SelectedOver => (StatePages.HasSelectedOver, StatePages.SelectedOver),
                VisualState.Disabled => (StatePages.HasDisabled, StatePages.Disabled),
                _ => (StatePages.HasSelectedDisabled, StatePages.SelectedDisabled),
            };
            if (exists)
                controller.page = page;
            return exists;
        }
    }
}
