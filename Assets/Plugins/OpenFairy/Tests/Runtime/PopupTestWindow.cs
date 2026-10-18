using System;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PopupTestWindow : Window
    {
        public int initialized, shown, hidden;
        public Action whenHidden;
        protected override void OnInit() => ++initialized;
        protected override void OnShown() => ++shown;
        protected override void OnHide()
        {
            ++hidden;
            whenHidden?.Invoke();
        }
    }
}
