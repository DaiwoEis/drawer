# 网络同步架构

## 1. 概述
Drawer 采用 **混合同步模型 (Hybrid Sync Model)**，结合了实时瞬态渲染 (Ghost Layer) 和最终一致性提交 (Commit)。目标是在弱网环境下提供 < 50ms 的感知延迟，并保证最终画板状态的一致性。

## 2. 传输层优化

### 2.1 增量压缩 (Delta Compression)
- **原理**: 使用 `VarInt` 编码坐标差值。
- **实现**: `StrokeDeltaCompressor`
- **效果**: 平均每个点仅需 ~3 字节 (X+Y+Pressure)。

### 2.2 自适应批处理 (Adaptive Batching)
- **策略**: 
  - **数量阈值**: 10 个点
  - **时间阈值**: 33ms (30Hz)
- **优势**: 在快速绘制时减少包头开销，在慢速绘制时保证实时性。

### 2.3 冗余传输 (Redundancy)
- **机制**: 每个 Update 包携带当前批次数据 + **上一批次**的数据。
- **恢复**: 接收端检测序列号间隙。如果是单包丢失，直接从当前包的 `RedundantPayload` 中恢复丢失数据。
- **代价**: 带宽增加约 80-90%，但换取了极高的抗丢包能力 (无需重传)。

### 2.4 Payload 生命周期 (Ownership)
- `SendUpdateStroke` 调用后，**发送方可以立即回收/复用 payload 缓冲区**（可能来自池化）。
- 若网络实现需要异步访问 payload，**必须复制数据**后再发送。

### 2.5 严格校验模式 (Strict Validation)
- 可配置严格模式用于异常包处理：在检测到非法包/序列断裂且无冗余时，**直接 Abort 并清理该远端笔画**。
- 默认 **Debug/Development** 开启，**Release** 关闭（可通过 `_useBuildDefaults` 关闭自动模式并手动配置）。

## 3. 客户端预测 (Client-Side Prediction)

为了掩盖网络抖动 (Jitter)，接收端实现了外推预测。

### 3.1 幽灵层架构 (Ghost Layer Architecture)
- **模式**: **Retained Mode** (保留模式)。
- **渲染**: `GhostOverlayRenderer` 每帧清空并重绘所有活跃的远程笔画。
- **优势**: 允许随时修改/撤销预测的点，避免预测错误导致的视觉残留。

### 3.2 外推算法 (Extrapolation)
- **逻辑**: `RemoteStrokeContext.Update()`
- **计算**: 基于最近两个数据包计算速度 (`Velocity`)。
- **预测**: 当超过 `PREDICTION_THRESHOLD` (33ms) 未收到包时，沿速度方向线性外推。
- **限制**: 最大预测时间 `MAX_PREDICTION_TIME` (100ms)，防止预测过远。

## 4. 协议定义
参见 `NetworkStrokePacket.cs`。
- `BeginStroke`: 笔刷元数据 (Color, Size, Seed)。**注意：不包含起始坐标，起始点通过随后的 UpdateStroke 发送。**
- `UpdateStroke`: 序列号 + 增量数据 + 冗余数据 + **显式数据长度** (PayloadLength/RedundantPayloadLength，用于池化缓冲区)。
- `EndStroke`: 校验和 + 总点数 (用于最终完整性检查)。

### 4.1 校验和规则 (Checksum)
- **算法**: FNV-1a 32-bit
- **输入**: 按顺序遍历每个 `LogicPoint`，依次喂入 `X低字节`、`X高字节`、`Y低字节`、`Y高字节`、`Pressure`。
- **用途**: `EndStroke.Checksum` 为 0 表示未提供；非 0 时用于严格一致性校验。
