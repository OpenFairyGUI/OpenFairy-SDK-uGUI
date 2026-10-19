#if FAIRYGUI_TMPRO
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenFairy.UGUI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Schema = OpenFairy.UGUI.Editor.Schema;
using Object = UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public static class CanvasResumeFixtureBuilder
    {
        private const BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;
        private static string _folder;
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size; return rect;
        }
        private static void Layout(RectTransform rect, RectTransform reference, RectTransform parent, Vector2 position, Vector2 size, Vector2 pivot, params RelationSide[] sides)
        {
            var rules = Array.ConvertAll(sides, side => new RelationRule { side = side });
            var display = new Schema.Display { Position = position, Pivot = pivot, Anchor = true,
                Relations = sides.Length == 0 ? Array.Empty<Schema.Relation>() : new[] { new Schema.Relation { Target = "", Rules = rules } } };
            typeof(Migrate).GetMethod("SetRect", Methods).Invoke(null, new object[] { rect, display, size, reference });
            if (parent != reference) typeof(Migrate).GetMethod("ReparentPreservingRect", Methods).Invoke(null, new object[] { rect, parent, true });
            typeof(Migrate).GetMethod("BuildRelations", Methods).Invoke(null, new object[] { reference, reference, display, rect.gameObject, new Dictionary<string, (Schema.Display Xml, GameObject Go)>() });
        }
        private static void ChildRelation(RectTransform owner, RectTransform reference, RectTransform target, RelationSide side)
        {
            var relation = owner.gameObject.AddComponent<Relation>();
            relation.reference = reference; relation.coordinateSpace = reference; relation.pivotAsAnchor = true;
            relation.bindings = new[] { new RelationBinding { target = target, pivotAsAnchor = true, rules = new[] { new RelationRule { side = side } } } };
            relation.Record();
        }
        private static RectTransform Item(string name, RectTransform strip, float right, float pivot)
        {
            var rect = Rect(name, strip, new Vector2(240, 70));
            Layout(rect, strip, strip, new Vector2(right-(1-pivot)*240, 10), rect.sizeDelta, new Vector2(pivot, 0), name == "Second" ? new[] { RelationSide.RightRight, RelationSide.TopTop } : Array.Empty<RelationSide>());
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.1f,.3f,.6f,.8f);
            var title = Rect("Caption", rect, new Vector2(140, 30)); title.pivot = new Vector2(0,.5f); title.anchoredPosition = new Vector2(44,-22);
            var text = title.gameObject.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24; text.textWrappingMode = TextWrappingModes.NoWrap; text.text = "123456789";
            var fitter = title.gameObject.AddComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ChildRelation(rect, rect, title, RelationSide.RightExtRight);
            var subtitle = Rect("Subtitle", rect, new Vector2(60,20)); subtitle.pivot = new Vector2(.5f,.5f); subtitle.anchoredPosition = new Vector2(114,-54);
            subtitle.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.yellow;
            ChildRelation(subtitle, rect, title, RelationSide.CenterCenter);
            return rect;
        }
        private static RectTransform Strip(string name, RectTransform root, RectTransform group, float y, bool bottom, float pivot)
        {
            var strip = Rect(name, root, new Vector2(2560,100));
            Layout(strip, root, group, new Vector2(0,y), strip.sizeDelta, new Vector2(0,0), bottom ? new[] { RelationSide.Width, RelationSide.BottomBottom } : new[] { RelationSide.Width });
            var first = Item("First", strip, 2268, pivot);
            var second = Item("Second", strip, 2528, pivot);
            ChildRelation(first, strip, second, RelationSide.RightLeft);
            return strip;
        }
        public static GameObject Build()=>BuildWithPivot(1);
        public static GameObject BuildWithPivot(float pivot)
        {
            if (_folder != null) throw new InvalidOperationException("The isolated fixture is still owned by another test.");
            var name="__CanvasResume_"+Guid.NewGuid().ToString("N");
            _folder="Assets/"+name;
            AssetDatabase.CreateFolder("Assets", name);
            var canvas = new GameObject("BakeCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var root = Rect("Panel", canvas.transform, new Vector2(2560,1440));
            try
            {
                var group = Rect("Group", root, root.sizeDelta);
                Strip("Top", root, group, 0, false, pivot);
                Strip("Bottom", root, group, 1340, true, pivot);
                var corner = Rect("Corner", root, new Vector2(200,100)); corner.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.green;
                Layout(corner,root,group,new Vector2(2528,1308),corner.sizeDelta,new Vector2(1,0),RelationSide.RightRight,RelationSide.BottomBottom);
                foreach(var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true,true);
                foreach(var fitter in root.GetComponentsInChildren<ContentSizeFitter>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)fitter.transform);
                for(var pass=0;pass<6;pass++) foreach(var relation in root.GetComponentsInChildren<Relation>()) relation.Sync();
                foreach(var relation in root.GetComponentsInChildren<Relation>()) relation.Record();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,_folder+"/Panel.prefab",out var success);
                if(!success||prefab==null)throw new InvalidOperationException("Failed to save the relation fixture.");
                return prefab;
            }
            finally{Object.DestroyImmediate(canvas);}
        }
        public static void Delete()
        {
            if(_folder==null)return;
            AssetDatabase.DeleteAsset(_folder);_folder=null;
        }

        public static IDisposable FixedGameView()=>new FixedSize();
        private sealed class FixedSize:IDisposable
        {
            private readonly object _group; private readonly EditorWindow _view; private readonly PropertyInfo _selected;
            private readonly int _previous,_index;
            public FixedSize()
            {
                var assembly=typeof(UnityEditor.Editor).Assembly;
                var sizesType=assembly.GetType("UnityEditor.GameViewSizes",true);
                var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
                var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType",true);
                _group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(groupType,"Standalone")});
                var sizeType=assembly.GetType("UnityEditor.GameViewSize",true);
                var kind=assembly.GetType("UnityEditor.GameViewSizeType",true);
                var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(kind,"FixedResolution"),(object)1920,1080,"Canvas relation test"});
                _index=(int)_group.GetType().GetMethod("GetBuiltinCount").Invoke(_group,null)+(int)_group.GetType().GetMethod("GetCustomCount").Invoke(_group,null);
                _group.GetType().GetMethod("AddCustomSize").Invoke(_group,new[]{size});
                _view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView",true));
                _selected=_view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                _previous=(int)_selected.GetValue(_view); _selected.SetValue(_view,_index);_view.Repaint();
            }
            public void Dispose()
            {
                _selected.SetValue(_view,_previous);
                _group.GetType().GetMethod("RemoveCustomSize").Invoke(_group,new object[]{_index});
            }
        }
    }
}
#endif
