# 源码导航

`MiniCpp.exe` 是 WinForms 编辑器；`MiniCpp.Console.exe` 是单独运行的交互命令行宿主。宿主按 `stdin`、`stdout`、`stderr` 分别顺序写临时文件，结束后才原子地发布包含各流长度、行数和退出码的 XML 清单。编辑器在后台读取摘要或某一页，不把整个大文件装入控件。`RunRecord` 归对应文档所有，替换、关闭文档或正常退出时释放。这两个程序不共享控件；`scripts/build.ps1` 分别递归编译它们及共用源码。

| 目录 | 修改什么时来看这里 |
| --- | --- |
| `MiniCpp/Workspace` | 窗口、文档标签、菜单和任务调度；把构建、运行、调试结果显示到对应文档 |
| `MiniCpp/Editor` | Scintilla 编辑行为、模板、行号、断点、查找、文档样式 |
| `MiniCpp/Documents` | 源文件编码识别、读取和安全写回，不依赖编辑器控件 |
| `MiniCpp/Build` | GCC 参数、产物、报错定位与中文建议 |
| `MiniCpp/Execution` | 判断运行模式、启动命令行宿主、读取已完成的运行记录 |
| `ConsoleHost/Execution`、`ConsoleHost/Recording` | 前者负责子进程与标准流，后者负责原始内容、行数与记录清单的写出。摘要与分页读取在 `MiniCpp/Execution/RunRecord.cs`，显示在 `MiniCpp/Interface/RunRecordView.cs` |
| `MiniCpp/Debugging` | GDB 会话、MI 协议解析、变量快照 |
| `MiniCpp/Interface`、`MiniCpp/Settings` | 前者是共享控件、绘制与配色；后者是偏好、字体及其持久化 |
| `MiniCpp/Updates` | GitHub 发布列表、更新窗口、后台下载、包校验及更新启动 |
| `UpdateHost`、`Shared/Update*` | 独立更新进程、程序文件清单、备份替换和失败恢复 |
| `Shared` | 两个可执行程序共用的 Windows 进程、参数和子进程生命周期工具 |

## 线程边界

- UI 线程拥有 Scintilla 和 WinForms 控件，负责保存确认、更新活动文档和显示结果。服务层不读取控件。后台 GDB 事件由 `MainForm` 转回 UI 线程。
- `Compiler.BuildAsync` 接收源文件路径和编译选项；读取与解码源文件、准备临时文件、启动 GCC 不阻塞 UI。标准输出和标准错误异步并行读取，退出、取消和清理仍属于同一次构建。
- 命令行宿主可同时等待键盘、读取 stdout/stderr。先排空子进程输出，再写完整记录；IDE 只在这之后显示记录。并行读取流不等于允许多个前台构建或调试器争用同一份文档。
