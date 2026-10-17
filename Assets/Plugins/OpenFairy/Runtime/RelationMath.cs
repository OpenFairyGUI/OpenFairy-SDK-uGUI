using UnityEngine;

namespace OpenFairy.UGUI
{
    [System.Serializable]
    internal struct RelationGeometry
    {
        public Vector2 position;
        public Vector2 size;
        public Vector2 pivot;
        public bool pivotAsAnchor;

        public Vector2 origin => position + (pivotAsAnchor ? Vector2.Scale(pivot, size) : Vector2.zero);
    }

    // Calculations use top-left coordinates. The component only converts to/from RectTransform space.
    internal static class RelationMath
    {
        internal static int Axis(RelationSide side) => side switch
        {
            <= RelationSide.RightRight => 0,
            <= RelationSide.BottomBottom => 1,
            RelationSide.Width => 0,
            RelationSide.Height => 1,
            >= RelationSide.LeftExtLeft and <= RelationSide.RightExtRight => 0,
            _ => 1,
        };

        internal static float OwnPoint(RelationSide side) => side switch
        {
            RelationSide.CenterCenter or RelationSide.MiddleMiddle => .5f,
            RelationSide.RightLeft or RelationSide.RightCenter or RelationSide.RightRight
                or RelationSide.BottomTop or RelationSide.BottomMiddle or RelationSide.BottomBottom => 1,
            <= RelationSide.BottomBottom => 0,
            _ => -1,
        };

        internal static float TargetPoint(RelationSide side) => side switch
        {
            RelationSide.LeftCenter or RelationSide.CenterCenter or RelationSide.RightCenter
                or RelationSide.TopMiddle or RelationSide.MiddleMiddle or RelationSide.BottomMiddle => .5f,
            RelationSide.LeftRight or RelationSide.RightRight or RelationSide.TopBottom or RelationSide.BottomBottom
                or RelationSide.LeftExtRight or RelationSide.RightExtRight or RelationSide.TopExtBottom or RelationSide.BottomExtBottom => 1,
            _ => 0,
        };

        internal static bool IsSize(RelationSide side) => side is RelationSide.Width or RelationSide.Height;

        internal static void SelfSize(ref RelationGeometry owner, Vector2 difference, bool applyPivot, RelationBinding[] bindings)
        {
            foreach (var binding in bindings)
                foreach (var rule in binding.rules)
                {
                    var point = OwnPoint(rule.side);
                    if (point <= 0)
                        continue;
                    var axis = Axis(rule.side);
                    owner.position[axis] -= (point - (applyPivot ? owner.pivot[axis] : 0)) * difference[axis];
                }
        }

        private static void SetSize(ref RelationGeometry owner, int axis, float value, bool ignorePivot, RelationBinding[] bindings)
        {
            var difference = Vector2.zero;
            difference[axis] = value - owner.size[axis];
            var applyPivot = owner.pivotAsAnchor || !ignorePivot;
            if (applyPivot)
                owner.position[axis] -= owner.pivot[axis] * difference[axis];
            owner.size[axis] = value;
            SelfSize(ref owner, difference, applyPivot, bindings);
        }

        internal static void Apply(ref RelationGeometry owner, RelationGeometry previous, RelationGeometry target,
            RelationBinding binding, RelationBinding[] bindings, bool parentTarget, bool ownerIsTargetParent)
        {
            var move = parentTarget ? Vector2.zero : target.origin - previous.origin;
            foreach (var rule in binding.rules)
            {
                var side = rule.side;
                var axis = Axis(side);
                if (OwnPoint(side) >= 0)
                    owner.position[axis] += move[axis];
                else if (!IsSize(side))
                {
                    var minimum = side is RelationSide.LeftExtLeft or RelationSide.LeftExtRight
                        or RelationSide.TopExtTop or RelationSide.TopExtBottom;
                    var before = owner.position[axis];
                    SetSize(ref owner, axis, owner.size[axis] + (minimum ? -move[axis] : move[axis]), false, bindings);
                    if (!ownerIsTargetParent)
                        owner.position[axis] = before + (minimum ? move[axis] : 0);
                }
            }
            if (target.size == previous.size)
                return;
            foreach (var rule in binding.rules)
            {
                var side = rule.side;
                var axis = Axis(side);
                var ratio = previous.size[axis] == 0 ? 0 : target.size[axis] / previous.size[axis];
                var difference = target.size[axis] - previous.size[axis];
                var point = parentTarget ? 0 : target.origin[axis];
                var pivot = !parentTarget && target.pivotAsAnchor ? target.pivot[axis] : 0;
                var ownPoint = OwnPoint(side);
                if (ownPoint >= 0)
                {
                    if (rule.percent)
                    {
                        var edge = owner.position[axis] + ownPoint * owner.size[axis];
                        owner.position[axis] = point + (edge - point) * ratio - ownPoint * owner.size[axis];
                    }
                    else
                        owner.position[axis] += difference * (TargetPoint(side) - pivot);
                }
                else if (IsSize(side))
                {
                    var before = owner.position[axis];
                    var value = target.size[axis] + (owner.size[axis] - previous.size[axis]) * (rule.percent ? ratio : 1);
                    SetSize(ref owner, axis, value, parentTarget, bindings);
                    if (parentTarget && owner.pivotAsAnchor)
                        owner.position[axis] = before;
                }
                else
                {
                    var minimum = side is RelationSide.LeftExtLeft or RelationSide.LeftExtRight
                        or RelationSide.TopExtTop or RelationSide.TopExtBottom;
                    var before = owner.position[axis];
                    if (ownerIsTargetParent && side is RelationSide.RightExtRight or RelationSide.BottomExtBottom)
                    {
                        var value = rule.percent ? point + (owner.size[axis] - point) * ratio
                            : owner.size[axis] + difference * (1 - pivot);
                        SetSize(ref owner, axis, value, false, bindings);
                        continue;
                    }
                    var edge = before + (minimum ? 0 : owner.size[axis]);
                    var delta = rule.percent ? point + (edge - point) * ratio - edge
                        : difference * (TargetPoint(side) - pivot);
                    SetSize(ref owner, axis, owner.size[axis] + (minimum ? -delta : delta), false, bindings);
                    owner.position[axis] = before + (minimum ? delta : 0);
                }
            }
        }
    }
}
