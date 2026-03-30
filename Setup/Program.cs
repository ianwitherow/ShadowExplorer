using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

partial class Program
{
    const string AppName = "Shadow Explorer";
    const string InstallDir = @"C:\Program Files\ShadowExplorer";

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);

    static void Msg(string text, string title, uint type = 0x40) => MessageBoxW(0, text, title, type);

    // Escape a path for .reg file string values (backslash -> double backslash)
    static string RegPath(string p) => p.Replace("\\", "\\\\");

    // Build a .reg command value: "\"<exe>\" \"<arg>\""
    static string RegCmd(string exePath, string arg)
    {
        var e = RegPath(exePath);
        return "\"\\\"" + e + "\\\" \\\"" + arg + "\\\"\"";
    }

    static int Main()
    {
        var myDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var sourceExe = Path.Combine(myDir, "ShadowExplorer.exe");
        var sourceUninstall = Path.Combine(myDir, "uninstall.exe");

        if (!File.Exists(sourceExe) || !File.Exists(sourceUninstall))
        {
            Msg("ShadowExplorer.exe and uninstall.exe must be\n" +
                "in the same folder as setup.exe.", AppName + " - Setup", 0x10);
            return 1;
        }

        // MB_YESNO=4 | MB_ICONQUESTION=0x20
        if (MessageBoxW(0,
            "Install " + AppName + "?\n\n" +
            "This will:\n" +
            "  - Copy files to " + InstallDir + "\n" +
            "  - Add \"View Shadow Copies\" to the right-click menu\n" +
            "  - Add an entry in Add/Remove Programs",
            AppName + " - Setup", 0x24) != 6)
            return 0;

        try
        {
            Directory.CreateDirectory(InstallDir);
            File.Copy(sourceExe, Path.Combine(InstallDir, "ShadowExplorer.exe"), true);
            File.Copy(sourceUninstall, Path.Combine(InstallDir, "uninstall.exe"), true);

            var exeReg = RegPath(Path.Combine(InstallDir, "ShadowExplorer.exe"));
            var uninstReg = RegPath(Path.Combine(InstallDir, "uninstall.exe"));
            var cmd1 = RegCmd(Path.Combine(InstallDir, "ShadowExplorer.exe"), "%1");
            var cmdV = RegCmd(Path.Combine(InstallDir, "ShadowExplorer.exe"), "%V");

            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();

            void AddShellKey(string keyPath, string cmdVal)
            {
                sb.AppendLine("[" + keyPath + "]");
                sb.AppendLine("@=\"View Shadow Copies\"");
                sb.AppendLine("\"Icon\"=\"" + exeReg + "\"");
                sb.AppendLine();
                sb.AppendLine("[" + keyPath + "\\command]");
                sb.AppendLine("@=" + cmdVal);
                sb.AppendLine();
            }

            AddShellKey(@"HKEY_CLASSES_ROOT\*\shell\ViewShadowCopies", cmd1);
            AddShellKey(@"HKEY_CLASSES_ROOT\Directory\shell\ViewShadowCopies", cmd1);
            AddShellKey(@"HKEY_CLASSES_ROOT\Directory\Background\shell\ViewShadowCopies", cmdV);

            // Add/Remove Programs
            sb.AppendLine(@"[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ShadowExplorer]");
            sb.AppendLine("\"DisplayName\"=\"" + AppName + "\"");
            sb.AppendLine("\"DisplayVersion\"=\"1.0.0\"");
            sb.AppendLine("\"Publisher\"=\"" + AppName + "\"");
            sb.AppendLine("\"DisplayIcon\"=\"" + exeReg + "\"");
            sb.AppendLine("\"UninstallString\"=\"\\\"" + uninstReg + "\\\"\"");
            sb.AppendLine("\"InstallLocation\"=\"" + RegPath(InstallDir) + "\"");
            sb.AppendLine("\"NoModify\"=dword:00000001");
            sb.AppendLine("\"NoRepair\"=dword:00000001");
            sb.AppendLine();

            var regFile = Path.Combine(Path.GetTempPath(), "shadow_setup.reg");
            File.WriteAllText(regFile, sb.ToString(), Encoding.Unicode);

            var proc = Process.Start(new ProcessStartInfo("reg", "import \"" + regFile + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true
            });
            proc?.WaitForExit();
            File.Delete(regFile);

            Msg(AppName + " has been installed!\n\n" +
                "Right-click any file or folder in Explorer and select\n" +
                "\"View Shadow Copies\".\n\n" +
                "(On Windows 11, click \"Show more options\" first.)",
                AppName + " - Setup");
            return 0;
        }
        catch (Exception ex)
        {
            Msg("Installation failed:\n\n" + ex.Message, AppName + " - Setup", 0x10);
            return 1;
        }
    }
}
