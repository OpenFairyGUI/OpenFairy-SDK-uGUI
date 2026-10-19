using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI.Tests
{
    public sealed class AutomaticFilterChange : MonoBehaviour
    {
        public RectTransform reference;
        public UnityEngine.UI.Image image;
        public bool pending;
        public int appliedFrame;

        private void LateUpdate()
        {
            if (!pending) return;
            pending = false;
            reference.sizeDelta = new Vector2(72, 48);
            image.color = Color.blue;
            appliedFrame = Time.frameCount;
        }
    }
}
