# 混合同步协议 (Hybrid Incremental Sync Protocol, HISP) 设计规范

> **状态**: 草稿
> **作者**: AI 助手 & 用户
> **目标系统**: Drawer (Unity 客户端) + 可靠 UDP 后端

## 1. 概述
本文档定义了 Drawer 应用的同步协议，旨在实现实时的笔触可见性 (幽灵层)，同时保持权威状态的确定性和零 GC (Zero-GC) 架构。

### 1.1 目标
*   **实时反馈**: 远程用户可以实时看到正在绘制的笔触 (延迟 < 100ms)。
*   **一致性**: 所有客户端的最终状态 (命令历史) 完全一致。
*   **性能**: 核心路径零 GC；通过增量编码和量化优化带宽。
*   **健壮性**: 优雅处理丢包、网络抖动和客户端断连。

### 1.2 架构: "幽灵 & 提交" (Ghost & Commit) 模型

我们将 **视觉状态 (Visual State)** 与 **逻辑状态 (Logical State)** 分离。

| 层级 | 组件 | 职责 | 持久性 |
| :--- | :--- | :--- | :--- |
| **幽灵层 (临时)** | `GhostOverlayRenderer` | 立即渲染传入的流式点数据。 | **短暂** (提交或中止时清除)。 |
| **逻辑层 (权威)** | `DrawingHistoryManager` | 管理已确认的 `StrokeEntity` 和 Undo/Redo。 | **永久** (序列化到磁盘/云端)。 |

---

## 2. 协议规范

### 2.1 传输层
*   **协议**: 可靠 UDP (如 KCP / ENet)。
*   **排序策略**:
    *   **流通道 (不可靠/有序)**: 用于 `UpdateStroke` (中间点)。允许丢弃少量的中间包 (表现为视觉跳变)，但优先保证数据的实时性。
    *   **控制通道 (可靠/有序)**: 用于 `BeginStroke` 和 `EndStroke`。必须保证送达。

### 2.2 数据包定义

#### A. 开始笔画 (BeginStroke) - [可靠]
当 `StartStroke` 发生时发送。

| 字段 | 类型 | 位数 | 描述 |
| :--- | :--- | :--- | :--- |
| `PacketType` | `byte` | 8 | `0x01` |
| `StrokeId` | `uint` | 32 | 笔画会话的唯一 ID。 |
| `BrushId` | `ushort` | 16 | 0=钢笔, 1=橡皮擦 等。 |
| `Color` | `uint` | 32 | RGBA 打包值。 |
| `Size` | `float` | 32 | 基础笔刷大小。 |
| `Seed` | `uint` | 32 | 程序化笔刷的随机种子。 |

#### B. 更新笔画 (UpdateStroke) - [不可靠 / 有序]
每 **N** 毫秒 (如 50ms) 或 **K** 个点 (如 10 个点) 发送一次。包含 **增量编码 (Delta-Encoded)** 的点。

| 字段 | 类型 | 位数 | 描述 |
| :--- | :--- | :--- | :--- |
| `PacketType` | `byte` | 8 | `0x02` |
| `StrokeId` | `uint` | 32 | 关联 ID。 |
| `Sequence` | `ushort` | 16 | 该笔画更新包的单调递增 ID。 |
| `Count` | `byte` | 8 | 本包中点的数量。 |
| `Payload` | `Bytes` | 变长 | **增量压缩点集** (见 2.3)。 |

#### C. 结束笔画 (EndStroke) - [可靠]
当 `EndStroke` 发生时发送。即 "提交 (Commit)" 消息。

| 字段 | 类型 | 位数 | 描述 |
| :--- | :--- | :--- | :--- |
| `PacketType` | `byte` | 8 | `0x03` |
| `StrokeId` | `uint` | 32 | 关联 ID。 |
| `TotalPoints` | `ushort` | 16 | 点总数 (完整性检查)。 |
| `Checksum` | `uint` | 32 | 所有点的 CRC32 (一致性检查)。 |

#### D. 中止笔画 (AbortStroke) - [可靠]
当用户取消 (如绘制中撤销) 或断开连接时发送。

