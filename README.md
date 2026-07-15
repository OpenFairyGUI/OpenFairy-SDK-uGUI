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
https://github.com/nanamicat/OpenFairy-SDK-uGUI.git?path=Assets/Plugins/OpenFairy
```

或直接编辑 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.openfairy.ugui": "https://github.com/nanamicat/OpenFairy-SDK-uGUI.git?path=Assets/Plugins/OpenFairy"
  }
}
```

如需锁定版本，在 URL 末尾加 `#<tag或commit>`。

要求 Unity 6000.0 及以上。

## 使用

1. 把 FairyGUI 工程放在 Unity 工程根目录的 `UIProject/`（与 `Assets/` 同级）。
2. 菜单 `Tools/OpenFairy/Migrate`：转换生成 `Assets/UIProject/Assets/` 下的 prefab/资源和 `Assets/UIProject/Scripts/` 下的 C# 代码。
3. 把生成的 prefab 挂到你的 Canvas 下即可使用；按钮 tab/单选组、滚动、下拉、窗口拖动等交互已烘焙进 prefab，开箱即用。
4. 运行时字体：烘焙按 `UIProject/settings/Common.json` 的设计期字体写入每个 `TextField.fontNames`（Common.json 允许不存在，缺省 Arial）。若运行时想换字体（对应 FairyGUI 的 `UIConfig.defaultFont`），在实例化后遍历 `TextField` 按 `fontNames` 替换即可，参考本仓库 `Assets/Scripts/DemoFont.cs`。

更多细节见包内 [`README`](Assets/Plugins/OpenFairy/README.md) 与 [`Docs/OpenFairy-Runtime-API.md`](Docs/OpenFairy-Runtime-API.md)。

## 仓库结构

本仓库同时是 SDK 的开发工程：`Assets/Plugins/OpenFairy/` 是对外发布的 UPM 包；`UIProject/` 是 FairyGUI 官方 demo 工程，`Assets/Tests/` 是与 FairyGUI 逐像素/逐交互对照的 parity 测试（当前 134/134 通过）。
