using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OpenFairy.UGUI
{
    public sealed partial class Root
    {
        private readonly List<RectTransform> _popups = new();
        private readonly HashSet<RectTransform> _justClosedPopups = new();
        private readonly Dictionary<RectTransform, UniTaskCompletionSource> _popupClosed = new();

        public UniTask ShowPopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            HideTooltip();
            HidePopup(popup);
            _popups.Remove(popup);
            if (_popupClosed.Remove(popup, out var previous)) previous.TrySetCanceled();
            popup.SetParent(rect, false);
            popup.SetAsLastSibling();
            var closed = new UniTaskCompletionSource();
            _popups.Add(popup);
            _popupClosed.Add(popup, closed);
            if (popup.TryGetComponent<Window>(out var window))
            {
                RegisterWindow(window);
                window.DoShow();
            }
            else popup.gameObject.SetActive(true);

            if (target != null || window == null || dir != PopupDirection.Auto)
            {
                var position = GetPopupPosition(popup, target, dir);
                popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0, 1);
                popup.anchoredPosition = new Vector2(position.x, -position.y);
            }
            RefreshModal();
            return closed.Task;
        }

        public UniTask TogglePopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto)
        {
            if (_justClosedPopups.Contains(popup)) return UniTask.CompletedTask;
            if (_popups.Contains(popup))
            {
                HidePopup(popup);
                return UniTask.CompletedTask;
            }
            return ShowPopup(popup, target, dir);
        }

        public void HidePopup(RectTransform popup = null)
        {
            var from = popup == null ? 0 : _popups.IndexOf(popup);
            if (from >= 0) ClosePopups(from);
        }

        private void ClosePopups(int from)
        {
            var closing = new List<KeyValuePair<RectTransform, UniTaskCompletionSource>>(_popups.Count - from);
            for (var i = from; i < _popups.Count; ++i) closing.Add(new(_popups[i], _popupClosed[_popups[i]]));
            _popups.RemoveRange(from, closing.Count);
            for (var i = closing.Count - 1; i >= 0; --i)
                ClosePopup(closing[i].Key, closing[i].Value).Forget();
        }

        private async UniTask ClosePopup(RectTransform popup, UniTaskCompletionSource closed)
        {
            if (!_popupClosed.TryGetValue(popup, out var current) || current != closed) return;
            if (popup.TryGetComponent<Window>(out var window))
            {
                if (!await window.DoHide()) return;
            }
            else popup.gameObject.SetActive(false);
            CompletePopup(popup, closed);
        }

        private void CompletePopup(RectTransform popup, UniTaskCompletionSource closed)
        {
            if (!_popupClosed.TryGetValue(popup, out var current) || current != closed) return;
            _popupClosed.Remove(popup);
            closed.TrySetResult();
        }

        public void HidePopupImmediately(RectTransform popup)
        {
            HidePopup(popup);
            if (!_popupClosed.TryGetValue(popup, out var closed)) return;
            if (popup.TryGetComponent<Window>(out var window)) window.DoHideImmediately();
            else popup.gameObject.SetActive(false);
            CompletePopup(popup, closed);
        }

        internal void CheckPopups(GameObject target)
        {
            _justClosedPopups.Clear();
            var retained = -1;
            for (var i = _popups.Count - 1; i >= 0; --i)
                if (target != null && target.transform.IsChildOf(_popups[i]))
                {
                    retained = i;
                    break;
                }
            for (var i = retained + 1; i < _popups.Count; ++i) _justClosedPopups.Add(_popups[i]);
            ClosePopups(retained + 1);
        }

        private void CancelPopups()
        {
            var closing = new List<KeyValuePair<RectTransform, UniTaskCompletionSource>>(_popupClosed);
            _popups.Clear();
            _popupClosed.Clear();
            _justClosedPopups.Clear();
            foreach (var pair in closing)
            {
                if (pair.Key != null)
                {
                    if (pair.Key.TryGetComponent<Window>(out var window)) window.DoHideImmediately();
                    else pair.Key.gameObject.SetActive(false);
                }
                pair.Value.TrySetCanceled();
            }
        }

        private void RegisterWindow(Window window)
        {
            window.contentPane.SetParent(rect, false);
            if (!_windows.Contains(window)) _windows.Add(window);
            window.Initialize();
        }

        public void ShowWindow(Window window)
        {
            HideTooltip();
            HidePopup(window.contentPane);
            _popups.Remove(window.contentPane);
            if (_popupClosed.Remove(window.contentPane, out var previous)) previous.TrySetCanceled();
            RegisterWindow(window);
            window.contentPane.SetAsLastSibling();
            if (!window.modal && _modalLayer != null && _modalLayer.activeSelf)
                window.contentPane.SetSiblingIndex(_modalLayer.transform.GetSiblingIndex());
            window.DoShow();
            RefreshModal();
        }

        public void HideWindow(Window window)
        {
            if (_popups.Contains(window.contentPane)) HidePopup(window.contentPane);
            else if (!window.hiding) window.DoHide().Forget();
        }

        public void HideWindowImmediately(Window window)
        {
            HidePopupImmediately(window.contentPane);
            window.DoHideImmediately();
        }

        internal void ForgetWindow(Window window)
        {
            _windows.Remove(window);
            RefreshModal();
        }

        internal void WindowDisabled(Window window)
        {
            var popup = window.contentPane;
            var index = _popups.IndexOf(popup);
            if (index >= 0)
            {
                ClosePopups(index + 1);
                _popups.Remove(popup);
            }
            if (_popupClosed.Remove(popup, out var closed)) closed.TrySetCanceled();
            ForgetWindow(window);
        }

        public void BringToFront(Window window)
        {
            window.contentPane.SetAsLastSibling();
            if (!window.modal && _modalLayer != null && _modalLayer.activeSelf)
                window.contentPane.SetSiblingIndex(_modalLayer.transform.GetSiblingIndex());
            if (_popups.Remove(window.contentPane)) _popups.Add(window.contentPane);
            RefreshModal();
        }
    }
}
