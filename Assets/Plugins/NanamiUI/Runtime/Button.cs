using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    public enum ButtonMode
    {
        Common,
        Check,
        Radio,
    }

    // 非泛型按钮基类：让 Root/Window/PopupMenu 不必知道 T 就能挂 onClick / 设 Title（复刻 FairyGUI GButton 面）。
    // Selected/Mode 非泛型面用于 Radio 组互斥；relatedController 面用于 tab/radio 组按共享控制器换页（跨 T、跨容器）。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick = new();

        // 关联控制器（FairyGUI relatedController/relatedPageId）：点击时把 owner 上名为 relatedControllerField 的控制器
        // 设到 relatedPage 页，并同步同组按钮 selected。由 Migrate 从 <Button controller=.. page=..> 烘焙。
        public Component relatedOwner;
        public string relatedControllerField;
        public int relatedPage = -1;
        public bool changeStateOnClick = true; // 复刻 GButton：列表项交给 ListSelection 管选择时置 false，禁本体自翻 selected

        public abstract string Title { get; set; }
        public abstract bool Selected { get; set; }
        public abstract ButtonMode Mode { get; }
        public abstract void SetGrayed(bool value);

        public bool HasRelatedController =>
            relatedOwner != null && relatedPage >= 0 && !string.IsNullOrEmpty(relatedControllerField);

        // 换页 + 同组同步：同一 (owner, controllerField) 下每个按钮 selected = 其 relatedPage 是否命中当前页。
        // 复刻 FairyGUI 共享 relatedController 的 tab/radio 组语义（组员不必是直接兄弟）。Common 模式按钮不置 selected
        // （其激活态由控制器 gears 驱动，置 selected 会卡在按下页；复刻 GButton.selected 对 Common 早返回）。
        protected void ApplyRelatedController()
        {
            ControllerBinding.SetPage(relatedOwner, relatedControllerField, relatedPage);
            foreach (var button in relatedOwner.GetComponentsInChildren<ButtonBase>(true))
                if (button.relatedOwner == relatedOwner && button.relatedControllerField == relatedControllerField
                    && button.Mode != ButtonMode.Common)
                    button.Selected = button.relatedPage == relatedPage;
        }
    }

    public abstract class Button<T> : ButtonBase, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler where T : struct, Enum
    {
        public Controller<T> controller;
        public TextField titleText;
        public Loader iconLoader;
        public ButtonMode mode;
        public bool selected;
        public bool grayed;
        public string selectedTitle;

        [SerializeField]
        private string _title;
        private bool _down, _over;

        public override string Title
        {
            get => _title;
            set
            {
                _title = value;
                RefreshTitle();
            }
        }

        public override ButtonMode Mode => mode;

        public override bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                RefreshState();
            }
        }

        // 复刻 GButton.HandleGrayedChanged：置灰后进 disabled/selectedDisabled 页并拦截交互（grayed 守卫在各 handler 里）。
        public override void SetGrayed(bool value)
        {
            grayed = value;
            RefreshState();
        }

        public Sprite Icon
        {
            get => iconLoader.sprite;
            set
            {
                iconLoader.sprite = value;
                iconLoader.enabled = value != null;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (grayed)
                return;
            _down = true;
            if (mode == ButtonMode.Common) // 复刻 GButton.__touchBegin：仅 Common 按下进 down 态，Check/Radio 按住不变
                SetState("down");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (grayed)
                return;
            _down = false;
            if (mode == ButtonMode.Common)
                RefreshState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (grayed)
                return;
            _over = true;
            if (!_down)
                RefreshState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (grayed)
                return;
            _over = false;
            if (!_down)
                RefreshState();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (grayed)
                return;
            if (HasRelatedController)
            {
                if (mode == ButtonMode.Check)
                {
                    if (changeStateOnClick)
                    {
                        selected = !selected;
                        RefreshState();
                    }
                    // 复刻 GButton：选中把关联控制器设到本页，取消则回对页（2 页控制器的 oppositePageId）。
                    ControllerBinding.SetPage(relatedOwner, relatedControllerField, selected ? relatedPage : relatedPage == 0 ? 1 : 0);
                }
                else
                    ApplyRelatedController(); // Common(tab)/Radio：换页并按控制器同步组内 selected
            }
            else if (mode == ButtonMode.Check)
            {
                if (changeStateOnClick)
                {
                    selected = !selected;
                    RefreshState();
                }
            }
            else if (mode == ButtonMode.Radio)
            {
                if (changeStateOnClick)
                {
                    DeselectSiblings();
                    selected = true;
                    RefreshState();
                }
            }
            else
                RefreshState();
            onClick.Invoke();
        }

        // 无关联控制器的 radio 组（纯视觉、同父）：兄弟里其它 radio 取消选中。
        private void DeselectSiblings()
        {
            var parent = transform.parent;
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).GetComponent<ButtonBase>() is { } sibling
                    && !ReferenceEquals(sibling, this) && sibling.Mode == ButtonMode.Radio && sibling.Selected)
                    sibling.Selected = false;
        }

        public void RefreshState()
        {
            RefreshTitle();
            // 复刻 GButton.SetCurrentState：按 grayed/selected/over 择页，不含瞬时按下态（按下由 OnPointerDown 单独处理）。
            if (grayed && selected && SetState("selectedDisabled"))
                return;
            if (grayed && SetState("disabled"))
                return;
            if (selected)
            {
                if (_over)
                    SetState("selectedOver", "down");
                else
                    SetState("down");
            }
            else
                SetState(_over ? "over" : "up");
        }

        private void RefreshTitle()
        {
            if (titleText == null)
                return;
            _title ??= titleText.text;
            titleText.text = selected && !string.IsNullOrEmpty(selectedTitle) ? selectedTitle : _title;
        }

        private bool SetState(params string[] pages)
        {
            foreach (var page in pages)
            {
                if (!Enum.TryParse(page, out T value))
                    continue;
                controller.page = value;
                return true;
            }
            return false;
        }
    }
}
