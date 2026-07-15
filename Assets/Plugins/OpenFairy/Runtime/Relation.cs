using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    // FairyGUI RelationType 的 XML 词表（sidePair="left-left" 等，解析时去标点折叠）。
    // Width/WidthWidth、Height/HeightHeight 是同一语义的两种 XML 拼写，行为一致。
    public enum RelationSide
    {
        LeftLeft,
        LeftCenter,
        LeftRight,
        CenterCenter,
        RightLeft,
        RightCenter,
        RightRight,
        TopTop,
        TopMiddle,
        TopBottom,
        MiddleMiddle,
        BottomTop,
        BottomMiddle,
        BottomBottom,
        Width,
        WidthWidth,
        Height,
        HeightHeight,
        Size,
        LeftExtLeft,
        LeftExtRight,
        RightExtLeft,
        RightExtRight,
        TopExtTop,
        TopExtBottom,
        BottomExtTop,
        BottomExtBottom,
    }

    // 复刻 FairyGUI 指向兄弟节点的关联：目标位置/尺寸变化时按增量跟随。
    // 基准状态在 Migrate 构建时序列化，运行时逐帧对比。
    // 复刻 RelationItem 非 percent、pivot=0 的情形（ApplyOnXYChanged + ApplyOnSizeChanged 合并成 move/grow 增量）。
    public class Relation : UIBehaviour
    {
        public RectTransform target;
        public RelationSide[] sidePairs;
        public Vector2 lastTopLeft;
        public Vector2 lastSize;

        public static Vector2 TopLeft(RectTransform rt) =>
            (Vector2)rt.localPosition + new Vector2(rt.rect.xMin, rt.rect.yMax);

        public void Record()
        {
            if (target == null) // AddComponent 时 OnEnable 先于字段注入
                return;
            lastTopLeft = TopLeft(target);
            lastSize = target.rect.size;
        }

        public void Sync()
        {
            if (target == null)
                return;
            var topLeft = TopLeft(target);
            var size = target.rect.size;
            var move = topLeft - lastTopLeft; // uGUI 本地系（y 向上）
            var grow = size - lastSize;
            if (move == Vector2.zero && grow == Vector2.zero)
                return;
            lastTopLeft = topLeft;
            lastSize = size;

            var rt = (RectTransform)transform;
            var position = Vector2.zero;
            var resize = Vector2.zero;
            // X 位置关联 = 目标锚点 x 的变化（左锚 move.x、中锚 +0.5 grow.x、右锚 +grow.x）；
            // Y 位置关联 = 目标锚点 y 的变化（上锚 move.y、中锚 -0.5 grow.y、下锚 -grow.y，y 向下取负 grow）。
            // ext 关联 = 本体一边跟随目标、对边保持不动（尺寸随之伸缩）。
            foreach (var pair in sidePairs)
                switch (pair)
                {
                    case RelationSide.LeftLeft:
                    case RelationSide.RightLeft:
                        position.x += move.x;
                        break;
                    case RelationSide.LeftCenter:
                    case RelationSide.CenterCenter:
                    case RelationSide.RightCenter:
                        position.x += move.x + grow.x * 0.5f;
                        break;
                    case RelationSide.LeftRight:
                    case RelationSide.RightRight:
                        position.x += move.x + grow.x;
                        break;
                    case RelationSide.TopTop:
                    case RelationSide.BottomTop:
                        position.y += move.y;
                        break;
                    case RelationSide.TopMiddle:
                    case RelationSide.MiddleMiddle:
                    case RelationSide.BottomMiddle:
                        position.y += move.y - grow.y * 0.5f;
                        break;
                    case RelationSide.TopBottom:
                    case RelationSide.BottomBottom:
                        position.y += move.y - grow.y;
                        break;
                    case RelationSide.Width:
                    case RelationSide.WidthWidth:
                        resize.x += grow.x;
                        break;
                    case RelationSide.Height:
                    case RelationSide.HeightHeight:
                        resize.y += grow.y;
                        break;
                    case RelationSide.Size:
                        resize += grow;
                        break;
                    case RelationSide.LeftExtLeft: // 左边跟随目标左边，右边不动
                        resize.x -= move.x;
                        position.x += move.x;
                        break;
                    case RelationSide.LeftExtRight: // 左边跟随目标右边，右边不动
                        position.x += move.x + grow.x;
                        resize.x -= grow.x + move.x;
                        break;
                    case RelationSide.RightExtLeft: // 右边跟随目标左边，左边不动
                        resize.x += move.x;
                        break;
                    case RelationSide.RightExtRight: // 右边跟随目标右边，左边不动
                        resize.x += move.x + grow.x;
                        break;
                    case RelationSide.TopExtTop: // 上边跟随目标上边，下边不动
                        resize.y += move.y;
                        position.y += move.y;
                        break;
                    case RelationSide.TopExtBottom: // 上边跟随目标下边，下边不动
                        resize.y += move.y - grow.y;
                        position.y += move.y - grow.y;
                        break;
                    case RelationSide.BottomExtTop: // 下边跟随目标上边，上边不动
                        resize.y -= move.y;
                        break;
                    case RelationSide.BottomExtBottom: // 下边跟随目标下边，上边不动
                        resize.y += grow.y - move.y;
                        break;
                }
            if (position != Vector2.zero)
                rt.anchoredPosition += position;
            var rectSize = rt.rect.size;
            if (resize.x != 0)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rectSize.x + resize.x);
            if (resize.y != 0)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rectSize.y + resize.y);
        }

        // 集中驱动：单个循环遍历所有活跃 Relation，替代每实例每帧一次 LateUpdate 的 native→managed 派发底噪。
        private static readonly List<Relation> Active = new();
        private static bool _running;

        protected override void OnEnable()
        {
            Record();
            Active.Add(this);
            if (!_running && Application.isPlaying)
                RunLoop().Forget();
        }

        protected override void OnDisable() => Active.Remove(this);

        private static async UniTask RunLoop()
        {
            _running = true;
            while (Active.Count > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                for (var i = Active.Count - 1; i >= 0; i--) // 逆序：Sync 连锁触发的 OnDisable 移除不打乱遍历
                    Active[i].Sync();
            }
            _running = false;
        }
    }
}
