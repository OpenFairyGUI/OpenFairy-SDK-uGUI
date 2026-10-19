using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Schema = OpenFairy.UGUI.Editor.Schema;

namespace OpenFairy.UGUI.Editor
{
    public static partial class Migrate
    {
        public static int RepairFilterPresentations(bool dryRun = false)
        {
            IndexSources();
            var ordered = new List<Resource>();
            var images = new HashSet<Resource>();
            foreach (var resource in Resources.Values)
                if (resource.Type == Schema.ResourceKind.Component
                    && File.Exists(Path.ChangeExtension(AssetPath(resource.File), ".prefab")))
                    Collect(resource, ordered, images);
            var total = 0;
            var changed = 0;
            foreach (var resource in ordered)
            {
                var path = Path.ChangeExtension(AssetPath(resource.File), ".prefab");
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var count = ObjectFilterBuilder.RepairPresentations(root, !dryRun);
                    if (count == 0) continue;
                    if (!dryRun)
                    {
                        var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                        if (!success || saved == null)
                            throw new InvalidOperationException($"OpenFairy failed to save repaired filter presentations in '{path}'.");
                    }
                    total += count;
                    changed++;
                    Debug.Log($"OpenFairy {(dryRun ? "would repair" : "repaired")} {count} filter presentations in '{path}'.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log($"OpenFairy {(dryRun ? "previewed" : "repaired")} {total} filter presentations in {changed} published prefabs.");
            return total;
        }
    }
}
