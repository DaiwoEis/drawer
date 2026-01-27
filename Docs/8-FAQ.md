# 常见问题与故障排除 (FAQ)

## 1. 常见问题

**Q: 为什么第一次画线时会卡顿一下？**
A: 这是 Unity Shader 编译造成的。我们已经实现了 `ShaderVariantCollection` 预热机制。如果仍然卡顿，请检查：
1. `DrawingShaderVariants` 资源是否存在。
2. `CanvasRenderer` 是否正确引用了该资源。
3. 运行 `Tools/Drawing/Assign Shader Variants` 重新绑定。

**Q: 为什么修改了分辨率后，笔画看起来错位了？**
A: `StrokeCollisionService` 依赖 `LogicToWorldRatio` 进行碰撞检测。请确保 `DrawingAppService` 中的 `UpdateResolutionRatio` 方法被正确触发。系统应自动监听分辨率变化事件。

**Q: 如何添加新的笔刷效果？**
A:
1. 编写新的 Shader（支持 GPU Instancing 推荐）。
2. 创建新的材质 Material。
3. 创建 `BrushStrategy` ScriptableObject，配置材质和参数。
4. 将其赋值给 `DrawingAppService`。

**Q: 橡皮擦擦不干净或者范围不对？**
A: 检查 `StrokeCollisionService.cs` 中的阈值计算逻辑。目前逻辑是基于 `(EraserSize + StrokeSize) * 0.5 * 1.2` 的欧几里得距离判定。如果分辨率比率不正确，会导致判定范围过大或过小。

## 2. 已知限制
*   **WebGL 内存**：在 WebGL 平台上，由于浏览器内存限制，过长的历史记录可能导致崩溃。建议在 WebGL 平台限制 Undo 步数。
*   **多线程**：目前的 `StrokeSpatialIndex` 是非线程安全的，必须在主线程运行。
