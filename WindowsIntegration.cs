using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NightDim
{
    // Current-user Windows integration shared by the app and the installer.
    internal static class WindowsIntegration
    {
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NightDim";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TheITDaily.NightDim";

        private static string ShortcutPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "NightDim.lnk");
            }
        }

        // A per-user uninstall entry is intentionally written in the original
        // unelevated setup process, never in the UAC copy helper.
        internal static string GetInstalledLocation()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                return key == null ? null : key.GetValue("InstallLocation") as string;
        }

        internal static bool IsRegisteredLocation(string folder)
        {
            string registered = GetInstalledLocation();
            if (string.IsNullOrEmpty(registered)) return false;
            try { return string.Equals(Path.GetFullPath(registered).TrimEnd('\\'),
                Path.GetFullPath(folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        internal static void RegisterUninstall(string exe, string uninstaller)
        {
            if (!File.Exists(exe) || !File.Exists(uninstaller))
                throw new FileNotFoundException("The installed application or uninstaller is missing.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                if (key == null) throw new IOException("Cannot register the Windows uninstaller.");
                key.SetValue("DisplayName", "NightDim", RegistryValueKind.String);
                key.SetValue("Publisher", "TheITDaily", RegistryValueKind.String);
                key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(exe).FileVersion,
                    RegistryValueKind.String);
                key.SetValue("InstallLocation", Path.GetDirectoryName(exe), RegistryValueKind.String);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall", RegistryValueKind.String);
                key.SetValue("DisplayIcon", "\"" + exe + "\",0", RegistryValueKind.String);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        internal static void UnregisterUninstall()
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        }

        internal static bool IsStartupEnabled(string exe)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey))
            {
                string command = key == null ? null : key.GetValue(AppName) as string;
                string quoted = "\"" + Path.GetFullPath(exe) + "\"";
                return string.Equals(command, quoted + " --tray", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(command, quoted, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void SetStartup(string exe, bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupKey))
            {
                if (key == null) throw new InvalidOperationException("Cannot open the Windows startup registry key.");
                if (enabled) key.SetValue(AppName, "\"" + Path.GetFullPath(exe) + "\" --tray", RegistryValueKind.String);
                else key.DeleteValue(AppName, false);
            }
        }

        internal static bool IsStartMenuEnabled(string exe)
        {
            if (!File.Exists(ShortcutPath)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(GetShortcutTarget(ShortcutPath)),
                    Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        internal static void SetStartMenu(string exe, bool enabled)
        {
            string link = ShortcutPath;
            if (!enabled)
            {
                if (File.Exists(link)) File.Delete(link);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            WithShortcut(link, delegate(object shortcut)
            {
                SetProperty(shortcut, "TargetPath", Path.GetFullPath(exe));
                SetProperty(shortcut, "WorkingDirectory", Path.GetDirectoryName(Path.GetFullPath(exe)));
                SetProperty(shortcut, "IconLocation", Path.GetFullPath(exe) + ",0");
                Invoke(shortcut, "Save");
            });
        }

        private static string GetShortcutTarget(string link)
        {
            string target = "";
            WithShortcut(link, delegate(object shortcut)
            {
                target = (string)shortcut.GetType().InvokeMember("TargetPath",
                    BindingFlags.GetProperty, null, shortcut, null);
            });
            return target;
        }

        private static void SetProperty(object com, string name, object value)
        {
            com.GetType().InvokeMember(name, BindingFlags.SetProperty, null, com, new[] { value });
        }

        private static void Invoke(object com, string method)
        {
            com.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, com, null);
        }

        private static void WithShortcut(string link, Action<object> action)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new NotSupportedException("Windows Script Host is unavailable.");
            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { link });
                action(shortcut);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
