using System;
using UnityEngine;

namespace OpenFairy.UGUI
{
    public enum RelationSide
    {
        LeftLeft, LeftCenter, LeftRight, CenterCenter, RightLeft, RightCenter, RightRight,
        TopTop, TopMiddle, TopBottom, MiddleMiddle, BottomTop, BottomMiddle, BottomBottom,
        Width, Height,
        LeftExtLeft, LeftExtRight, RightExtLeft, RightExtRight,
        TopExtTop, TopExtBottom, BottomExtTop, BottomExtBottom,
    }

    [Serializable]
    public struct RelationRule
    {
        public RelationSide side;
        public bool percent;
    }

    [Serializable]
    public sealed class RelationBinding
    {
        public RectTransform target;
        public RelationRule[] rules = Array.Empty<RelationRule>();
        public bool pivotAsAnchor;
        public bool nativeAnchors;
        [SerializeField, HideInInspector] internal RelationGeometry previous;
    }
}
