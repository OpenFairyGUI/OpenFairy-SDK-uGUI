# FairyGUI SDK API Reference

本文用 C# 声明式摘要描述 FairyGUI Unity SDK 的常用公开 API。声明省略 private/internal 实现细节，保留本仓库迁移、对照和 demo 中最常用的 public 类型、字段、属性和方法；它是阅读用 reference，不是完整源码索引。

命名空间：`FairyGUI`

典型入口：

1. 启动时配置 `UIConfig`。
2. 通过 `UIPackage.AddPackage` 注册 FairyGUI 包。
3. 用 `UIPackage.CreateObject` / `CreateObjectFromURL` 创建 `GObject`。
4. 将根组件加入 `GRoot.inst`，或用 `UIPanel` / `UIPainter` 托管。
5. 用 `GComponent.GetChild`、`Controller`、`Transition` 和各控件 API 驱动 UI。

## Package / Object Factory

```csharp
namespace FairyGUI
{
    public class UIPackage
    {
        public const string URL_PREFIX = "ui://";

        public static bool unloadBundleByFGUI;
        public static event Action<PackageItem> onReleaseResource;

        public string id { get; }
        public string name { get; }
        public string assetPath { get; }
        public string customId { get; set; }
        public AssetBundle resBundle { get; }
        public Dictionary<string, string>[] dependencies { get; }

        public delegate object LoadResource(string name, string extension, Type type, out DestroyMethod destroyMethod);
        public delegate void LoadResourceAsync(string name, string extension, Type type, PackageItem item);
        public delegate void CreateObjectCallback(GObject result);

        public static string branch { get; set; }
        public static string GetVar(string key);
        public static void SetVar(string key, string value);

        public static UIPackage GetById(string id);
        public static UIPackage GetByName(string name);
        public static List<UIPackage> GetPackages();

        public static UIPackage AddPackage(AssetBundle bundle);
        public static UIPackage AddPackage(AssetBundle desc, AssetBundle res);
        public static UIPackage AddPackage(AssetBundle desc, AssetBundle res, string mainAssetName);
        public static UIPackage AddPackage(string descFilePath);
        public static UIPackage AddPackage(string assetPath, LoadResource loadFunc);
        public static UIPackage AddPackage(byte[] descData, string assetNamePrefix, LoadResource loadFunc);
        public static UIPackage AddPackage(byte[] descData, string assetNamePrefix, LoadResourceAsync loadFunc);

        public static void RemovePackage(string packageIdOrName);
        public static void RemoveAllPackages();

        public static GObject CreateObject(string pkgName, string resName);
        public static GObject CreateObject(string pkgName, string resName, Type userClass);
        public static GObject CreateObjectFromURL(string url);
        public static GObject CreateObjectFromURL(string url, Type userClass);
        public static void CreateObjectAsync(string pkgName, string resName, CreateObjectCallback callback);
        public static void CreateObjectFromURL(string url, CreateObjectCallback callback);

        public static object GetItemAsset(string pkgName, string resName);
        public static object GetItemAssetByURL(string url);
        public static string GetItemURL(string pkgName, string resName);
        public static PackageItem GetItemByURL(string url);
        public static string NormalizeURL(string url);
        public static void SetStringsSource(XML source);

        public void LoadAllAssets();
        public void UnloadAssets();
        public void ReloadAssets();
        public void ReloadAssets(AssetBundle resBundle);

        public GObject CreateObject(string resName);
        public GObject CreateObject(string resName, Type userClass);
        public void CreateObjectAsync(string resName, CreateObjectCallback callback);

        public object GetItemAsset(string resName);
        public object GetItemAsset(PackageItem item);
        public List<PackageItem> GetItems();
        public PackageItem GetItem(string itemId);
        public PackageItem GetItemByName(string itemName);
        public void SetItemAsset(PackageItem item, object asset, DestroyMethod destroyMethod);
    }

    public class UIObjectFactory
    {
        public delegate GComponent GComponentCreator();
        public delegate GLoader GLoaderCreator();

        public static void SetPackageItemExtension(string url, Type type);
        public static void SetPackageItemExtension(string url, GComponentCreator creator);
        public static void SetLoaderExtension(Type type);
        public static void SetLoaderExtension(GLoaderCreator creator);
        public static void Clear();

        public static GObject NewObject(PackageItem pi, Type userClass = null);
        public static GObject NewObject(ObjectType type);
    }

    public class PackageItem
    {
        public UIPackage owner;
        public PackageItemType type;
        public ObjectType objectType;
        public string id;
        public string name;
        public int width;
        public int height;
        public string file;
        public bool exported;
        public NTexture texture;
        public ByteBuffer rawData;
        public string[] branches;
        public string[] highResolution;

        public Rect? scale9Grid;
        public bool scaleByTile;
        public int tileGridIndice;
        public PixelHitTestData pixelHitTestData;

        public float interval;
        public float repeatDelay;
        public bool swing;
        public MovieClip.Frame[] frames;

        public BitmapFont bitmapFont;
        public NAudioClip audioClip;

        public object Load();
        public PackageItem getBranch();
        public PackageItem getHighResolution();
    }
}
```

## GObject / GComponent / Controller

