using UnityEngine;

namespace OpenFairy.UGUI.Editor
{
    public static class GrayedBuilder
    {
        public static Grayed Configure(ColorAdjust filter, bool enabled)
        {
            if (!filter.TryGetComponent(out Grayed effect)) effect = filter.gameObject.AddComponent<Grayed>();
            effect.filter = filter;
            filter.grayed = effect;
            effect.enabled = enabled;
            effect.Refresh();
            return effect;
        }
    }
}
