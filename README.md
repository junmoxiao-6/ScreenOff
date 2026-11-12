# 一键息屏 · ScreenOff

一个 Windows 小工具：**按一下快捷键，锁屏 + 关闭显示器**，后台任务照常运行。

> Press a hotkey → lock the screen and turn the monitor off. Background tasks keep running.

单文件绿色版，自带 .NET 运行时，拷贝到任意 64 位 Windows 10/11 即可运行。

---

## 它解决什么问题

Windows 自带的 `Win + L` 只能锁屏，屏幕要等电源计划里的「关闭显示器」超时才熄灭；
而直接发熄屏指令又容易被某些常驻程序（持有 `ES_DISPLAY_REQUIRED` 的那类）顶回来。

这个工具把两件事合成一步：**先锁屏（切到安全桌面），再立刻熄屏**——
锁屏之后，用户会话里的「保持唤醒」请求就压不住熄屏了。

## 功能

- **主面板**：双击 exe 弹出一个面板，中间是月亮按钮，点一下就熄屏；常用设置直接改
- **全局快捷键**：默认 `Ctrl + Alt + L`，可在设置里改（自动检测冲突）
- **两种模式**
  - 先锁屏再熄屏（默认）：隐私友好，回来需密码
  - 只熄屏不锁屏：只关显示器，回来直接回桌面
- **持续守护**：熄屏后一直盯着，若屏幕被系统或其它程序点亮、而用户并未操作，
  立刻压回去；一旦检测到真实的鼠标键盘输入，立即收手。上限 8 小时仅作防呆。
- **延时熄屏**：立即 / 5 / 10 / 30 / 60 秒
- **开机自启**：写当前用户 `Run` 键，无需管理员
- **托盘常驻**：双击托盘图标唤出面板
- **桌面快捷方式**：一键生成
- **高级选项**：显示器硬断电（DDC/CI），附「检测本机是否支持」

## 用法

1. **下载 `ScreenOff.exe`，双击运行** —— 弹出主面板：
   - 中间的大月亮按钮：点一下 = 锁屏 + 熄屏
   - 下方四行设置：熄屏方式、延时、开机自启、DDC，点一下即切换并立即保存
   - 底部：更多设置 / 打开数据目录
2. 关掉面板会收进托盘，程序继续在后台待命；再双击 exe 会唤出同一个面板（单实例）。
3. 也可以直接按 `Ctrl + Alt + L` 熄屏，不必打开面板。
4. 回来时动一下鼠标键盘点亮，输密码解锁。

托盘右键菜单：显示主面板 / 立即熄屏 / 设置… / 在桌面创建快捷方式 / 查看日志 / 打开数据目录 / 关于 / 退出

### 命令行参数

| 参数 | 说明 |
|---|---|
| 无参数 | 启动并弹出主面板（已在运行则唤出那个面板） |
| `--tray` | 只驻留托盘，不弹面板（适合开机自启） |
| `--settings` | 直接打开设置窗口 |
| `--shortcut` | 在桌面创建快捷方式 |
| `--uicheck` | 自检主面板与设置窗口（写日志，不熄屏） |

### 数据存放（绿色便携）

配置和日志**就放在 exe 同一个目录里**，整个软件只有一个文件夹，不往 C 盘写：

| 文件 | 位置 |
|---|---|
| 配置 | `程序目录\config.json`（首次运行自动生成） |
| 日志 | `程序目录\log.txt` |

如果程序目录不可写（比如放进 `C:\Program Files`），会自动回退到 `%APPDATA%\ScreenOff\`。
从旧版本升级时，会自动把旧位置的配置和日志搬过来，不会丢设置。

托盘右键「打开数据目录」可直接定位到该文件夹。

```json
{
  "Hotkey": "Ctrl+Alt+L",
  "DelayMs": 0,
  "LockBeforeOff": true,
  "DdcFallback": false,
  "ShowFirstRunTip": true
}
```

## 实现要点

关显示器用的是 Windows 自己那条指令：

```c
SendMessage(Progman, WM_SYSCOMMAND, SC_MONITORPOWER, 2);  // 2 = 关闭显示器
```

实测发现投递目标很关键：

| 投递目标 | 耗时 | 表现 |
|---|---|---|
| `Progman` 窗口 | ~15 ms | 立即生效，稳定 |
| `HWND_BROADCAST` | 最长 5.7 s | 逐个窗口投递，线程被长时间卡住；期间用户输入无法及时检测，屏幕容易反复亮灭 |

所以优先用 `Progman`，只有它没生效时才用广播兜底——这一条是修掉「闪屏」bug 的关键。

守护线程每 150 ms（前 2 秒）/ 500 ms（之后）检查一次显示器状态，
发现被点亮后还会等 200 ms 复核输入状态，避免和用户的唤醒动作抢。

## 构建

需要 .NET SDK 9+：

```bat
build.cmd
```

或手动：

```bat
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o dist
```

产物 `dist\ScreenOff.exe` 约 46 MB，自带运行时。

图标由 `tools/make_icon.py` 生成（纯标准库，无需 Pillow）。

## 已知限制

- **仅 Windows 10/11 x64**
- **笔记本内屏通常不支持 DDC/CI 硬断电**（走 eDP），「只有电源键能唤醒」这个效果
  在内屏上做不到；外接显示器多数支持，可用设置里的「检测本机是否支持」确认
- 全局热键会覆盖同名组合的软件内部快捷键。默认的 `Ctrl+Alt+L` 在以下软件里有自己的用途：
  - JetBrains 系（IDEA / PyCharm / WebStorm / CLion / GoLand / Android Studio）：重新格式化代码
  - Visual Studio：解决方案资源管理器
  如冲突，请在设置里换一个组合。

## 实测记录（Dell Inspiron 5593 / Windows）

| 项目 | 结果 |
|---|---|
| `Progman` 投递熄屏 | 15.6 ms 返回，0.4 s 内屏幕变黑 |
| `HWND_BROADCAST` 投递 | 5685 ms 返回 |
| 熄屏后静置 20 秒（不锁屏 / 锁屏） | 均全程保持黑屏，无自发点亮 |
| 鼠标移动 2 px | 立即唤醒 |
| 快捷键注册 | 外部进程抢注返回 `1409`（已被占用） |
| DDC/CI 电源控制 | 本机内屏不支持（`GetVCPFeatureAndVCPFeatureReply(0xD6)` 失败） |

## 许可

GNU General Public License v3.0（`GPL-3.0-or-later`），全文见 [LICENSE](LICENSE)。

你可以自由使用、修改、分发，但**衍生作品必须同样以 GPL 开源**。

## 发布

发布资产就是 `dist\ScreenOff.exe` **一个文件**（约 46 MB，自带 .NET 运行时），
用户下载后双击即用，不需要任何运行库。

```bash
git tag v1.6.0
git push --tags
```

然后在 GitHub Releases 上传 `ScreenOff.exe`。

版权署名：`Copyright (C) 2026 junmoxiao-6`，已写入三个源文件的 SPDX 头与 `.csproj` 的 `<Copyright>`。