| 字段 | 类型 | 位数 | 描述 |
| :--- | :--- | :--- | :--- |
| `PacketType` | `byte` | 8 | `0x04` |
| `StrokeId` | `uint` | 32 | 关联 ID。 |

### 2.3 数据压缩 (极致优化)

为了最小化带宽，我们对 `UpdateStroke` 使用 **相对量化 (Relative Quantization)**。

*   **原点 (Origin)**: 笔画的第一个点 (在 `BeginStroke` 或首个 `UpdateStroke` 中) 是绝对坐标 (完整的 `ushort` 0-65535)。
*   **增量 (Delta)**: 后续点编码为相对于 *前一个点* 的偏移量。
*   **量化策略**:
    *   `LogicPoint` X/Y 原始为 `ushort` (0-65535)。
    *   通常相邻点的 Delta 很小 (< 100 单位)。
    *   **VarInt 编码**:
        *   如果 Delta 在 [-127, 127] 范围内，使用 1 字节。
        *   否则使用 2 字节 (short)。
    *   **压感 (Pressure)**: 根据质量设置量化为 4 位 (16 级) 或 8 位 (256 级)。

---

## 3. 客户端实现策略

### 3.1 幽灵层 (表现层)
*   **新组件**: `GhostStrokeRenderer` (实现 `IStrokeRenderer`)。
*   **实现**:
    *   使用独立的 `RenderTexture` (GhostRT) 叠加在主画布之上。
    *   **橡皮擦处理**:
        *   幽灵橡皮擦 **不** 擦除真实的墨水 (回滚太复杂)。
        *   相反，它绘制一条 "红色半透明轨迹" 来指示用户正在擦除的位置。
        *   在 `Commit` (提交) 时，执行真实的擦除逻辑。

### 3.2 一致性模型 (工业标准)

#### 场景 A: 正常完成
1.  **远程**: 收到 `Begin` -> `Update`... -> `Update`。
    *   绘制到 GhostRT。
2.  **远程**: 收到 `End`。
    *   **清除** 该 StrokeId 对应的 GhostRT 内容。
    *   **重构** 完整的点列表 (如果 `Update` 包丢失，可能需要请求缺失的数据块，或者如果 `Checksum` 匹配则直接使用现有数据。*优化: 为保证严格一致性，若 Checksum 校验失败，请求全量拉取*)。
    *   **执行** `DrawStrokeCommand` 绘制到主画布。

#### 场景 B: 丢包 (Update)
*   如果 `Sequence` 5 丢失但 6 到达:
    *   直接连接最后已知点 (Seq 4) 到 Seq 6 的第一个点。
    *   视觉表现: 一条直线段 (视觉瑕疵)。
    *   对于 "幽灵" 状态是可接受的。
    *   **修正**: `EndStroke` (提交) 包含权威数据 (或触发拉取)，因此最终结果总是正确的。

#### 场景 C: 断连 / 超时
*   如果超过 5 秒未收到 `StrokeId` X 的包:
    *   触发 `AbortStroke(X)`。
    *   幽灵笔触淡出并消失。
    *   不提交任何命令。

#### 场景 D: 并发 (绘制中撤销)
*   用户 A 正在画 Stroke 100。
*   用户 A 在抬笔前按下了撤销 (快捷键)。
*   客户端 A 发送 `AbortStroke(100)`。
*   客户端 B 收到 `Abort`: 清除 GhostRT。什么都没发生。

---

## 4. 迁移计划

### 第一阶段: 基础建设
1.  定义 `NetworkStrokePacket` 结构体。
2.  实现 `StrokeDeltaCompressor` (增量压缩器)。
3.  创建 `GhostOverlayRenderer` (初期可直接复用 CanvasRenderer 逻辑，但操作不同的 RT)。

### 第二阶段: 集成
1.  修改 `DrawingAppService` 以分发事件到 `NetworkManager`。
2.  实现 `RemoteStrokeContext` 以缓冲传入的点。

### 第三阶段: 验证
1.  单元测试: 压缩/解压的回环测试。
2.  集成测试: 模拟丢包并验证 `EndStroke` 能自动修正视觉结果。
