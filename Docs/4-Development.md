# 详细开发指南 (Development Guide)

## 1. 代码结构与规范

### 1.1 目录结构
```text
Assets/
├── Scripts/
│   ├── Common/             # 通用基础设施 (常量, 诊断, 工具)
│   ├── Features/           # 业务功能模块
│   │   ├── Drawing/        # 绘图核心模块
│   │   │   ├── App/        # 应用服务层
│   │   │   ├── Domain/     # 领域实体与值对象
│   │   │   ├── Presentation/ # 渲染与视图
│   │   │   └── Service/    # 领域服务
│   │   └── Room/           # 房间/网络模块 (如有)
│   └── Tests/              # 单元测试与集成测试
├── Resources/              # 动态加载资源 (Shaders, Prefabs)
└── Scenes/                 # 游戏场景
```

### 1.2 命名规范
*   **类名**：PascalCase (e.g., `DrawingAppService`)
*   **私有字段**：_camelCase (e.g., `_currentSize`)
*   **公共属性**：PascalCase (e.g., `CurrentSize`)
*   **接口**：IPrefix (e.g., `IStrokeRenderer`)

### 1.3 编码原则
*   **SOLID**：严格遵守单一职责和依赖倒置原则。
*   **零 GC**：在 `Update` 和绘图热路径 (`MoveStroke`) 中，严禁使用 `new` 分配内存。使用对象池或预分配数组。
*   **显式依赖**：优先通过 `Initialize()` 方法或构造函数注入依赖，便于测试。
*   **文档优先**：任何架构变动（如新增模块、修改核心流程），**必须优先更新**根目录下的 `AGENTS.md` 文件。

## 2. 本地开发环境配置

1.  **IDE 设置**：
    *   确保 IDE 安装了 Unity 支持插件。
    *   启用 .editorconfig 支持（如有）。
2.  **调试**：
    *   在 IDE 中 "Attach to Unity Editor"。
    *   在 `DrawingAppService.cs` 的 `StartStroke` 方法设置断点，调试输入流程。

## 3. 核心 API 说明

### DrawingAppService
主入口服务，挂载在场景的 `DrawingManager` GameObject 上。

*   `Initialize(IStrokeRenderer renderer, ...)`: 手动初始化服务（用于测试或非 MonoBehaviour 启动）。
*   `StartStroke(LogicPoint point)`: 开始一笔。
*   `MoveStroke(LogicPoint point)`: 移动笔触。
*   `EndStroke()`: 结束当前笔画并提交到历史记录。
*   `Undo()` / `Redo()`: 撤销重做。
*   `SetColor(Color color)`: 设置画笔颜色。
*   `SetEraser(bool isEraser)`: 切换橡皮擦模式。

## 4. 诊断系统使用
若要添加新的日志或埋点：

```csharp
// 注入日志记录器
private IStructuredLogger _logger;

// 记录普通日志
_logger.Info("User clicked button", default, new Dictionary<string, object> { { "btn_id", 1 } });

// 记录带追踪上下文的日志
var trace = TraceContext.New();
_logger.Info("Process started", trace);
// ...
_logger.Info("Process finished", trace);
```