```csharp
namespace FairyGUI
{
    public class GObject : EventDispatcher
    {
        public string id { get; }
        public string name;
        public object data;

        public int sourceWidth;
        public int sourceHeight;
        public int initWidth;
        public int initHeight;
        public int minWidth;
        public int maxWidth;
        public int minHeight;
        public int maxHeight;

        public Relations relations { get; }
        public Rect? dragBounds;
        public GComponent parent { get; }
        public DisplayObject displayObject { get; }
        public static GObject draggingObject { get; }
        public PackageItem packageItem;

        public EventListener onClick { get; }
        public EventListener onRightClick { get; }
        public EventListener onTouchBegin { get; }
        public EventListener onTouchMove { get; }
        public EventListener onTouchEnd { get; }
        public EventListener onRollOver { get; }
        public EventListener onRollOut { get; }
        public EventListener onAddedToStage { get; }
        public EventListener onRemovedFromStage { get; }
        public EventListener onKeyDown { get; }
        public EventListener onClickLink { get; }
        public EventListener onPositionChanged { get; }
        public EventListener onSizeChanged { get; }
        public EventListener onDragStart { get; }
        public EventListener onDragMove { get; }
        public EventListener onDragEnd { get; }
        public EventListener onGearStop { get; }
        public EventListener onFocusIn { get; }
        public EventListener onFocusOut { get; }

        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public Vector2 xy { get; set; }
        public Vector3 position { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public Vector2 size { get; set; }
        public float actualWidth { get; }
        public float actualHeight { get; }
        public float xMin { get; set; }
        public float yMin { get; set; }
        public float scaleX { get; set; }
        public float scaleY { get; set; }
        public Vector2 scale { get; set; }
        public Vector2 skew { get; set; }
        public float pivotX { get; set; }
        public float pivotY { get; set; }
        public Vector2 pivot { get; set; }
        public bool pivotAsAnchor { get; }

        public bool touchable { get; set; }
        public bool grayed { get; set; }
        public bool enabled { get; set; }
        public float rotation { get; set; }
        public float rotationX { get; set; }
        public float rotationY { get; set; }
        public float alpha { get; set; }
        public bool visible { get; set; }
        public int sortingOrder { get; set; }

        public bool focusable { get; set; }
        public bool tabStop { get; set; }
        public bool focused { get; }
        public string tooltips { get; set; }
        public string cursor { get; set; }
        public string gameObjectName { get; set; }
        public bool inContainer { get; }
        public bool onStage { get; }
        public string resourceURL { get; }
        public GRoot root { get; }
        public GGroup group { get; set; }

        public GearXY gearXY { get; }
        public GearSize gearSize { get; }
        public GearLook gearLook { get; }
        public GearBase GetGear(int index);

        public void SetXY(float x, float y);
        public void SetXY(float x, float y, bool topLeftValue);
        public void SetPosition(float x, float y, float z);
        public void Center();
        public virtual void Center(bool restraint);
        public void MakeFullScreen();
        public void SetSize(float width, float height);
        public void SetSize(float width, float height, bool ignorePivot);
        public void SetScale(float scaleX, float scaleY);
        public void SetPivot(float x, float y);
        public void SetPivot(float x, float y, bool asAnchor);
        public void RequestFocus();
        public void RequestFocus(bool byKey);
        public void SetHome(GObject obj);
        public void InvalidateBatchingState();
        public void AddRelation(GObject target, RelationType relationType);
        public void AddRelation(GObject target, RelationType relationType, bool usePercent);
        public void RemoveRelation(GObject target, RelationType relationType);
        public void RemoveFromParent();

        public bool draggable { get; set; }
        public void StartDrag();
        public void StartDrag(int touchId);
        public void StopDrag();
        public bool dragging { get; }
        public Vector2 dragStartPos { get; }

        public Vector2 LocalToGlobal(Vector2 pt);
        public Vector2 GlobalToLocal(Vector2 pt);
        public Rect LocalToGlobal(Rect rect);
        public Rect GlobalToLocal(Rect rect);
        public Vector2 LocalToRoot(Vector2 pt, GRoot root);
        public Vector2 RootToLocal(Vector2 pt, GRoot root);
        public Vector2 WorldToLocal(Vector3 pt);
        public Vector2 WorldToLocal(Vector3 pt, Camera camera);
        public Vector2 TransformPoint(Vector2 pt, GObject targetSpace);
        public Rect TransformRect(Rect rect, GObject targetSpace);

        public bool isDisposed { get; }
        public GImage asImage { get; }
        public GComponent asCom { get; }
        public GButton asButton { get; }
        public GLabel asLabel { get; }
        public GProgressBar asProgress { get; }
        public GSlider asSlider { get; }
        public GComboBox asComboBox { get; }
        public GTextField asTextField { get; }
        public GRichTextField asRichTextField { get; }
        public GTextInput asTextInput { get; }
        public GLoader asLoader { get; }
        public GLoader3D asLoader3D { get; }
        public GList asList { get; }
        public GGraph asGraph { get; }
        public GGroup asGroup { get; }
        public GMovieClip asMovieClip { get; }
        public GTree asTree { get; }
        public GTreeNode treeNode { get; }

        public GTweener TweenMove(Vector2 endValue, float duration);
        public GTweener TweenMoveX(float endValue, float duration);
        public GTweener TweenMoveY(float endValue, float duration);
        public GTweener TweenScale(Vector2 endValue, float duration);
        public GTweener TweenScaleX(float endValue, float duration);
        public GTweener TweenScaleY(float endValue, float duration);
        public GTweener TweenResize(Vector2 endValue, float duration);
        public GTweener TweenFade(float endValue, float duration);
        public GTweener TweenRotate(float endValue, float duration);
    }

    public class GComponent : GObject
    {
        public Container rootContainer { get; }
        public Container container { get; }
        public ScrollPane scrollPane { get; }
        public EventListener onDrop { get; }

        public bool fairyBatching { get; set; }
        public bool opaque { get; set; }
        public Margin margin { get; set; }
        public ChildrenRenderOrder childrenRenderOrder { get; set; }
        public int apexIndex { get; set; }
        public bool tabStopChildren { get; set; }
        public int numChildren { get; }

        public List<Controller> Controllers { get; }
        public List<Transition> Transitions { get; }

        public Vector2 clipSoftness { get; set; }
        public DisplayObject mask { get; set; }
        public bool reversedMask { get; set; }
        public string baseUserData { get; }
        public float viewWidth { get; set; }
        public float viewHeight { get; set; }

        public GObject AddChild(GObject child);
        public GObject RemoveChild(GObject child);
        public GObject RemoveChild(GObject child, bool dispose);
        public GObject RemoveChildAt(int index);
        public void RemoveChildren();
        public void RemoveChildren(int beginIndex, int endIndex, bool dispose);

        public GObject GetChildAt(int index);
        public GObject GetChild(string name);
        public GObject GetChildByPath(string path);
        public GObject GetVisibleChild(string name);
        public GObject GetChildInGroup(GGroup group, string name);
        public GObject[] GetChildren();

        public int GetChildIndex(GObject child);
        public void SetChildIndex(GObject child, int index);
        public int SetChildIndexBefore(GObject child, int index);
        public void SwapChildren(GObject child1, GObject child2);
        public void SwapChildrenAt(int index1, int index2);
        public bool IsAncestorOf(GObject obj);
        public void ChangeChildrenOrder(IList<GObject> objs);

        public void AddController(Controller controller);
        public Controller GetControllerAt(int index);
        public Controller GetController(string name);
        public void RemoveController(Controller controller);

        public Transition GetTransitionAt(int index);
        public Transition GetTransition(string name);

        public bool IsChildInView(GObject child);
        public void SetBoundsChangedFlag();
        public void EnsureBoundsCorrect();
        public void GetSnappingPosition(ref float xValue, ref float yValue);
        public virtual int GetFirstChildInView();
    }

    public class Controller : EventDispatcher
    {
        public string name;
        public EventListener onChanged { get; }

        public int selectedIndex { get; set; }
        public string selectedPage { get; set; }
        public int previsousIndex { get; } // SDK legacy spelling
        public int previousIndex { get; }
        public string previousPage { get; }
        public int pageCount { get; }

        public void Dispose();
        public void SetSelectedIndex(int value);
        public void SetSelectedPage(string value);
        public string GetPageName(int index);
        public string GetPageId(int index);
        public string GetPageIdByName(string name);
        public void AddPage(string name);
        public void AddPageAt(string name, int index);
        public void RemovePage(string name);
        public void RemovePageAt(int index);
        public void ClearPages();
        public bool HasPage(string name);
        public void RunActions();
    }
}
```

