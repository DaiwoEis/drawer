# Unity MCP Integration Guide

本文档指导如何在本项目中集成和使用 `unity-mcp`，实现 AI 助手（如 Trae, Claude, Cursor）与 Unity 编辑器的直接交互。

## 1. 简介 (Introduction)

`unity-mcp` 是一个桥接工具，允许 AI 助手通过 [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) 直接控制 Unity 编辑器。

**功能包括**：
*   管理 Assets、Scene、GameObject
*   自动化重复性任务
*   读取 Console 日志
*   运行测试

## 2. 安装 (Installation)

### 2.1 项目依赖 (已完成)
项目的 `Packages/manifest.json` 已添加以下依赖：
```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"
```
Unity 启动时会自动下载并安装该 Package。

### 2.2 环境依赖 (Prerequisites)
需要在本地机器上安装 Python 环境和 `uv` 包管理器。

**macOS/Linux**:
```bash
# 1. 安装 uv (如果未安装)
curl -LsSf https://astral.sh/uv/install.sh | sh

# 2. 确保 Python 3.10+ 可用
uv python install 3.11
```

**注意**: 如果安装后找不到 `uv` 命令，请检查 `~/Library/Python/3.9/bin` 是否在 PATH 中，或者使用绝对路径 `/Users/klutz/Library/Python/3.9/bin/uv`。


## 3. 启动服务器 (Start Server)

1.  打开 Unity 项目。
2.  在菜单栏点击 **Window > MCP for Unity**。
3.  点击 **Start Server** 按钮。
    *   这将启动一个本地 HTTP 服务器，默认端口为 `8080`。
    *   状态栏应显示 "Running on http://localhost:8080/mcp"。

## 4. 配置 IDE (Trae Configuration)

为了让 Trae 连接到 Unity，需要配置 MCP Server。

**配置步骤**:
1.  在 Trae 中打开设置 (Settings)。
2.  找到 MCP Server 配置部分 (通常在 Project Settings 或 Extensions 设置中)。
3.  添加一个新的 MCP Server：
    *   **Type**: HTTP (SSE)
    *   **URL**: `http://localhost:8080/mcp`
4.  保存配置。

**连接验证**:
*   在 Unity 的 MCP 窗口中，你应该能看到连接状态变为 🟢 **Connected**。

## 5. 使用示例 (Usage)

连接成功后，MCP 为 AI 提供了直接操作 Unity 编辑器的能力。结合本项目的 **Drawer** 功能，您可以尝试以下指令：

### 🔍 调试与诊断 (Debugging)
*   **"检查场景中的 CanvasRenderer 配置"**: AI 可以读取 `CanvasRenderer` 组件的参数，确认是否启用了 GPU 渲染 (`UseGpuStamping`)。
*   **"获取控制台最近的报错"**: 快速查看笔刷失效的具体报错堆栈。
*   **"查找所有 Stroke 对象"**: 统计当前场景中生成的笔触数量。

### 🎬 场景搭建 (Scene Setup)
*   **"创建一个测试场景"**: 自动新建 Scene 并挂载 `DrawingAppService` 和 `CanvasRenderer`。
*   **"在 (0,0,0) 创建一个红色的 Cube"**: 验证场景操作能力。

### 🧪 自动化测试 (Testing)
*   **"运行所有 EditMode 测试"**: 验证代码逻辑是否正常。

### 📂 资源管理 (Assets)
*   **"查找所有 Compute Shader"**: 确认 `StrokeGeneration.compute` 是否存在且路径正确。

## 6. 故障排除 (Troubleshooting)

*   **Unity 无法下载 Package**: 检查网络连接，确保可以访问 GitHub。
*   **服务器启动失败**: 确保已安装 `uv` 并且在系统 PATH 中。
    *   **临时修复**: 如果遇到 `command not found`，请在终端运行：
        ```bash
        export PATH="$HOME/Library/Python/3.9/bin:$PATH"
        ```
    *   **永久修复**: 将上述命令添加到 `~/.zshrc` 或 `~/.bash_profile` 中。
*   **Trae 无法连接**: 确保 Unity 中的服务器已启动（端口 8080 未被占用）。
