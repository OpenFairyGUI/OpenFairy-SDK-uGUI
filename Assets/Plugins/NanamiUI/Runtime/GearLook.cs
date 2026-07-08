using System;
using DG.Tweening;
using UnityEngine;

namespace NanamiUI
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

        [NonSerialized] private Tweener _tweener;

        // 组透明度按 CanvasGroup.alpha 乘算传播（复刻 FairyGUI 组 alpha），不覆盖各子物体 authored 的 color.a。
        private CanvasGroup Group()
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private void ApplyGrayed(bool isGrayed)
        {
            var effect = target.GetComponent<Grayed>();
            if (isGrayed && effect == null)
                target.AddComponent<Grayed>();
            else if (!isGrayed && effect != null)
                UnityEngine.Object.Destroy(effect); // Grayed.OnDisable 还原原材质
        }

        public override void Apply(T page)
        {
            _tweener?.Kill();
            var index = Array.IndexOf(pages, page);
            Group().alpha = index >= 0 ? alphas[index] : defaultAlpha;
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
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed); // grayed 是离散态，直接切换
            _tweener?.Kill();
            var startAlpha = group.alpha;
            var startRot = rt.localEulerAngles.z;
            if (!animate || !tween || (Mathf.Approximately(startAlpha, alpha) && Mathf.Approximately(Mathf.DeltaAngle(startRot, rotation), 0)))
            {
                group.alpha = alpha;
                rt.localEulerAngles = new Vector3(0, 0, rotation);
                return;
            }
            _tweener = DOTween.To(() => 0f, t =>
            {
                group.alpha = Mathf.Lerp(startAlpha, alpha, t);
                rt.localEulerAngles = new Vector3(0, 0, Mathf.LerpAngle(startRot, rotation, t));
            }, 1f, duration).SetEase(ease).SetDelay(delay).SetLink(rt.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() => _tweener = null);
        }
    }
}
