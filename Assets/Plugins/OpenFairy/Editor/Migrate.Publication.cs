using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Editor
{
    public static partial class Migrate
    {
        private static void BuildPrefabs(IReadOnlyList<Resource> components)
        {
            var levels = new List<List<Resource>>();
            foreach (var component in components)
            {
                while (levels.Count <= component.BuildLevel) levels.Add(new List<Resource>());
                levels[component.BuildLevel].Add(component);
            }
            foreach (var level in levels)
            {
                var roots = new List<(Resource Resource, GameObject Root)>();
                try
                {
                    // Fresh materials, sprite sub-assets and other native dependencies must be
                    // imported before asset editing is paused. Only independent saves are batched.
                    foreach (var component in level)
                        roots.Add((component, CreatePrefab(component)));
                    using (new AssetDatabase.AssetEditingScope())
                        foreach (var (resource, root) in roots)
                        {
                            var path = Path.ChangeExtension(AssetPath(resource.File), ".prefab");
                            PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                            if (!saved)
                                throw new InvalidOperationException($"OpenFairy failed to save prefab '{path}'.");
                        }
                    // SaveAsPrefabAsset may return null while imports are paused. Never place that
                    // temporary result in the cache used to instantiate the next dependency level.
                    foreach (var (resource, _) in roots)
                    {
                        var path = Path.ChangeExtension(AssetPath(resource.File), ".prefab");
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null)
                            throw new InvalidOperationException($"OpenFairy failed to import saved prefab '{path}'.");
                        Prefabs[path] = prefab;
                    }
                }
                finally
                {
                    foreach (var (_, root) in roots) Object.DestroyImmediate(root);
                }
            }
        }
    }
}