## Events

```csharp
namespace FairyGUI
{
    public delegate void EventCallback0();
    public delegate void EventCallback1(EventContext context);

    public class EventContext
    {
        public EventDispatcher sender { get; }
        public object initiator { get; }
        public InputEvent inputEvent { get; }
        public string type;
        public object data;
        public bool isDefaultPrevented { get; }

        public void StopPropagation();
        public void PreventDefault();
        public void CaptureTouch();
    }

    public class EventListener
    {
        public string type { get; }
        public bool isEmpty { get; }
        public bool isDispatching { get; }

        public void AddCapture(EventCallback1 callback);
        public void RemoveCapture(EventCallback1 callback);
        public void Add(EventCallback1 callback);
        public void Add(EventCallback0 callback);
        public void Remove(EventCallback1 callback);
        public void Remove(EventCallback0 callback);
        public void Set(EventCallback1 callback);
        public void Set(EventCallback0 callback);
        public void Clear();

        public bool Call();
        public bool Call(object data);
        public bool BubbleCall();
        public bool BubbleCall(object data);
        public bool BroadcastCall();
        public bool BroadcastCall(object data);
    }

    public class EventDispatcher : IEventDispatcher
    {
        public void AddEventListener(string type, EventCallback1 callback);
        public void AddEventListener(string type, EventCallback0 callback);
        public void RemoveEventListener(string type, EventCallback1 callback);
        public void RemoveEventListener(string type, EventCallback0 callback);
        public void AddCapture(string type, EventCallback1 callback);
        public void RemoveCapture(string type, EventCallback1 callback);
        public void RemoveEventListeners();
        public void RemoveEventListeners(string type);
        public bool hasEventListeners(string type);
        public bool isDispatching(string type);

        public bool DispatchEvent(string type);
        public bool DispatchEvent(string type, object data);
        public bool DispatchEvent(string type, object data, object initiator);
        public bool DispatchEvent(EventContext context);
        public bool BubbleEvent(string type, object data);
        public bool BroadcastEvent(string type, object data);
    }
}
```

## Basic Controls

