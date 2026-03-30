using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

partial class Program
{
    const string AppName = "Shadow Explorer";

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);

    static void Msg(string text, string title, uint type = 0x40) => MessageBoxW(0, text, title, type);

    static int Main()
    {
        // MB_YESNO=4 | MB_ICONQUESTION=0x20
        if (MessageBoxW(0,
            $"Uninstall {AppName}?\n\n" +
            "This will remove the context menu entries\n" +
            "and delete the application.",
            $"{AppName} - Uninstall", 0x24) != 6)
            return 0;

        try
        {
            // Remove registry entries
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            sb.AppendLine(@"[-HKEY_CLASSES_ROOT\*\shell\ViewShadowCopies]");
            sb.AppendLine(@"[-HKEY_CLASSES_ROOT\Directory\shell\ViewShadowCopies]");
            sb.AppendLine(@"[-HKEY_CLASSES_ROOT\Directory\Background\shell\ViewShadowCopies]");
            sb.AppendLine(@"[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ShadowExplorer]");
            sb.AppendLine();

            var regFile = Path.Combine(Path.GetTempPath(), "shadow_explorer_uninstall.reg");
            File.WriteAllText(regFile, sb.ToString(), Encoding.Unicode);
            var proc = Process.Start(new ProcessStartInfo("reg", $"import \"{regFile}\"")
            {
                UseShellExecute = false, CreateNoWindow = true
            });
            proc?.WaitForExit();
            File.Delete(regFile);

            // Clean up temp files
            var tempDir = Path.Combine(Path.GetTempPath(), "ShadowExplorer");
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { }

            Msg($"{AppName} has been uninstalled.\n\n" +
                "Context menu entries have been removed.",
                $"{AppName} - Uninstall");

            // Schedule self-deletion of install folder
            var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            Process.Start(new ProcessStartInfo("cmd.exe",
                $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{installDir}\"")
            {
                UseShellExecute = false, CreateNoWindow = true
            });

            return 0;
        }
        catch (Exception ex)
        {
            Msg($"Uninstall failed:\n\n{ex.Message}", $"{AppName} - Uninstall", 0x10);
            return 1;
        }
    }
}
