using System;
using DG.Tweening;
using UnityEngine;

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearLook<T> : Gear<T> where T : struct, Enum
    {
        public float[] alphas;
        public float defaultAlpha;
        public float[] rotations;
        public float defaultRotation;
        public bool[] grayed;
        public bool defaultGrayed;
        public bool[] touchables;
        public bool defaultTouchable = true;

        [NonSerialized] private CanvasGroup _group;
        [NonSerialized] private Grayed _grayedEffect;
        [NonSerialized] private ButtonBase _button;
        [NonSerialized] private bool _effectsResolved;

        // 组透明度按 CanvasGroup.alpha 乘算传播（复刻 FairyGUI 组 alpha），不覆盖各子物体 authored 的 color.a。
        // CanvasGroup 由 Migrate 烘焙；运行时只缓存静态引用，不再动态补组件。
        private CanvasGroup Group()
        {
            if (_group == null)
                _group = target.GetComponent<CanvasGroup>();
            return _group;
        }

        private void ApplyGrayed(bool isGrayed)
        {
            // Grayed 由 Migrate 烘焙在 target 上，只切 enabled（OnDisable 还原原材质）。
            if (!_effectsResolved)
            {
                target.TryGetComponent(out _grayedEffect);
                target.TryGetComponent(out _button);
                _effectsResolved = true;
            }
            if (_grayedEffect != null)
                _grayedEffect.enabled = isGrayed;
            // 传播到按钮：置 grayed → 进 disabled 页并拦截点击（复刻 GButton.HandleGrayedChanged），否则灰显却仍可点。
            if (_button != null)
                _button.grayed = isGrayed;
        }

        private bool Touchable(int index) => touchables == null || (index >= 0 ? touchables[index] : defaultTouchable);

        public override void Apply(T page)
        {
            KillTween();
            var index = Array.IndexOf(pages, page);
            var group = Group();
            group.alpha = index >= 0 ? alphas[index] : defaultAlpha;
            group.blocksRaycasts = Touchable(index);
            ((RectTransform)target.transform).localEulerAngles = new Vector3(0, 0, -(index >= 0 ? rotations[index] : defaultRotation));
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed);
        }

        public override void Apply(T page, bool animate)
        {
            var index = Array.IndexOf(pages, page);
            var alpha = index >= 0 ? alphas[index] : defaultAlpha;
            var rotation = -(index >= 0 ? rotations[index] : defaultRotation);
            var rt = (RectTransform)target.transform;
            var group = Group();
            group.blocksRaycasts = Touchable(index); // touchable 是离散态，直接切换
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed); // grayed 是离散态，直接切换
            KillTween();
            var startAlpha = group.alpha;
            var startRot = rt.localEulerAngles.z;
            if (!animate || !tween || (Mathf.Approximately(startAlpha, alpha) && Mathf.Approximately(Mathf.DeltaAngle(startRot, rotation), 0)))
            {
                group.alpha = alpha;
                rt.localEulerAngles = new Vector3(0, 0, rotation);
                return;
            }
            TrackTween(DOTween.To(() => 0f, t =>
            {
                group.alpha = Mathf.Lerp(startAlpha, alpha, t);
                rt.localEulerAngles = new Vector3(0, 0, Mathf.LerpAngle(startRot, rotation, t));
            }, 1f, duration).SetEase(ease).SetDelay(delay).SetLink(rt.gameObject, LinkBehaviour.KillOnDestroy));
        }
    }
}