```csharp
namespace FairyGUI
{
    public class GButton : GComponent, IColorGear
    {
        public const string UP = "up";
        public const string DOWN = "down";
        public const string OVER = "over";
        public const string SELECTED_OVER = "selectedOver";
        public const string DISABLED = "disabled";
        public const string SELECTED_DISABLED = "selectedDisabled";

        public NAudioClip sound;
        public float soundVolumeScale;
        public bool changeStateOnClick;
        public GObject linkedPopup;

        public EventListener onChanged { get; }
        public string title { get; set; }
        public string selectedIcon { get; set; }
        public string selectedTitle { get; set; }
        public Color titleColor { get; set; }
        public Color color { get; set; }
        public int titleFontSize { get; set; }
        public bool selected { get; set; }
        public ButtonMode mode { get; set; }
        public Controller relatedController { get; set; }
        public string relatedPageId { get; set; }

        public void FireClick(bool downEffect, bool clickCall = false);
        public GTextField GetTextField();
    }

    public class GComboBox : GComponent
    {
        public int visibleItemCount;
        public GComponent dropdown;
        public NAudioClip sound;
        public float soundVolumeScale;

        public EventListener onChanged { get; }
        public string title { get; set; }
        public Color titleColor { get; set; }
        public int titleFontSize { get; set; }
        public string[] items { get; set; }
        public string[] icons { get; set; }
        public string[] values { get; set; }
        public List<string> itemList { get; }
        public List<string> valueList { get; }
        public List<string> iconList { get; }
        public int selectedIndex { get; set; }
        public Controller selectionController { get; set; }
        public string value { get; set; }
        public PopupDirection popupDirection { get; set; }

        public void ApplyListChange();
        public GTextField GetTextField();
        public void UpdateDropdownList();
    }

    public class GProgressBar : GComponent
    {
        public ProgressTitleType titleType { get; set; }
        public double min { get; set; }
        public double max { get; set; }
        public double value { get; set; }
        public bool reverse { get; set; }

        public GTweener TweenValue(double value, float duration);
        public void Update(double newValue);
    }

    public class GSlider : GComponent
    {
        public bool changeOnClick;
        public bool canDrag;

        public EventListener onChanged { get; }
        public EventListener onGripTouchEnd { get; }
        public ProgressTitleType titleType { get; set; }
        public double min { get; set; }
        public double max { get; set; }
        public double value { get; set; }
        public bool wholeNumbers { get; set; }
    }
}
```

## Text / Image / Loader / Graph

```csharp
namespace FairyGUI
{
    public class GTextField : GObject, ITextColorGear
    {
        public Dictionary<string, string> templateVars { get; }
        public TextFormat textFormat { get; set; }
        public Color color { get; set; }
        public AlignType align { get; set; }
        public VertAlignType verticalAlign { get; set; }
        public bool singleLine { get; set; }
        public float stroke { get; set; }
        public Color strokeColor { get; set; }
        public Vector2 shadowOffset { get; set; }
        public bool UBBEnabled { get; set; }
        public AutoSizeType autoSize { get; set; }
        public float textWidth { get; }
        public float textHeight { get; }

        public GTextField SetVar(string name, string value);
        public void FlushVars();
        public bool HasCharacter(char ch);
    }

    public class GTextInput : GTextField
    {
        public InputTextField inputTextField { get; }
        public EventListener onChanged { get; }
        public EventListener onSubmit { get; }

        public bool editable { get; set; }
        public bool hideInput { get; set; }
        public int maxLength { get; set; }
        public string restrict { get; set; }
        public bool displayAsPassword { get; set; }
        public int caretPosition { get; set; }
        public string promptText { get; set; }
        public bool keyboardInput { get; set; }
        public int keyboardType { get; set; }
        public bool disableIME { get; set; }
        public Dictionary<uint, Emoji> emojies { get; set; }
        public int border { get; set; }
        public int corner { get; set; }
        public Color borderColor { get; set; }
        public Color backgroundColor { get; set; }
        public bool mouseWheelEnabled { get; set; }

        public void SetSelection(int start, int length);
        public void ReplaceSelection(string value);
    }

    public class GImage : GObject, IColorGear
    {
        public Color color { get; set; }
        public FlipType flip { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public bool fillClockwise { get; set; }
        public float fillAmount { get; set; }
        public NTexture texture { get; set; }
        public Material material { get; set; }
        public string shader { get; set; }
    }

    public class GMovieClip : GObject, IAnimationGear, IColorGear
    {
        public EventListener onPlayEnd { get; }
        public bool playing { get; set; }
        public int frame { get; set; }
        public Color color { get; set; }
        public FlipType flip { get; set; }
        public Material material { get; set; }
        public string shader { get; set; }
        public float timeScale { get; set; }
        public bool ignoreEngineTimeScale { get; set; }

        public void Rewind();
        public void SyncStatus(GMovieClip anotherMc);
        public void Advance(float time);
        public void SetPlaySettings(int start, int end, int times, int endAt);
    }

    public class GLoader : GObject, IAnimationGear, IColorGear
    {
        public bool showErrorSign;
        public Action __loadExternal;
        public Action<NTexture> __freeExternal;

        public string url { get; set; }
        public AlignType align { get; set; }
        public VertAlignType verticalAlign { get; set; }
        public FillType fill { get; set; }
        public bool useResize { get; set; }
        public bool shrinkOnly { get; set; }
        public bool autoSize { get; set; }
        public bool playing { get; set; }
        public int frame { get; set; }
        public float timeScale { get; set; }
        public bool ignoreEngineTimeScale { get; set; }
        public Material material { get; set; }
        public string shader { get; set; }
        public Color color { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public bool fillClockwise { get; set; }
        public float fillAmount { get; set; }

        public Image image { get; }
        public MovieClip movieClip { get; }
        public GComponent component { get; }
        public NTexture texture { get; }

        public void Advance(float time);
        public void onExternalLoadSuccess(NTexture texture);
        public void onExternalLoadFailed();
    }

    public class GGraph : GObject, IColorGear
    {
        public Color color { get; set; }
        public Shape shape { get; }

        public void ReplaceMe(GObject target);
        public void AddBeforeMe(GObject target);
        public void AddAfterMe(GObject target);
        public void SetNativeObject(DisplayObject obj);
        public void DrawRect(float width, float height, int lineSize, Color lineColor, Color fillColor);
        public void DrawRoundRect(float width, float height, Color fillColor, float[] corner);
        public void DrawEllipse(float width, float height, Color fillColor);
        public void DrawPolygon(float width, float height, IList<Vector2> points, Color fillColor);
        public void DrawPolygon(float width, float height, IList<Vector2> points, Color fillColor, float lineSize, Color lineColor);
    }
}
```

