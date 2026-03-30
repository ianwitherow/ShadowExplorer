using System.IO;
using System.Windows;

namespace ShadowExplorer;

public partial class App : System.Windows.Application
{
    public static string? TargetPath { get; private set; }
    public static bool IsFolder { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0)
        {
            TargetPath = string.Join(" ", e.Args);
            IsFolder = Directory.Exists(TargetPath);
        }
    }
}
