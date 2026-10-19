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
        [NonSerialized] private Vector2 _end;
        [NonSerialized] private float? _rotation;

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
            // Visual graying and touchability are independent authored channels.
            if (_button != null)
                _button.grayed = isGrayed;
        }

        // FairyGUI 的 rotation 是不取模的角度：沿用上次写入的值，被外部改过时再从 transform 读回。
        private float Rotation(RectTransform rt) =>
            _rotation is { } rotation && Mathf.Approximately(Mathf.DeltaAngle(-rotation, rt.localEulerAngles.z), 0)
                ? rotation : -Mathf.DeltaAngle(0, rt.localEulerAngles.z);

        private void SetRotation(RectTransform rt, float rotation)
        {
            _rotation = rotation;
            rt.localEulerAngles = new Vector3(0, 0, -rotation);
        }

        public override void Apply(T page) => Apply(page, false);

        public override void Apply(T page, bool animate)
        {
            var index = Array.IndexOf(pages, page);
            var alpha = index >= 0 ? alphas[index] : defaultAlpha;
            var rotation = index >= 0 ? rotations[index] : defaultRotation;
            var rt = (RectTransform)target.transform;
            // 组透明度按 CanvasGroup.alpha 乘算传播（复刻 FairyGUI 组 alpha），由 Migrate 烘焙。
            var group = _group ??= target.GetComponent<CanvasGroup>();
            // grayed/touchable 是离散态，直接切换。
            group.blocksRaycasts = index >= 0 ? touchables[index] : defaultTouchable;
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed);
            if (!animate || !tween)
            {
                KillTween();
                group.alpha = alpha;
                SetRotation(rt, rotation);
                return;
            }
            var end = new Vector2(alpha, rotation);
            if (ContinueTween(end == _end))
                return;
            var startAlpha = group.alpha;
            var startRotation = Rotation(rt);
            if (startAlpha == alpha && startRotation == rotation)
                return;
            _end = end;
            TrackTween(DOTween.To(() => 0f, t =>
            {
                group.alpha = Mathf.Lerp(startAlpha, alpha, t);
                SetRotation(rt, Mathf.Lerp(startRotation, rotation, t));
            }, 1f, duration).SetEase(ease).SetDelay(delay).SetLink(rt.gameObject, LinkBehaviour.KillOnDestroy));
        }
    }
}
