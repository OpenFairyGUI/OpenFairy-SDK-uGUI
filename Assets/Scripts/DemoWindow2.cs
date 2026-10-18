using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OpenFairy.UGUI.Example
{
    public sealed class DemoWindow2 : Window
    {
        public Transition contentTransition;

        protected override void OnInit()
        {
            DemoFont.Apply(gameObject);
            Center();
            SetPivotKeepPosition(contentPane, Vector2.one * .5f);
        }

        protected override void OnShown() => contentTransition.Play().Forget();
        protected override void OnHide() => contentTransition.Stop();
    }
}
