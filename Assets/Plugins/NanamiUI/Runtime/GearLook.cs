using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

        private void SetAlpha(float alpha)
        {
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                var color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }

        private void ApplyGrayed(bool isGrayed)
        {
            var effect = target.GetComponent<Grayed>();
            if (isGrayed && effect == null)
                target.AddComponent<Grayed>();
            else if (!isGrayed && effect != null)
                UnityEngine.Object.DestroyImmediate(effect);
        }

        public override void Apply(T page)
        {
            _tweener?.Kill();
            var index = Array.IndexOf(pages, page);
            SetAlpha(index >= 0 ? alphas[index] : defaultAlpha);
            ((RectTransform)target.transform).localEulerAngles = new Vector3(0, 0, -(index >= 0 ? rotations[index] : defaultRotation));
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed);
        }

        public override void Apply(T page, bool animate)
        {
            var index = Array.IndexOf(pages, page);
            var alpha = index >= 0 ? alphas[index] : defaultAlpha;
            var rotation = -(index >= 0 ? rotations[index] : defaultRotation);
            var rt = (RectTransform)target.transform;
            ApplyGrayed(index >= 0 ? grayed[index] : defaultGrayed); // grayed 是离散态，直接切换
            _tweener?.Kill();
            var graphic = target.GetComponentInChildren<Graphic>(true);
            var startAlpha = graphic != null ? graphic.color.a : alpha;
            var startRot = rt.localEulerAngles.z;
            if (!animate || !tween || (Mathf.Approximately(startAlpha, alpha) && Mathf.Approximately(Mathf.DeltaAngle(startRot, rotation), 0)))
            {
                SetAlpha(alpha);
                rt.localEulerAngles = new Vector3(0, 0, rotation);
                return;
            }
            _tweener = DOTween.To(() => 0f, t =>
            {
                SetAlpha(Mathf.Lerp(startAlpha, alpha, t));
                rt.localEulerAngles = new Vector3(0, 0, Mathf.LerpAngle(startRot, rotation, t));
            }, 1f, duration).SetEase(ease).SetDelay(delay).OnComplete(() => _tweener = null);
        }
    }
}
