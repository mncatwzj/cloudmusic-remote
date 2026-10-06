# 随手控 / CloudMusic Remote

原生 Windows 小软件：鼠标侧键切歌，手机在同一局域网控制网易云音乐。

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4)
![License](https://img.shields.io/badge/license-MIT-green)
![AI generated](https://img.shields.io/badge/AI--generated-OpenAI%20Codex-orange)

> **AI 编写声明：** 程序代码、界面、文档及测试由 **OpenAI Codex** 根据人类需求生成和迭代。发布与使用由人类负责，尚未经过独立安全审计。详见 [AI_DISCLOSURE.md](AI_DISCLOSURE.md)。
>
> 本项目是独立开源辅助工具，与网易云音乐或 OpenAI 无官方隶属、合作或背书关系。

## 下载

前往 [v1.0.0 正式版发布页](https://github.com/mncatwzj/cloudmusic-remote/releases/tag/v1.0.0)，下载 `CloudMusicRemote-v1.0.0-win-x64.zip`。同时提供 SHA-256 校验文件和完整源码。

变更见 [CHANGELOG.md](CHANGELOG.md) 和 [v1.0.0 更新说明](https://github.com/mncatwzj/cloudmusic-remote/blob/main/docs/RELEASE-v1.0.0.md)。

## 1.0 功能

- **原生桌面窗口**：WPF 实现，ChatGPT 风格深色界面；支持缩放、窄窗口收起侧栏、滚动布局。
- **动效**：侧键选中底块平滑滑动，按钮悬停及按压反馈。
- **三选一侧键**：上侧键 XButton2、下侧键 XButton1、禁用。禁用后两个侧键均不触发本工具切歌。
- **自定义快捷键**：在软件内录入与网易云现有全局快捷键一致的组合，支持冲突检查、Esc 取消、恢复默认。
- **本机保存配置**：重启后保留侧键与快捷键设置，兼容旧版侧键偏好迁移。
- **手机液态玻璃界面**：只有一版手机遥控页，提供配对、曲目标题、播放 / 暂停、上一首、下一首及音量增减。
- **托盘运行**：关闭窗口后继续服务；右击托盘图标可退出。

## 使用方法

1. 完整解压，先退出旧版，再打开 `CloudMusicRemote.exe`。
2. 打开网易云音乐，并准备一个可播放的列表。
3. 在“鼠标侧键”中选择上侧键、下侧键或禁用。
4. 如果你使用了自定义网易云全局快捷键，点击本工具对应的快捷键按钮并按下相同组合。**无需更改网易云已有的快捷键。**
5. 手机连接电脑所在路由器的 Wi-Fi；电脑通过网线连接也可以。手机浏览器打开面板显示的地址，输入六位配对码。
6. 手机无法访问时，点击“允许手机连接…”，自行完成 Windows 管理员确认。规则仅放行此程序、此端口、同一子网的入站请求。

“打开遥控页”可在本机浏览器打开同一手机页面；“复制配对链接”复制带配对码的链接。每次启动重新生成配对码和访问凭据。

## 默认快捷键

| 操作 | 默认组合 |
| --- | --- |
| 播放 / 暂停 | Ctrl + Alt + Z |
| 上一首 | Ctrl + Alt + A |
| 下一首 | Ctrl + Alt + S |
| 音量增大 | Ctrl + Alt + ↑ |
| 音量减小 | Ctrl + Alt + ↓ |

支持 Ctrl / Alt / Shift 加字母、数字、方向键、空格或 F1–F12。本工具仅配置自己发送的组合键，不注册或修改网易云内部的快捷键。网易云中的对应全局快捷键应可用且未被其他软件占用。

## 运行要求与限制

- Windows 10/11 x64，.NET Framework 4.x，网易云音乐 Windows 客户端。无需 Node.js、Python 或手机 App。
- 通常无需管理员权限；若网易云以管理员权限运行，本工具也需要匹配权限。
- 鼠标驱动应保留标准 XButton1/XButton2。被改成键盘宏的侧键可能无法识别。
- 选中的侧键启用时不执行浏览器前进 / 后退；禁用时保留其他程序中的原有用途。
- 不回读准确播放状态、进度及绝对音量，不显示虚构进度和数值。音量通过网易云快捷键逐档调节。
- “已发送”代表快捷键发送成功，不保证网易云执行；客户端状态、快捷键冲突或权限不匹配可能影响行为。
- 按住 Ctrl / Alt / Shift / Win 时最多等待约 1.8 秒，未松开则取消发送。
- 默认端口 17663，占用时依次尝试至 17672，以面板为准。
- 访客网络、客户端隔离、VPN、第三方防火墙可能阻止手机连接。移动程序后可能需要重设防火墙规则。
- 不保证锁屏、休眠、不同桌面会话或游戏反作弊环境下可用。不同鼠标、手机和网易云版本仍需实机验证。
- EXE 尚未进行代码签名。

## 配置、安全与隐私

配置位于 `%LOCALAPPDATA%\CloudMusicRemote\preferences.json`。没有新配置时读取旧版 `settings.txt` 的侧键偏好。非法配置不会自动删除，程序提示并使用默认值。

不读取网易云密码、登录令牌或歌单文件，不修改网易云文件。曲目来自窗口标题。运行时无遥测、云端 API 或外部网页脚本依赖。

配对后的手机可以读取曲目标题并控制播放。凭据保存在当前手机标签页。HTTP 不加密，**只在可信局域网使用，不要映射到公网**。详见 [SECURITY.md](SECURITY.md)。

## 构建与验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package.ps1
```

使用 Windows 自带 .NET Framework 编译器。`dist/` 为构建目录，`artifacts/` 为发布包及 SHA-256 文件。重新编译前应退出同路径程序。

37 项自动化检查覆盖接口、配对、校验、自定义快捷键和侧键映射，不触发真实音乐或更改防火墙。仓库保留测试，发布包不含测试程序。[验证范围](https://github.com/mncatwzj/cloudmusic-remote/blob/main/docs/TESTING.md)。

```text
src/Desktop.cs          原生窗口、托盘和快捷键录入
src/Window.xaml         桌面布局与动效
src/Preferences.cs      配置校验及持久化
src/CloudMusicRemote.cs 鼠标钩子、快捷键发送、局域网服务
src/remote.html         唯一的手机遥控页面
scripts/                构建、验证、打包、网络规则
tests/                  内部质量验证
.github/workflows/      Windows CI 和版本发布
```

## 卸载与贡献

退出托盘程序后删除解压目录。可选删除 `%LOCALAPPDATA%\CloudMusicRemote` 及防火墙中 `CloudMusicRemote-LAN-` 开头的规则。程序不创建开机启动项。

欢迎 Issue 和 Pull Request，请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。本项目采用 [MIT License](LICENSE)，第三方名称及商标归其权利人所有。
