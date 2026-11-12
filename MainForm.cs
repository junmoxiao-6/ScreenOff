// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 junmoxiao-6

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenOffTool
{
    // ─────────────────────── 月亮按钮 ───────────────────────
    internal sealed class MoonButton : Control
    {
        private float _glow;
        private bool _hover;
        private bool _pressed;
        private readonly Timer _anim;

        public MoonButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            _anim = new Timer();
            _anim.Interval = 16;
            _anim.Tick += delegate { Step(); };
        }

        private void Step()
        {
            float target = (_hover || _pressed) ? 1f : 0f;
            float d = target - _glow;
            if (Math.Abs(d) < 0.02f)
            {
                _glow = target;
                _anim.Stop();
            }
            else
            {
                _glow += d * 0.28f;
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            _anim.Start();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            _anim.Start();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            _anim.Start();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            _anim.Start();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Parent != null ? Parent.BackColor : Color.FromArgb(11, 16, 32));

            // 只画一个月牙，不要底盘、不要描边圆
            int size = Math.Min(Width, Height);
            float cx = Width / 2f;
            float cy = Height / 2f;

            float r = size * 0.33f;
            if (_pressed) r *= 0.93f;
            else if (_hover) r *= 1.04f;

            // 月牙天然偏左，整体右移一点让视觉重心落在中心
            float mx = cx + r * 0.18f;

            float cutR = r * 1.25f;
            float cutCx = mx + r * 0.52f;
            float cutCy = cy - r * 0.22f;

            using (GraphicsPath outer = new GraphicsPath())
            {
                outer.AddEllipse(mx - r, cy - r, r * 2f, r * 2f);

                using (GraphicsPath cut = new GraphicsPath())
                {
                    cut.AddEllipse(cutCx - cutR, cutCy - cutR, cutR * 2f, cutR * 2f);

                    using (Region reg = new Region(outer))
                    {
                        reg.Exclude(cut);

                        // 悬停/按下时略微提亮
                        int b = (int)(232 + 23 * _glow);
                        using (LinearGradientBrush lb = new LinearGradientBrush(
                            new RectangleF(mx - r, cy - r, r * 2f, r * 2f),
                            Color.FromArgb(255, 255, 255),
                            Color.FromArgb(b, Math.Min(255, b + 6), 255),
                            55f))
                        {
                            g.FillRegion(lb, reg);
                        }
                    }
                }
            }
        }
    }

    // ─────────────────── 设置行（点击循环切换） ───────────────────
    internal sealed class PillRow : Control
    {
        private readonly string _title;
        private readonly string[] _options;
        private int _index;
        private bool _hover;

        public event EventHandler IndexChanged;

        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Index
        {
            get { return _index; }
            set
            {
                if (value < 0 || value >= _options.Length || value == _index) return;
                _index = value;
                Invalidate();
            }
        }

        public PillRow(string title, string[] options, int index)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _title = title;
            _options = options;
            _index = Math.Max(0, Math.Min(index, options.Length - 1));
            Font = new Font("Microsoft YaHei UI", 9.5f);
            Cursor = Cursors.Hand;
            Height = 36;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            _index = (_index + 1) % _options.Length;
            Invalidate();
            EventHandler h = IndexChanged;
            if (h != null) h(this, EventArgs.Empty);
            base.OnClick(e);
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Round(r, 9))
            {
                using (SolidBrush b = new SolidBrush(_hover
                    ? Color.FromArgb(34, 45, 70) : Color.FromArgb(21, 28, 47)))
                {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(Color.FromArgb(_hover ? 80 : 42, 90, 130), 1f))
                {
                    g.DrawPath(p, path);
                }
            }

            int ty = (Height - Font.Height) / 2;
            TextRenderer.DrawText(g, _title, Font, new Point(14, ty),
                Color.FromArgb(148, 160, 186), TextFormatFlags.NoPadding);

            string val = _options[_index];
            Size vs = TextRenderer.MeasureText(g, val, Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, val, Font, new Point(Width - 26 - vs.Width, ty),
                Color.FromArgb(238, 243, 252), TextFormatFlags.NoPadding);

            // 右侧小箭头
            using (Pen pen = new Pen(Color.FromArgb(110, 130, 170), 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float ax = Width - 16f;
                float ay = Height / 2f;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(ax - 3f, ay - 4f),
                    new PointF(ax + 1f, ay),
                    new PointF(ax - 3f, ay + 4f)
                });
            }
        }
    }

    // ─────────────────────── 主面板 ───────────────────────
    internal sealed class MainForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly AppConfig _cfg;
        private readonly Action _turnOff;
        private readonly Action _changed;

        private readonly MoonButton _moon;
        private readonly PillRow _mode;
        private readonly PillRow _delay;
        private readonly PillRow _startup;
        private readonly PillRow _ddc;
        private readonly Label _hotkeyChip;

        private static readonly int[] Delays = new int[] { 0, 5000, 10000, 30000, 60000 };

        public Func<string> HotkeyProvider;

        public MainForm(AppConfig cfg, Action turnOff, Action changed)
        {
            _cfg = cfg;
            _turnOff = turnOff;
            _changed = changed;

            Text = "ScreenOff";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 596);
            BackColor = Color.FromArgb(11, 16, 32);
            ShowInTaskbar = true;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = new Font("Microsoft YaHei UI", 9.5f);

            // 顶部标题
            Label title = MakeLabel("一键息屏", 20, 16, 11f, FontStyle.Bold, Color.FromArgb(240, 245, 255));
            Controls.Add(title);

            Label sub = MakeLabel("ScreenOff", 20, 40, 9f, FontStyle.Regular, Color.FromArgb(110, 125, 155));
            Controls.Add(sub);

            Button close = new Button();
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Text = "✕";
            close.Font = new Font("Segoe UI", 10f);
            close.ForeColor = Color.FromArgb(150, 165, 195);
            close.BackColor = Color.Transparent;
            close.Cursor = Cursors.Hand;
            close.SetBounds(356, 14, 28, 26);
            close.Click += delegate { Hide(); };
            Controls.Add(close);

            // 月亮按钮
            _moon = new MoonButton();
            _moon.SetBounds(100, 66, 200, 200);
            _moon.Click += delegate { DoTurnOff(); };
            Controls.Add(_moon);

            // 提示
            Label hint = MakeLabel("点击月亮，立即熄屏", 0, 276, 9.5f, FontStyle.Regular, Color.FromArgb(150, 163, 190));
            hint.AutoSize = false;
            hint.SetBounds(0, 276, ClientSize.Width, 22);
            hint.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(hint);

            // 快捷键 chip
            _hotkeyChip = MakeLabel("Ctrl + Alt + L", 0, 306, 9f, FontStyle.Regular, Color.FromArgb(120, 175, 255));
            _hotkeyChip.AutoSize = false;
            _hotkeyChip.SetBounds(0, 306, ClientSize.Width, 24);
            _hotkeyChip.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_hotkeyChip);

            // 设置区
            int y = 356;
            _mode = new PillRow("熄屏方式", new string[] { "锁屏并熄屏", "只熄屏" },
                                cfg.LockBeforeOff ? 0 : 1);
            _mode.SetBounds(20, y, ClientSize.Width - 40, 36);
            _mode.IndexChanged += delegate
            {
                _cfg.LockBeforeOff = _mode.Index == 0;
                _cfg.Save();
                if (_changed != null) _changed();
            };
            Controls.Add(_mode);
            y += 44;

            int di = Array.IndexOf(Delays, cfg.DelayMs);
            _delay = new PillRow("延时", new string[] { "立即", "5 秒", "10 秒", "30 秒", "60 秒" },
                                 di < 0 ? 0 : di);
            _delay.SetBounds(20, y, ClientSize.Width - 40, 36);
            _delay.IndexChanged += delegate
            {
                _cfg.DelayMs = Delays[_delay.Index];
                _cfg.Save();
                if (_changed != null) _changed();
            };
            Controls.Add(_delay);
            y += 44;

            _startup = new PillRow("开机自动启动", new string[] { "关", "开" },
                                   StartupHelper.IsEnabled() ? 1 : 0);
            _startup.SetBounds(20, y, ClientSize.Width - 40, 36);
            _startup.IndexChanged += delegate
            {
                try
                {
                    StartupHelper.Set(_startup.Index == 1);
                }
                catch (Exception ex)
                {
                    Log.Write("设置开机自启失败：" + ex.Message);
                    _startup.Index = StartupHelper.IsEnabled() ? 1 : 0;
                }
            };
            Controls.Add(_startup);
            y += 44;

            _ddc = new PillRow("显示器硬断电 (DDC/CI)", new string[] { "关", "开" },
                               cfg.DdcFallback ? 1 : 0);
            _ddc.SetBounds(20, y, ClientSize.Width - 40 - 66, 36);
            _ddc.IndexChanged += delegate
            {
                _cfg.DdcFallback = _ddc.Index == 1;
                _cfg.Save();
                if (_changed != null) _changed();
            };
            Controls.Add(_ddc);

            Button probe = new Button();
            probe.FlatStyle = FlatStyle.Flat;
            probe.FlatAppearance.BorderColor = Color.FromArgb(60, 90, 140);
            probe.FlatAppearance.BorderSize = 1;
            probe.Text = "检测";
            probe.ForeColor = Color.FromArgb(150, 190, 240);
            probe.BackColor = Color.FromArgb(21, 28, 47);
            probe.Font = new Font("Microsoft YaHei UI", 8.5f);
            probe.Cursor = Cursors.Hand;
            probe.SetBounds(ClientSize.Width - 20 - 58, y + 4, 58, 28);
            probe.Click += delegate { CheckDdc(); };
            Controls.Add(probe);

            // 底部
            LinkLabel more = new LinkLabel();
            more.Text = "更多设置";
            more.LinkColor = Color.FromArgb(120, 175, 255);
            more.ActiveLinkColor = Color.White;
            more.LinkBehavior = LinkBehavior.HoverUnderline;
            more.SetBounds(20, 552, 80, 22);
            more.Click += delegate { OpenSettings(); };
            Controls.Add(more);

            LinkLabel folder = new LinkLabel();
            folder.Text = "打开数据目录";
            folder.LinkColor = Color.FromArgb(120, 140, 175);
            folder.ActiveLinkColor = Color.White;
            folder.LinkBehavior = LinkBehavior.HoverUnderline;
            folder.SetBounds(ClientSize.Width - 120, 552, 100, 22);
            folder.TextAlign = ContentAlignment.MiddleRight;
            folder.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "\"" + AppPaths.Dir + "\""); }
                catch { }
            };
            Controls.Add(folder);

            // 拖动 + 关闭
            MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            sub.MouseDown += DragWindow;
            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Hide();
            };
        }

        private static Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Microsoft YaHei UI", size, style);
            l.ForeColor = color;
            l.Location = new Point(x, y);
            l.BackColor = Color.Transparent;
            return l;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // 圆角遮罩
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), 14))
            {
                if (Region == null) Region = new Region(path);
            }

            // 顶部分隔线
            using (Pen p = new Pen(Color.FromArgb(34, 44, 66), 1f))
            {
                g.DrawLine(p, 20, 340, Width - 20, 340);
                g.DrawLine(p, 20, 534, Width - 20, 534);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void RefreshHotkey()
        {
            if (HotkeyProvider != null) _hotkeyChip.Text = HotkeyProvider();
        }

        public void RefreshSettings()
        {
            _mode.Index = _cfg.LockBeforeOff ? 0 : 1;
            int di = Array.IndexOf(Delays, _cfg.DelayMs);
            _delay.Index = di < 0 ? 0 : di;
            _startup.Index = StartupHelper.IsEnabled() ? 1 : 0;
            _ddc.Index = _cfg.DdcFallback ? 1 : 0;
            RefreshHotkey();
        }

        private void DoTurnOff()
        {
            Log.Write("面板按钮：熄屏（" + (_cfg.LockBeforeOff ? "锁屏" : "不锁屏") + "）");
            Hide();
            Action act = _turnOff;
            if (act != null) System.Threading.Tasks.Task.Run(act);
        }

        private void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(_cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    AppConfig re = AppConfig.Load();
                    _cfg.Hotkey = re.Hotkey;
                    _cfg.LockBeforeOff = re.LockBeforeOff;
                    _cfg.DelayMs = re.DelayMs;
                    _cfg.DdcFallback = re.DdcFallback;
                    if (_changed != null) _changed();
                    RefreshSettings();
                }
            }
        }

        private void CheckDdc()
        {
            string detail;
            bool ok = DdcCheck.Supported(out detail);
            MessageBox.Show(this,
                detail + "\n\n" + (ok
                    ? "结论：本机显示器支持硬断电。\n注意：开启后键鼠可能唤不醒，需要按显示器电源键。"
                    : "结论：本机显示器不支持硬断电，开了也不会有任何效果，保持关闭即可。"),
                "显示器 DDC/CI 检测", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 关窗口 = 收进托盘，不退出程序
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshSettings();
            Activate();
        }
    }
}
