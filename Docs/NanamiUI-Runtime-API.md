# NanamiUI.Runtime API Reference

本文用 C# 声明式摘要描述 `NanamiUI` 运行时公开 API。声明省略 private/internal 实现细节，保留 public 类型、字段、属性、方法和少量用途注释；它是阅读用 reference，不是可直接编译的源码。

命名空间：`NanamiUI`

典型入口：

1. 通过 `Tools/NanamiUI/Migrate` 生成 prefab 和 C# 组件类。
2. 在生成类字段上访问子控件，例如 `m_btn_OK`、`m_title`。
3. 用 `ButtonBase`、`Text`、`Slider`、`ProgressBar`、`Transition` 等基础运行时类型驱动 UI。
4. 弹窗、窗口、拖拽需要先创建 `GRoot` 覆盖层。

## Component / Controller / Gear

```csharp
namespace NanamiUI
{
    // 生成组件的默认基类。
    public class Component : UIBehaviour
    {
    }

    // 生成组件中的 controller 字段。T 是生成类内的 page enum。
    [Serializable]
    public struct Controller<T> where T : struct, Enum
    {
        public Gear<T>[] gears;

        // 设置 page 会应用所有 gear；运行时 tween gear 会播放动画。
        public T page { get; set; }
    }

    [Serializable]
    public abstract class Gear<T> where T : struct, Enum
    {
        public GameObject target;
        public T[] pages;

        public bool tween;
        public float duration;
        public Ease ease;
        public float delay;

        public abstract void Apply(T page);
        public virtual void Apply(T page, bool animate);
    }

    public interface IDisplayGear
    {
        bool On { get; }
        int Condition { get; }
        GameObject Target { get; }
        void AddLock();
        void ReleaseLock();
    }

    [Serializable]
    public class GearDisplay<T> : Gear<T>, IDisplayGear where T : struct, Enum
    {
        public int condition;
        public IDisplayGear partner;
        public bool on;

        public bool On { get; }
        public int Condition { get; }
        public GameObject Target { get; }

        public void AddLock();
        public void ReleaseLock();
        public override void Apply(T page);
    }

    [Serializable]
    public class GearXY<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] values;
        public Vector2 defaultValue;

        public override void Apply(T page);
        public override void Apply(T page, bool animate);
    }

    [Serializable]
    public class GearSize<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] sizes;
        public Vector2 defaultSize;
        public Vector2[] scales;
        public Vector2 defaultScale;

        public override void Apply(T page);
        public override void Apply(T page, bool animate);
    }

    [Serializable]
    public class GearLook<T> : Gear<T> where T : struct, Enum
    {
        public float[] alphas;
        public float defaultAlpha;
        public float[] rotations;
        public float defaultRotation;
        public bool[] grayed;
        public bool defaultGrayed;

        public override void Apply(T page);
        public override void Apply(T page, bool animate);
    }

    [Serializable] public class GearColor<T> : Gear<T> where T : struct, Enum
    {
        public Color[] values;
        public Color defaultValue;
        public override void Apply(T page);
    }

    [Serializable] public class GearText<T> : Gear<T> where T : struct, Enum
    {
        public string[] values;
        public string defaultValue;
        public override void Apply(T page);
    }

    [Serializable] public class GearIcon<T> : Gear<T> where T : struct, Enum
    {
        public Sprite[] values;
        public Sprite defaultValue;
        public override void Apply(T page);
    }

    [Serializable] public class GearFontSize<T> : Gear<T> where T : struct, Enum
    {
        public int[] values;
        public int defaultValue;
        public override void Apply(T page);
    }

    [Serializable] public class GearAni<T> : Gear<T> where T : struct, Enum
    {
        public int[] frames;
        public bool[] playings;
        public int defaultFrame;
        public bool defaultPlaying;
        public override void Apply(T page);
    }
}
```

## Buttons / ComboBox