## List / ScrollPane

```csharp
namespace FairyGUI
{
    public delegate void ListItemRenderer(int index, GObject item);
    public delegate string ListItemProvider(int index);

    public class GList : GComponent
    {
        public bool foldInvisibleItems;
        public ListSelectionMode selectionMode;
        public ListItemRenderer itemRenderer;
        public ListItemProvider itemProvider;
        public bool scrollItemToViewOnClick;

        public EventListener onClickItem { get; }
        public EventListener onRightClickItem { get; }
        public string defaultItem { get; set; }
        public ListLayoutType layout { get; set; }
        public int lineCount { get; set; }
        public int columnCount { get; set; }
        public int lineGap { get; set; }
        public int columnGap { get; set; }
        public AlignType align { get; set; }
        public VertAlignType verticalAlign { get; set; }
        public bool autoResizeItem { get; set; }
        public Vector2 defaultItemSize { get; }
        public GObjectPool itemPool { get; }
        public int selectedIndex { get; set; }
        public Controller selectionController { get; set; }
        public GObject touchItem { get; }
        public bool isVirtual { get; }
        public int numItems { get; set; }

        public GObject GetFromPool(string url);
        public GObject AddItemFromPool();
        public GObject AddItemFromPool(string url);
        public void RemoveChildToPoolAt(int index);
        public void RemoveChildToPool(GObject child);
        public void RemoveChildrenToPool();
        public void RemoveChildrenToPool(int beginIndex, int endIndex);

        public List<int> GetSelection();
        public List<int> GetSelection(List<int> result);
        public void AddSelection(int index, bool scrollItToView);
        public void RemoveSelection(int index);
        public void ClearSelection();
        public void SelectAll();
        public void SelectNone();
        public void SelectReverse();
        public void EnableSelectionFocusEvents(bool enabled);
        public void EnableArrowKeyNavigation(bool enabled);
        public int HandleArrowKey(int dir);

        public void ResizeToFit();
        public void ResizeToFit(int itemCount);
        public void ResizeToFit(int itemCount, int minSize);
        public void ScrollToView(int index);
        public void ScrollToView(int index, bool ani);
        public void ScrollToView(int index, bool ani, bool setFirst);
        public override int GetFirstChildInView();
        public int ChildIndexToItemIndex(int index);
        public int ItemIndexToChildIndex(int index);
        public void SetVirtual();
        public void SetVirtualAndLoop();
        public void RefreshVirtualList();
    }

    public class ScrollPane : EventDispatcher
    {
        public static ScrollPane draggingPane { get; }
        public static float TWEEN_TIME_GO;
        public static float TWEEN_TIME_DEFAULT;
        public static float PULL_RATIO;

        public EventListener onScroll { get; }
        public EventListener onScrollEnd { get; }
        public EventListener onPullDownRelease { get; }
        public EventListener onPullUpRelease { get; }

        public GComponent owner { get; }
        public GScrollBar hzScrollBar { get; }
        public GScrollBar vtScrollBar { get; }
        public GComponent header { get; }
        public GComponent footer { get; }

        public bool bouncebackEffect { get; set; }
        public bool touchEffect { get; set; }
        public bool inertiaDisabled { get; set; }
        public bool softnessOnTopOrLeftSide { get; set; }
        public float scrollStep { get; set; }
        public bool snapToItem { get; set; }
        public bool pageMode { get; set; }
        public Controller pageController { get; set; }
        public bool mouseWheelEnabled { get; set; }
        public float decelerationRate { get; set; }

        public bool isDragged { get; }
        public float percX { get; set; }
        public float percY { get; set; }
        public float posX { get; set; }
        public float posY { get; set; }
        public bool isBottomMost { get; }
        public bool isRightMost { get; }
        public int currentPageX { get; set; }
        public int currentPageY { get; set; }
        public float scrollingPosX { get; }
        public float scrollingPosY { get; }
        public float contentWidth { get; }
        public float contentHeight { get; }
        public float viewWidth { get; set; }
        public float viewHeight { get; set; }

        public void SetPercX(float value, bool ani);
        public void SetPercY(float value, bool ani);
        public void SetPosX(float value, bool ani);
        public void SetPosY(float value, bool ani);
        public void SetCurrentPageX(int value, bool ani);
        public void SetCurrentPageY(int value, bool ani);

        public void ScrollTop(bool ani = false);
        public void ScrollBottom(bool ani = false);
        public void ScrollUp(float ratio, bool ani);
        public void ScrollDown(float ratio, bool ani);
        public void ScrollLeft(float ratio, bool ani);
        public void ScrollRight(float ratio, bool ani);
        public void ScrollToView(GObject obj, bool ani = false, bool setFirst = false);
        public void ScrollToView(Rect rect, bool ani, bool setFirst);
        public bool IsChildInView(GObject obj);
        public void CancelDragging();
        public void LockHeader(int size);
        public void LockFooter(int size);
        public void UpdateScrollBarVisible();
    }
}
```

## Root / Popup / Window / DragDrop

