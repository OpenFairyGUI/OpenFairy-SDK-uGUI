# OpenFairy.UGUI API Reference

本文用 C# 声明式摘要描述 `OpenFairy` 运行时公开 API。声明省略 private/internal 实现细节，保留 public 类型、字段、属性、方法和少量用途注释；它是阅读用 reference，不是可直接编译的源码。

命名空间：`OpenFairy.UGUI`

典型入口：

1. 通过 `Tools/OpenFairy/Migrate` 生成 prefab 和 C# 组件类。
2. 在生成类字段上访问子控件，例如 `m_btn_OK`、`m_title`。

OpenFairy 是 bake-first 的 FairyGUI→uGUI Runtime SDK：绝大多数结构、样式与交互在 Migrate 期烘进 prefab，
运行时 API 只覆盖 FairyGUI 中本就属于"运行时"的部分（选中态、动效播放、窗口/弹窗、拖拽、动态列表）。
命名约定：类型 = FairyGUI 控件去 `G` 前缀；公共属性 camelCase（与 uGUI/FairyGUI 一致）；方法 PascalCase。
标注为"烘焙接线"的字段是 `[SerializeField] internal`，由 Migrate 写入，不属用户 API。

## Component / Controller / Gear

```csharp
public class Component : UIBehaviour          // 生成的自定义组件基类（codegen 为每个组件生成子类，子物体引用字段 m_ 前缀）
public struct Controller<T> where T : struct, Enum
{
    public T page;                            // 切页即应用全部 gears（运行时带缓动，复刻 HandleControllerChanged 的 display-lock 顺序）
    public Gear<T>[] gears;
}
public abstract class Gear<T> where T : struct, Enum
{
    public GameObject target;
    public T[] pages;
    public bool tween; public float duration; public Ease ease; public float delay;
}
```

Controller 不生成单独类：组件类里生成同名 enum + `Controller<该enum>` 字段。
Gear 子类：`GearDisplay`（含 `DisplayCondition` And/Or 组合与 display lock）、`GearXY`、`GearSize`、`GearLook`、
`GearColor`、`GearAni`、`GearFontSize`、`GearText`、`GearIcon`、`GearButton`（关联控制器换页反向同步按钮选中态）。
均由 Migrate 烘焙，一般无需手动构造。

## Button / ButtonBase

```csharp
public enum ButtonMode { Common, Check, Radio }

public abstract class ButtonBase : Component   // 非泛型面：不知道控制器 enum T 的代码经它操作按钮
{
    public UnityEvent onClick;
    public UnityEvent onChanged;               // 仅用户点击翻转 Check/Radio 时发（复刻 GButton.onChanged）
    public ButtonMode mode;
    public string selectedTitle;
    public bool changeStateOnClick;            // 列表项交给 ListSelection 管选择时为 false
    public string title { get; set; }
    public Sprite icon { get; set; }
    public bool selected { get; set; }         // 复刻 GButton.selected：Common 忽略；驱动关联控制器（tab/radio 组）
    public bool grayed { get; set; }           // 置灰进 disabled 页并拦截交互
}

public abstract class Button<T> : ButtonBase, ... where T : struct, Enum
{
    public Controller<T> controller;           // "button" 控制器（up/down/over/…页）
}
```

按钮关联控制器（tab/radio 组）由 Migrate 从 `<Button controller=.. page=..>` 烘焙，点击/程序化 `selected` 均会换页，
换页（含程序化）经 GearButton 反向同步整组选中态。

## Label

```csharp
public class Label : Component
{
    public string title { get; set; }
    public Sprite icon { get; set; }
}
```

## ComboBox

```csharp
public interface IComboBox                     // 非泛型面
{
    string[] items { get; set; }               // setter 标脏，下次打开重建下拉（复刻 _itemsUpdated）
    string[] values { get; set; }
    int selectedIndex { get; set; }            // 程序化赋值只刷标题，不发 onChanged
    string text { get; set; }                  // 标题直通
    string value { get; set; }                 // 按 values 反查，找不到回退首项
}

public abstract class ComboBox<T> : Button<T>, IComboBox where T : struct, Enum
{
    public int visibleItemCount;               // 下拉同屏项数，超出裁剪并滚动
    public PopupDirection popupDirection;      // 默认 Auto：贴屏幕底时上翻
    // onChanged 复用 ButtonBase 的事件（FairyGUI 同一事件通道）；仅点选下拉项触发（含重选当前项）
}
```

