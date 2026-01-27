# 部署与运维手册 (Deployment & Operations)

## 1. 生产环境部署

### 1.1 构建设置 (Build Settings)
1.  打开 `File > Build Settings`。
2.  选择目标平台 (PC, Mac, iOS, Android, WebGL)。
3.  **注意**：对于 WebGL 平台，需确保 `Color Space` 设置为 `Gamma` 或处理好 `Linear` 空间的材质兼容性。

### 1.2 资源管理
*   **Shaders**：确保 `DrawingShaderVariants` 包含在 `Always Included Shaders` 列表中，或位于 `Resources` 文件夹下被脚本引用，防止构建时被剔除。
*   **预热**：应用启动时会自动调用 `CanvasRenderer` 中的 `InitializeGraphics` 进行 Shader 预热。

## 2. 配置管理
项目主要通过 `ScriptableObject` 和常量类进行配置。

*   **常量配置**：`Assets/Scripts/Common/Constants/DrawingConstants.cs`
    *   `LOGICAL_RESOLUTION`: 逻辑分辨率 (65536)。
    *   `MAX_PRESSURE`: 最大压感级数。
*   **笔刷配置**：`BrushStrategy` (ScriptableObject)
    *   可在 `Assets/Data/Brushes` (示例路径) 下创建不同的笔刷配置文件，调整平滑度、纹理等。

## 3. 监控与日志
### 3.1 日志查看
*   **开发模式**：日志会直接输出到 Unity Console。
*   **生产模式**：
    *   `StructuredLogger` 默认配置为缓冲模式。
    *   需对接后端 HTTP 接口（需实现 `HttpLogSender`）以收集 JSON 格式的日志。
    *   日志包含 `ts` (时间戳), `lvl` (级别), `tid` (TraceID), `ctx` (上下文数据)。

### 3.2 性能指标
系统内置 `PerformanceMonitor`，会自动采集以下指标：
*   `fps`: 实时帧率。
*   `mem_mb`: 托管堆内存占用 (MB)。

## 4. 常见运维问题
*   **内存泄漏**：关注 `mem_mb` 指标是否随时间线性增长。检查是否有 `Texture2D` 或 `Mesh` 未被销毁。
*   **绘图延迟**：如果 FPS 低于 30，检查 `CanvasRenderer` 的 DrawCall 数量，或确认是否在 `Update` 中进行了大量 GC 分配。
