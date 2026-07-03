# AGENTS.md

本项目的 AI 协作约定。开始工作前请先阅读本文件。这是一个 **Unity** 项目。你应该已经连接到 unity-mcp。
优先考虑使用 unity-mcp 来操作工程。

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
有个 abstract 的 Controller<T>: MonoBehavior 。

## codegen
类似于 FairyGUI 自带的 codegen 功能一样，NanamiUI 也可以根据 FairyGUI 工程结构，预先生成脚本。
每个工程里自定义组件会生成出一个类，继承自 Component。里面会有每个子物体的静态引用，通过 prefab 序列化的方式预先挂上去。
每个 Controller 会生成一个类，放在那个拥有它的组件类内，每个 Controller 还会生成一个 Enum，对应于那个 Controller 的每个页。这个 Enum 就是生成的Controller 继承 Controller<T> 的泛型类型。这样生成的每个具体 Controller 就不再是泛型了，可以挂在 GameObject 上了。

## 代码风格

**最简化。** 核心原则是用最少的代码把功能实现出来。

- 能少写就少写，优先最直接的实现。
- 尽量少封装，不要为了"以后可能用到"而提前抽象。
- **不写防御性代码**：假设传入的数据合法，不做多余的空判断、参数校验、异常兜底。
- 不写无关的注释、日志和配置。
- 与周围代码保持一致的命名和风格。

**现代化** 使用最新的写法。
- 使用当前 Unity 版本能够支持的最新的写法。