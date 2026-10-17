using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using NativeInput = TMPro.TMP_InputField;
#else
using NativeInput = UnityEngine.UI.InputField;
#endif

namespace OpenFairy.UGUI
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(NativeInput))]
    public sealed class InputEditing : UIBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] internal NativeInput field;
        private const int HistoryLimit = 5;
        private readonly List<Snapshot> _undo = new();
        private readonly List<Snapshot> _redo = new();
        private Snapshot _current;
        private bool _selected, _restoring, _composing;
        private int _editFrame = -1;
        private Keyboard _keyboard;
        private static int _shortcutFrame = -1;

        private readonly struct Snapshot
        {
            public readonly string text;
            public readonly int anchor, focus;
            public Snapshot(string text, int anchor, int focus)
            { this.text = text; this.anchor = anchor; this.focus = focus; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShortcutFrame() => _shortcutFrame = -1;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (field == null) field = GetComponent<NativeInput>();
            field.onValueChanged.AddListener(Changed);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                OnSelect(null);
        }

        protected override void OnDisable()
        {
            if (field != null) field.onValueChanged.RemoveListener(Changed);
            OnDeselect(null);
            base.OnDisable();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            ResetHistory();
            BindKeyboard(Keyboard.current);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            _undo.Clear();
            _redo.Clear();
            BindKeyboard(null);
        }

        private void BindKeyboard(Keyboard keyboard)
        {
            if (ReferenceEquals(_keyboard, keyboard)) return;
            if (_keyboard != null) _keyboard.onIMECompositionChange -= CompositionChanged;
            _keyboard = keyboard;
            _composing = false;
            if (_keyboard != null) _keyboard.onIMECompositionChange += CompositionChanged;
        }

        private void CompositionChanged(IMECompositionString composition) => _composing = composition.Count != 0;

        private Snapshot Capture()
        {
#if FAIRYGUI_TMPRO
            return new Snapshot(field.text, field.selectionStringAnchorPosition, field.selectionStringFocusPosition);
#else
            return new Snapshot(field.text, field.selectionAnchorPosition, field.selectionFocusPosition);
#endif
        }

        public void ResetHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _editFrame = -1;
            _current = Capture();
        }

        public void SetTextWithoutNotify(string text)
        {
            field.SetTextWithoutNotify(text);
            ResetHistory();
        }

        // Run before the native EventSystem update, keeping caret-only changes out of history.
        private void Update()
        {
            if (!_selected) return;
            if (field.text != _current.text) ResetHistory();
            else _current = Capture();
        }

        private void Changed(string text)
        {
            if (!_selected || _restoring || text == _current.text) return;
            if (field.readOnly) { ResetHistory(); return; }
            if (_editFrame != Time.frameCount)
            {
                _undo.Add(_current);
                if (_undo.Count > HistoryLimit) _undo.RemoveAt(0);
                _editFrame = Time.frameCount;
            }
            else if (_undo.Count != 0 && _undo[_undo.Count - 1].text == text)
                _undo.RemoveAt(_undo.Count - 1);
            _redo.Clear();
            _current = Capture();
        }

        private bool CanEdit => _selected && field.isFocused && field.IsInteractable()
            && !field.readOnly && !_composing;

        public bool Undo() => CanEdit && Restore(_undo, _redo);
        public bool Redo() => CanEdit && Restore(_redo, _undo);

        private bool Restore(List<Snapshot> from, List<Snapshot> to)
        {
            if (field.text != _current.text) ResetHistory();
            if (from.Count == 0) return false;
            to.Add(Capture());
            var target = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            _restoring = true;
            try
            {
                field.SetTextWithoutNotify(target.text);
#if FAIRYGUI_TMPRO
                field.selectionStringAnchorPosition = target.anchor;
                field.selectionStringFocusPosition = target.focus;
                field.ForceLabelUpdate();
                field.textComponent.ForceMeshUpdate();
#else
                field.selectionAnchorPosition = target.anchor;
                field.selectionFocusPosition = target.focus;
#endif
                _current = Capture();
                _editFrame = -1;
                field.onValueChanged.Invoke(field.text);
            }
            finally { _restoring = false; }
            return true;
        }

        public bool MoveFocus(bool backward)
        {
            if (!_selected || !field.isFocused || _composing || field.lineType != NativeInput.LineType.SingleLine)
                return false;
            var visited = new HashSet<Selectable> { field };
            Selectable next = backward ? field.FindSelectableOnUp() : field.FindSelectableOnDown();
            while (next != null && visited.Add(next))
            {
                if (next.IsActive() && next.IsInteractable())
                {
                    next.Select();
                    return true;
                }
                next = backward ? next.FindSelectableOnUp() : next.FindSelectableOnDown();
            }
            return false;
        }

        private void LateUpdate()
        {
            if (!_selected || !field.isFocused) return;
            BindKeyboard(Keyboard.current);
            if (_keyboard == null || _composing || _shortcutFrame == Time.frameCount || _keyboard.altKey.isPressed)
                return;
            bool modifier = _keyboard.ctrlKey.isPressed || _keyboard.leftMetaKey.isPressed || _keyboard.rightMetaKey.isPressed;
            bool shift = _keyboard.shiftKey.isPressed;
            if (modifier && _keyboard.zKey.wasPressedThisFrame)
            {
                _shortcutFrame = Time.frameCount;
                if (shift) Redo(); else Undo();
            }
            else if (modifier && _keyboard.yKey.wasPressedThisFrame)
            {
                _shortcutFrame = Time.frameCount;
                Redo();
            }
            else if (!modifier && _keyboard.tabKey.wasPressedThisFrame)
            {
                // Native single-line fields ignore the character before navigation happens.
                _shortcutFrame = Time.frameCount;
                MoveFocus(shift);
            }
        }
    }
}
