using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI.TestSupport
{
    // 交互测试的核心信号：把一棵子树的每个节点记成"相对交互目标左上角、页面像素、y 向下"的几何 + 少量视觉标量。
    // 参照来自 FairyGUI（由 BasicsRenderDiff 用 GObject 侧构建），实测来自 NanamiUI（这里从 RectTransform 构建）。
    // 因为静态渲染 parity 已单独保证"任意状态渲成像素 = FairyGUI"，交互只需证明"到达了正确状态"，故比几何不比像素。
    // 坐标一律相对子树根的左上角，避免依赖画布世界摆位与 y 翻转的跨引擎歧义。
    public sealed class GeometrySnapshot
    {
        [System.Serializable]
        public struct Node
        {
            public string path;         // 相对子树根的名字路径，根本身为 ""
            public float x, y, w, h;     // 相对根左上角、页面像素、y 向下
            public bool active;
            public string text;          // 无文本为 ""
        }

        public List<Node> nodes = new();

        // 从 NanamiUI 实例的子树构建。rig 下 CanvasScaler=ConstantPixelSize/1 且 WorldSpace，故 1 world unit = 1px。
        public static GeometrySnapshot FromNanami(Transform root)
        {
            var snapshot = new GeometrySnapshot();
            var corners = new Vector3[4];
            ((RectTransform)root).GetWorldCorners(corners);
            var originX = corners[1].x;   // 根左上角 world x
            var originY = corners[1].y;   // 根左上角 world y（world y 向上）
            Walk(root, root, originX, originY, corners, snapshot.nodes);
            return snapshot;
        }

        private static void Walk(Transform node, Transform root, float originX, float originY, Vector3[] corners, List<Node> nodes)
        {
            var rt = node as RectTransform;
            if (rt != null)
            {
                rt.GetWorldCorners(corners);
                var xMin = corners[0].x;
                var xMax = corners[2].x;
                var yTop = corners[1].y;  // world y 向上，top 是较大值
                var yBottom = corners[0].y;
                nodes.Add(new Node
                {
                    path = Path(node, root),
                    x = xMin - originX,
                    y = originY - yTop,   // world y 向上 → 相对根左上、y 向下
                    w = xMax - xMin,
                    h = yTop - yBottom,
                    active = node.gameObject.activeSelf, // 对应 FairyGUI 的本地 visible 语义（非 activeInHierarchy）
                    text = (node.GetComponent<TextField>() is { } t) ? t.text : "",
                });
            }
            for (var i = 0; i < node.childCount; i++)
                Walk(node.GetChild(i), root, originX, originY, corners, nodes);
        }

        private static string Path(Transform node, Transform root)
        {
            if (node == root)
                return "";
            var path = node.name;
            for (var p = node.parent; p != null && p != root; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        public string ToJson() => JsonUtility.ToJson(new Wrapper { nodes = nodes }, true);

        public void Save(string path)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, ToJson());
        }

        public static GeometrySnapshot Load(string path)
        {
            var wrapper = JsonUtility.FromJson<Wrapper>(File.ReadAllText(path));
            return new GeometrySnapshot { nodes = wrapper.nodes };
        }

        // FairyGUI 参照是规格：要求 参照 ⊆ 实测（每个 FairyGUI 节点都在 NanamiUI 存在且几何/显隐/文本一致）。
        // NanamiUI 侧多出的节点（描边/阴影/遮罩等渲染辅助，FairyGUI 无对应）被忽略。超 epsilon/不一致即返回失败信息。
        public string Compare(GeometrySnapshot reference, float epsilonPx)
        {
            var mine = new Dictionary<string, Node>();
            foreach (var n in nodes)
                mine[n.path] = n;

            foreach (var b in reference.nodes)
            {
                if (!mine.TryGetValue(b.path, out var a))
                    return $"node '{b.path}' present in FairyGUI reference but missing in NanamiUI";
                if (a.active != b.active)
                    return $"node '{b.path}' active {a.active} != FairyGUI {b.active}";
                if (a.text != b.text)
                    return $"node '{b.path}' text '{a.text}' != FairyGUI '{b.text}'";
                if (Mathf.Abs(a.x - b.x) > epsilonPx || Mathf.Abs(a.y - b.y) > epsilonPx ||
                    Mathf.Abs(a.w - b.w) > epsilonPx || Mathf.Abs(a.h - b.h) > epsilonPx)
                    return $"node '{b.path}' rect ({a.x:F1},{a.y:F1},{a.w:F1},{a.h:F1}) != FairyGUI ({b.x:F1},{b.y:F1},{b.w:F1},{b.h:F1}) beyond {epsilonPx}px";
            }
            return null;
        }

        [System.Serializable]
        private struct Wrapper
        {
            public List<Node> nodes;
        }
    }
}
