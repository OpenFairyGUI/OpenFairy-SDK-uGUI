using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    public abstract class Button<T> : Component, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler where T : struct, Enum
    {
        public Controller<T> controller;
        public Text titleText;
        public UnityEvent onClick = new();

        private bool _down, _over;

        public string Title
        {
            get => titleText.text;
            set => titleText.text = value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            SetState("down");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
            SetState(_over ? "over" : "up");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            if (!_down)
                SetState("over");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            if (!_down)
                SetState("up");
        }

        public void OnPointerClick(PointerEventData eventData) => onClick.Invoke();

        private void SetState(string page)
        {
            if (Enum.TryParse(page, out T value))
            {
                controller.page = value;
            }
        }
    }
}
