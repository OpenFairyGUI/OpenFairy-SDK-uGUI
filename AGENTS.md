# AGENTS.md

本项目的 AI 协作约定。开始工作前请先阅读本文件。

## 环境

- **代码检索**: 如果当前目录下存在 .codegraph 目录，请优先使用 codegraph MCP 来检索代码。否则运行 `codegraph init` 初始化 (可能用时较长请耐心等待)。如果连 codegraph 命令行也不存在，用 npm 安装：
  - `npm i -g @colbymchenry/codegraph`
- **命令行工具**：系统已安装 `ripgrep`（`rg`）和 `coreutils`，有需要请直接调用。如果命令找不到，用 winget 安装：
  - `winget install BurntSushi.ripgrep.MSVC`
  - `winget install uutils.coreutils`
- **项目类型**：这是一个 **Unity** 项目。你应该已经连接到 MCP for Unity；如果没有，请提示用户安装。

## 项目目标

本项目的目标是实现是一个 FairyGUI 的三方 SDK，叫做 NanamiUI，基于 uGUI。希望能完美还原 FairyGUI 的效果，让用户可以继续用 FairyGUI Editor 做编辑器，用基于 uGUI 的 NanamiUI SDK 做 Runtime。
每个组件转换后是一个 prefab。
可以像 FairyGUI 自带的生成代码功能那样根据 FairyGUI 工程结构生成出对应的 C# 代码。

## 目录结构

待转换的 FairyGUI 项目目录在 UIProject。
插件脚本根目录在 Assets/Plugins/NanamiUI。有下列子目录：
Editor：转换的脚本，命名空间为 NanamiUI.Editor。
Runtime：运行时 SDK，命名空间为 NanamiUI。包含基础组件的定义，例如 Controller 等。

转换生成的产物根目录应该在 Assets/{FairyGUI项目名，这里叫UIProject}。有下列子目录：
Assets：需要用到的字体、图片等资源文件，也包含转换后的组件，每个组件是一个 prefab。保持以前的目录结构。
Scripts：codegen 生成的代码。

## 代码结构
SDK (Assets/Plugins/NanamiUI/Runtime) 里包含每个基础组件的定义，跟 FairyGUI SDK 里的 GObject 子类 1:1 对应，有 Text、Button、Label 等。
他们都是 UIBehaviour 的子类，也可以继承 UnityEngine.UI.Selectable、UnityEngine.UI.Button 这些更具体的，看怎样弄会让SDK代码最简单。
Controller 是 struct Controller<T> where T : struct, Enum，不是 MonoBehaviour。
Gear 及其子类也不是 MonoBehaviour，并且是泛型，泛型类型是对应 Controller 的 enum。
Button 也是泛型，泛型类型是对应 Controller 的 enum。

## codegen
类似于 FairyGUI 自带的 codegen 功能一样，NanamiUI 也可以根据 FairyGUI 工程结构，预先生成脚本。
每个工程里自定义组件会生成出一个类，继承自 Component。里面会有每个子物体的引用，通过 prefab 序列化的方式预先挂上去，字段名加 m_ 前缀。
每个 Controller 不再生成单独的类。组件类里直接生成一个同名 enum，枚举值对应 Controller 的每个页；组件类里再生成 Controller<该 enum> 字段，字段名加 m_ 前缀。
Button 组件继承 Button<对应 Controller enum>。

## 代码风格

**最简化。** 核心原则是用最少的代码把功能实现出来。

- 能少写就少写，优先最直接的实现。
- 尽量少封装，不要为了"以后可能用到"而提前抽象。
- **不写防御性代码**：假设传入的数据合法，不做多余的空判断、参数校验、异常兜底。
- 不写无关的注释、日志和配置。
- 与周围代码保持一致的命名和风格。

**现代化** 使用最新的写法。
- 使用当前 Unity 版本能够支持的最新的写法。

## 验证与调试

**渲染对比**：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`（脚本 `Assets/Editor/NanamiUI/BasicsRenderDiff.cs`）进 Play 模式，把 FairyGUI 参照与 NanamiUI 产物逐页同帧渲染对比，输出到 `Docs/RenderDiff/`：
- `{page}_strip.png`：动效胶片条，上排 FairyGUI / 中排 NanamiUI / 下排像素 diff，列=采样时刻，用来看动效轨迹与相位是否一致。
- `{page}_fairygui|_nanami|_diff.png`：末帧全分辨率三联图，看静态精度。

改完组件/转换/动效后跑一次对着 diff 确认。diff 里红色=差异；**~1px 文字/精灵边缘晕是已知残留**（FairyGUI 直通 alpha vs uGUI 预乘 + 亚像素采样），可忽略。Shake 用随机数，两侧无法逐像素对齐，也不该追。

截图脚本直接 `UIPackage.CreateObject` 建 FairyGUI 参照视图，**不跑官方 demo 的初始化**，所以凡是 demo 里 `UIConfig.*` 配的东西都要在脚本里补上，否则产生假 diff。已补：`defaultFont` 以及滚动条资源 `UIConfig.verticalScrollBar/horizontalScrollBar = "ui://Basics/ScrollBar_VT|HZ"`（不补 FairyGUI 侧就没滚动条、viewport 用满宽）。

**动效同步的坑（重要）**：`Time.captureDeltaTime` 只锁 `Time.deltaTime`，**不锁 `Time.unscaledDeltaTime`**（已实测坐实）。FairyGUI 的 GTweener/Transition 默认 `ignoreEngineTimeScale=true` 用 unscaledDeltaTime、按真实墙钟推进；NanamiUI Transition 用 deltaTime。在确定性截图（captureDeltaTime）下两者会脱同步，表现为 FairyGUI 动效"一闪而过、中段帧空白"。对比脚本已在播放前把 FairyGUI transition 的 `ignoreEngineTimeScale` 设为 false 规避（会传播到 nested）。这跟 Player Settings 的 Run In Background 无关。另注：生产中若游戏改 `timeScale`，两侧语义仍分歧（NanamiUI 跟 timeScale、FairyGUI 不跟），要完全对齐或需让 NanamiUI Transition 改用 unscaledDeltaTime。

**unity-mcp 控制台失灵**：本项目 `read_console` / `GetConsoleLogs` 即使有报错也返回空，不可依赖。排错改用：
- 编译/运行时错误：`grep "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log"`（或 `-Tail`）。刷新编译用 `refresh_unity(compile=request)`。
- 一次性操作：`RunCommand` 里包 try/catch，用 result.LogError 输出（禁止 System.Reflection 命名空间）。

**文字排版**：`NanamiUI.Text` 在 `OnPopulateMesh` 里复刻 FairyGUI 公式（不用 TextGenerator 布局）。改排版要对照 `Assets/FairyGUI/Scripts/Core/Text/{TextField,DynamicFont}.cs`。

**翻转 + 平铺图的相位**：tile 图导入为独立贴图 + `wrapMode=Repeat`，会走 Unity `Image.Type.Tiled` 的单 quad repeat 优化（UV 从 0 跑到 N=瓦片数）。`FlipImage` 翻转时**必须绕 sprite 自身的 UV 区间**（`DataUtility.GetOuterUV`）镜像，而非整块 mesh 的 UV min/max——后者会绕整条跨度中心反射，把残缺瓦片翻到原点侧、与 FairyGUI 差半格相位。
