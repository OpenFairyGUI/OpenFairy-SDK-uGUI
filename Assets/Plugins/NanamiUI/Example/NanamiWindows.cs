using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace NanamiUI.Example
{
    // Demo 窗口（对标 Examples/Basics/Window1.cs）：居中显示。OnShown 本应填 6 项列表 "n6"，
    // 但 List 运行时未实现 → 暂留空（窗体/组合框/按钮照常显示，仅列表空）。
    public sealed class NanamiWindow1 : NanamiUI.Window
    {
        protected override void OnInit()
        {
            base.OnInit();
            Center();
        }

        protected override void OnShown()
        {
            // 填 WindowA 里的列表（按 ListSource 找，不依赖名字）为 6 项（复刻 Window1.OnShown）。
            foreach (var src in go.GetComponentsInChildren<NanamiUI.ListSource>())
            {
                NanamiUI.List.Fill((RectTransform)src.transform, 6, (item, i) =>
                {
                    if (item.GetComponent<NanamiUI.ButtonBase>() is { } button)
                        button.Title = i.ToString();
                });
                break;
            }
        }
    }

    // 对标 Window2.cs：绕中心缩放进出 + 播内容 transition "t1"。
    public sealed class NanamiWindow2 : NanamiUI.Window
    {
        protected override void OnInit()
        {
            base.OnInit();
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

        protected override void OnShown() => Transition("t1")?.Play();
        protected override void OnHide() => Transition("t1")?.Stop();

        private NanamiUI.Transition Transition(string name) =>
            go.GetComponents<NanamiUI.Transition>().FirstOrDefault(t => t.transitionName == name);
    }
}
