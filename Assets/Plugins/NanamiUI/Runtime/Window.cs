using UnityEngine;

namespace NanamiUI
{
    // 复刻 FairyGUI Window：包一个 content prefab，经 Root 托管显示/隐藏。默认无动效直接显隐；
    // 子类可覆盖 OnInit/OnShown/OnHide/DoShowAnimation/DoHideAnimation（缩放进出等）。
    public class Window
    {
        public GameObject prefab;      // content 内容 prefab（如 WindowA/WindowB），由调用方赋值
        public bool modal;             // true：窗口下方铺半透明模态层，拦截下层点击（复刻 Window.modal）
        public bool inited { get; private set; }

        protected GameObject go;
        protected RectTransform contentPane;

        public RectTransform Root => go != null ? (RectTransform)go.transform : null;

        public void Show() => NanamiUI.Root.inst.ShowWindow(this);
        public void Hide()
        {
            if (go != null && go.activeSelf)
                DoHideAnimation();
        }
        public void HideImmediately()
        {
            NanamiUI.Root.inst.HideWindowImmediately(this);
            OnHide();
        }

        internal void EnsureInited(RectTransform parent)
        {
            if (inited)
                return;
            go = Object.Instantiate(prefab, parent, false);
            contentPane = (RectTransform)go.transform;
            contentPane.anchorMin = contentPane.anchorMax = contentPane.pivot = new Vector2(0, 1);
            contentPane.localScale = Vector3.one;
            inited = true;
            OnInit();
        }

        internal void DoShow() => DoShowAnimation();

        // 在 Root 内居中（复刻 GObject.Center）。
        protected void Center()
        {
            var s = contentPane.rect.size;
            var r = NanamiUI.Root.inst.Size;
            contentPane.anchoredPosition = new Vector2(Mathf.Round((r.x - s.x) / 2), -Mathf.Round((r.y - s.y) / 2));
        }

        // 找到关闭按钮（frame 下名为 closeButton 的 ButtonBase）并接 Hide。
        protected void BindCloseButton()
        {
            foreach (var button in contentPane.GetComponentsInChildren<ButtonBase>(true))
                if (button.name == "closeButton")
                {
                    button.onClick.AddListener(Hide);
                    return;
                }
        }

        // 改 pivot 但保持视觉位置不变（复刻 RectTransform pivot setter 的补偿），用于缩放动效绕中心。
        protected static void SetPivotKeepPosition(RectTransform rt, Vector2 pivot)
        {
            var size = rt.rect.size;
            var delta = pivot - rt.pivot;
            rt.pivot = pivot;
            rt.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
        }

        protected virtual void OnInit() => BindCloseButton();
        protected virtual void OnShown() { }
        protected virtual void OnHide() { }
        protected virtual void DoShowAnimation() => OnShown();
        protected virtual void DoHideAnimation() => HideImmediately();
    }
}
