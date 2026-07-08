# Basics / Transition 页面实现状态

本文件记录 `Assets/FairyGUI/Examples` 的 Basics 与 Transition 示例对应到 NanamiUI 的覆盖情况。截图对比脚本会把 FairyGUI、NanamiUI、diff 三张图输出到 `Docs/RenderDiff`。

运行方式：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`。

截图流程（播放对齐截屏）：每页 FairyGUI 与 NanamiUI **同帧创建、同时播放**（含 t0 动效、movieclip、Relation 等运行时行为），经过 1 秒后**同帧截屏**——两侧用同一 Time.deltaTime 序列推进，动画相位一致，对比的是真实播放中的最终画面。动效起播第一帧按 GTweener smoothStart 语义钳制 dt。

差异像素占比为 2026-07-07 播放式截图的实测值（diff 中任一通道 >1/255 即计入，含反锯齿噪声；纯反锯齿差异的页面约 1%~3%）。

| 页面 | 差异占比 | 说明 |
| --- | --- | --- |
| Main | 2.85% | 完成。差异全部为动态字体反锯齿噪声。 |
| Button | 2.58% | 完成。含 grayed 控制器约定、OnOffButton gearXY 默认值。 |
| Image | 15.98% | 基本完成。残差：平铺单元 ~1px 重叠（图集 duplicate padding，已知），九宫格渐变 banding。翻转九宫格（n18，6_png flip=vt）已修：`FlipImage.SliceFillFlipped` 复刻 FairyGUI 的 grid-rect 翻转 + 预翻转 uvRect，非对称边框厚度不再错位。 |
| Graph | 完成 | 已复刻运行时 `PlayGraph`：pie 扇形（`Shape` 椭圆 start/endDegree）、trapezoid 带贴图百分比多边形、line/line2/line3 折线（新 `Line` 组件 + `LineMesh` 移植，走 `TransitionPath`），line 的 fillEnd 5s 线性扫入。逻辑抽到 `NanamiUI.Example.GraphDemo`，运行时 `BasicsMain` 与截图脚本共用；对比脚本两侧同步驱动 `PlayGraph`+fillEnd。残差：trapezoid 贴图 FairyGUI 用投影校正 UV、uGUI 仿射，轻微透视差（已知，需自定义 shader 写 uv.w 才能消除）。 |
| MovieClip | 0.93% | 完成。播放相位与 FairyGUI 对齐。 |
| Depth | 2.36% | 完成（静态）。 |
| Loader | 1.09% | 完成。 |
| List | 3.52% | 完成。滚动条（UIConfig 默认 ScrollBar_VT/HZ）已生成，grip 按显示比例。 |
| ProgressBar | 0.49% | 完成。 |
| Slider | 0.07% | 完成。 |
| ComboBox | 0.16% | 完成（静态）。 |
| Clip&Scroll | 7.83% | 基本完成。残差：滚动条细节（箭头/滑块态）与底部小字号断行个别字。 |
| Controller | 3.49% | 完成。 |
| Relation | 1.78% | 完成。 |
| Label | 1.63% | 完成。 |
| Popup | 0.19% | 完成（静态）。 |
| Window | 0.40% | 完成（静态）。 |
| Drag&Drop | 1.26% | 完成（静态）。 |
| Component | 2.55% | 完成。 |
| Grid | 2.51% | 完成。 |
| Text | 5.54% | 基本完成。残差：UBB 混合字号段落断行差 1 字、行内图片行基线 2~3px。 |
| Transition/BOSS | 0.00% | 完成。路径动画 + 嵌套动效（ColorFilter/Scale 循环）逐帧一致。 |
| Transition/BOSS_SKILL | 0.38% | 完成。 |
| Transition/TRAP | 0.00% | 完成。Back.Out 旋转链逐帧一致。 |
| Transition/GoodHit | 14.19% | 视觉一致（Bounce 缩放 + Shake + 逐字位图字体）；小画布上血迹图反锯齿与 Shake 随机性撑高百分比。 |
| Transition/PowerUp | 30.61% | 视觉一致（Expo 滑入、数字滚动、火焰特效、淡出位移）；火焰 movieclip 帧相位 ±1 帧 + 加色发光在小画布上撑高百分比。 |
| Transition/PathDemo | 0.20% | 完成。贝塞尔路径 + 无限循环动效逐帧一致。 |

## 动效（Transition）实现

纯数据 + 轻量运行时：`Migrate` 把 `<transition>` 解析为 `NanamiUI.Transition` 组件（item 数组直接序列化，目标为对象引用），运行时逐帧求值。缓动用 DOTween 的 `EaseManager.Evaluate`（与 FairyGUI 内置的 Penner 方程同源，数值一致）；路径为 GPath 的移植（`TransitionPath`）。已支持 item 类型：XY（含路径）/Size/Scale/Pivot/Alpha/Rotation/Color/Animation/Visible/Sound/嵌套 Transition/Shake/ColorFilter/Text，repeat(-1 无限)/yoyo、autoPlay、播放次数与延迟、`Play/Stop/Step`。ColorFilter 通过 `ColorAdjust` + `NanamiUI/UI ColorMatrix` shader（亮度/对比度/饱和度/色相矩阵）。

## 已知差异

- **平铺（tile）图片**：FairyGUI 发布图集带 duplicate padding，平铺单元间有 ~1px 内容重叠；NanamiUI 用原图平铺无重叠。
- **动态字体反锯齿**：两侧各自创建动态字体图集，边缘像素有 1~2/255 级差异，无视觉差异（同区域平均亮度实测相等）。
- **截图底色**：截图相机使用不透明底色 #363C44（FairyGUI 文字 shader 直通 alpha 与 uGUI 预乘 alpha 在透明底上的 alpha 通道不同，实底下两侧像素一致）。
- **Shake 与 movieclip 帧相位**：Shake 为随机偏移，两引擎随机序列不同；movieclip 激活时机可能差 ±1 帧。仅影响 GoodHit/PowerUp 的百分比，不影响观感。

## 通用导出器与本次改动（2026-07-07）

- **通用导出器（Migrate.cs）**：移除所有 Basics/Transition 专属假设（`Entries[]`、`BasicsDemoNames[]`、硬编码滚动条名、`ConfigureBasicsExample`、`Basics/Main.xml` 特判、`Microsoft YaHei` 硬编码）。现在按 `package.xml` 的 `exported="true"` 发现组件，`Collect` 依赖闭包；`OutputRoot` 从 `UIProject` 目录名推导。运行时 UIConfig 类配置（默认字体、滚动条、按钮音）来自 `settings/Common.json` + 可选的 `NanamiUISettings` ScriptableObject（`Assets/Plugins/NanamiUI/NanamiUISettings.asset`）。`NanamiUISettings.packages` 限定导出包（留空=全部），本工程设为 `Basics,Transition`。示例胶水通过 `[MigratePostProcess]` 扩展点（`BasicsExample.cs`）自动挂载，不再污染通用导出器。已实测导出 97 个组件无报错。
- **C# 关键字转义**：`Identifier()` 现在对 C# 关键字（如 button 控制器的 `checked` 页）转义为 `_checked`，否则生成的 enum/类名非法。任意合法 FairyGUI 工程都可能撞上。
- **声音（Basics demo 无音效）**：NanamiUI 本身静音（`Common.json.buttonClickSound` 为空）。之前的点击音来自 SampleScene 里**活着的 FairyGUI 参照 UIPanel**（官方 `BasicsMain.Awake` 设 `UIConfig.buttonSound="click"`）。已在 SampleScene 把该 UIPanel 置为 inactive 并禁用其 `BasicsMain`——正常播放只剩静音的 NanamiUI；截图对比脚本用 `FindObjectsInactive.Include` 显式重新激活它，比对不受影响。
- **文字宽度（Component 偏大）**：实测为**已知的 alpha 边缘晕**误判，非字号问题。scaleFactor=1 下 NanamiUI 与 FairyGUI 逐字 advance 逐像素相同（"Component" 两侧均 127px）。工程内无 `autoSize="shrink"/"ellipsis"`，无需实现收缩。（潜在的 `keepCrisp` 亚像素路径仅在 scaleFactor>1 生效，当前管线恒为 1，未实现。）
- **切换页面动效（gear tween）**：`Gear` 基类新增 `tween/duration/ease/delay`（默认 QuadOut/0.3s/0，复刻 FairyGUI `GearTweenConfig`），`Controller.page` setter 传 `Application.isPlaying` 作 animate 标志（烘焙=编辑态直接置位，运行时切页缓动，等价 FairyGUI `_constructing==0`）。`GearXY/GearSize/GearLook` 用 DOTween 驱动，切页时 kill 旧 tween、`current==end` 跳过。Main 的 container/btns（`Main.xml` gearXY tween=true）现在切页时滑入/滑出；已实测运行时 c1→1 时 container 由 (-1143) 缓动而非瞬移。Migrate 对所有 gear 读取 `tween/ease/duration/delay`。
- **Graph 左下 line 颜色**：并非 SDK 渲染错误。经逐像素比对，红色圆头帽两侧完全一致 (202,55,55)，仅绿色段**长度**不同——因截图脚本对 NanamiUI 侧 `Canvas.ForceUpdateCanvases` 立即重建、却没强制重建 FairyGUI 侧网格，导致 FairyGUI 参照 `fillEnd` 滞后一个采样。已在 `DriveGraph` 补 `_fairyGraphLine.UpdateMesh()`，diff 中该 line 已干净。`Line.cs/GraphDemo.cs` 无需改动。

## 运行时交互（2026-07-08 已全部复刻）

以下 `BasicsMain.Play*` 交互现已在 NanamiUI 复刻（PlayMode 39/39，含端到端 DemoSmokeTest）。详见 AGENTS.md 尾部与 [[nanamiui-demo-parity-roadmap]]。
- **翻页动画**：Main 进/退 Demo 时旧页飞出再消失（gearDisplay+gearXY display-lock）。
- **ProgressBar**：值循环（每帧 +1 回绕）。
- **Grid**：`RuntimePlatform` 名 + 随机数据填两个列表（GList 填表 + ScrollPane 滚动）。
- **Depth**：sortingOrder 兄弟序 + 可拖 + 运行时建矩形。
- **Drag&Drop**：自由拖 / dragBounds 钳制 / DragDropManager agent + DropTarget。
- **Window / Popup**：GRoot 覆盖层 + Window（含 Window2 缩放进出）+ PopupMenu。
- **Text 链接点击**：`onClickLink` + UBB `<a href>` 命中；文本拷贝。
- **List / Clip&Scroll / ComboBox 下拉**：ScrollPane 拖动滚动；ComboBox 弹下拉选项。
- **MovieClip**：运行时自播（`Update` 推帧）。

**已重烘焙**（`Tools/Migrate` 全量重跑，仍 39/39）：ComboBox items + dropdown 已烘焙成 `NanamiUI.ComboBox<T>`（不再硬编码）；`ListSource` 已烘焙（`GList.Fill` 动态建项）；Window1 列表填 6 项。**仍存的取舍**（AGENTS.md 详述）：popup 外点关闭用透明 blocker（新 Input System 不能读旧 Input）故表现为模态；ScrollPane 仅拖动无惯性；ComboBox 下拉不裁剪滚动、Dropdown 变体(n4/n5)不接；Grid 只设可见文本。

### SDK 修复：Text 链接点击抢走按钮导航（2026-07-08）

Text 链接点击支持让 `NanamiUI.Text` 实现了 `IPointerClickHandler`，但默认 `raycastTarget=true`。主页按钮的标题是子 Text，真实 Unity 输入会出现 pointerDown 命中父 Button、pointerClick 候选命中子 Text 的情况，导致 `Button.onClick` 不触发；旧 smoke test 直接 `onClick.Invoke()`，所以没抓到。修复：`Text` 默认 `raycastTarget=false`，`onClickLink` 改为属性，只有赋链接回调时才打开 raycast。新增 `MainNavigationTests.Real_pointer_clicks_open_demo_and_back_to_home` 走 `GraphicRaycaster` 真实命中验证主页进/退 Demo；`TextLinkTests` 仍通过，富文本链接不受影响。

## 截图输出约定

每个页面会生成 `{Page}_fairygui.png`、`{Page}_nanami.png`、`{Page}_diff.png`；`&`、`/` 会替换为 `_`。

## 自动化回归测试（静态 golden，PlayMode）

`Assets/Tests` 下有一套基于 Unity Test Framework 的静态 golden parity 回归测试，作为 `Capture Basics Render Diff` 之上的**可判定门禁**（差分工具只出图给人看，无 pass/fail）。

- **参照（golden）**：FairyGUI 末帧，由 `BasicsRenderDiff` 菜单 `Tools/NanamiUI/Generate Golden References` **一步直接渲染写入** `Assets/Tests/Golden/ReferenceImages/`（已 gitignore、不入库，按需现生成；同一次跑也照常输出 `Docs/RenderDiff/` 差分图）。**它答的是"NanamiUI 有没有偏离 FairyGUI"，不是自比回归。**
- **测试**：`NanamiUI.Tests.PlayMode` 的 `StaticGoldenTests` 逐页渲 NanamiUI 末帧，与 golden 做 >2/255 RGB diff，超每页阈值（`ParityCatalog.StaticPages`）即失败。
- **程序集**：新增 3 个 asmdef —— `NanamiUI.Runtime`（包住 `Runtime/`）、`NanamiUI.TestSupport`（渲染器/目录/diff）、`NanamiUI.Tests.PlayMode`。FairyGUI **不进**测试程序集（golden 由 `BasicsRenderDiff`（编辑器预定义程序集，能引 FairyGUI）渲染生成，测试只读 PNG）。
- **范围（17 页）**：末帧收敛、无 per-demo 胶水的静态页。**排除** Graph/Main（需胶水）、MovieClip/ProgressBar（循环 / 墙钟计时器）、所有 Transition_*（动效轨迹）——这些属后续"实时双跑"层（FairyGUI + NanamiUI 同帧推进再 diff；因 `unscaledTime` 破坏轨迹相位，冻结成静态 golden 会脆）。
- **跑法**：`Window > General > Test Runner > PlayMode > Run All`；无头 `Unity.exe -runTests -batchmode -testPlatform PlayMode -testResults r.xml`（**不要** `-nographics`，URP 需 GPU 才能渲染像素）。
- 2026-07-08 首次全扫 **17/17 通过**，阈值已按实测收紧（见 `ParityCatalog` 尾注）。

**拆 asmdef 的坑**：把 `Runtime/` 从 Assembly-CSharp 拆成独立 asmdef 后，`GearXY` 的 `rt.DOAnchorPos()` 编译失败——该扩展定义在 DOTween 松散模块 `DOTweenModuleUI.cs`（编在 Assembly-CSharp），Runtime 独立后够不到。已改用核心 `DOTween.To()`（在 `DOTween.dll` 内，自动引用可用）。

## 交互几何回归（PlayMode，2026-07-08 新增）

静态 golden 已保证"任意状态渲成像素 = FairyGUI"，故交互测试只比**几何快照**不比像素（组合原理）。产物：`Assets/Tests/Support/GeometrySnapshot.cs`、`InteractionDriver.cs`、`ParityCatalog.InteractionCase[]`、`Assets/Tests/PlayMode/InteractionGeometryTests.cs`、`BasicsRenderDiff` 里的 FairyGUI 侧 dump。

- **流程**：实例化烘焙态 prefab → 经真实 handler 把目标驱动到某交互态（NanamiUI 只用非泛型 Runtime 面 `Slider`/`IPointerClickHandler`/`IPointerDownHandler`，因测试程序集够不到生成的 `UI.{包}` 类型）→ settle 掉 gear 缓动 → 快照目标子树几何（`path/rect/active/text`，相对目标左上、页面设计像素、y 向下）→ 与 FairyGUI 参照比（参照⊆实测，epsilon 默认 1.5px）。
- **参照**：`Generate Golden References` 同一趟额外写 `{case}.geo.json`（同 gitignore、按需现生成）。FairyGUI 有效可见性取 `displayObject.parent!=null && displayObject.visible`（**非** `GObject.visible`，后者恒 true）。missing 参照现在 `Assert.Fail`（不再静默 Ignore；静态也改了）。
- **首批 5 case**：Slider 横/竖拖到值、Checkbox 勾选、Common 按钮按下。**22/22 通过**（17 静态 + 5 交互）。
- **无头跑**：`Tools/NanamiUI/Run PlayMode Tests`（`Assets/Editor/NanamiUI/TestRunner/`），结果写 `Temp/NanamiUIPlayModeResults.txt`。

## SDK 修复：运行时 gears 反序列化为 null（2026-07-08）

交互测试首跑即抓到真 bug：点 checkbox → `Controller.page` 遍历 gears → **NRE**。根因是 asmdef 拆分把 NanamiUI 移出 `Assembly-CSharp-firstpass` 后，**拆分前烘焙的 prefab** 的 `[SerializeReference]` 仍记旧程序集，Unity 找不到类型 → gears 数组元素全 null。静态 golden 没抓到（烘焙期把 gear 结果烤进 prefab，运行时不重跑 gears）。已把 36 个 prefab 里 `ns: NanamiUI, asm: Assembly-CSharp-firstpass` 改写为 `asm: NanamiUI.Runtime` 修复。**运行时所有 gears/控制器切页现已可用。** 详见 AGENTS.md 与记忆。

### 尚未覆盖 / 下一步

- **动画 gears（GearXY/Size/Look）的交互终态**：这些 gear 运行时经 DOTween 缓动。NanamiUI 侧 settle 帧数已覆盖；但 FairyGUI 侧 dump 是**同步一帧**，不会 settle FairyGUI 的 GTweener，故带缓动的交互态（如 OnOffButton 拨钮滑动、切页 gearXY）参照会取到未 settle 的位置。需把 dump 改成推帧 settle（或 kill-complete FairyGUI tweener）才能覆盖。
- **被运行时阻塞的交互**：List 滚动、ComboBox 下拉、Window/Popup、Drag&Drop、Text 链接点击——需先建对应 Runtime（ScrollPane/GRoot 覆盖层/Window/PopupMenu/DragDropManager）。
- **Controller 按名切页驱动**：泛型 `Controller<T>` + 生成类型不可达，暂用 Button 间接覆盖；需要时给 SDK 加非泛型按名取 controller 面（对标 FairyGUI `GComponent.GetController`）。
