# Linear 工程中的 Gamma UI

## 开启与关闭

1. 打开 **Edit → Project Settings → OpenFairy**。
2. 勾选 **Gamma UI Rendering**，点击 **Apply / Migrate UI**。
3. 在项目实际使用的 **Universal Renderer Data → Add Renderer Feature** 中添加 **OpenFairy Gamma UI** 并启用。使用多套 Quality/Renderer 配置时，分别添加。
4. 将生成的 UI 放在 **Screen Space - Overlay** Canvas 下。

导入选项默认关闭。关闭时，取消勾选并再次 Migrate，然后停用 Renderer Feature。不要只停用 Renderer Feature：Gamma 纹理、顶点色和混合状态必须一起切换。导入选项保存在 `ProjectSettings/OpenFairySettings.asset` 的 `gammaRendering` 字段中；改变设置后，已有产物必须重新迁移。

SDK 不自动修改 URP Asset 或 Renderer Data。Renderer Feature 由使用者手动配置；Migrate 只准备生成的 UI 资产。运行时生成的 SDK 弹窗继承 Canvas 的 Gamma 顶点色设置，无需另外创建 UI 相机或配置 shader 列表。

若已自行准备好 Gamma 纹理、兼容材质及 Canvas 的 Gamma 顶点色设置，可以直接启用 Feature，跳过 Migrate 的资源准备选项。Feature 本身不依赖生成的组件标记。

## 工作方式与开销

- 导入的 UI 图片使用 `sRGBTexture=false`，保留原始 Gamma 数值并在 Gamma 数值上过滤；不在运行时复制纹理。
- 生成 prefab 的 `GammaUI` 设置所属 Canvas 的 `vertexColorAlwaysGammaSpace`；SDK 材质的 `_OpenFairyGamma` 保留 Gamma 顶点色。最后一个使用者停用时恢复 Canvas 原值。
- Renderer Feature 的轻量 Render Graph pass 记录已有屏幕颜色/深度目标，录制期间暂时跳过默认 Overlay pass，执行前恢复所有权。之后在公开 `endCameraRendering` 回调中、相机原有场景提交完成后，以 `GL.sRGBWrite=false` 创建并提交原生 UI RendererList；finally 立即恢复原值，再提交 IMGUI/软件光标。
- Feature 替代 URP 默认 Overlay UI pass，使用相同的 Unity 原生 UI RendererList 绘制 UI 一次。没有 UI 合成 RT、全屏拷贝或第二次 UI 绘制；相较原路径增加两次 `ScriptableRenderContext.Submit()`。场景和编辑器 Gizmos 都先完成，UI 最后覆盖在它们上面。
- 不能只在原 pass 外侧直接切状态：图中途 Submit 会使 URP 提前创建的 UI/Gizmos 等 RendererList 失效。也不能用 `Graphics.ExecuteCommandBuffer` 直接执行全部场景命令代替提交：受测场景的三维绘制丢失。因此等原相机提交完成后才创建新的原生 UI 列表，不在 Render Graph 内中途 Submit。
- 已使用模糊或容器颜色滤镜的对象仍使用这些效果原有的捕获 RT；Gamma 模式下这些 RT 保存原始 Gamma 数值。本功能不为普通 UI 增加 RT。

Canvas 内置的 **Vertex Color Always In Gamma Color Space** 单独使用只改善 Linear 工作流中的转换精度；内置 UI shader 仍会转换为 Linear。它不能单独实现本功能的 Gamma 采样和透明混合。

## 使用条件

- **Unity 6.6+、URP 17.6+、Render Graph、Universal Renderer、Direct3D 11/12、SDR 屏幕输出**。本功能限定在该版本范围；其他 SDK 功能仍不依赖 URP。已验证的具体版本与结果见仓库状态文档。
- 适用于最终屏幕的 **Screen Space - Overlay** UI。World Space、Screen Space - Camera、相机 RenderTexture 输出、XR、HDR 显示输出不属于支持路径。内部 HDR 场景渲染与 SDR 最终输出是不同概念。
- 设置作用于最终 Overlay 中的 uGUI/UI Toolkit 绘制阶段。混用的其他 UI 也必须使用 Gamma 数值和兼容材质；不能在同一次原生 Overlay 绘制中为不同 Canvas 选择不同混合空间。UI Toolkit backdrop filter 合成路径不属于支持范围。IMGUI/软件光标在恢复状态后绘制。
- 生成的 SDK 图片、图形和 Builtin 文本材质自动处理。自定义材质、TMP 材质以及自行增加的 Graphic 需保持 Gamma 颜色并采用匹配的混合方式；完整 TMP 材质组合尚未做本功能的 GPU 认证。
- 外部 UI 纹理也要保存原始 Gamma 数值：导入贴图关闭 sRGB，运行时 `Texture2D` 使用 `linear:true`。用材质、贴图同时驱动 3D 与 UI 时，为两种用途分别配置资产。
- Renderer Feature 应保留在其他会修改相机/颜色写入状态的功能之后。升级 URP 后应重新验证 UI 前后的提交顺序。

本功能解决颜色空间与混合路径，不表示 OpenFairy 和 FairyGUI 的全部布局、字体、采样、alpha 量化已经达到浮点精度一致。
