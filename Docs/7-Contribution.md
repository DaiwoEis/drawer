# 贡献指南 (Contribution Guide)

欢迎参与 Drawer 项目的开发！为了保证代码质量和协作效率，请遵守以下流程。

## 1. 分支管理
*   `main` / `master`：主分支，保持随时可发布状态。
*   `develop`：开发分支，包含最新合并的功能。
*   `feature/xxx`：功能分支，从 `develop` 检出。
*   `fix/xxx`：修复分支。

## 2. 提交规范 (Commit Convention)
请遵循 [Conventional Commits](https://www.conventionalcommits.org/) 规范：

*   `feat: ...` 新增功能
*   `fix: ...` 修复 Bug
*   `docs: ...` 文档变更
*   `style: ...` 代码格式调整（不影响逻辑）
*   `refactor: ...` 重构
*   `perf: ...` 性能优化
*   `test: ...` 测试相关

**示例**：
```text
feat(drawing): implement dynamic resolution adaptation
fix(renderer): fix shader variant warming issue
docs: update architecture diagram
```

## 3. Pull Request 流程
1.  Fork 本仓库（如果是外部贡献者）。
2.  创建功能分支 `git checkout -b feature/my-cool-feature`。
3.  提交代码并推送到远程。
4.  创建 Pull Request (PR) 到 `develop` 分支。
5.  **必选检查**：
    *   通过所有单元测试。
    *   附带相关的测试用例（如果是新功能）。
    *   更新相关文档（**特别是 `AGENTS.md`，如果涉及架构变更**）。
6.  等待代码评审 (Code Review) 并合并。

## 4. 核心原则
*   **保持简单 (KISS)**：不要引入不必要的复杂性。
*   **注重性能**：这是一项绘图应用，任何一帧的延迟都能被用户感知。请时刻关注性能影响。
