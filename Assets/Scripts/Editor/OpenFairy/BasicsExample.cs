using System;
using System.IO;
using DG.Tweening;
using OpenFairy.UGUI.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace OpenFairy.UGUI.Example
{
    // Basics 示例自己的编辑器胶水（不属于通用导出器 Migrate）：把 BasicsMain 挂到 Main 预制体，
    // 并按 Demo_*.prefab 通用扫描填好各 Demo 引用。通过 [MigratePostProcess] 在每次导出后自动执行。
    public static class BasicsExample
    {
        private const string Root = "Assets/UIProject/Assets/Basics";
        private const string MainPath = Root + "/Main.prefab";

        [MigratePostProcess]
        public static void Configure()
        {
            if (!File.Exists(MainPath))
                return;
            var main = PrefabUtility.LoadPrefabContents(MainPath);
            if (!main.TryGetComponent(out BasicsMain demo))
                demo = main.AddComponent<BasicsMain>();
            var prefabs = Directory.GetFiles(Root, "Demo_*.prefab").AsValueEnumerable()
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/')))
                .Where(prefab => prefab != null)
                .ToArray();
            demo.demoNames = prefabs.AsValueEnumerable().Select(prefab => prefab.name["Demo_".Length..]).ToArray();
            demo.demoPrefabs = prefabs;
            demo.changeSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/images/change.png");
            demo.windowAPrefab = ConfigureWindow<DemoWindow1>($"{Root}/WindowA.prefab");
            demo.windowBPrefab = ConfigureWindow<DemoWindow2>($"{Root}/WindowB.prefab");
            demo.popupMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/popupmenu/PopupMenu.prefab");
            demo.popupItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/popupmenu/PopupMenuItem.prefab");
            demo.popupComPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/components/Component12.prefab");
            PrefabUtility.SaveAsPrefabAsset(main, MainPath);
            PrefabUtility.UnloadPrefabContents(main);
        }

        private static T ConfigureWindow<T>(string path) where T : Window
        {
            var content = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!content.TryGetComponent(out T window)) window = content.AddComponent<T>();
                Transition show = null, hide = null;
                if (window is DemoWindow2 animated)
                {
                    animated.contentTransition = Array.Find(content.GetComponents<Transition>(), t => t.transitionName == "t1");
                    show = Scale(content, "Window.Show", new[] { .1f, .1f }, Vector2.one);
                    hide = Scale(content, "Window.Hide", new[] { float.NaN, float.NaN }, Vector2.one * .1f);
                }
                WindowBuilder.Configure(window, show, hide,
                    content.transform.Find("frame/closeButton").GetComponent<ButtonBase>(),
                    (RectTransform)content.transform.Find("frame/dragArea"));
                content.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(content, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<T>();
        }

        private static Transition Scale(GameObject content, string name, float[] from, Vector2 to)
        {
            var transition = Array.Find(content.GetComponents<Transition>(), value => value.transitionName == name);
            if (transition == null) transition = content.AddComponent<Transition>();
            transition.transitionName = name;
            transition.items = new[] { new TransitionItem {
                type = TransitionItemType.Scale, target = (RectTransform)content.transform,
                tween = true, duration = .3f, ease = Ease.OutQuad,
                start = from, end = new[] { to.x, to.y },
            } };
            return transition;
        }
    }
}
