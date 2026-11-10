// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 junmoxiao-6

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScreenOffTool
{
    // 用系统标准的 IShellLink 创建 .lnk（资源管理器双击即可用，带箭头和图标）
    internal static class ShellShortcut
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        /// <summary>在桌面创建指向当前 exe 的快捷方式，返回 .lnk 的完整路径。</summary>
        public static string CreateOnDesktop()
        {
            string exe = Application.ExecutablePath;
            string dir = Path.GetDirectoryName(exe);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string lnk = Path.Combine(desktop, "一键息屏.lnk");

            IShellLinkW link = (IShellLinkW)new ShellLinkCoClass();
            link.SetPath(exe);
            link.SetWorkingDirectory(dir);
            link.SetDescription("先锁屏再熄屏，后台任务照常运行");
            link.SetIconLocation(exe, 0);
            link.SetShowCmd(1);

            ((IPersistFile)link).Save(lnk, true);
            Marshal.FinalReleaseComObject(link);

            return lnk;
        }

        /// <summary>回读校验：把刚生成的 .lnk 读回来，确认目标路径一致。</summary>
        public static bool Verify(string lnkPath, out string resolved)
        {
            resolved = "";
            try
            {
                IShellLinkW link = (IShellLinkW)new ShellLinkCoClass();
                ((IPersistFile)link).Load(lnkPath, 0);

                StringBuilder sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                resolved = sb.ToString();

                Marshal.FinalReleaseComObject(link);
                return string.Equals(
                    Path.GetFullPath(resolved),
                    Path.GetFullPath(Application.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                resolved = "回读失败：" + ex.Message;
                return false;
            }
        }
    }
}
