using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    // Parent constraints that are affine in the parent's size become native anchors.
    internal static class RelationAnchorBaker
    {
        internal static bool TryBake(Schema.Display element, Vector2 size, Vector2 parentSize,
            out Vector2 minimum, out Vector2 maximum)
        {
            minimum = maximum = new Vector2(0, 1);
            var resultMin = minimum;
            var resultMax = maximum;
            if (element.LoaderAutoSize || element.Relations.Length != 1 || element.Relations[0].Target != "")
                return false;
            var rules = element.Relations[0].Rules;
            var position = element.Position ?? Vector2.zero;
            if (element.Anchor)
                position -= Vector2.Scale(element.Pivot ?? Vector2.zero, size);
            for (var axis = 0; axis < 2; axis++)
            {
                var alignment = -1;
                var sizing = -1;
                for (var i = 0; i < rules.Length; i++)
                {
                    var side = rules[i].side;
                    if (RelationMath.Axis(side) != axis)
                        continue;
                    if (RelationMath.IsSize(side))
                    {
                        if (sizing >= 0) return false;
                        sizing = i;
                    }
                    else if (RelationMath.OwnPoint(side) >= 0)
                    {
                        if (alignment >= 0) return false;
                        alignment = i;
                    }
                    else return false;
                }
                if ((sizing >= 0 && rules[sizing].percent || alignment >= 0 && rules[alignment].percent)
                    && parentSize[axis] == 0)
                    return false;
                var scale = sizing < 0 ? 0 : rules[sizing].percent ? size[axis] / parentSize[axis] : 1;
                var move = 0f;
                if (alignment >= 0)
                {
                    var rule = rules[alignment];
                    var ownPoint = RelationMath.OwnPoint(rule.side);
                    // With an anchored pivot, size-before-percent-alignment can be quadratic.
                    if (element.Anchor && ownPoint > 0 && rule.percent && sizing >= 0 && sizing < alignment && scale != 0)
                        return false;
                    move = rule.percent ? (position[axis] + ownPoint * size[axis]) / parentSize[axis]
                        : RelationMath.TargetPoint(rule.side);
                    if (!element.Anchor)
                        move -= ownPoint * scale;
                }
                if (axis == 0)
                {
                    resultMin.x = move;
                    resultMax.x = move + scale;
                }
                else
                {
                    resultMin.y = 1 - move - scale;
                    resultMax.y = 1 - move;
                }
            }
            minimum = resultMin;
            maximum = resultMax;
            return true;
        }
    }
}