点击弹下拉、外点关闭、`visibleItemCount` 裁剪滚动均已烘焙自足；无 `Root` 时首次弹下拉会自建覆盖层。

## ProgressBar / Slider

```csharp
public enum ProgressTitleType { Percent, ValueAndMax, Value, Max }

public class ProgressBar : Component
{
    public float value, max, min;              // setter 即刷视觉
    public ProgressTitleType titleType;
    public bool reverse;
    public void TweenValue(float target, float duration, Ease ease = Ease.Linear);
}

public class Slider : Component, IPointerDownHandler
{
    public float value, max, min;
    public bool wholeNumbers, changeOnClick, reverse;
    public ProgressTitleType titleType;
    public UnityEvent onChanged;               // 用户交互改值时发（程序化赋值不发）
    public UnityEvent onGripTouchBegin, onGripTouchEnd;
}
```

grip 拖动、轨道点按跳值（changeOnClick）、Filled-Image bar 均已烘焙。

## TextField / TextInput / 文字效果

```csharp
public class TextField : UnityEngine.UI.Text, IPointerClickHandler
{
    public static string defaultFont;          // 无烘焙字体时的回退（烘焙工程用 fontNames，无需设置）
    public int leading, letterSpacing;
    public bool ubb, html, underlined;
    public Sprite[] imageSprites;              // 富文本 [img]/<img> 按出现序号绑定的图片（Migrate 烘焙，换图直接改数组）
    public Action<string> onClickLink { get; set; }  // <a href>/[url=..] 点击回调；赋值自动开 raycastTarget
}

public class TextInput : Component             // 基于 uGUI InputField
{
    public string text { get; set; }           // 程序化赋值不发 onChanged（SetTextWithoutNotify）
    public bool password { get; set; }
    public int maxLength { get; set; }
    public bool editable { get; set; }         // false = readOnly（可聚焦），非禁用
    public UnityEvent<string> onChanged;
    public UnityEvent<string> onSubmit;        // 仅回车（ISubmitHandler），失焦不误触发
}

public class TextShadow : MonoBehaviour        // FairyGUI 阴影/描边（Migrate 烘焙）
public class TextStroke : MonoBehaviour
```

`TextField` 在 `OnPopulateMesh` 复刻 FairyGUI 排版公式，带排版缓存：文本/字号/尺寸不变时不重排版，
纯色 tween 只回填顶点色。

## Image / Loader / MovieClip / Graph / Line

```csharp
public class Image : UnityEngine.UI.Image      // 平铺相位、九宫格翻转对齐 FairyGUI
{
    public bool flipX, flipY;
}

public class MovieClip : UnityEngine.UI.Image  // 逐帧动画
{
    public Sprite[] frames;
    public float interval, repeatDelay, timeScale;
    public float[] addDelays;
    public bool swing;
    public bool playing { get; set; }
    public int frame { get; set; }             // setter 即刷帧并钳进有效范围
    public UniTask Play(int start = 0, int end = -1, int times = 1, int endAt = -1);
    public void Rewind();
}

public class Loader : MovieClip                // 静态内容也用它（帧数 1）；fill/align 复刻 GLoader
{
    public FillType fill;                      // None/Scale/ScaleMatchHeight/ScaleMatchWidth/ScaleFree/ScaleNoBorder
    public AlignType align;                    // Left/Center/Right
    public VertAlignType vAlign;               // Top/Middle/Bottom
}

public class Graph : MaskableGraphic          // GGraph：Rect/RoundedRect/Ellipse/Polygon/RegularPolygon
{
    public Kind kind;
    public float lineSize; public Color lineColor;
    public float[] corners; public Vector2[] points; public int sides; public float startAngle; public float[] distances;
    public Vector2 skew;
    public float startDegree, endDegree;       // 椭圆扇形
    public bool usePercentPositions; public Vector2[] texcoords; public Sprite texture; // 带贴图多边形
}

public class Line : MaskableGraphic           // Graph demo 的折线渲染 helper（lineWidth/gradient/fillEnd/SetPath）
```