```csharp
namespace FairyGUI
{
    public class GRoot : GComponent
    {
        public static float contentScaleFactor { get; }
        public static int contentScaleLevel { get; }
        public static GRoot inst { get; }

        public GGraph modalLayer { get; }
        public bool hasModalWindow { get; }
        public bool modalWaiting { get; }
        public GObject touchTarget { get; }
        public bool hasAnyPopup { get; }
        public GObject focus { get; set; }
        public float soundVolume { get; set; }

        public void SetContentScaleFactor(int designResolutionX, int designResolutionY);
        public void SetContentScaleFactor(int designResolutionX, int designResolutionY, UIContentScaler.ScreenMatchMode screenMatchMode);
        public void SetContentScaleFactor(float constantScaleFactor);
        public void ApplyContentScaleFactor();

        public void ShowWindow(Window win);
        public void HideWindow(Window win);
        public void HideWindowImmediately(Window win);
        public void HideWindowImmediately(Window win, bool dispose);
        public void BringToFront(Window win);
        public void ShowModalWait();
        public void CloseModalWait();
        public void CloseAllExceptModals();
        public void CloseAllWindows();
        public Window GetTopWindow();

        public GObject DisplayObjectToGObject(DisplayObject obj);
        public void ShowPopup(GObject popup);
        public void ShowPopup(GObject popup, GObject target);
        public void ShowPopup(GObject popup, GObject target, PopupDirection dir);
        public void ShowPopup(GObject popup, GObject target, PopupDirection dir, bool closeUntilUpEvent);
        public Vector2 GetPoupPosition(GObject popup, GObject target, PopupDirection dir);
        public void TogglePopup(GObject popup);
        public void TogglePopup(GObject popup, GObject target, PopupDirection dir);
        public void TogglePopup(GObject popup, GObject target, PopupDirection dir, bool closeUntilUpEvent);
        public void HidePopup();
        public void HidePopup(GObject popup);

        public void ShowTooltips(string msg);
        public void ShowTooltips(string msg, float delay);
        public void ShowTooltipsWin(GObject tooltipWin);
        public void ShowTooltipsWin(GObject tooltipWin, float delay);
        public void HideTooltips();
        public void EnableSound();
        public void DisableSound();
        public void PlayOneShotSound(AudioClip clip, float volumeScale);
        public void PlayOneShotSound(AudioClip clip);
    }

    public class PopupMenu : EventDispatcher
    {
        public int visibleItemCount;
        public bool hideOnClickItem;
        public bool autoSize;

        public EventListener onPopup { get; }
        public EventListener onClose { get; }
        public int itemCount { get; }
        public GComponent contentPane { get; }
        public GList list { get; }

        public PopupMenu();
        public PopupMenu(string resourceURL);

        public GButton AddItem(string caption, EventCallback0 callback);
        public GButton AddItem(string caption, EventCallback1 callback);
        public GButton AddItemAt(string caption, int index, EventCallback1 callback);
        public GButton AddItemAt(string caption, int index, EventCallback0 callback);
        public void AddSeperator();
        public void AddSeperator(int index);
        public string GetItemName(int index);
        public void SetItemText(string name, string caption);
        public void SetItemVisible(string name, bool visible);
        public void SetItemGrayed(string name, bool grayed);
        public void SetItemCheckable(string name, bool checkable);
        public void SetItemChecked(string name, bool check);
        public bool IsItemChecked(string name);
        public void RemoveItem(string name);
        public void ClearItems();
        public void Show();
        public void Show(GObject target);
        public void Show(GObject target, PopupDirection dir);
        public void Show(GObject target, PopupDirection dir, PopupMenu parentMenu);
        public void Hide();
        public void Dispose();
    }

    public class Window : GComponent
    {
        public bool bringToFontOnClick;

        public Action __onInit;
        public Action __onShown;
        public Action __onHide;
        public Action __doShowAnimation;
        public Action __doHideAnimation;

        public void AddUISource(IUISource source);
        public GComponent contentPane { get; set; }
        public GComponent frame { get; }
        public GObject closeButton { get; set; }
        public GObject dragArea { get; set; }
        public GObject contentArea { get; set; }
        public GObject modalWaitingPane { get; }

        public void Show();
        public void ShowOn(GRoot root);
        public void Hide();
        public void HideImmediately();
        public void CenterOn(GRoot root, bool restraint);
        public void ToggleStatus();
        public bool isShowing { get; }
        public bool isTop { get; }
        public bool modal { get; set; }
        public void BringToFront();
        public void ShowModalWait();
        public void ShowModalWait(int requestingCmd);
        public bool CloseModalWait();
        public bool CloseModalWait(int requestingCmd);
        public bool modalWaiting { get; }
        public void Init();
    }

    public class DragDropManager
    {
        public static DragDropManager inst { get; }
        public GLoader dragAgent { get; }
        public bool dragging { get; }

        public void StartDrag(GObject source, string icon, object sourceData, int touchPointID = -1);
        public void Cancel();
    }
}
```

## Transition / Tween / Path