```csharp
namespace NanamiUI
{
    public enum ButtonMode
    {
        Common,
        Check,
        Radio,
    }

    // 非泛型按钮面，方便业务代码不关心生成 enum。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick;
        public abstract string Title { get; set; }
    }

    // FairyGUI GButton 对应物。T 是按钮 controller 的 page enum。
    public abstract class Button<T> : ButtonBase,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
        where T : struct, Enum
    {
        public Controller<T> controller;
        public Text titleText;
        public Loader iconLoader;
        public ButtonMode mode;
        public bool selected;
        public bool grayed;
        public string selectedTitle;

        public override string Title { get; set; }
        public Sprite Icon { get; set; }

        public void OnPointerDown(PointerEventData eventData);
        public void OnPointerUp(PointerEventData eventData);
        public void OnPointerEnter(PointerEventData eventData);
        public void OnPointerExit(PointerEventData eventData);
        public virtual void OnPointerClick(PointerEventData eventData);
        public void RefreshState();
    }

    // FairyGUI GComboBox 对应物。点击按钮后用 GRoot 显示 dropdownPrefab。
    public abstract class ComboBox<T> : Button<T> where T : struct, Enum
    {
        public string[] items;
        public GameObject dropdownPrefab;
        public int selectedIndex;
        public UnityEvent onChanged;

        public override void OnPointerClick(PointerEventData eventData);
    }
}
```

## Progress / Slider

```csharp
namespace NanamiUI
{
    public enum ProgressTitleType
    {
        Percent,
        ValueAndMax,
        Value,
        Max,
    }

    public class ProgressBar : Component
    {
        public float value;
        public float max;
        public float min;
        public ProgressTitleType titleType;
        public bool reverse;

        public Text title;
        public RectTransform bar;
        public RectTransform barV;
        public MovieClip ani;

        public float barMaxWidthDelta;
        public float barMaxHeightDelta;
        public float barStartX;
        public float barStartY;

        public void Apply();
    }

    public class Slider : Component, IPointerDownHandler, IDragHandler
    {
        public float value;
        public float max;
        public float min;
        public ProgressTitleType titleType;

        public Text title;
        public RectTransform bar;
        public RectTransform barV;
        public RectTransform grip;

        public float barMaxWidthDelta;
        public float barMaxHeightDelta;

        public void Apply();
        public void OnPointerDown(PointerEventData eventData);
        public void OnDrag(PointerEventData eventData);
    }
}
```

## Text / Text Effects

```csharp
namespace NanamiUI
{
    // 继承 UnityEngine.UI.Text，并复刻 FairyGUI 文本排版、UBB 链接、图片字和位图字体。
    public class Text : UnityEngine.UI.Text, IPointerClickHandler
    {
        public static string defaultFont;

        public string fontNames;
        public int leading;
        public bool ubb;
        public bool html;
        public bool underlined;
        public BitmapFont bitmapFont;
        public Sprite[] imageSprites;

        public Action<string> onClickLink { get; set; }

        public override Texture mainTexture { get; }

        public void OnPointerClick(PointerEventData eventData);
        public void WarmUp();
        public void RebuildImages();
    }

    public class TextStroke : BaseMeshEffect
    {
        public Color color;
        public float width;

        public override void ModifyMesh(VertexHelper vh);
    }

    public class TextShadow : BaseMeshEffect
    {
        public Color color;
        public Vector2 offset;

        public override void ModifyMesh(VertexHelper vh);
    }
}
```

## Image / Loader / Shape / Line

