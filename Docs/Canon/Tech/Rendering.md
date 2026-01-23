# 渲染架构

## 笔刷系统

### 程序化 SDF 笔刷 (新增于 2025-01-23)
为了解决基于纹理的笔刷产生的锯齿问题（通常由低分辨率纹理或缩放伪影引起），我们在 `BrushStamp.shader` 中引入了程序化有向距离场 (Signed Distance Field, SDF) 渲染模式。

#### 机制
片元着色器不再采样纹理，而是计算当前像素到 UV 中心 (0.5, 0.5) 的距离。
- **完美圆形**: 无论缩放级别或笔刷大小如何，形状在数学上都是完美的。
- **抗锯齿**: 使用 `smoothstep` 在圆形边缘附近插值 Alpha 值，提供高质量的柔和边缘。

#### 配置
SDF 设置已暴露在 `BrushStrategy` ScriptableObjects 中（例如 `HardBrush.asset`, `SoftBrush.asset`）。

| 属性 | Inspector 名称 | 默认值 | 描述 |
| :--- | :--- | :--- | :--- |
| `UseProceduralSDF` | **Use Procedural SDF** | True | 勾选以启用完美圆形渲染。取消勾选以使用 `Main Texture`。 |
| `EdgeSoftness` | **Edge Softness** | 0.05 | 控制边缘平滑度。0.001 = 硬边缘，0.5 = 非常柔和。 |

#### Shader 参数 (内部)
| 属性 | 类型 | 默认值 | 描述 |
| :--- | :--- | :--- | :--- |
| `_UseProcedural` | Float (Toggle) | 1.0 (On) | 在程序化 SDF 模式 (1) 和纹理模式 (0) 之间切换。 |
| `_EdgeSoftness` | Float (Range) | 0.05 | 控制笔刷边缘的柔和度。 |

### 纹理模式 (传统)
当取消勾选 `UseProceduralSDF` 时，仍支持原始的基于纹理的渲染。这种模式使用 `Main Texture`，适用于非圆形笔刷或艺术纹理（例如粉笔、铅笔纹理）。