```csharp
namespace FairyGUI
{
    public delegate void PlayCompleteCallback();
    public delegate void TransitionHook();

    public class Transition : ITweenListener
    {
        public string name { get; }
        public bool invalidateBatchingEveryFrame;
        public bool playing { get; }
        public float totalDuration { get; }
        public float timeScale { get; set; }
        public bool ignoreEngineTimeScale { get; set; }

        public void Play();
        public void Play(PlayCompleteCallback onComplete);
        public void Play(int times, float delay, PlayCompleteCallback onComplete);
        public void Play(int times, float delay, float startTime, float endTime, PlayCompleteCallback onComplete);
        public void PlayReverse();
        public void PlayReverse(PlayCompleteCallback onComplete);
        public void PlayReverse(int times, float delay, PlayCompleteCallback onComplete);

        public void ChangePlayTimes(int value);
        public void SetAutoPlay(bool autoPlay, int times, float delay);
        public void Stop();
        public void Stop(bool setToComplete, bool processCallback);
        public void SetPaused(bool paused);
        public void Dispose();

        public void SetValue(string label, params object[] values);
        public void SetHook(string label, TransitionHook callback);
        public void ClearHooks();
        public void SetTarget(string label, GObject newTarget);
        public void SetDuration(string label, float value);
        public float GetLabelTime(string label);
    }

    public class GTween
    {
        public static bool catchCallbackExceptions;

        public static GTweener To(float startValue, float endValue, float duration);
        public static GTweener To(Vector2 startValue, Vector2 endValue, float duration);
        public static GTweener To(Vector3 startValue, Vector3 endValue, float duration);
        public static GTweener To(Vector4 startValue, Vector4 endValue, float duration);
        public static GTweener To(Color startValue, Color endValue, float duration);
        public static GTweener ToDouble(double startValue, double endValue, float duration);
        public static GTweener DelayedCall(float delay);
        public static GTweener Shake(Vector3 startValue, float amplitude, float duration);

        public static bool IsTweening(object target);
        public static bool IsTweening(object target, TweenPropType propType);
        public static void Kill(object target);
        public static void Kill(object target, bool complete);
        public static void Kill(object target, TweenPropType propType, bool complete);
        public static GTweener GetTween(object target);
        public static GTweener GetTween(object target, TweenPropType propType);
        public static void Clean();
    }

    public delegate void GTweenCallback();
    public delegate void GTweenCallback1(GTweener tweener);

    public interface ITweenListener
    {
        void OnTweenStart(GTweener tweener);
        void OnTweenUpdate(GTweener tweener);
        void OnTweenComplete(GTweener tweener);
    }

    public class GTweener
    {
        public static EaseType defaultEaseType;
        public static bool defaultIgnoreEngineTimeScale;
        public static float defaultTimeScale;

        public GTweener SetDelay(float value);
        public float delay { get; }
        public GTweener SetDuration(float value);
        public float duration { get; }
        public GTweener SetBreakpoint(float value);
        public GTweener SetEase(EaseType value);
        public GTweener SetEase(EaseType value, CustomEase customEase);
        public GTweener SetEasePeriod(float value);
        public GTweener SetEaseOvershootOrAmplitude(float value);
        public GTweener SetRepeat(int times, bool yoyo = false);
        public int repeat { get; }
        public GTweener SetTimeScale(float value);
        public GTweener SetIgnoreEngineTimeScale(bool value);
        public GTweener SetSnapping(bool value);
        public GTweener SetPath(GPath value);
        public GTweener SetTarget(object value);
        public GTweener SetTarget(object value, TweenPropType propType);
        public object target { get; }
        public GTweener SetUserData(object value);
        public object userData { get; }

        public GTweener OnUpdate(GTweenCallback callback);
        public GTweener OnStart(GTweenCallback callback);
        public GTweener OnComplete(GTweenCallback callback);
        public GTweener OnUpdate(GTweenCallback1 callback);
        public GTweener OnStart(GTweenCallback1 callback);
        public GTweener OnComplete(GTweenCallback1 callback);
        public GTweener SetListener(ITweenListener value);

        public TweenValue startValue { get; }
        public TweenValue endValue { get; }
        public TweenValue value { get; }
        public TweenValue deltaValue { get; }
        public float normalizedTime { get; }
        public bool completed { get; }
        public bool allCompleted { get; }

        public GTweener SetPaused(bool paused);
        public void Seek(float time);
        public void Kill(bool complete = false);
    }

    public struct GPathPoint
    {
        public enum CurveType
        {
            CRSpline,
            Bezier,
            CubicBezier,
            Straight,
        }

        public Vector3 pos;
        public Vector3 control1;
        public Vector3 control2;
        public CurveType curveType;
        public bool smooth;

        public GPathPoint(Vector3 pos);
        public GPathPoint(Vector3 pos, Vector3 control);
        public GPathPoint(Vector3 pos, Vector3 control1, Vector3 control2);
        public GPathPoint(Vector3 pos, CurveType curveType);
    }

    public class GPath
    {
        public float length { get; }
        public int segmentCount { get; }

        public void Create(GPathPoint pt1, GPathPoint pt2);
        public void Create(GPathPoint pt1, GPathPoint pt2, GPathPoint pt3);
        public void Create(GPathPoint pt1, GPathPoint pt2, GPathPoint pt3, GPathPoint pt4);
        public void Create(IEnumerable<GPathPoint> points);
        public void Clear();

        public Vector3 GetPointAt(float t);
        public float GetSegmentLength(int segmentIndex);
        public void GetPointsInSegment(int segmentIndex, float t0, float t1, List<Vector3> points, List<float> ts = null, float pointDensity = 0.1f);
        public void GetAllPoints(List<Vector3> points, float pointDensity = 0.1f);
    }
}
```

## Unity Hosts / Config

