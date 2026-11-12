// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 junmoxiao-6

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenOffTool
{
    // ─────────────────────────── 路径 ───────────────────────────
    // 绿色便携策略：配置和日志默认放在 exe 同目录，整个软件就一个文件夹，不往 C 盘写。
    // 只有当程序目录不可写（比如放进 Program Files）时，才回退到 %APPDATA%\ScreenOff。
    internal static class AppPaths
    {
        private static string _dir;

        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;

                string exeDir = AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (IsWritable(exeDir))
                {
                    _dir = exeDir;
                }
                else
                {
                    _dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenOff");
                    try { Directory.CreateDirectory(_dir); } catch { }
                }

                MigrateIfNeeded(_dir);
                return _dir;
            }
        }

        /// <summary>是否绿色便携模式（数据在程序目录）。</summary>
        public static bool IsPortable
        {
            get
            {
                string exeDir = AppContext.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(Dir, exeDir, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".probe-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                using (FileStream fs = File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch { return false; }
        }

        // 从旧位置搬家：先看 %APPDATA%\ScreenOff（上一版），再看 %APPDATA%\一键息屏（更早的版本）
        private static void MigrateIfNeeded(string target)
        {
            try
            {
                if (File.Exists(Path.Combine(target, "config.json"))) return;

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string[] candidates = new string[]
                {
                    Path.Combine(appData, "ScreenOff"),
                    Path.Combine(appData, "一键息屏")
                };

                foreach (string c in candidates)
                {
                    string srcCfg = Path.Combine(c, "config.json");
                    if (!File.Exists(srcCfg)) continue;

                    File.Copy(srcCfg, Path.Combine(target, "config.json"));

                    string srcLog = Path.Combine(c, "log.txt");
                    string dstLog = Path.Combine(target, "log.txt");
                    if (File.Exists(srcLog) && !File.Exists(dstLog)) File.Copy(srcLog, dstLog);

                    Log.Write("已从旧位置迁移配置：" + c);
                    break;
                }
            }
            catch { }
        }
    }

    // ───────────────────────────── 配置 ─────────────────────────────
    internal sealed class AppConfig
    {
        public string Hotkey { get; set; } = "Ctrl+Alt+L";
        public int DelayMs { get; set; } = 0;
        // 先锁屏再熄屏：锁屏会切到安全桌面，后台程序的「保持显示器开启」请求就压不住熄屏了
        public bool LockBeforeOff { get; set; } = true;
        public bool DdcFallback { get; set; } = false;
        public bool ShowFirstRunTip { get; set; } = true;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 配置放在 %APPDATA%\ScreenOff\config.json，
        // 这样 exe 复制到桌面/启动项后仍然共用同一份设置。
        private static string FilePath
        {
            get { return Path.Combine(AppPaths.Dir, "config.json"); }
        }

        public static string FilePathForDisplay
        {
            get { return Path.Combine(AppPaths.Dir, "config.json"); }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    AppConfig c = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath, Encoding.UTF8), JsonOpts);
                    if (c != null) return c;
                }
            }
            catch { }

            AppConfig def = new AppConfig();
            def.Save();
            return def;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ───────────────────────────── 日志 ─────────────────────────────
    internal static class Log
    {
        public static string Path
        {
            get { return System.IO.Path.Combine(AppPaths.Dir, "log.txt"); }
        }

        public static void Write(string msg)
        {
            try
            {
                // 只保留最近 200KB，避免日志无限长大
                FileInfo fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > 200 * 1024) File.WriteAllText(Path, "");

                File.AppendAllText(Path,
                    DateTime.Now.ToString("MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ───────────────────────────── Win32 ─────────────────────────────
    internal static class Native
    {
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MONITORPOWER = 0xF170;
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetDevicePowerState(IntPtr hDevice, out bool pfOn);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte bVCPCode,
            IntPtr pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

        [DllImport("dxva2.dll", SetLastError = true)]
        public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);
    }

    // ──────────────────────── 熄屏核心逻辑 ────────────────────────
    internal static class ScreenController
    {
        // 显示器当前是否点着。用于判断首轮指令是否真的生效。
        public static bool IsDisplayOn()
        {
            IntPtr h = Native.CreateFileW(@"\\.\LCD",
                0x80000000u | 0x40000000u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return true;
            try
            {
                bool on;
                if (!Native.GetDevicePowerState(h, out on)) return true;
                return on;
            }
            finally { Native.CloseHandle(h); }
        }

        // 发一条和 Windows 自己熄屏完全相同的指令：
        // WM_SYSCOMMAND / SC_MONITORPOWER，lParam = 2 表示关闭显示器。
        // 只关显示器，不动电源状态、不锁会话、不结束任何进程。
        //
        // 通道选择有讲究（实测）：
        //   发给 Progman 窗口：约 15ms 返回，即刻生效；
        //   发给 HWND_BROADCAST：要逐个窗口投递，实测可阻塞 5 秒以上，
        //   期间线程被卡住，用户输入无法被及时检测，容易造成屏幕反复亮灭。
        // 所以优先走 Progman，只有它没生效时才用广播兜底。
        private static void SendMonitorOffFast()
        {
            IntPtr result;
            IntPtr progman = Native.FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                Native.SendMessageTimeout(progman, Native.WM_SYSCOMMAND,
                    new IntPtr(Native.SC_MONITORPOWER), new IntPtr(2),
                    0x0002 /* SMTO_ABORTIFHUNG */, 300, out result);
            }
        }

        private static void SendMonitorOff()
        {
            SendMonitorOffFast();

            // 兜底：只给 Progman 没管用时，才用广播补一次（超时给短，避免长时间卡住）
            Thread.Sleep(300);
            if (!IsDisplayOn()) return;

            IntPtr result;
            Native.SendMessageTimeout(new IntPtr(0xFFFF), Native.WM_SYSCOMMAND,
                new IntPtr(Native.SC_MONITORPOWER), new IntPtr(2),
                0x0002, 800, out result);
        }

        // 可选的兜底通道：走 DDC/CI 直接给显示器下电，
        // 用于极少数「首轮指令被系统立即拉回」的情况。默认关闭。
        private static void DdcPowerOff()
        {
            try
            {
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMon, IntPtr hdc, IntPtr rc, IntPtr data)
                {
                    uint count = 0;
                    if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) || count == 0) return true;

                    Native.PHYSICAL_MONITOR[] arr = new Native.PHYSICAL_MONITOR[count];
                    if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            try { Native.SetVCPFeature(arr[i].hPhysicalMonitor, 0xD6, 4); }
                            catch { }
                        }
                        Native.DestroyPhysicalMonitors(count, arr);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        public static void TurnOff(AppConfig cfg)
        {
            SendMonitorOff();

            if (cfg.DdcFallback)
            {
                Thread.Sleep(700);
                if (IsDisplayOn()) DdcPowerOff();
            }
        }

        private static uint LastInputTick()
        {
            Native.LASTINPUTINFO li = new Native.LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(Native.LASTINPUTINFO));
            if (!Native.GetLastInputInfo(ref li)) return 0;
            return li.dwTime;
        }

        // 熄屏 + 持续守护。
        // 先锁屏：切到安全桌面（Winlogon）后，后台程序的「保持显示器开启」请求就压不住熄屏了。
        // 之后持续盯着：只要屏幕被任何东西点亮、而此时用户并没有动键鼠，就立刻压回去。
        // 直到用户真正动了鼠标或键盘（要唤醒电脑）才收手。最长守 8 小时后自动放弃。
        public static void TurnOffAndGuard(AppConfig cfg)
        {
            if (cfg.LockBeforeOff)
            {
                if (Native.LockWorkStation()) Log.Write("已锁屏");
                else Log.Write("锁屏失败 err=" + Marshal.GetLastWin32Error());

                // 锁屏动画期间系统会点亮一次屏，等它落定再熄
                Thread.Sleep(520);
            }

            uint input = LastInputTick();
            TurnOff(cfg);

            DateTime deadline = DateTime.UtcNow.AddHours(8);
            DateTime started = DateTime.UtcNow;
            int reforced = 0;

            while (DateTime.UtcNow < deadline)
            {
                // 熄屏后头 2 秒最容易出状况，密集采样；之后放慢
                int wait = (DateTime.UtcNow - started).TotalSeconds < 2.0 ? 150 : 500;
                Thread.Sleep(wait);

                if (InputChanged(input))
                {
                    Log.Write("检测到用户输入，守护结束（期间重新熄屏 " + reforced + " 次）");
                    return;
                }

                if (!IsDisplayOn()) continue;

                // 防抖：屏幕刚亮很可能是「系统正在处理这次输入」，
                // 再等 200ms 复核一次，确认用户确实没有输入才压回去，
                // 否则会和用户的唤醒动作打架，表现为屏幕一闪一闪。
                Thread.Sleep(200);
                if (InputChanged(input))
                {
                    Log.Write("检测到用户输入（防抖复核），守护结束（期间重新熄屏 " + reforced + " 次）");
                    return;
                }
                if (!IsDisplayOn()) continue;

                reforced++;
                Log.Write("屏幕被点亮且无人操作，重新熄屏（累计 " + reforced + " 次）");
                SendMonitorOffFast();
            }

            Log.Write("守护达到 8 小时上限，自动退出");
        }

        private static bool InputChanged(uint baseline)
        {
            uint now = LastInputTick();
            return now != 0 && now != baseline;
        }
    }

    // ─────────────────── 显示器 DDC/CI 能力检测 ───────────────────
    // 只读取 VCP 0xD6（电源控制）当前值，不下发任何写指令，不会改变显示器状态。
    internal static class DdcCheck
    {
        public static bool Supported(out string detail)
        {
            bool any = false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            try
            {
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    delegate (IntPtr hMon, IntPtr hdc, IntPtr rc, IntPtr data)
                    {
                        uint count = 0;
                        if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) || count == 0)
                        {
                            sb.AppendLine("未识别到物理显示器");
                            return true;
                        }

                        Native.PHYSICAL_MONITOR[] arr = new Native.PHYSICAL_MONITOR[count];
                        if (Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                        {
                            for (int i = 0; i < arr.Length; i++)
                            {
                                string name = string.IsNullOrEmpty(arr[i].szPhysicalMonitorDescription)
                                    ? "显示器" : arr[i].szPhysicalMonitorDescription;

                                uint cur, max;
                                if (Native.GetVCPFeatureAndVCPFeatureReply(
                                        arr[i].hPhysicalMonitor, 0xD6, IntPtr.Zero, out cur, out max))
                                {
                                    any = true;
                                    sb.AppendLine(name + "：支持（当前电源状态 0x" + cur.ToString("X2") + "）");
                                }
                                else
                                {
                                    sb.AppendLine(name + "：不支持 DDC/CI 电源控制");
                                }
                            }
                            Native.DestroyPhysicalMonitors(count, arr);
                        }
                        return true;
                    }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                sb.AppendLine("检测过程出错：" + ex.Message);
            }

            detail = sb.ToString().Trim();
            return any;
        }
    }

    // ─────────────────────────── 快捷键 ───────────────────────────
    internal static class Hotkey
    {
        public const uint MOD_ALT = 0x1;
        public const uint MOD_CONTROL = 0x2;
        public const uint MOD_SHIFT = 0x4;
        public const uint MOD_WIN = 0x8;

        public static bool TryParse(string text, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] parts = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            string keyPart = null;

            foreach (string raw in parts)
            {
                string p = raw.Trim();
                switch (p.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        mods |= MOD_CONTROL; break;
                    case "alt":
                        mods |= MOD_ALT; break;
                    case "shift":
                        mods |= MOD_SHIFT; break;
                    case "win":
                    case "windows":
                        mods |= MOD_WIN; break;
                    default:
                        keyPart = p; break;
                }
            }

            if (keyPart == null || mods == 0) return false;

            Keys k;
            if (keyPart.Equals("printscreen", StringComparison.OrdinalIgnoreCase) ||
                keyPart.Equals("prtsc", StringComparison.OrdinalIgnoreCase))
            {
                k = Keys.Snapshot;
            }
            else if (!Enum.TryParse<Keys>(keyPart, true, out k))
            {
                return false;
            }

            vk = (uint)k;
            return vk != 0;
        }

        public static string Format(uint mods, Keys key)
        {
            StringBuilder sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(key == Keys.Snapshot ? "PrintScreen" : key.ToString());
            return sb.ToString();
        }
    }

    // 用来接收 WM_HOTKEY 的隐形窗口
    internal sealed class HotkeyWindow : NativeWindow
    {
        public event Action<int> HotkeyPressed;

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "ScreenOffHotkeySink";
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                Action<int> h = HotkeyPressed;
                if (h != null) h(m.WParam.ToInt32());
                return;
            }
            base.WndProc(ref m);
        }
    }

    // ─────────────────────────── 图标 ───────────────────────────
    internal static class Icons
    {
        private static Icon _tray;
        private static Icon _window;

        /// <summary>托盘图标：按当前 DPI 的小图标尺寸，从 app.ico 里取最接近的那一帧。</summary>
        public static Icon Create()
        {
            if (_tray != null) return _tray;

            Size want = SystemInformation.SmallIconSize;
            _tray = Load(want.Width, want.Height);
            return _tray;
        }

        /// <summary>窗口/任务栏图标：取 48 像素帧，缩小比放大清晰。</summary>
        public static Icon CreateWindowIcon()
        {
            if (_window != null) return _window;
            _window = Load(48, 48);
            return _window;
        }

        private static Icon Load(int w, int h)
        {
            try
            {
                using (Stream s = typeof(Icons).Assembly.GetManifestResourceStream("app.ico"))
                {
                    if (s != null)
                    {
                        // 多尺寸 ico：.NET 会挑最接近的一帧，完全匹配时原样使用
                        Icon ic = new Icon(s, new Size(w, h));
                        Log.Write("图标载入：" + w + "x" + h + " 请求 → 实际 " + ic.Width + "x" + ic.Height);
                        return ic;
                    }
                    Log.Write("图标资源 app.ico 未找到，改用系统默认图标");
                }
            }
            catch (Exception ex)
            {
                Log.Write("载入图标失败：" + ex.Message);
            }

            return SystemIcons.Application;
        }
    }

    // ─────────────────────── 快捷键设置窗口 ───────────────────────
    internal sealed class HotkeyForm : Form
    {
        private readonly Label _hint;
        private readonly Label _shown;
        private readonly Button _ok;
        private uint _mods;
        private Keys _key = Keys.None;

        public string Result { get; private set; }

        public HotkeyForm(string current)
        {
            Text = "设置快捷键";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 168);
            Font = new Font("Microsoft YaHei UI", 9f);
            KeyPreview = true;

            _hint = new Label();
            _hint.Text = "请直接按下你想用的组合键（必须含 Ctrl / Alt / Shift / Win 至少一个）";
            _hint.SetBounds(18, 14, 344, 36);

            _shown = new Label();
            _shown.Text = string.IsNullOrWhiteSpace(current) ? "未设置" : current;
            _shown.Font = new Font("Consolas", 15f, FontStyle.Bold);
            _shown.TextAlign = ContentAlignment.MiddleCenter;
            _shown.BorderStyle = BorderStyle.FixedSingle;
            _shown.BackColor = Color.White;
            _shown.SetBounds(18, 56, 344, 46);

            _ok = new Button();
            _ok.Text = "确定";
            _ok.SetBounds(196, 118, 78, 30);
            _ok.DialogResult = DialogResult.OK;

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(284, 118, 78, 30);
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(_hint);
            Controls.Add(_shown);
            Controls.Add(_ok);
            Controls.Add(cancel);

            AcceptButton = _ok;
            CancelButton = cancel;

            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu ||
                k == Keys.LWin || k == Keys.RWin || k == Keys.None)
            {
                return;
            }

            uint mods = 0;
            if (e.Control) mods |= Hotkey.MOD_CONTROL;
            if (e.Alt) mods |= Hotkey.MOD_ALT;
            if (e.Shift) mods |= Hotkey.MOD_SHIFT;

            if (mods == 0)
            {
                _shown.Text = "需要 Ctrl / Alt / Shift 至少一个";
                _shown.ForeColor = Color.Firebrick;
                _ok.Enabled = false;
                return;
            }

            _mods = mods;
            _key = k;
            _shown.ForeColor = Color.Black;
            _shown.Text = Hotkey.Format(mods, k);
            _ok.Enabled = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && _key != Keys.None)
            {
                Result = Hotkey.Format(_mods, _key);
            }
            base.OnFormClosing(e);
        }
    }

    // ─────────────────────────── 托盘主体 ───────────────────────────
    internal sealed class TrayApp : ApplicationContext
    {
        private const int HOTKEY_ID = 0xB001;

        private readonly AppConfig _cfg;
        private readonly NotifyIcon _icon;
        private readonly HotkeyWindow _sink;
        private readonly ToolStripMenuItem _doItem;
        private readonly System.Collections.Generic.List<int> _hotkeyIds = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<string> _activeHotkeys = new System.Collections.Generic.List<string>();

        private MainForm _panel;
        private readonly Control _marshal;
        private readonly EventWaitHandle _showEvent;
        private bool _exiting;

        public const string ShowEventName = "Global\\ScreenOffShowPanel-9F2A17";

        // 主键被占用时的退路。平时只用主键，不额外抢占别的组合。
        private static readonly string[] BackupHotkeys = new string[]
        {
            "Ctrl+Alt+F9", "Ctrl+Alt+F8"
        };

        public TrayApp(AppConfig cfg, bool showPanel)
        {
            _cfg = cfg;

            _doItem = new ToolStripMenuItem("立即熄屏");
            _doItem.Click += delegate { RunNow(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.Items.Add(new ToolStripMenuItem("显示主面板", null, delegate { ShowPanel(); }));
            menu.Items.Add(_doItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("设置…", null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("在桌面创建快捷方式", null, delegate { CreateShortcutUi(); }));
            menu.Items.Add(new ToolStripMenuItem("查看日志", null, delegate { OpenLog(); }));
            menu.Items.Add(new ToolStripMenuItem("打开数据目录", null, delegate { OpenDataDir(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("关于", null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { ExitApp(); }));

            _icon = new NotifyIcon();
            _icon.Icon = Icons.Create();
            _icon.Text = "一键息屏";
            _icon.ContextMenuStrip = menu;
            _icon.Visible = true;
            _icon.DoubleClick += delegate { ShowPanel(); };

            // 用于把「显示面板」请求从后台线程切到 UI 线程
            _marshal = new Control();
            IntPtr force = _marshal.Handle;

            // 第二个实例启动时，通过命名事件通知这里弹出面板
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            Thread watcher = new Thread(delegate ()
            {
                while (true)
                {
                    _showEvent.WaitOne();
                    if (_exiting) return;
                    try { _marshal.BeginInvoke((MethodInvoker)delegate { ShowPanel(); }); }
                    catch { }
                }
            });
            watcher.IsBackground = true;
            watcher.Start();

            _sink = new HotkeyWindow();
            _sink.HotkeyPressed += delegate (int id)
            {
                if (_hotkeyIds.Contains(id)) RunNow();
            };

            ApplyHotkey();

            if (showPanel)
            {
                ShowPanel();
            }

            if (_cfg.ShowFirstRunTip)
            {
                _icon.ShowBalloonTip(5000, "一键息屏已就绪",
                    "按 " + PrimaryHotkey + (_cfg.LockBeforeOff
                        ? " 先锁屏再熄屏；回来动一下鼠标键盘，输入密码即可。"
                        : " 立即熄屏；回来动一下鼠标键盘即可。"),
                    ToolTipIcon.Info);
                _cfg.ShowFirstRunTip = false;
                _cfg.Save();
            }
            Log.Write("托盘启动完成，可用快捷键：" + string.Join(" / ", _activeHotkeys.ToArray()));
        }

        private void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(_cfg))
            {
                if (f.ShowDialog() != DialogResult.OK) return;

                AppConfig reloaded = AppConfig.Load();
                _cfg.Hotkey = reloaded.Hotkey;
                _cfg.LockBeforeOff = reloaded.LockBeforeOff;
                _cfg.DelayMs = reloaded.DelayMs;
                _cfg.DdcFallback = reloaded.DdcFallback;

                ApplyHotkey();
                Log.Write("设置已更新：热键=" + _cfg.Hotkey
                          + " 锁屏=" + _cfg.LockBeforeOff
                          + " 延时=" + _cfg.DelayMs
                          + " DDC=" + _cfg.DdcFallback);
            }
        }

        public void ShowPanel()
        {
            if (_exiting) return;

            if (_panel == null || _panel.IsDisposed)
            {
                _panel = new MainForm(_cfg, RunNow, delegate
                {
                    ApplyHotkey();
                    RefreshPanelHotkey();
                });
                _panel.HotkeyProvider = delegate { return PrimaryHotkey; };
            }

            _panel.RefreshSettings();
            _panel.Show();
            if (_panel.WindowState == FormWindowState.Minimized)
                _panel.WindowState = FormWindowState.Normal;

            _panel.BringToFront();
            _panel.Activate();
            try { SetForegroundWindow(_panel.Handle); } catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void RefreshPanelHotkey()
        {
            if (_panel != null && !_panel.IsDisposed) _panel.RefreshHotkey();
        }

        private void RunNow()
        {
            if (_cfg.DelayMs <= 0)
            {
                Log.Write("熄屏（立即）");
                Fire();
                return;
            }

            Log.Write("熄屏（延时 " + (_cfg.DelayMs / 1000) + " 秒）");

            // 延时熄屏不阻塞界面，让托盘在等待期间仍可操作
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = _cfg.DelayMs;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                Fire();
            };
            t.Start();
        }

        // 放到后台线程执行，避免守护的那 2 秒卡住托盘菜单
        private void Fire()
        {
            Thread th = new Thread(delegate () { ScreenController.TurnOffAndGuard(_cfg); });
            th.IsBackground = true;
            th.Start();
        }

        private string PrimaryHotkey
        {
            get { return _activeHotkeys.Count > 0 ? _activeHotkeys[0] : "未注册"; }
        }

        private bool TryRegister(string hk, int id)
        {
            uint mods, vk;
            if (!Hotkey.TryParse(hk, out mods, out vk))
            {
                Log.Write("热键解析失败：" + hk);
                return false;
            }

            if (Native.RegisterHotKey(_sink.Handle, id, mods | 0x4000u, vk))
            {
                _hotkeyIds.Add(id);
                _activeHotkeys.Add(hk);
                Log.Write("热键注册成功：" + hk);
                return true;
            }

            Log.Write("热键注册失败（被占用）：" + hk + "  err=" + Marshal.GetLastWin32Error());
            return false;
        }

        private void ApplyHotkey()
        {
            foreach (int id in _hotkeyIds) Native.UnregisterHotKey(_sink.Handle, id);
            _hotkeyIds.Clear();
            _activeHotkeys.Clear();

            // 只注册一个主键，避免抢占系统和其它软件的快捷键。
            if (!TryRegister(_cfg.Hotkey, HOTKEY_ID))
            {
                // 主键被占用才退到备用键，并明确告知用户
                int id = HOTKEY_ID + 1;
                foreach (string b in BackupHotkeys)
                {
                    if (TryRegister(b, id))
                    {
                        _icon.ShowBalloonTip(6000, "快捷键被占用",
                            "「" + _cfg.Hotkey + "」已被其他程序占用，暂时改用 " + b +
                            "。右键托盘图标可自行设置。", ToolTipIcon.Warning);
                        break;
                    }
                    id++;
                }
            }

            _doItem.Text = (_cfg.LockBeforeOff ? "立即锁屏并熄屏" : "立即熄屏") + "\t" + PrimaryHotkey;
            try { _icon.Text = "一键息屏 · " + PrimaryHotkey; } catch { }
            RefreshPanelHotkey();

            if (_activeHotkeys.Count == 0)
            {
                Log.Write("警告：没有任何热键注册成功");
                _icon.ShowBalloonTip(5000, "快捷键注册失败",
                    "没有抢到可用组合，请右键托盘图标手动设置一个。", ToolTipIcon.Warning);
            }
        }

        private void OpenDataDir()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + AppPaths.Dir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败：" + ex.Message, "一键息屏");
            }
        }

        private void CreateShortcutUi()
        {
            try
            {
                string lnk = ShellShortcut.CreateOnDesktop();
                string resolved;
                bool ok = ShellShortcut.Verify(lnk, out resolved);
                Log.Write("创建快捷方式：" + lnk + " 校验=" + ok + " 目标=" + resolved);

                MessageBox.Show(
                    "已放到桌面：\n" + lnk + (ok ? "" : "\n\n（回读校验未通过，目标：" + resolved + "）"),
                    "一键息屏", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Write("创建快捷方式失败：" + ex.Message);
                MessageBox.Show("创建失败：" + ex.Message, "一键息屏",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenLog()
        {
            try
            {
                if (!File.Exists(Log.Path)) Log.Write("（尚无记录）");
                System.Diagnostics.Process.Start("notepad.exe", "\"" + Log.Path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开日志失败：" + ex.Message, "一键息屏");
            }
        }

        private void ShowAbout()
        {
            string list = _activeHotkeys.Count > 0 ? string.Join(" / ", _activeHotkeys.ToArray()) : "（无）";
            MessageBox.Show(
                "ScreenOff 1.6 · 一键息屏\n\n" +
                "当前流程：" + (_cfg.LockBeforeOff ? "先锁屏 → 再熄屏" : "只熄屏（不锁屏）") + "\n" +
                (_cfg.LockBeforeOff
                    ? "锁屏后切到安全桌面，后台程序的「保持显示器开启」压不住熄屏；\n回来时动一下鼠标键盘点亮屏幕，输入密码解锁。\n\n"
                    : "不锁屏，只关显示器；回来动一下鼠标键盘即亮。\n\n") +
                "快捷键：" + list + "\n" +
                "延时：" + (_cfg.DelayMs <= 0 ? "立即" : (_cfg.DelayMs / 1000) + " 秒") + "\n" +
                "DDC/CI 兜底：" + (_cfg.DdcFallback ? "已开启" : "已关闭") + "\n" +
                "数据存放：" + (AppPaths.IsPortable ? "绿色便携（程序目录）" : "用户数据目录") + "\n\n" +
                "配置文件：\n" + AppConfig.FilePathForDisplay + "\n" +
                "日志：\n" + Log.Path,
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApp()
        {
            _exiting = true;
            foreach (int id in _hotkeyIds) Native.UnregisterHotKey(_sink.Handle, id);
            _hotkeyIds.Clear();
            Log.Write("退出托盘");

            if (_panel != null && !_panel.IsDisposed)
            {
                _panel.Hide();
                _panel.Dispose();
            }

            try { _showEvent.Close(); } catch { }
            _icon.Visible = false;
            _icon.Dispose();
            ExitThread();
        }

    }

    // ─────────────────────────── 入口 ───────────────────────────
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }

            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log.Write("UI 线程异常：" + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Write("未捕获异常：" + e.ExceptionObject);
            };

            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                Log.Write("启动失败：" + ex);
                MessageBox.Show("启动失败：" + ex.Message, "ScreenOff",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            AppConfig cfg = AppConfig.Load();

            // --uicheck：自检主面板能否正常构建（不驻留、不熄屏）
            if (args.Any(a => string.Equals(a, "--uicheck", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using (MainForm f = new MainForm(cfg, delegate { }, delegate { }))
                    {
                        f.Show();
                        Application.DoEvents();
                        Log.Write("UI 自检：主面板创建成功 " + f.Width + "x" + f.Height
                                  + "，控件数=" + f.Controls.Count);
                        f.Close();
                    }

                    using (SettingsForm s = new SettingsForm(cfg))
                    {
                        s.Show();
                        Application.DoEvents();
                        Log.Write("UI 自检：设置窗口创建成功 " + s.Width + "x" + s.Height
                                  + "，控件数=" + s.Controls.Count);
                        s.Close();
                    }
                    Log.Write("UI 自检：通过");
                }
                catch (Exception ex)
                {
                    Log.Write("UI 自检失败：" + ex);
                }
                return;
            }

            // --settings：直接打开设置窗口（不驻留托盘）
            if (args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)))
            {
                using (SettingsForm f = new SettingsForm(cfg))
                {
                    f.ShowDialog();
                }
                return;
            }

            // --shortcut：在桌面创建快捷方式后退出（供安装脚本或手动调用）
            if (args.Any(a => string.Equals(a, "--shortcut", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    string lnk = ShellShortcut.CreateOnDesktop();
                    string resolved;
                    bool ok = ShellShortcut.Verify(lnk, out resolved);
                    Log.Write("创建快捷方式：" + lnk);
                    Log.Write("回读校验：" + (ok ? "通过" : "失败") + "，目标=" + resolved);
                }
                catch (Exception ex)
                {
                    Log.Write("创建快捷方式失败：" + ex.Message);
                }
                return;
            }

            // --tray：只驻留托盘，不弹面板。无参数：弹出主面板。
            bool trayOnly = args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));

            const string mutexName = "Global\\ScreenOffTray-9F2A17";
            bool createdNew;
            Mutex mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // 已有实例：请它把面板弹出来。若它正好在退出（互斥量未释放、事件已关闭），
                // 就稍等重试接管，避免用户双击后毫无反应。
                bool handedOver = false;
                for (int i = 0; i < 20; i++)
                {
                    if (SignalShowPanel())
                    {
                        handedOver = true;
                        break;
                    }

                    Thread.Sleep(150);

                    bool nowNew;
                    mutex.Dispose();
                    mutex = new Mutex(true, mutexName, out nowNew);
                    if (nowNew)
                    {
                        createdNew = true;
                        Log.Write("旧实例已退出，由本次启动接管");
                        break;
                    }
                }

                if (handedOver || !createdNew)
                {
                    mutex.Dispose();
                    return;
                }
            }

            Log.Write(trayOnly ? "启动（仅托盘）" : "启动（主面板）");
            try
            {
                Application.Run(new TrayApp(cfg, !trayOnly));
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
                mutex.Dispose();
            }
        }

        /// <summary>通知已在运行的实例显示主面板。成功返回 true。</summary>
        private static bool SignalShowPanel()
        {
            try
            {
                using (EventWaitHandle ev = EventWaitHandle.OpenExisting(TrayApp.ShowEventName))
                {
                    ev.Set();
                }
                Log.Write("已有实例在运行，已请求其显示主面板");
                return true;
            }
            catch { return false; }
        }
    }
}