```csharp
namespace NanamiUI
{
    public class MovieClip : Image
    {
        public Sprite[] frames;
        public float interval;
        public float[] addDelays;
        public bool playing;
        public int frame;

        public void SetFrame(int value);
    }

    public class Loader : MovieClip
    {
        public enum FillType
        {
            None,
            Scale,
            ScaleMatchHeight,
            ScaleMatchWidth,
            ScaleFree,
            ScaleNoBorder,
        }

        public FillType fill;
        public int align;   // 0 left, 1 center, 2 right
        public int vAlign;  // 0 top, 1 middle, 2 bottom
    }

    public class FlipImage : Image
    {
        public bool flipX;
        public bool flipY;
    }

    public class Shape : MaskableGraphic
    {
        public enum Kind
        {
            Rect,
            RoundedRect,
            Ellipse,
            Polygon,
            RegularPolygon,
        }

        public Kind kind;
        public float lineSize;
        public Color lineColor;
        public float[] corners;
        public Vector2[] points;
        public int sides;
        public float startAngle;
        public float[] distances;
        public Vector2 skew;

        public float startDegree;
        public float endDegree;

        public bool usePercentPositions;
        public Vector2[] texcoords;
        public Sprite texture;

        public override Texture mainTexture { get; }
    }

    public class Line : MaskableGraphic
    {
        public float lineWidth;
        public AnimationCurve lineWidthCurve;
        public Gradient gradient;
        public bool roundEdge;
        public float fillStart;
        public float fillEnd;
        public float pointDensity;
        public Sprite sprite;

        public override Texture mainTexture { get; }
        public void SetPath(IReadOnlyList<TransitionPath.PathPoint> pts);
    }
}
```

## List / ScrollPane

```csharp
namespace NanamiUI
{
    public sealed class ListSource : MonoBehaviour
    {
        public GameObject itemPrefab;
        public Vector2 itemSize;
        public float lineGap;
        public float colGap;
        public string layout;
    }

    public static class GList
    {
        public static RectTransform Container(RectTransform list);
        public static void Fill(RectTransform list, int count, Action<GameObject, int> setup);
    }

    // 简化 ScrollPane：支持拖动滚动，不含惯性、回弹、虚拟化。
    public sealed class ScrollPane : UIBehaviour, IBeginDragHandler, IDragHandler
    {
        public static ScrollPane Attach(RectTransform scrollRoot);

        public void OnBeginDrag(PointerEventData e);
        public void OnDrag(PointerEventData e);
    }
}
```

## Popup / Window / GRoot

```csharp
namespace NanamiUI
{
    public enum PopupDirection
    {
        Auto,
        Up,
        Down,
    }

    // 顶层覆盖层，承载 popup 和 window。
    public sealed class GRoot : MonoBehaviour
    {
        public static GRoot inst { get; }

        public RectTransform rect;
        public Vector2 Size { get; }
        public bool HasAnyPopup { get; }
        public int ActiveWindowCount { get; }

        public static GRoot Create(RectTransform designRoot);

        public void Center(RectTransform obj);
        public void ShowPopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto);
        public void TogglePopup(RectTransform popup, RectTransform target = null, PopupDirection dir = PopupDirection.Auto);
        public void HidePopup(RectTransform popup = null);
        public Vector2 RootTopLeft(RectTransform node);

        public void ShowWindow(Window win);
        public void HideWindowImmediately(Window win);
        public void BringToFront(Window win);
    }

    public sealed class PopupBlocker : UIBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData);
    }

    public sealed class PopupMenu
    {
        public bool hideOnClickItem;
        public RectTransform ContentPane { get; }

        public PopupMenu(GameObject contentPrefab, GameObject itemPrefab);
        public void AddItem(string caption, Action callback);
        public void Show(RectTransform target = null, PopupDirection dir = PopupDirection.Auto);
        public void Hide();
    }

    // 轻量窗口类，不是 MonoBehaviour。
    public class Window
    {
        public GameObject prefab;
        public bool inited { get; }
        public RectTransform Root { get; }

        public void Show();
        public void Hide();
        public void HideImmediately();

        protected void Center();
        protected void BindCloseButton();
        protected static void SetPivotKeepPosition(RectTransform rt, Vector2 pivot);

        protected virtual void OnInit();
        protected virtual void OnShown();
        protected virtual void OnHide();
        protected virtual void DoShowAnimation();
        protected virtual void DoHideAnimation();
    }
}
```

## Drag / Drop / Depth / Relation

