# OpenFairy SDK for uGUI

基于 uGUI 的 FairyGUI Runtime SDK。继续用 **FairyGUI Editor** 编辑 UI，用 OpenFairy 把 FairyGUI 工程转换成 **uGUI prefab + C# 代码**，并尽量还原 FairyGUI 的渲染、动效与交互表现。

- **烘焙优先**：控件接线、锚点关联、动效、交互全部在转换期烘进 prefab，运行时零动态解析、接近零 GC。
- **复用 uGUI 原生**：`RectMask2D`、`CanvasGroup`、`InputField`、锚点拉伸等直接用 Unity 原生能力，不复刻 FairyGUI 的动态对象模型。
- **静态强类型 codegen**：每个 FairyGUI 组件生成一个 C# 类，控制器是编译期 enum，改名/删页在编译期报错。

## 安装

### 前置依赖（需手动安装）

包依赖以下无法从 Unity Registry 自动解析的库，请先安装：

| 依赖 | 安装方式 |
|---|---|
| [UniTask](https://github.com/Cysharp/UniTask) | Package Manager 添加 git URL：`https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| [ZLinq](https://github.com/Cysharp/ZLinq) | Package Manager 添加 git URL：`https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity` |
| [DOTween](https://dotween.demigiant.com/) | Asset Store 导入到 `Assets/`，运行 DOTween Setup |

`com.unity.ugui`、`com.unity.splines`、`com.unity.mathematics` 会作为包依赖自动安装。

### 安装本包

Unity 菜单 **Window → Package Manager → ＋ → Add package from git URL...**，填入：

```
https://github.com/OpenFairyGUI/OpenFairy-SDK-uGUI.git?path=Assets/Plugins/OpenFairy
```

## 使用

1. 把 FairyGUI 工程放在 Unity 工程根目录的 `UIProject/`（与 `Assets/` 同级）。
2. 菜单 `Tools/OpenFairy/Migrate`：转换生成 `Assets/UIProject/Assets/` 下的 prefab/资源和 `Assets/UIProject/Scripts/` 下的 C# 代码。
3. 把生成的 prefab 挂到你的 Canvas 下即可使用；按钮 tab/单选组、滚动、下拉、窗口拖动等交互已烘焙进 prefab，开箱即用。

源工程与输出位置可通过 `OpenFairy.UGUI.Editor.MigrationSettings.instance` 的
`sourceDirectory`、`outputDirectory` 配置，再调用 `Save()` 保存到
`ProjectSettings/OpenFairySettings.asset`。目录相对于 Unity 工程根目录解析，支持位于工程外的
FairyGUI 源目录；缺省仍为 `UIProject` 和 `Assets/UIProject`。脚本重编译后的自动续建使用同一份配置。
跨源工程的资源包可加入 `dependencyDirectories`，仅导出主工程引用到的依赖。

## Linear 工程中的 Gamma UI

在 **Project Settings → OpenFairy → Gamma UI Rendering** 开启后执行 **Apply / Migrate UI**，再手动给实际使用的 Universal Renderer Data 添加并启用 **OpenFairy Gamma UI** Renderer Feature。SDK 不自动修改 URP 配置，不增加 UI 合成 RT。适用条件及材质要求见 [Gamma UI 文档](Assets/Plugins/OpenFairy/Documentation~/GammaRendering.md)。

## 全局 TextMeshPro

定义 `FAIRYGUI_TMPRO` 后执行 `Tools/OpenFairy/Migrate`，所有普通文本、富文本和输入框分别使用原生 `TextMeshProUGUI` / `TMP_InputField`，无需在 XML 配置 `tmp="true"`。按钮标题、Label、进度条、滑块、文字/字号 gear 和文字动效使用同一后端；生成字段通过编译宏选择强类型。

字体优先使用可选 `tmpFont` 指定的 TMP Font Asset；其次按 FairyGUI `font` 匹配 TMP 字体资源；未指定字体时使用 `TMP Settings` 的默认字体。字体必须覆盖需要显示的字符，缺失配置会在发布时报错。`tmpFont` 仅用于字体覆盖，不是启用 TMP 的条件。

移除宏后代码恢复传统文本分支。每次切换宏都需重新 Migrate，才能让 Prefab 的组件和生成代码对应。项目手写代码使用 TMP 的原生 API，或通过 `TextRendering` 设置两种后端共有的样式。

UBB 的 `b/i/u/s/size/color/url` 会在 TMP 文本预处理阶段转换，绑定和 gear 中保留源文本；八位颜色从 FairyGUI 的 AARRGGBB 转为 TMP 的 RRGGBBAA。TMP 分支尚未验证传统 HTML、行内图片和链接点击行为的完整兼容性，旧版 parity 成绩不代表 TMP 的覆盖结果。

## 仓库结构

本仓库同时是 SDK 的开发工程：`Assets/Plugins/OpenFairy/` 是对外发布的 UPM 包；`UIProject/` 是 FairyGUI 官方 demo 工程，`Assets/Tests/` 是与 FairyGUI 逐像素/逐交互对照的 parity 测试（传统文本分支历史验证 134/134 通过；TMP 验证另见 Docs/BasicsImplementationStatus.md）。