## List / ListSelection / ScrollPane

```csharp
public enum ListLayoutType { SingleColumn, SingleRow, FlowHorizontal, FlowVertical, Pagination }

public sealed class ListSource : MonoBehaviour // 列表动态实例化描述（Migrate 烘焙 itemPrefab/itemSize/gap/layout）
{
    public void Fill(int count, Action<GameObject, int> setup, bool rebindSelection = true);
    // 池化复用项、按 layout 排布、刷新 ScrollPane 内容边界并重绑 ListSelection（复刻 GList numItems+itemRenderer 简版）
}

public static class List
{
    public static RectTransform Container(RectTransform list); // 项所在容器（viewport/content > viewport > list）
}

public enum ListSelectionMode { Single, Multiple, MultipleSingleClick, None }

public sealed class ListSelection : UIBehaviour
{
    public ListSelectionMode selectionMode;
    public UnityEvent<int> onClickItem;        // 点任意项（含非按钮项）发索引
    public int selectedIndex { get; set; }     // set 排它选中；-1 清空
    public void ClearSelection();
    public List<int> GetSelection();           // Multiple 模式读全部选中索引
    public void Rebind();                      // Fill 后自动调；手动改 content 后可再调
}

public sealed class ScrollPane : UIBehaviour, ...
{
    public bool bounceEffect, mouseWheelEnabled;
    public UnityEvent onScroll;
    public float percX { get; set; }           // 滚动位置比例（0 顶/左），setter 直接跳转
    public float percY { get; set; }
    public void RefreshContent();              // 手动改 content 后重算滚动范围
    public void ScrollToView(RectTransform target); // 仅竖直
    public static ScrollPane Attach(RectTransform scrollRoot); // 一般无需手调：ScrollPaneHost 自挂
}
```

`overflow=scroll` 组件由烘焙的 `ScrollPaneHost` 运行时自挂 `ScrollPane`（拖动/滚轮/滚动条/惯性回弹开箱即用）。
无虚拟化、无分页吸附。

## Root / Window / PopupMenu

```csharp
public enum PopupDirection { Auto, Up, Down }

public sealed class Root : MonoBehaviour       // 顶层覆盖层（GRoot）：承载 window/popup，设计坐标（左上原点 y-down）
{
    public static Root inst { get; }
    public static Root Create(RectTransform designRoot); // 幂等；传画布根或设计根
    public Vector2 size { get; }
    public Color modalColor;
    public bool hasAnyPopup { get; }
    public bool hasModalWindow { get; }
    public int activeWindowCount { get; }
    public void Center(RectTransform obj);
    public UniTask ShowPopup(RectTransform popup, RectTransform target = null, PopupDirection dir = Auto);
    public UniTask ShowPopupAt(RectTransform popup, Vector2 designPos, PopupDirection dir = Down); // 指针处弹出用
    public UniTask TogglePopup(RectTransform popup, RectTransform target = null, PopupDirection dir = Auto);
    public void HidePopup(RectTransform popup = null);  // null = 收起全部
    public Vector2 ScreenToDesign(Vector2 screen, Camera camera);
    public Vector2 RootTopLeft(RectTransform node);
    public void ShowWindow(Window win); public void HideWindowImmediately(Window win); public void BringToFront(Window win);
}

public class Window                            // 复刻 FairyGUI Window：包一个 content prefab
{
    public GameObject prefab;                  // 调用方赋值
    public bool modal;                         // 模态层插到最上层模态窗之下（复刻 AdjustModalLayer）
    public bool inited { get; }
    public RectTransform contentPane { get; }  // 实例化后的内容根（未 init 为 null）
    public void Show(); public void Hide(); public void HideImmediately();
    // 可覆盖：OnInit/OnShown/OnHide/DoShowAnimation/DoHideAnimation
    // OnInit 默认绑 closeButton→Hide、dragArea→整窗拖动（提到最前）
}

public sealed class PopupMenu : IDisposable
{
    public bool hideOnClickItem;
    public RectTransform ContentPane { get; }
    public PopupMenu(GameObject contentPrefab, GameObject itemPrefab);
    public ButtonBase AddItem(string caption); // 返回项按钮，可直接设 grayed/selected/icon/onClick
    public void ClearItems();
    public UniTask Show(RectTransform target = null, PopupDirection dir = Auto);
    public UniTask ShowAtPointer(PointerEventData e, PopupDirection dir = Auto); // 右键在指针处弹出
    public void Hide(); public void Dispose();
}
```

