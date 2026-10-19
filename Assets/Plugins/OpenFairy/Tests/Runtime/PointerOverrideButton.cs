using UnityEngine.EventSystems;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PointerOverrideButton : Button<PointerOverrideButton.State>
    {
        public enum State { up, down, over, selectedOver, disabled, selectedDisabled }
        public int enters, exits, presses, releases;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            enters++;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            exits++;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            presses++;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            releases++;
        }
    }
}
