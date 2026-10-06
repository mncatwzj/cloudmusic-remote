# 网易云 · 随手控 / CloudMusic Remote

鼠标侧键切歌，通过同一局域网内的手机控制 Windows 网易云音乐。

![Version](https://img.shields.io/badge/version-0.1.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4)
![License](https://img.shields.io/badge/license-MIT-green)
![AI generated](https://img.shields.io/badge/AI--generated-OpenAI%20Codex-orange)

> **AI 编写声明：** 本项目的首个版本由 **OpenAI Codex** 根据人类提出的需求生成，包括程序代码、手机界面、文档及测试。发布和使用由人类负责；AI 生成不代表通过独立安全审计。完整说明见 [AI_DISCLOSURE.md](AI_DISCLOSURE.md)。
>
> **v0.1.0 为首个预发布版本（Pre-release）**，欢迎反馈。此项目是社区独立辅助工具，与网易云音乐及其运营方无隶属或合作关系。

## 功能

- 默认将鼠标侧上键 **XButton2** 映射为下一首，支持切换为 XButton1 或关闭映射。
- 手机浏览器控制：播放 / 暂停、上一首、下一首、音量增大 / 减小。
- 显示网易云窗口中的当前曲目信息。
- 六位配对码、请求鉴权和错误配对限流。
- 关闭窗口后在系统托盘继续运行；右击托盘可退出。
- 不修改网易云文件，不需要网易云账号密码，也不需要手机安装 App。

## 下载与使用

1. 在本仓库的 **Releases** 页面下载 `CloudMusicRemote-v0.1.0-win-x64.zip`，解压全部文件。
2. 打开网易云音乐，再双击 `CloudMusicRemote.exe`。
3. 按鼠标侧上键即可切换下一首。若鼠标定义相反，在工具里选择另一侧键。
4. 手机连接与电脑相同的路由器，浏览器输入控制面板显示的 `http://电脑IP:端口/` 地址。电脑使用网线连接也可以。
5. 输入面板上的六位配对码，点击“连接电脑”。复制手机链接时会附带配对码，页面会自动填入。
6. 如果手机打不开页面，点击电脑工具里的 **允许手机连接…**，并自行完成 Windows 管理员确认。该操作只放行本程序对应端口的同一子网入站连接。

通常不需要以管理员身份运行主程序。配置防火墙需要管理员权限；若网易云本身以管理员身份运行，工具也需要匹配权限。

## 运行要求

- Windows 10/11 x64，.NET Framework 4.x（这些系统通常已自带）。
- 网易云音乐 Windows 桌面客户端，以下**全局快捷键**处于启用且未冲突状态：

| 功能 | 网易云全局快捷键 |
| --- | --- |
| 播放 / 暂停 | `Ctrl + Alt + Z` |
| 上一首 | `Ctrl + Alt + A` |
| 下一首 | `Ctrl + Alt + S` |
| 音量增大 | `Ctrl + Alt + ↑` |
| 音量减小 | `Ctrl + Alt + ↓` |

请在网易云 **设置 → 快捷键 → 全局快捷键** 核对。v0.1.0 使用这组固定配置；更改网易云的配置后，工具不会自动同步。

## 限制与故障排查

- 手机音量按一档一档调节网易云内部音量；当前版本没有绝对音量滑块或音量数值回读。
- “播放 / 暂停”是切换操作；页面不会猜测真实播放状态。
- “已发送”仅表示快捷键成功注入，不能保证网易云执行。快捷键冲突、权限不一致或没有播放列表都可能导致无响应。
- 该工具通过网易云的全局快捷键控制播放。若其他应用抢占同样的快捷键，可能发生误响应。
- 鼠标驱动需保留标准的 XButton1/XButton2；映射成键盘宏的侧键可能无法识别。启用映射后，选中的侧键不再执行原来的浏览器前进/后退。
- 按住 Ctrl / Alt / Shift / Win 时，操作最多等待约 1.8 秒，超时取消，避免释放用户按住的修饰键。
- 不支持电脑休眠、锁屏或其他桌面会话下的可靠控制，不承诺在游戏反作弊环境中可用。
- 不同网易云版本、不同鼠标和手机仍需实际验证。v0.1.0 尚未完成全面兼容性测试。
- 手机须能访问电脑。访客网络、路由器客户端隔离、VPN 路由和第三方防火墙可能阻止连接。
- 默认端口为 `17663`；占用时依次尝试至 `17672`。请使用面板显示的实际端口。
- 重启后会更换配对码和访问凭据，手机需要重新配对。IP 变化后请在面板刷新地址。

## 安全与隐私

这是供**可信局域网**使用的 HTTP 遥控工具。不要做公网端口映射。配对请求没有 TLS 加密，同网段的攻击者可能截获通信。

服务监听 IPv4 地址，仅接受回环和私有 / 链路本地地址；使用配对凭据验证状态查询和控制请求，校验 Host / Origin，并限制请求大小及并发数量。具体威胁边界见 [SECURITY.md](SECURITY.md)。

运行时没有遥测或云端 API；程序不会读取网易云登录令牌、账号密码或歌单文件。曲目信息来自网易云的窗口标题，发送给已配对的客户端。侧键偏好存于 `%LOCALAPPDATA%\CloudMusicRemote\settings.txt`。登录凭据仅存于手机浏览器当前标签页的 sessionStorage。

## 从源码构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package.ps1
```

使用 Windows 自带的 .NET Framework C# 编译器，无需安装 Node.js、Python 或 NuGet 依赖。构建产物位于 `dist/`，发布压缩包和 SHA-256 校验文件位于 `artifacts/`。运行中的同路径 EXE 需要先退出才能重新构建。

```text
src/                    C# 程序和内嵌手机网页
scripts/                构建、测试、打包和防火墙配置
tests/                  不触发真实播放操作的接口测试
docs/TESTING.md          自动化与手动验收范围
.github/workflows/      Windows 持续集成与版本发布
```

## 卸载

退出托盘程序，删除解压目录即可。可选：在 Windows 防火墙中删除 `CloudMusicRemote-LAN-` 开头的本工具规则，并删除 `%LOCALAPPDATA%\CloudMusicRemote`。程序不创建开机启动项。

## 贡献与许可

欢迎提交 Issue 或 Pull Request。提交前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)；变更记录见 [CHANGELOG.md](CHANGELOG.md)。

本项目使用 [MIT License](LICENSE)。网易云音乐名称及相关商标归其权利人所有。
