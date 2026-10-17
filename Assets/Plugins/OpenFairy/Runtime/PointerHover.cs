using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    [DisallowMultipleComponent, AddComponentMenu("OpenFairy/Pointer Hover")]
    public sealed class PointerHover : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UnityEvent<PointerEventData> onEnter = new();
        public UnityEvent<PointerEventData> onExit = new();
        private PointerEventData _pointer;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointer = eventData;
            onEnter.Invoke(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointer = null;
            onExit.Invoke(eventData);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_pointer == null) return;
            var pointer = _pointer;
            _pointer = null;
            onExit.Invoke(pointer);
        }
    }
}