外点关闭用透明 blocker（表现为模态，避开新 Input System 下旧 Input 的异常）。

## Drag / Drop / Depth / Relation

```csharp
public sealed class Draggable : UIBehaviour, ...
{
    public Rect? dragBounds;                   // parent-local、y-down（本 SDK 语义，非 GRoot-local）
    public Func<PointerEventData, bool> onDragStart;  // 返回 true = PreventDefault，改走 DragDropManager agent
    public Action onDragMove, onDragEnd;
    public static Draggable dragging;
}

public sealed class DropTarget : UIBehaviour, IDropHandler { public Action<object> onDrop; }

public class DragDropManager                   // 复刻 FairyGUI DragDropManager（icon agent 拖到 DropTarget）
{
    public static DragDropManager inst;
    public void StartDrag(Canvas canvas, GraphicRaycaster raycaster, Sprite icon, object data, PointerEventData e);
    public void Cancel();
}

public static class Depth                      // 复刻 GObject.sortingOrder 兄弟序
{
    public static void SetSortingOrder(RectTransform child, int order);
    public static int SortIndex(RectTransform parent, int order, Transform ignore = null);
    public static Graph CreateRect(RectTransform parent, Vector2 fairyXY, float w, float h, int lineSize, Color line, Color fill, int order);
}

public class Relation : UIBehaviour            // 指向兄弟的关联（Migrate 烘焙），集中循环逐帧增量跟随
{
    public RectTransform target;
    public RelationSide[] sidePairs;           // FairyGUI RelationType 全词表
}
```

容器关联在烘焙期已映射为 uGUI 锚点，无运行时组件。

## Transition

```csharp
public class Transition : MonoBehaviour        // 复刻 FairyGUI Transition 时间轴
{
    public string transitionName;
    public bool autoPlay; public int autoPlayTimes; public float autoPlayDelay;
    public TransitionItem[] items;             // Migrate 烘焙
    public bool playing { get; }
    public UniTask Play(int times = 1, float delay = 0);
    public UniTask PlayReverse(int times = 1, float delay = 0); // 播完或被打断后完成
    public void Stop();                        // 复刻 Stop() 默认 setToComplete：item 落终态（倒放落起态），Shake 归位
}
```

item 类型：XY/Size/Scale/Pivot/Alpha/Rotation/Color/Animation/Visible/Sound/Nested/Shake/ColorFilter/Text/Skew，
支持曲线路径（`TransitionPath`，`CurveType` CRSpline/Bezier/CubicBezier/Straight）。

## 其它

```csharp
public class ColorAdjust : UIBehaviour         // ColorFilter 4x5 颜色矩阵（亮度/对比/饱和/色相），Migrate 烘焙 shader
{
    public void Set(float brightness, float contrast, float saturation, float hue);
}
public class Grayed : MonoBehaviour            // 灰度材质（Migrate 烘焙，运行时只切 enabled）
public sealed class ScrollPaneHost : MonoBehaviour   // 烘焙接线：Start 自挂 ScrollPane
public sealed class InputSubmit : MonoBehaviour      // 烘焙接线：InputField 的 Enter 提交中继
public sealed class SortObject : MonoBehaviour       // sortingOrder 记录
public class OpenFairySettings : ScriptableObject     // 工程配置（defaultFont 等），放 SDK 外（Assets/）
```
