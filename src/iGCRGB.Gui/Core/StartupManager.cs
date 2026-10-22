using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace iGCRGB.Core
{
    /// <summary>
    /// 开机自启管理：优先写当前用户注册表 Run 项；若被安全软件/策略拦截，
    /// 退回到「启动」文件夹的快捷方式，再不行就写一个隐藏启动的 VBS。
    /// </summary>
    public static class StartupManager
    {
        private const string RunValueName = "iGCRGB";
        private const string LnkName = "iGCRGB.lnk";
        private const string VbsName = "iGCRGB.vbs";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private const int KEY_QUERY_VALUE = 0x0001;
        private const int KEY_SET_VALUE = 0x0002;
        private const int REG_SZ = 1;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, uint options, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegSetValueEx(IntPtr hKey, string name, uint reserved, uint type, byte[] data, uint size);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string name, IntPtr reserved, out uint type, byte[] data, ref uint size);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegDeleteValue(IntPtr hKey, string name);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        public static string ExePath
        {
            get { return Assembly.GetExecutingAssembly().Location; }
        }

        public static bool SetEnabled(bool enabled, out string message)
        {
            message = null;
            if (enabled)
            {
                string exe = ExePath;
                if (TryRegistryWrite(exe))
                {
                    message = "已加入开机启动（注册表 Run 项）";
                    CleanupOtherForms(false);
                    return true;
                }
                if (TryCreateShortcut(exe))
                {
                    message = "已加入开机启动（启动文件夹快捷方式）";
                    return true;
                }
                if (TryCreateVbs(exe))
                {
                    message = "已加入开机启动（启动文件夹 VBS 脚本）";
                    return true;
                }
                message = "开机自启设置失败：注册表与启动文件夹均不可写";
                return false;
            }

            bool removed = false;
            try
            {
                if (GetRunValue() != null)
                {
                    removed = DeleteRunValue() || removed;
                }
            }
            catch
            {
            }
            removed = DeleteStartupFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LnkName)) || removed;
            removed = DeleteStartupFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), VbsName)) || removed;
            message = removed ? "已取消开机启动" : "没有找到开机启动项";
            return true;
        }

        public static bool IsEnabled()
        {
            try
            {
                if (!string.IsNullOrEmpty(GetRunValue())) return true;
            }
            catch
            {
            }
            try
            {
                string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                return File.Exists(Path.Combine(startup, LnkName)) || File.Exists(Path.Combine(startup, VbsName));
            }
            catch
            {
                return false;
            }
        }

        // ---------------- 注册表 ----------------

        private static bool TryRegistryWrite(string exePath)
        {
            try
            {
                IntPtr key;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, RunKeyPath, 0, KEY_SET_VALUE, out key);
                if (result != 0) return false;
                try
                {
                    byte[] data = Encoding.Unicode.GetBytes("\"" + exePath + "\" --tray" + "\0");
                    result = RegSetValueEx(key, RunValueName, 0, REG_SZ, data, (uint)data.Length);
                    return result == 0;
                }
                finally
                {
                    RegCloseKey(key);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetRunValue()
        {
            try
            {
                IntPtr key;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, RunKeyPath, 0, KEY_QUERY_VALUE, out key);
                if (result != 0) return null;
                try
                {
                    uint type;
                    uint size = 0;
                    result = RegQueryValueEx(key, RunValueName, IntPtr.Zero, out type, null, ref size);
                    if (result != 0 || size == 0) return null;
                    byte[] buffer = new byte[size];
                    result = RegQueryValueEx(key, RunValueName, IntPtr.Zero, out type, buffer, ref size);
                    if (result != 0) return null;
                    string value = Encoding.Unicode.GetString(buffer, 0, (int)size);
                    int end = value.IndexOf('\0');
                    return end >= 0 ? value.Substring(0, end) : value;
                }
                finally
                {
                    RegCloseKey(key);
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool DeleteRunValue()
        {
            try
            {
                IntPtr key;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, RunKeyPath, 0, KEY_SET_VALUE, out key);
                if (result != 0) return false;
                try
                {
                    return RegDeleteValue(key, RunValueName) == 0;
                }
                finally
                {
                    RegCloseKey(key);
                }
            }
            catch
            {
                return false;
            }
        }

        // ---------------- 启动文件夹 ----------------

        private static bool TryCreateShortcut(string exePath)
        {
            try
            {
                string lnkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LnkName);
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (shortcut == null) return false;
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "--tray" });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exePath) });
                shortcutType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 7 });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "iGCRGB 主板灯光控制" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateVbs(string exePath)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), VbsName);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("' iGCRGB 开机自启脚本");
                sb.AppendLine("Set sh = CreateObject(\"WScript.Shell\")");
                sb.AppendLine("sh.Run \"\"\"" + exePath + "\"\" --tray\", 0, False");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void CleanupOtherForms(bool removeRegistry)
        {
            try
            {
                string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                DeleteStartupFile(Path.Combine(startup, LnkName));
                DeleteStartupFile(Path.Combine(startup, VbsName));
            }
            catch
            {
            }
            if (removeRegistry) DeleteRunValue();
        }

        private static bool DeleteStartupFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
