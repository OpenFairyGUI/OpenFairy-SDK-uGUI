using System.IO;
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
            demo.windowAPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/WindowA.prefab");
            demo.windowBPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/WindowB.prefab");
            demo.popupMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/popupmenu/PopupMenu.prefab");
            demo.popupItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/popupmenu/PopupMenuItem.prefab");
            demo.popupComPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/components/Component12.prefab");
            PrefabUtility.SaveAsPrefabAsset(main, MainPath);
            PrefabUtility.UnloadPrefabContents(main);
        }
    }
}
