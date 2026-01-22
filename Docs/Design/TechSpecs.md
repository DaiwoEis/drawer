# 技术方案：高性能跨端实时画板 (Unity)

## 1. 核心优化思路

针对 "Apple Notes 般丝滑" 与 "跨端一致" 的需求，在原 PRD 基础上进行以下技术深化与优化。

### 1.1 渲染层：Mesh Stamping + GPU 加速
原 PRD 提到的 "RenderTexture 增量盖章" 是正确方向，但具体实现决定手感。
- **放弃 LineRenderer**：Unity 原生 LineRenderer 在拐角和变宽处理上非常生硬。
- **采用 Mesh Stamping (印章法)**：
  - 将笔触视为连续的 "点"（Quad）。
  - 根据笔压 (Pressure) 和速度 (Velocity) 动态调整每个 Quad 的 Scale 和 Alpha。
  - 使用 `CommandBuffer` 在 `RenderTexture` 上直接绘制 Mesh，避免 CPU `SetPixel` 开销。
- **MSAA 处理**：RenderTexture 需开启 Anti-Aliasing，或者在 Shader 中做 SDF (Signed Distance Field) 渲染以获得极其锐利的边缘。

### 1.2 平滑算法：预测与修正
"丝滑" 的本质是：**低延迟** + **曲线拟合**。
- **输入层**：使用 `Unity Input System` 的 `Coalesced Actions` 获取高频触控点（高于帧率）。
- **算法**：
  - **Chaikin's Algorithm** 或 **Catmull-Rom Spline** 用于平滑。
  - **One Euro Filter** 用于过滤手抖（Jitter Reduction）。
- **预测绘制 (Predictive Painting)**：
  - 在当前帧，根据速度向量预测下一帧位置并预渲染（虚线或淡色），下一帧真实数据到来时覆盖。这能显著降低视觉延迟感。

### 1.3 跨端一致性：定点数物理世界
浮点数 (float) 在不同 CPU (ARM vs x86) 下可能存在微小精度差异，导致长笔画累积误差。
- **逻辑坐标**：严格遵守 PRD 的 `0-65535` (uint16)。
- **数学库**：在 `Domain` 层禁止使用 `Mathf` 或 `float` 进行累积计算。
  - 使用 `Vector2Int` 存储逻辑坐标。
  - 笔刷随机散布使用自定义的 **Deterministic Random** (基于 `strokeSeed`)，不依赖 `System.Random` 或 `UnityEngine.Random`。

---

## 2. 架构设计 (遵循 Clean Architecture)

### 2.1 目录结构映射
```text
Assets/Scripts/
├── App/                  # 应用入口与配置
│   ├── Config/           # 画布尺寸、网络配置
│   └── MainEntry.cs
├── Common/               # 通用工具
│   ├── Math/             # 定点数计算工具
│   └── Network/          # 基础 Socket 封装
├── Features/
│   ├── Drawing/          # 核心绘画业务
│   │   ├── Domain/       # 实体 (Stroke, Point), 逻辑 (Smoothing)
│   │   ├── Service/      # 笔刷管理, 历史记录
│   │   └── Presentation/ # Unity View (InputListener, CanvasRenderer)
│   └── Room/             # 房间管理业务
```

### 2.2 数据流向 (ODD Loop)
1. **Input (View)**: 监听屏幕坐标 -> 转换为逻辑坐标 (0-65535)。
2. **Process (Domain)**: 
   - 应用平滑算法。
   - 生成 `StrokePoint`。
   - 序列化为 Command。
3. **Network (Service)**: 发送 `STROKE_POINTS`。
4. **Render (View)**: 
   - 本地：立即渲染到 `ActiveRenderTexture`。
   - 远端：接收数据 -> 放入 Buffer -> 插值 -> 渲染到 `ActiveRenderTexture`。

---

## 3. 关键数据结构 (C#)

```csharp
// 逻辑层点结构 (内存紧凑)
public struct LogicPoint {
    public ushort x;
    public ushort y;
    public byte pressure; // 0-255
    
    // 压缩辅助：从 float 转换
    public static LogicPoint FromUnity(Vector2 uv, float p);
    // 解压：转回 Unity 坐标
    public Vector2 ToUnity();
}

// 笔画数据
public class StrokeEntity {
    public uint Id;
    public uint Seed;
    public ushort BrushId;
    public List<LogicPoint> Points;
}
```

## 4. 优化建议 (Beyond MVP)

1. **笔刷纹理图集 (Atlas)**：将所有笔刷 Tip 打包到一个图集，减少 DrawCall 切换。
2. **笔迹重放 (Replay)**：不仅用于 Late Join，也可用于 "撤销/重做"。
   - 既然是矢量存储，Undo 操作只需：`Clear RT` -> `Replay History (minus last)`。
   - 优化：每 N 步保存一张 `Snapshot RT`，Undo 时从最近 Snapshot 开始重放，速度极快。