```csharp
namespace NanamiUI
{
    public sealed class Draggable : UIBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        public Rect? dragBounds; // parent-local, FairyGUI y-down
        public Func<PointerEventData, bool> onDragStart;
        public Action onDragMove;
        public Action onDragEnd;
        public static Draggable dragging;

        public void OnBeginDrag(PointerEventData e);
        public void OnDrag(PointerEventData e);
        public void OnEndDrag(PointerEventData e);
    }

    public sealed class DropTarget : UIBehaviour
    {
        public Action<object> onDrop;
    }

    public sealed class DragDropManager
    {
        public static DragDropManager inst { get; }
        public bool dragging { get; }

        public void StartDrag(Canvas root, GraphicRaycaster raycaster, Sprite icon, object sourceData, PointerEventData e);
        public void MoveAgent(PointerEventData e);
        public void Drop(PointerEventData e);
    }

    public static class Depth
    {
        public static int SortIndex(RectTransform parent, int order, Transform ignore = null);
        public static void SetSortingOrder(RectTransform child, int order);
        public static Shape CreateRect(RectTransform parent, Vector2 fairyXY, float w, float h, int lineSize, Color line, Color fill, int order);
    }

    public class Relation : UIBehaviour
    {
        public RectTransform target;
        public string[] sidePairs;
        public Vector2 lastTopLeft;
        public Vector2 lastSize;

        public static Vector2 TopLeft(RectTransform rt);
        public void Record();
        public void Sync();
    }
}
```

## Transition

```csharp
namespace NanamiUI
{
    public enum TransitionItemType
    {
        XY,
        Size,
        Scale,
        Pivot,
        Alpha,
        Rotation,
        Color,
        Animation,
        Visible,
        Sound,
        Nested,
        Shake,
        ColorFilter,
        Text,
    }

    [Serializable]
    public class TransitionItem
    {
        public float time;
        public RectTransform target; // null means transition root
        public TransitionItemType type;

        public bool tween;
        public float duration;
        public Ease ease;
        public int repeat; // -1 means infinite
        public bool yoyo;

        public float[] start;
        public float[] end;
        public string stringValue;
        public AudioClip sound;
        public Vector2 positionOffset;
        public float[] pathData;
    }

    public class Transition : MonoBehaviour
    {
        public string transitionName;
        public bool autoPlay;
        public int autoPlayTimes;
        public float autoPlayDelay;
        public TransitionItem[] items;

        public void Play(int times = 1, float delay = 0, Action onComplete = null);
        public void Stop();
        public void Step(float deltaTime);
    }

    public class TransitionPath
    {
        public readonly struct PathPoint
        {
            public readonly int Type; // 0 CRSpline, 1 Bezier, 2 CubicBezier, 3 Straight
            public readonly Vector2 Pos;
            public readonly Vector2 C1;
            public readonly Vector2 C2;

            public PathPoint(Vector2 pos, int type = 0, Vector2 c1 = default, Vector2 c2 = default);
        }

        public TransitionPath(float[] tokens);
        public TransitionPath(IReadOnlyList<PathPoint> pts);

        public float Length { get; }
        public int SegmentCount { get; }

        public float GetSegmentLength(int i);
        public void GetPointsInSegment(int segIndex, float t0, float t1, List<Vector2> points, List<float> ts, float pointDensity);
        public Vector2 GetPointAt(float t);
    }
}
```

## Config / Resources / Effects

```csharp
namespace NanamiUI
{
    [CreateAssetMenu(fileName = "NanamiUISettings", menuName = "NanamiUI/Settings")]
    public class NanamiUISettings : ScriptableObject
    {
        public string defaultFont;
        public string buttonSound;
        public string[] packages;
    }

    public class BitmapFont : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public int code;
            public Rect uv;
            public float x;
            public float y;
            public float width;
            public float height;
            public int advance;
            public int lineHeight;
        }

        public int size;
        public bool canTint;
        public Texture2D texture;
        public Glyph[] glyphs;

        public bool TryGetGlyph(char ch, out Glyph glyph);
    }

    public class ColorAdjust : UIBehaviour
    {
        public float brightness;
        public float contrast;
        public float saturation;
        public float hue;

        public void Set(float brightnessValue, float contrastValue, float saturationValue, float hueValue);
    }

    public class Grayed : UIBehaviour
    {
    }

    public sealed class SortObject : UIBehaviour
    {
        public int order;
    }
}
```
