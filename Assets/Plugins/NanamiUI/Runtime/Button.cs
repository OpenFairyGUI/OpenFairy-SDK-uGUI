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

    // 非泛型按钮基类（复刻 FairyGUI GButton 面）：让 Root/Window/PopupMenu/Migrate 不必知道 T 就能设标题/图标/选中态。
    // 关联控制器（FairyGUI relatedController/relatedPageId）由 Migrate 从 <Button controller=.. page=..> 烘焙；
    // 换页→选中态的反向同步由烘焙进控制器的 GearButton 完成（复刻 GButton.HandleControllerChanged）。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick = new();
        public UnityEvent onChanged = new(); // 复刻 GButton.onChanged：仅用户点击翻转 Check/Radio 时发

        public ButtonMode mode;
        public bool selected;
        public bool grayed;
        public string selectedTitle;
        public TextField titleText;
        public Loader iconLoader;

        public Component relatedOwner;
        public string relatedControllerField;
        public int relatedPage = -1;
        public bool changeStateOnClick = true; // 复刻 GButton：列表项交给 ListSelection 管选择时置 false，禁本体自翻 selected

        [SerializeField]
        private string _title;

        public ButtonMode Mode => mode;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                RefreshTitle();
            }
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

        // 复刻 GButton.selected 的 setter：Common 忽略选中态、同值早返回；
        // 选中把关联控制器设到本页，Check 取消且当前正在本页则回对页（2 页控制器的 oppositePageId）。
        public bool Selected
        {
            get => selected;
            set
            {
                if (mode == ButtonMode.Common || selected == value)
                    return;
                selected = value;
                RefreshState();
                if (HasRelatedController && Application.isPlaying) // 烘焙期选中态由控制器初始页经 GearButton 驱动，不反向写页
                {
                    if (selected)
                        ControllerBinding.SetPage(relatedOwner, relatedControllerField, relatedPage);
                    else if (mode == ButtonMode.Check && ControllerBinding.GetPage(relatedOwner, relatedControllerField) == relatedPage)
                        ControllerBinding.SetPage(relatedOwner, relatedControllerField, relatedPage == 0 ? 1 : 0);
                }
            }
        }

        // 复刻 GButton.HandleGrayedChanged：置灰后进 disabled/selectedDisabled 页并拦截交互（grayed 守卫在各 handler 里）。
        public void SetGrayed(bool value)
        {
            grayed = value;
            RefreshState();
        }

        public bool HasRelatedController =>
            relatedOwner != null && relatedPage >= 0 && !string.IsNullOrEmpty(relatedControllerField);

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
        public Controller<T> controller;

        private bool _down, _over;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || grayed) // 复刻 GButton.__touchBegin：仅左键
                return;
            _down = true;
            if (mode == ButtonMode.Common) // 仅 Common 按下进 down 态，Check/Radio 按住不变
                SetState("down");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || grayed)
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

        // 复刻 GButton.__click：Check/Radio 经 Selected setter 翻选并驱动关联控制器；Common 直接换页。
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || grayed)
                return;
            if (mode == ButtonMode.Check)
            {
                if (changeStateOnClick)
                {
                    Selected = !selected;
                    onChanged.Invoke();
                }
            }
            else if (mode == ButtonMode.Radio)
            {
                if (changeStateOnClick && !selected)
                {
                    if (!HasRelatedController)
                        DeselectSiblings(); // 无关联控制器的 radio 组回退：同父兄弟互斥
                    Selected = true;
                    onChanged.Invoke();
                }
            }
            else if (HasRelatedController)
                ControllerBinding.SetPage(relatedOwner, relatedControllerField, relatedPage);
            onClick.Invoke();
        }

        private void DeselectSiblings()
        {
            var parent = transform.parent;
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).GetComponent<ButtonBase>() is { } sibling
                    && !ReferenceEquals(sibling, this) && sibling.Mode == ButtonMode.Radio && sibling.Selected)
                    sibling.Selected = false;
        }

        public override void RefreshState()
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
