using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class Window : UIBehaviour
    {
        public bool modal;
        [SerializeField] internal Transition showTransition;
        [SerializeField] internal Transition hideTransition;
        [SerializeField] internal ButtonBase closeButton;
        [SerializeField] internal CanvasGroup canvasGroup;
        private int _version;
        private bool _shown;
        private bool _managedHide;
        private bool _blocksRaycasts;
        internal bool hiding { get; private set; }
        public bool inited { get; private set; }
        public RectTransform contentPane => (RectTransform)transform;

        public void Show() => Root.inst.ShowWindow(this);
        public void Hide() => Root.inst.HideWindow(this);
        public void HideImmediately() => Root.inst.HideWindowImmediately(this);

        internal void Initialize()
        {
            if (inited) return;
            canvasGroup = GetComponent<CanvasGroup>();
            _blocksRaycasts = canvasGroup.blocksRaycasts;
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            inited = true;
            OnInit();
        }

        internal void DoShow()
        {
            var version = ++_version;
            StopAnimations();
            hiding = false;
            _shown = true;
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = _blocksRaycasts;
            ShowAnimated(version).Forget();
        }

        private async UniTask ShowAnimated(int version)
        {
            await DoShowAnimation();
            if (this != null && version == _version) OnShown();
        }

        internal async UniTask<bool> DoHide()
        {
            if (!gameObject.activeSelf) return true;
            var version = ++_version;
            StopAnimations();
            hiding = true;
            canvasGroup.blocksRaycasts = false;
            await DoHideAnimation();
            if (this == null || version != _version) return false;
            DoHideImmediately();
            return true;
        }

        internal void DoHideImmediately()
        {
            ++_version;
            StopAnimations();
            hiding = false;
            Root.inst?.ForgetWindow(this);
            _managedHide = true;
            gameObject.SetActive(false);
            _managedHide = false;
            NotifyHidden();
        }

        protected override void OnDisable()
        {
            ++_version;
            StopAnimations();
            hiding = false;
            if (!_managedHide)
            {
                Root.inst?.WindowDisabled(this);
                NotifyHidden();
            }
            base.OnDisable();
        }

        private void NotifyHidden()
        {
            if (!_shown) return;
            _shown = false;
            OnHide();
        }

        protected override void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            base.OnDestroy();
        }

        private void StopAnimations()
        {
            if (showTransition != null) showTransition.Stop();
            if (hideTransition != null) hideTransition.Stop();
        }

        protected void Center() => Root.inst.Center(contentPane);

        protected static void SetPivotKeepPosition(RectTransform rect, Vector2 pivot)
        {
            var delta = pivot - rect.pivot;
            rect.pivot = pivot;
            rect.anchoredPosition += Vector2.Scale(delta, rect.sizeDelta);
        }

        protected virtual void OnInit() { }
        protected virtual void OnShown() { }
        protected virtual void OnHide() { }
        protected virtual UniTask DoShowAnimation() => showTransition != null ? showTransition.Play() : UniTask.CompletedTask;
        protected virtual UniTask DoHideAnimation() => hideTransition != null ? hideTransition.Play() : UniTask.CompletedTask;
    }

}
