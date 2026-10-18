using System;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PooledClickTestButton : Button<PooledClickTestButton.State>
    {
        public enum State { up, down }
        public int clicks;
        public Action clicked;

        protected override void Awake()
        {
            base.Awake();
            onClick.AddListener(Click);
        }

        private void Click()
        {
            clicks++;
            clicked?.Invoke();
        }
    }
}
