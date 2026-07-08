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

    // 非泛型按钮基类：让 GRoot/Window/PopupMenu 不必知道 T 就能挂 onClick / 设 Title（复刻 FairyGUI GButton 面）。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick = new();
        public abstract string Title { get; set; }
    }

    public abstract class Button<T> : ButtonBase, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler where T : struct, Enum
    {
        public Controller<T> controller;
        public Text titleText;
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
            SetState("down");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (grayed)
                return;
            _down = false;
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
            if (mode == ButtonMode.Check)
                selected = !selected;
            else if (mode == ButtonMode.Radio)
                selected = true;
            RefreshState();
            onClick.Invoke();
        }

        public void RefreshState()
        {
            RefreshTitle();
            if (grayed && selected && SetState("selectedDisabled"))
                return;
            if (grayed && SetState("disabled"))
                return;
            if (_over && selected && SetState("selectedOver"))
                return;
            if (_down || selected)
                SetState("down", "selectedOver");
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
