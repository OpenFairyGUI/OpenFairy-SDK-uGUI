#if UNITY_EDITOR && FAIRYGUI_TMPRO
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace OpenFairy.UGUI.Tests
{
    public sealed class CanvasResumeTests
    {
        [Serializable] private sealed class RectState { public string path;public Vector2 size,position;public string[] relations; }
        [Serializable] private sealed class Frame { public string stage;public int screenWidth,screenHeight;public float scale;public RectState[] rects; }
        [Serializable] private sealed class Receipt { public List<Frame> frames=new(); }
        private IDisposable _screen;
        private GameObject _canvas;
        private Receipt _receipt;
        private string _caseName;
        [TearDown]
        public void Cleanup()
        {
            if(_receipt!=null)
            {
                var folder=Path.Combine(Path.GetTempPath(),"OpenFairyCanvasResume");Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,_caseName+".json"),JsonUtility.ToJson(_receipt,true));
            }
            if(_canvas!=null)Object.DestroyImmediate(_canvas);
            Type.GetType("OpenFairy.UGUI.Tests.CanvasResumeFixtureBuilder, OpenFairy.UGUI.Tests.Editor",true).GetMethod("Delete").Invoke(null,null);
            _screen?.Dispose();_screen=null;_receipt=null;_canvas=null;
        }
        private static RectTransform Stretch(string name,Transform parent)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(0,1);
            rect.sizeDelta=rect.anchoredPosition=Vector2.zero;return rect;
        }
        private static Rect Local(RectTransform rect,RectTransform reference)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var top=reference.InverseTransformPoint(corners[1]);var bottom=reference.InverseTransformPoint(corners[3]);
            return new Rect(top.x-reference.rect.xMin,reference.rect.yMax-top.y,bottom.x-top.x,top.y-bottom.y);
        }
        private static void Geometry(RectTransform root)
        {
            foreach(var name in new[]{"Top","Bottom"})
            {
                var strip=(RectTransform)root.Find("Group/"+name);var frame=Local(strip,root);
                Assert.That(frame.width,Is.EqualTo(root.rect.width).Within(.05f),name+" width");
                Assert.That(frame.x,Is.EqualTo(0).Within(.05f),name+" left");
                if(name=="Bottom")Assert.That(root.rect.height-frame.yMax,Is.EqualTo(0).Within(.05f),"Bottom margin");
                var first=(RectTransform)strip.Find("First");var second=(RectTransform)strip.Find("Second");
                var a=Local(first,strip);var b=Local(second,strip);
                Assert.That(strip.rect.width-b.xMax,Is.EqualTo(32).Within(.05f),"Right margin");
                Assert.That(b.xMin-a.xMax,Is.EqualTo(20).Within(.05f),"Sibling margin");
                foreach(var item in new[]{first,second})
                {
                    var title=(RectTransform)item.Find("Caption");var subtitle=(RectTransform)item.Find("Subtitle");
                    Assert.That(title.rect.width,Is.EqualTo(title.GetComponent<TMP_Text>().preferredWidth).Within(.05f),"Native fitted text width");
                    Assert.That(item.rect.width-title.rect.width,Is.EqualTo(100).Within(.05f),"Content padding");
                    Assert.That(Local(title,item).center.x,Is.EqualTo(Local(subtitle,item).center.x).Within(.05f),"Child center");
                }
            }
            var corner=Local((RectTransform)root.Find("Group/Corner"),root);
            Assert.That(root.rect.width-corner.xMax,Is.EqualTo(32).Within(.05f),"Corner right");
            Assert.That(root.rect.height-corner.yMax,Is.EqualTo(32).Within(.05f),"Corner bottom");
        }
        private static void Record(Receipt receipt,string stage,Canvas canvas,RectTransform root)
        {
            var rects=new List<RectState>();
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                var relative=Local(rect,root);
                var relations=Array.ConvertAll(rect.GetComponents<Relation>(),relation=>JsonUtility.ToJson(relation));
                rects.Add(new RectState{path=AnimationUtility.CalculateTransformPath(rect,root),size=rect.rect.size,position=relative.position,relations=relations});
            }
            receipt.frames.Add(new Frame{stage=stage,screenWidth=Screen.width,screenHeight=Screen.height,scale=canvas.scaleFactor,rects=rects.ToArray()});
        }
        private static IEnumerator Stable(Receipt receipt,string stage,Canvas canvas,RectTransform root)
        {
            for(var frame=0;frame<8;frame++){yield return null;Record(receipt,stage+"-"+frame,canvas,root);}
            Geometry(root);
        }
        [UnityTest] public IEnumerator Overlay_canvas_suspend_resume_preserves_grouped_relations()=>Run(false);
        [UnityTest] public IEnumerator Overlay_canvas_resume_applies_real_hidden_parent_and_content_changes()=>Run(true);
        [UnityTest] public IEnumerator Native_parent_resize_and_fitted_width_changes_preserve_all_pivots_in_same_and_different_frames()
        {
            foreach(var pivot in new[]{0f,.5f,1f})yield return Run(false,pivot,true);
        }
        private IEnumerator Run(bool changeHidden,float pivot=1,bool activeChanges=false)
        {
            var type=Type.GetType("OpenFairy.UGUI.Tests.CanvasResumeFixtureBuilder, OpenFairy.UGUI.Tests.Editor",true);
            _screen=(IDisposable)type.GetMethod("FixedGameView").Invoke(null,null);
            _caseName=activeChanges?"native-pivot-"+pivot.ToString(System.Globalization.CultureInfo.InvariantCulture):changeHidden?"hidden-change":"resume";
            var receipt=_receipt=new Receipt();GameObject canvasObject=null;GameObject instance=null;
            try
            {
                yield return null;yield return null;
                Assert.That(Screen.width,Is.EqualTo(1920));Assert.That(Screen.height,Is.EqualTo(1080));
                canvasObject=_canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(2560,1440);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                yield return null;yield return null;
                var first=Stretch("FirstStretch",canvasObject.transform);var second=Stretch("SecondStretch",first);
                var prefab=(GameObject)(activeChanges?type.GetMethod("BuildWithPivot").Invoke(null,new object[]{pivot}):type.GetMethod("Build").Invoke(null,null));
                instance=Object.Instantiate(prefab,second,false);
                var root=(RectTransform)instance.transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.pivot=new Vector2(0,1);root.sizeDelta=root.anchoredPosition=Vector2.zero;
                Assert.That(root.Find("Group/Top").GetComponent<Relation>().bindings[0].nativeAnchors,Is.False,"Grouping requires runtime parent-size rules.");
                yield return Stable(receipt,"initial",canvas,root);
                if(activeChanges)
                {
                    for(var step=0;step<3;step++)
                    {
                        scaler.referenceResolution+=step==2?new Vector2(-512,-288):new Vector2(256,144);
                        if(step!=1)foreach(var title in root.GetComponentsInChildren<TMP_Text>(true))title.text=step==0?"12":"1234567890123";
                        yield return Stable(receipt,"parent-"+step,canvas,root);
                        if(step==1)
                        {
                            foreach(var title in root.GetComponentsInChildren<TMP_Text>(true))title.text="123456";
                            yield return Stable(receipt,"content-"+step,canvas,root);
                        }
                    }
                    yield break;
                }
                for(var round=0;round<3;round++)
                {
                    canvasObject.SetActive(false);Record(receipt,"disabled-"+round,canvas,root);
                    yield return null;Record(receipt,"hidden-"+round,canvas,root);
                    if(changeHidden)
                    {
                        scaler.referenceResolution+=new Vector2(256,144);
                        foreach(var title in root.GetComponentsInChildren<TMP_Text>(true))title.text=round%2==0?"12":"1234567890123";
                    }
                    yield return null;
                    canvasObject.SetActive(true);Record(receipt,"enabled-"+round,canvas,root);
                    yield return Stable(receipt,"resumed-"+round,canvas,root);
                }
            }
            finally
            {
                Cleanup();
            }
        }
    }
}
#endif
