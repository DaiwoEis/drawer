# 测试与质量保障 (Testing & QA)

## 1. 测试策略
项目采用分层测试策略，重点覆盖核心逻辑。

*   **单元测试 (Unit Tests)**：覆盖领域层 (Domain) 和服务层 (Service) 的纯逻辑。
    *   框架：NUnit
    *   路径：`Assets/Scripts/Tests`
*   **集成测试 (Integration Tests)**：覆盖 `DrawingAppService` 与 `CanvasRenderer` 的交互。
*   **性能测试**：针对 10k+ 点的压力测试。

## 2. 如何运行测试
1.  打开 Unity 编辑器。
2.  菜单栏选择 `Window > General > Test Runner`。
3.  **PlayMode Tests**：包含需要运行游戏循环的测试（如 MonoBehaviour 生命周期）。
4.  **EditMode Tests**：包含纯逻辑测试（如 `DiagnosticsTests`, `LogicPointTests`）。
5.  点击 **Run All**。

## 3. 现有测试套件
*   `DiagnosticsTests.cs`：
    *   验证结构化日志格式是否正确。
    *   验证 TraceContext ID 生成唯一性。
    *   验证日志缓冲与 Flush 机制。

## 4. 代码质量门禁
*   **编译检查**：任何提交必须无编译错误。
*   **无警告**：尽量消除 Unity Console 中的黄色警告。
*   **内存分配**：在 Profiler 中检查 `GC.Alloc`，绘图过程中应为 0 B (Zero Allocation)。
