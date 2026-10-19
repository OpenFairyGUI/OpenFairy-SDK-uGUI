using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // A baked template reference; resource URLs are resolved by the importer.
    [DisallowMultipleComponent]
    public sealed class Tooltip : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] internal Label template;
        [TextArea] public string text;
        public float delay = .1f;
        private Root _root;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (template == null || string.IsNullOrEmpty(text))
                return;
            _root = Root.inst != null ? Root.inst
                : Root.Create((RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform);
            _root.ShowTooltip(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData) => Hide();
        protected override void OnDisable()
        {
            Hide();
            base.OnDisable();
        }

        public void RefreshText()
        {
            if (_root != null) _root.RefreshTooltip(this);
        }

        private void Hide()
        {
            if (_root != null)
                _root.HideTooltip(this);
            _root = null;
        }
    }
}
