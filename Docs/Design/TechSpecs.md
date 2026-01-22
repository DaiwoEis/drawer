# 架构与技术深度解析

本文档详细介绍了高性能画板背后的内部架构、算法和技术决策。旨在供核心开发人员和希望扩展渲染或数据层的人员参考。

## 1. 核心设计理念

为了实现 "Apple Notes 般" 的流畅度和跨平台一致性，系统基于三大支柱构建：

1.  **网格盖章渲染 (Mesh Stamping Rendering)**: 绕过 Unity 的 `LineRenderer`，转而采用 GPU 实例化的四边形盖章技术，以实现完美的笔触控制。
2.  **确定性逻辑 (Deterministic Logic)**: 所有空间数据都存储在归一化的逻辑坐标系 (0-65535) 中，以确保在不同设备和分辨率下获得相同的结果。
3.  **整洁架构 (Clean Architecture)**: 严格分离领域逻辑、应用服务和表现层（Unity 视图）。

## 2. 坐标系统

理解坐标转换对于开发至关重要。

### 2.1 坐标空间

| 空间 | 范围 | 类型 | 用途 |
| :--- | :--- | :--- | :--- |
| **输入空间** | `Vector2` (Pixel) | `float` | 来自 `Input.mousePosition` 的原始数据 |
| **归一化空间** | `Vector2` (0.0 - 1.0) | `float` | 分辨率无关的中间步骤 |
| **逻辑空间** | `LogicPoint` (0 - 65535) | `ushort` | **存储与网络**。数据的唯一真理源。 |

### 2.2 数据流向

1.  **输入**: `MouseInputProvider` 捕获屏幕像素。
2.  **转换**: 使用 `DrawingConstants.LOGICAL_RESOLUTION` 转换为 `LogicPoint` (0-65535)。
3.  **存储**: 存储在 `StrokeEntity` 中。
4.  **渲染**: 转换回归一化 (0-1) -> 屏幕像素，供 `CommandBuffer` 执行。

## 3. 渲染管线 (网格盖章)

`CanvasRenderer.cs` 实现了自定义的 "Mesh Stamping" 技术。

### 3.1 为什么不用 LineRenderer？

Unity 的 `LineRenderer` 生成三角形带。这在尖角处会产生伪影，并且不支持自然地改变每段的不透明度/纹理。

### 3.2 盖章算法

我们不是连接点，而是沿着路径重复“盖”上笔刷纹理。

1.  **输入**: 平滑点列表 (`LogicPoint`)。
2.  **生成器**: `StrokeStampGenerator` 根据 `SpacingRatio` 计算输入点之间的插值位置。
    *   *示例*: 如果点间距为 10px，间距为 1px，则生成 10 个印章。
3.  **批处理**: 印章被收集到 `StampData` 缓冲区中。
4.  **GPU 实例化**: `CommandBuffer.DrawMesh` 在单个绘制调用（或分批）中绘制数千次简单的四边形网格。
5.  **材质**: 使用 `BrushStamp.shader`，处理：
    *   **纹理**: 笔刷笔尖形状。
    *   **颜色/Alpha**: 通过 `MaterialPropertyBlock` 传递的顶点颜色。
    *   **混合模式**: 支持标准混合和自定义橡皮擦逻辑 (`ReverseSubtract`)。

## 4. 平滑算法

来自鼠标或触摸屏的原始输入通常是锯齿状的 (10-60Hz)。为了获得平滑的曲线，我们使用 **Catmull-Rom Splines**。

*   **实现**: `StrokeSmoothingService.cs`
*   **窗口**: 使用 4 个控制点的滑动窗口 ($P_{i-1}, P_i, P_{i+1}, P_{i+2}$)。
*   **插值**: 在 $P_i$ 和 $P_{i+1}$ 之间生成固定步长（默认为 4）。
*   **压力**: 随位置线性插值压力值。

## 5. 领域模型与数据结构

### 5.1 LogicPoint (Struct)

针对内存和网络带宽进行了优化。

```csharp
public struct LogicPoint {
    public ushort X;        // 2 bytes (0-65535)
    public ushort Y;        // 2 bytes
    public byte Pressure;   // 1 byte (0-255)
    // 总计: 每点 5 字节 (相比 Vector3 的 12 字节)
}
```

### 5.2 StrokeEntity (Class)

表示单个连续的笔画。

*   **Id**: 唯一标识符。
*   **Seed**: 用于确定性笔刷抖动的随机种子。
*   **Points**: `LogicPoint` 列表。
*   **ColorRGBA**: 用于紧凑存储的 32 位整数颜色。

## 6. 项目结构 (DDD)

```text
Assets/Scripts/Features/Drawing/
├── Domain/              # 纯 C# 逻辑，尽量无 Unity 依赖
│   ├── Entity/          # StrokeEntity
│   ├── ValueObject/     # LogicPoint
│   └── Interface/       # IStrokeRenderer
├── Service/             # 业务逻辑
│   └── StrokeSmoothingService
├── Presentation/        # Unity 视图层
│   ├── CanvasRenderer   # GPU 实现
│   └── MouseInputProvider # 输入处理
└── App/                 # 应用层
    └── DrawingAppService # 外观/控制器
```

## 7. 未来路线图与优化

1.  **四叉树空间索引 (QuadTree Spatial Indexing)**: 为了高效的“按笔画擦除”或“选择”操作，我们需要快速查找区域内的笔画。
2.  **计算着色器 (Compute Shaders)**: 将 `StrokeStampGenerator` 逻辑移动到 Compute Shader 以实现大规模并行（每帧 10k+ 印章）。
3.  **纹理图集 (Texture Atlas)**: 目前每个笔刷笔尖可能是一个单独的纹理。将它们合并到图集中可以减少状态切换。
4.  **网络协议 (Network Protocol)**: 当前的 `LogicPoint` 结构已准备好进行二进制序列化。增量压缩方案可以进一步减少带宽。
