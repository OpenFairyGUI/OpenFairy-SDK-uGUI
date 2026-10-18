using System;
using Cysharp.Threading.Tasks;

namespace OpenFairy.UGUI.Tests
{
    public sealed class PopupSelfReentryWindow : Window
    {
        public Action whenHidden;
        public bool waitOnHide;
        public int hideAnimations;
        protected override UniTask DoHideAnimation()
        {
            hideAnimations++;
            return waitOnHide ? UniTask.DelayFrame(1) : UniTask.CompletedTask;
        }
        protected override void OnHide() => whenHidden?.Invoke();
    }
}
