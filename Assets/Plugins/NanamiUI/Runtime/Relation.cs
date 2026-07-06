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
            lastTopLeft = TopLeft(target);
            lastSize = target.rect.size;
        }

        public void Sync()
        {
            var topLeft = TopLeft(target);
            var size = target.rect.size;
            var move = topLeft - lastTopLeft;
            var grow = size - lastSize;
            if (move == Vector2.zero && grow == Vector2.zero)
                return;
            lastTopLeft = topLeft;
            lastSize = size;

            var rt = (RectTransform)transform;
            foreach (var pair in sidePairs)
                switch (pair)
                {
                    case "left-left":
                        rt.anchoredPosition += new Vector2(move.x, 0);
                        break;
                    case "top-top":
                        rt.anchoredPosition += new Vector2(0, move.y);
                        break;
                    case "right-right":
                        rt.anchoredPosition += new Vector2(move.x + grow.x, 0);
                        break;
                    case "bottom-bottom":
                        rt.anchoredPosition += new Vector2(0, move.y - grow.y);
                        break;
                    case "width-width":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width + grow.x);
                        break;
                    case "height-height":
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rt.rect.height + grow.y);
                        break;
                    case "leftext-right":
                        rt.anchoredPosition += new Vector2(move.x + grow.x, 0);
                        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rt.rect.width - grow.x - move.x);
                        break;
                }
        }

        protected override void OnEnable() => Record();

        private void LateUpdate() => Sync();
    }
}
