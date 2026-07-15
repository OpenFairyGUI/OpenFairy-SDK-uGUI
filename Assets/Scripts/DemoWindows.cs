using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using ZLinq;

namespace OpenFairy.UGUI.Example
{
    // Demo 窗口（对标 Examples/Basics/Window1.cs）：居中显示，并在 OnShown 填 6 项列表 "n6"。
    public sealed class DemoWindow1 : OpenFairy.UGUI.Window
    {
        protected override void OnInit()
        {
            base.OnInit();
            DemoFont.Apply(contentPane.gameObject);
            Center();
        }

        protected override void OnShown()
        {
            // 填 WindowA 里的列表（按 ListSource 找，不依赖名字）为 6 项（复刻 Window1.OnShown）。
            if (contentPane.GetComponentInChildren<OpenFairy.UGUI.ListSource>() is { } src)
                src.Fill(6, (item, i) =>
                {
                    if (item.GetComponent<OpenFairy.UGUI.ButtonBase>() is { } button)
                        button.title = i.ToString();
                });
        }
    }

    // 对标 Window2.cs：绕中心缩放进出 + 播内容 transition "t1"。
    public sealed class DemoWindow2 : OpenFairy.UGUI.Window
    {
        protected override void OnInit()
        {
            base.OnInit();
            DemoFont.Apply(contentPane.gameObject);
            Center();
        }

        protected override void DoShowAnimation()
        {
            SetPivotKeepPosition(contentPane, new Vector2(0.5f, 0.5f)); // 绕中心缩放
            contentPane.localScale = Vector3.one * 0.1f;
            DOTween.To(() => contentPane.localScale, v => contentPane.localScale = v, Vector3.one, 0.3f)
                .SetEase(Ease.OutQuad).SetLink(contentPane.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(OnShown);
        }

        protected override void DoHideAnimation()
        {
            // 关键：整段缩小 tween 结束后才 HideImmediately（同"旧页飞出再消失"的 display-lock 类，避免瞬间消失）。
            DOTween.To(() => contentPane.localScale, v => contentPane.localScale = v, Vector3.one * 0.1f, 0.3f)
                .SetEase(Ease.OutQuad).SetLink(contentPane.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(HideImmediately);
        }

        protected override void OnShown()
        {
            if (Transition("t1") is { } transition)
                transition.Play().Forget();
        }
        protected override void OnHide() => Transition("t1")?.Stop();

        private OpenFairy.UGUI.Transition Transition(string name) =>
            contentPane.GetComponents<OpenFairy.UGUI.Transition>().AsValueEnumerable().FirstOrDefault(t => t.transitionName == name);
    }
}
