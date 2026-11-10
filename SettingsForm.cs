// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 junmoxiao-6

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenOffTool
{
    // 设置窗口：所有选项都在这里改，不用手编 JSON
    internal sealed class SettingsForm : Form
    {
        private readonly AppConfig _cfg;

        private readonly TextBox _hotkeyBox;
        private readonly RadioButton _lockRadio;
        private readonly RadioButton _noLockRadio;
        private readonly ComboBox _delayBox;
        private readonly CheckBox _startupBox;
        private readonly CheckBox _ddcBox;

        public SettingsForm(AppConfig cfg)
        {
            _cfg = cfg;

            Text = "一键息屏 · 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 470);
            Font = new Font("Microsoft YaHei UI", 9f);

            int y = 18;

            // ── 快捷键 ──
            AddLabel("快捷键", 20, y);
            _hotkeyBox = new TextBox();
            _hotkeyBox.ReadOnly = true;
            _hotkeyBox.Text = cfg.Hotkey;
            _hotkeyBox.SetBounds(20, y + 22, 300, 26);
            _hotkeyBox.TextChanged += delegate { };
            Controls.Add(_hotkeyBox);

            Button capture = new Button();
            capture.Text = "按下新键…";
            capture.SetBounds(332, y + 21, 108, 28);
            capture.Click += delegate { CaptureHotkey(); };
            Controls.Add(capture);
            y += 66;

            // ── 触发方式 ──
            AddLabel("触发时", 20, y);
            _lockRadio = new RadioButton();
            _lockRadio.Text = "先锁屏，再熄屏（推荐，后台程序的「保持唤醒」压不住）";
            _lockRadio.SetBounds(24, y + 22, 430, 22);
            _lockRadio.Checked = cfg.LockBeforeOff;
            Controls.Add(_lockRadio);

            _noLockRadio = new RadioButton();
            _noLockRadio.Text = "只熄屏，不锁屏（回到桌面不需要密码）";
            _noLockRadio.SetBounds(24, y + 46, 430, 22);
            _noLockRadio.Checked = !cfg.LockBeforeOff;
            Controls.Add(_noLockRadio);
            y += 84;

            // ── 延时 ──
            AddLabel("按下后延时", 20, y);
            _delayBox = new ComboBox();
            _delayBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _delayBox.SetBounds(20, y + 22, 150, 26);
            _delayBox.Items.AddRange(new object[] { "立即", "5 秒", "10 秒", "30 秒", "60 秒" });
            int[] delays = new int[] { 0, 5000, 10000, 30000, 60000 };
            int idx = Array.IndexOf(delays, cfg.DelayMs);
            _delayBox.SelectedIndex = idx < 0 ? 0 : idx;
            Controls.Add(_delayBox);
            y += 66;

            // ── 开机启动 ──
            _startupBox = new CheckBox();
            _startupBox.Text = "开机自动启动（登录后常驻托盘，快捷键随时可用）";
            _startupBox.SetBounds(20, y, 430, 22);
            _startupBox.Checked = StartupHelper.IsEnabled();
            Controls.Add(_startupBox);
            y += 34;

            // ── 高级 ──
            AddLabel("高级选项", 20, y);
            y += 22;

            _ddcBox = new CheckBox();
            _ddcBox.Text = "让显示器直接断电（DDC/CI）";
            _ddcBox.SetBounds(24, y, 290, 22);
            _ddcBox.Checked = cfg.DdcFallback;
            Controls.Add(_ddcBox);

            Button ddcTest = new Button();
            ddcTest.Text = "检测本机是否支持";
            ddcTest.SetBounds(322, y - 3, 118, 28);
            ddcTest.Click += delegate { CheckDdc(); };
            Controls.Add(ddcTest);
            y += 26;

            Label warn = new Label();
            warn.Text =
                "普通熄屏 = 让系统关掉显示器，动一下鼠标键盘就亮。\n" +
                "这个选项绕过系统，直接命令显示器断电：键鼠唤不醒，只能按显示器电源键。\n" +
                "笔记本内屏通常不支持，外接显示器多数支持。不确定就先点上面的检测。";
            warn.ForeColor = Color.FromArgb(150, 90, 20);
            warn.SetBounds(38, y, 410, 56);
            Controls.Add(warn);
            y += 62;

            // ── 按钮 ──
            Button ok = new Button();
            ok.Text = "确定";
            ok.SetBounds(268, y, 80, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate { Apply(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(360, y, 80, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            Label path = new Label();
            path.Text = "配置文件：" + AppConfig.FilePathForDisplay;
            path.ForeColor = Color.Gray;
            path.SetBounds(20, y + 38, 430, 20);
            Controls.Add(path);
        }

        private void AddLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.SetBounds(x, y, 300, 20);
            l.ForeColor = Color.FromArgb(70, 70, 70);
            Controls.Add(l);
        }

        private void CheckDdc()
        {
            string detail;
            bool ok = DdcCheck.Supported(out detail);

            MessageBox.Show(
                detail + "\n\n" +
                (ok
                    ? "结论：本机显示器支持硬断电。\n注意：开启后键鼠可能唤不醒，需要按显示器电源键才能恢复。"
                    : "结论：本机显示器不支持硬断电，开了也不会有任何效果，保持关闭即可。"),
                "显示器 DDC/CI 检测",
                MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void CaptureHotkey()
        {
            using (HotkeyForm f = new HotkeyForm(_hotkeyBox.Text))
            {
                if (f.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(f.Result))
                {
                    _hotkeyBox.Text = f.Result;
                }
            }
        }

        private void Apply()
        {
            _cfg.Hotkey = string.IsNullOrWhiteSpace(_hotkeyBox.Text) ? _cfg.Hotkey : _hotkeyBox.Text;
            _cfg.LockBeforeOff = _lockRadio.Checked;

            int[] delays = new int[] { 0, 5000, 10000, 30000, 60000 };
            _cfg.DelayMs = delays[Math.Max(0, _delayBox.SelectedIndex)];

            _cfg.DdcFallback = _ddcBox.Checked;
            _cfg.Save();

            try { StartupHelper.Set(_startupBox.Checked); }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启失败：" + ex.Message, "一键息屏",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // 开机自启（写当前用户的 Run 键，无需管理员）
    internal static class StartupHelper
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "ScreenOffTray";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    return k != null && k.GetValue(RunName) != null;
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;

                if (on)
                {
                    string exe = Application.ExecutablePath;
                    k.SetValue(RunName, "\"" + exe + "\" --tray");
                }
                else
                {
                    k.DeleteValue(RunName, false);
                }
            }
        }
    }
}