```csharp
namespace FairyGUI
{
    public class UIConfig : MonoBehaviour
    {
        public static string defaultFont;
        public static bool renderingTextBrighterOnDesktop;
        public static string windowModalWaiting;
        public static string globalModalWaiting;
        public static Color modalLayerColor;
        public static NAudioClip buttonSound;
        public static float buttonSoundVolumeScale;
        public static string horizontalScrollBar;
        public static string verticalScrollBar;
        public static float defaultScrollStep;
        public static float defaultScrollDecelerationRate;
        public static ScrollBarDisplayType defaultScrollBarDisplay;
        public static bool defaultScrollTouchEffect;
        public static bool defaultScrollBounceEffect;
        public static float defaultScrollSnappingThreshold;
        public static float defaultScrollPagingThreshold;
        public static string popupMenu;
        public static string popupMenu_seperator;
        public static string loaderErrorSign;
        public static string tooltipsWin;
        public static int defaultComboBoxVisibleItemCount;
        public static int touchScrollSensitivity;
        public static int touchDragSensitivity;
        public static int clickDragSensitivity;
        public static bool bringWindowToFrontOnClick;
        public static float inputCaretSize;
        public static Color inputHighlightColor;
        public static float frameTimeForAsyncUIConstruction;
        public static bool depthSupportForPaintingMode;
        public static bool enhancedTextOutlineEffect;
        public static VertAlignType richTextRowVerticalAlign;
        public static bool makePixelPerfect;

        public List<ConfigValue> Items;
        public List<string> PreloadPackages;

        public void Load();
        public static void SetDefaultValue(ConfigKey key, ConfigValue value);
        public static void ClearResourceRefs();
        public void ApplyModifiedProperties();

        public delegate NAudioClip SoundLoader(string url);
        public static SoundLoader soundLoader;
    }

    public enum FitScreen
    {
        None,
        FitSize,
        FitWidthAndSetMiddle,
        FitHeightAndSetCenter,
    }

    public class UIPanel : MonoBehaviour, EMRenderTarget
    {
        public Container container { get; }
        public string packageName;
        public string componentName;
        public FitScreen fitScreen;
        public int sortingOrder;
        public GComponent ui { get; }

        public void CreateUI();
        public void SetSortingOrder(int value, bool apply);
        public void SetHitTestMode(HitTestMode value);
        public void CacheNativeChildrenRenderers();
        public void ApplyModifiedProperties(bool sortingOrderChanged, bool fitScreenChanged);
        public void MoveUI(Vector3 delta);
        public Vector3 GetUIWorldPosition();
    }

    public class UIPainter : MonoBehaviour, EMRenderTarget
    {
        public Container container { get; }
        public string packageName;
        public string componentName;
        public int sortingOrder;
        public GComponent ui { get; }

        public void SetSortingOrder(int value, bool apply);
        public void CreateUI();
        public void ApplyModifiedProperties(bool sortingOrderChanged);
    }

    public class UIContentScaler : MonoBehaviour
    {
        public enum ScaleMode
        {
            ConstantPixelSize,
            ScaleWithScreenSize,
            ConstantPhysicalSize,
        }

        public enum ScreenMatchMode
        {
            MatchWidthOrHeight,
            MatchWidth,
            MatchHeight,
        }

        public ScaleMode scaleMode;
        public ScreenMatchMode screenMatchMode;
        public int designResolutionX;
        public int designResolutionY;
        public int fallbackScreenDPI;
        public int defaultSpriteDPI;
        public float constantScaleFactor;
        public bool ignoreOrientation;

        public static float scaleFactor;
        public static int scaleLevel;

        public void ApplyModifiedProperties();
        public void ApplyChange();
    }
}
```

## Common Enums

```csharp
namespace FairyGUI
{
    public enum PackageItemType
    {
        Image,
        MovieClip,
        Sound,
        Component,
        Atlas,
        Font,
        Swf,
        Misc,
        Unknown,
        Spine,
        DragoneBones,
    }

    public enum ObjectType
    {
        Image,
        MovieClip,
        Swf,
        Graph,
        Loader,
        Group,
        Text,
        RichText,
        InputText,
        Component,
        List,
        Label,
        Button,
        ComboBox,
        ProgressBar,
        Slider,
        ScrollBar,
        Tree,
        Loader3D,
    }

    public enum AlignType { Left, Center, Right }
    public enum VertAlignType { Top, Middle, Bottom }
    public enum OverflowType { Visible, Hidden, Scroll }
    public enum FillType { None, Scale, ScaleMatchHeight, ScaleMatchWidth, ScaleFree, ScaleNoBorder }
    public enum AutoSizeType { None, Both, Height, Shrink, Ellipsis }
    public enum ScrollType { Horizontal, Vertical, Both }
    public enum ScrollBarDisplayType { Default, Visible, Auto, Hidden }

    public enum RelationType
    {
        Left_Left,
        Left_Center,
        Left_Right,
        Center_Center,
        Right_Left,
        Right_Center,
        Right_Right,
        Top_Top,
        Top_Middle,
        Top_Bottom,
        Middle_Middle,
        Bottom_Top,
        Bottom_Middle,
        Bottom_Bottom,
        Width,
        Height,
        LeftExt_Left,
        LeftExt_Right,
        RightExt_Left,
        RightExt_Right,
        TopExt_Top,
        TopExt_Bottom,
        BottomExt_Top,
        BottomExt_Bottom,
        Size,
    }

    public enum ListLayoutType { SingleColumn, SingleRow, FlowHorizontal, FlowVertical, Pagination }
    public enum ListSelectionMode { Single, Multiple, Multiple_SingleClick, None }
    public enum ProgressTitleType { Percent, ValueAndMax, Value, Max }
    public enum ButtonMode { Common, Check, Radio }
    public enum GroupLayoutType { None, Horizontal, Vertical }
    public enum ChildrenRenderOrder { Ascent, Descent, Arch }
    public enum PopupDirection { Auto, Up, Down }
    public enum FlipType { None, Horizontal, Vertical, Both }
    public enum FillMethod { None, Horizontal, Vertical, Radial90, Radial180, Radial360 }
    public enum OriginHorizontal { Left, Right }
    public enum OriginVertical { Top, Bottom }
    public enum Origin90 { TopLeft, TopRight, BottomLeft, BottomRight }
    public enum Origin180 { Top, Bottom, Left, Right }
    public enum Origin360 { Top, Bottom, Left, Right }
    public enum FocusRule { NotFocusable, Focusable, NavigationBase }

    public enum TransitionActionType
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
        Transition,
        Shake,
        ColorFilter,
        Skew,
        Text,
        Icon,
        Unknown,
    }
}
```
