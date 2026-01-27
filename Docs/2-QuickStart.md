# 快速开始指南 (Quick Start Guide)

## 1. 环境要求
在开始之前，请确保您的开发环境满足以下要求：

*   **操作系统**：Windows 10+, macOS 10.15+
*   **Unity 版本**：`2021.3.41f1c1` (LTS) 或更高版本
*   **IDE**：JetBrains Rider (推荐), Visual Studio 2019+, 或 VS Code
*   **Git**：2.0+

## 2. 获取代码
```bash
git clone <repository-url>
cd drawer
```

## 3. 安装与运行

### 步骤 1：打开项目
1.  启动 Unity Hub。
2.  点击 "Open"，选择项目根目录下的 `draw` 文件夹（注意不是外层的 `drawer`，而是包含 `Assets` 的 `draw` 文件夹）。
3.  等待 Unity 导入资源及解析包依赖 (Manifest located at `Packages/manifest.json`)。

### 步骤 2：场景运行
1.  在 Project 窗口中，导航至 `Assets/Scenes`。
2.  双击打开主场景（例如 `MainScene` 或 `DrawingScene`）。
3.  点击编辑器顶部的 **Play** 按钮。

### 步骤 3：验证运行
*   **绘图**：在 Game 视口中，使用鼠标左键按住并拖动，应能看到黑色笔迹。
*   **橡皮擦**：切换到橡皮擦模式（如果有 UI 按钮）或检查 Inspector 中的 `DrawingAppService` 设置，再次拖动应能擦除笔迹。
*   **撤销/重做**：如果 UI 有对应按钮，点击测试撤销功能；或者观察 Console 日志中的 Command 执行情况。
*   **性能监控**：查看 Console 输出，应能看到 `[PerformanceHeartbeat]` 相关的 FPS 和内存日志。

## 4. 常见启动问题
*   **Shader 报错**：如果出现材质粉红或 Shader 丢失，请确保 `Assets/Resources/Shaders/DrawingShaderVariants` 已正确加载，并运行 `Tools/Drawing/Assign Shader Variants` 菜单项进行自动修复。
*   **依赖缺失**：如果 Console 提示包丢失，请检查网络连接并尝试 `Window > Package Manager > Resolve Packages`。
