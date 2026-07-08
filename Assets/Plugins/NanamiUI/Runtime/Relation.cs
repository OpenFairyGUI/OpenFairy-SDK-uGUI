using UnityEngine;
using UnityEngine.EventSystems;

namespace NanamiUI
{
    // 复刻 FairyGUI 指向兄弟节点的关联：目标位置/尺寸变化时按增量跟随。
    // 基准状态在 Migrate 构建时序列化，运行时逐帧对比。
    public class Relation : UIBehaviour
    {
        public RectTransform target;
        public string[] sidePairs;
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
            var move = topLeft - lastTopLeft;
            var grow = size - lastSize;
            if (move == Vector2.zero && grow == Vector2.zero)
                return;
            lastTopLeft = topLeft;
            lastSize = size;

            var rt = (RectTransform)transform;
            // X 位置关联 = 目标锚点 x 的变化（左锚 move.x、中锚 +0.5 grow.x、右锚 +grow.x）；
            // Y 位置关联 = 目标锚点 y 的变化（上锚 move.y、中锚 -0.5 grow.y、下锚 -grow.y，y 向下取负 grow）。
            // 复刻 FairyGUI RelationItem 非 percent、pivot=0 的情形；size 关联按 grow 增量跟随（"width"=="width-width"→RelationType.Width）。
            foreach (var pair in sidePairs)
                switch (pair)
                {
                    case "left-left":
                    case "right-left":
                        rt.anchoredPosition += new Vector2(move.x, 0);
                        break;
                    case "left-center":
                    case "center-center":
                    case "right-center":
                        rt.anchoredPosition += new Vector2(move.x + grow.x * 0.5f, 0);
                        break;
                    case "left-right":
                    case "right-right":
                        rt.anchoredPosition += new Vector2(move.x + grow.x, 0);
                        break;
                    case "top-top":
                    case "bottom-top":
                        rt.anchoredPosition += new Vector2(0, move.y);
                        break;
                    case "top-middle":
                    case "middle-middle":
                    case "bottom-middle":
                        rt.anchoredPosition += new Vector2(0, move.y - grow.y * 0.5f);
                        break;
                    case "top-bottom":
                    case "bottom-bottom":
                        rt.anchoredPosition += new Vector2(0, move.y - grow.y);
                        break;
                    case "width":
                    case "width-width":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width + grow.x);
                        break;
                    case "height":
                    case "height-height":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rt.rect.height + grow.y);
                        break;
                    case "leftext-left":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width - move.x);
                        rt.anchoredPosition += new Vector2(move.x, 0);
                        break;
                    case "leftext-right":
                        rt.anchoredPosition += new Vector2(move.x + grow.x, 0);
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width - grow.x - move.x);
                        break;
                    case "rightext-right":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width + grow.x);
                        break;
                }
        }

        protected override void OnEnable() => Record();

        private void LateUpdate() => Sync();
    }
}
