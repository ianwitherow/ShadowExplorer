using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ShadowExplorer.Models;
using ShadowExplorer.Services;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragDrop = System.Windows.DragDrop;
using Clipboard = System.Windows.Clipboard;

namespace ShadowExplorer;

public partial class MainWindow : Window
{
    private ShadowCopyService _service = null!;
    private List<ShadowFileVersion> _versions = new();
    private List<FolderFileEntry> _folderEntries = new();
    private string _targetPath = "";

    private bool _isFolder;
    private Point _dragStartPoint;

    private static readonly HashSet<string> PreviewableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".csv", ".log", ".ini", ".cfg", ".conf",
        ".yaml", ".yml", ".toml", ".md", ".html", ".htm", ".css", ".js",
        ".ts", ".cs", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
        ".sh", ".bat", ".cmd", ".ps1", ".sql", ".gitignore", ".env",
        ".config", ".csproj", ".sln", ".props", ".targets", ".xaml",
        ".jsx", ".tsx", ".vue", ".svelte", ".rs", ".go", ".rb", ".php"
    };

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _service = new ShadowCopyService();
        _targetPath = App.TargetPath ?? "";
        _isFolder = App.IsFolder;

        if (string.IsNullOrEmpty(_targetPath))
        {
            // No path provided - show an open file/folder dialog
            LoadingOverlay.Visibility = Visibility.Collapsed;
            var result = MessageBox.Show(
                "Browse for a file or folder?\n\nYes = File\nNo = Folder\nCancel = Exit",
                "Shadow Explorer", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var ofd = new OpenFileDialog { Title = "Select a file to view shadow copies" };
                if (ofd.ShowDialog() == true)
                {
                    _targetPath = ofd.FileName;
                    _isFolder = false;
                }
                else { Close(); return; }
            }
            else if (result == MessageBoxResult.No)
            {
                var folder = FolderPicker.ShowDialog("Select a folder to view shadow copy history");
                if (folder != null)
                {
                    _targetPath = folder;
                    _isFolder = true;
                }
                else { Close(); return; }
            }
            else { Close(); return; }

            LoadingOverlay.Visibility = Visibility.Visible;
        }

        PathDisplay.Text = _targetPath;
        Title = $"Shadow Explorer - {Path.GetFileName(_targetPath)}";

        if (_isFolder)
        {
            ModeDisplay.Text = "[Folder History]";
            await LoadFolderHistoryAsync();
        }
        else
        {
            ModeDisplay.Text = "[File Versions]";
            await LoadFileVersionsAsync();
        }
    }

    private async Task LoadFileVersionsAsync()
    {
        StatusText.Text = "Scanning shadow copies...";
        VersionsList.Visibility = Visibility.Visible;
        FolderList.Visibility = Visibility.Collapsed;

        try
        {
            _versions = await Task.Run(() => _service.GetFileVersions(_targetPath));
            VersionsList.ItemsSource = _versions;

            StatusText.Text = $"Found {_versions.Count} version(s) across {_versions.Count(v => !v.IsCurrentVersion)} shadow copies";

            if (_versions.Count == 0)
                StatusText.Text = "No versions found in any shadow copy.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to scan shadow copies:\n\n{ex.Message}\n\nMake sure the application is running as Administrator.",
                "Shadow Explorer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadFolderHistoryAsync()
    {
        StatusText.Text = "Scanning folder history across shadow copies...";
        VersionsList.Visibility = Visibility.Collapsed;
        FolderList.Visibility = Visibility.Visible;
        PreviewText.Text = "Double-click a file to view its version history";

        try
        {
            _folderEntries = await Task.Run(() => _service.GetFolderHistory(_targetPath));
            FolderList.ItemsSource = _folderEntries;

            var deleted = _folderEntries.Count(f => !f.ExistsCurrently);
            StatusText.Text = $"Found {_folderEntries.Count} files ({deleted} deleted) across shadow copies";

            if (_folderEntries.Count == 0)
                StatusText.Text = "No files found in this folder or its shadow copies.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void VersionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionsList.SelectedItem is ShadowFileVersion version)
        {
            LoadPreview(version);
        }
    }

    private void LoadPreview(ShadowFileVersion version)
    {
        var ext = Path.GetExtension(version.OriginalPath);
        if (!PreviewableExtensions.Contains(ext))
        {
            PreviewText.Text = $"[Binary file - {version.SizeDisplay}]\n\nNo preview available for {ext} files.";
            return;
        }

        try
        {
            PreviewText.Text = ShadowCopyService.RobustReadText(version.ShadowPath);
        }
        catch (Exception ex)
        {
            PreviewText.Text = $"[Could not load preview: {ex.Message}]";
        }
    }

    private void VersionsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(VersionsList);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (VersionsList.SelectedItem is not ShadowFileVersion version) return;

        try
        {
            var tempFile = _service.CreateTempCopy(version);
            var data = new DataObject(DataFormats.FileDrop, new[] { tempFile });
            DragDrop.DoDragDrop(VersionsList, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Drag failed: {ex.Message}";
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _dragStartPoint = e.GetPosition(VersionsList);
    }

    private void CopyFile_Click(object sender, RoutedEventArgs e)
    {
        if (VersionsList.SelectedItem is not ShadowFileVersion version) return;

        try
        {
            var tempFile = _service.CreateTempCopy(version);
            var files = new StringCollection { tempFile };
            Clipboard.SetFileDropList(files);
            StatusText.Text = $"Copied to clipboard: {Path.GetFileName(tempFile)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Copy failed: {ex.Message}";
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (VersionsList.SelectedItem is not ShadowFileVersion version) return;

        try
        {
            var tempFile = _service.CreateTempCopy(version);
            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
            StatusText.Text = $"Opened: {Path.GetFileName(tempFile)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Open failed: {ex.Message}";
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (VersionsList.SelectedItem is not ShadowFileVersion version) return;

        var ext = Path.GetExtension(version.OriginalPath);
        var nameNoExt = Path.GetFileNameWithoutExtension(version.OriginalPath);
        var sfd = new SaveFileDialog
        {
            FileName = $"{nameNoExt}_{version.TimestampSuffix}{ext}",
            Filter = $"Original type (*{ext})|*{ext}|All files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(version.OriginalPath)
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(version.ShadowPath, sfd.FileName, true);
                StatusText.Text = $"Saved: {sfd.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RestoreFile_Click(object sender, RoutedEventArgs e)
    {
        if (VersionsList.SelectedItem is not ShadowFileVersion version) return;
        if (version.IsCurrentVersion) return;

        var result = MessageBox.Show(
            $"Restore file to the version from {version.DateDisplay}?\n\n" +
            $"A backup of the current file will be saved as:\n{version.OriginalPath}.bak",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _service.RestoreFile(version);
            StatusText.Text = "File restored! A .bak backup of the previous version was created.";
            // Reload versions
            _ = LoadFileVersionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void FolderList_OpenItem_Click(object sender, RoutedEventArgs e)
    {
        await DrillIntoSelectedItem();
    }

    private async void FolderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await DrillIntoSelectedItem();
    }

    private async Task DrillIntoSelectedItem()
    {
        if (FolderList.SelectedItem is not FolderFileEntry entry) return;

        if (entry.IsFolder)
        {
            // Navigate into the subfolder
            _targetPath = entry.FullPath;
            _isFolder = true;
    
    
            PathDisplay.Text = _targetPath;
            Title = $"Shadow Explorer - {entry.FileName}";
            ModeDisplay.Text = "[Folder History]";
            LoadingOverlay.Visibility = Visibility.Visible;
            await LoadFolderHistoryAsync();
        }
        else
        {
            // Remember the folder so we can go back
            // up arrow navigates back to the parent folder

            _targetPath = entry.FullPath;
            _isFolder = false;
            PathDisplay.Text = _targetPath;
            Title = $"Shadow Explorer - {entry.FileName}";
            ModeDisplay.Text = "[File Versions]";

            LoadingOverlay.Visibility = Visibility.Visible;
            await LoadFileVersionsAsync();
        }
    }

    private void PathDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        PathDisplay.Visibility = Visibility.Collapsed;
        PathEdit.Visibility = Visibility.Visible;
        PathEdit.Text = _targetPath;
        PathEdit.Focus();
        PathEdit.SelectAll();
    }

    private void PathEdit_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            CommitPathEdit();
        else if (e.Key == System.Windows.Input.Key.Escape)
            CancelPathEdit();
    }

    private void PathEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelPathEdit();
    }

    private async void CommitPathEdit()
    {
        var newPath = PathEdit.Text.Trim();
        PathEdit.Visibility = Visibility.Collapsed;
        PathDisplay.Visibility = Visibility.Visible;

        if (string.IsNullOrEmpty(newPath) || newPath == _targetPath) return;
        if (!File.Exists(newPath) && !Directory.Exists(newPath))
        {
            StatusText.Text = "Path not found: " + newPath;
            return;
        }

        _targetPath = newPath;
        _isFolder = Directory.Exists(newPath);


        PathDisplay.Text = _targetPath;
        Title = $"Shadow Explorer - {Path.GetFileName(_targetPath)}";
        LoadingOverlay.Visibility = Visibility.Visible;

        if (_isFolder)
        {
            ModeDisplay.Text = "[Folder History]";
            await LoadFolderHistoryAsync();
        }
        else
        {
            ModeDisplay.Text = "[File Versions]";
            await LoadFileVersionsAsync();
        }
    }

    private void CancelPathEdit()
    {
        PathEdit.Visibility = Visibility.Collapsed;
        PathDisplay.Visibility = Visibility.Visible;
    }

    private async void BtnUpLevel_Click(object sender, RoutedEventArgs e)
    {
        var parent = Path.GetDirectoryName(_targetPath);
        if (string.IsNullOrEmpty(parent)) return;

        _targetPath = parent;
        _isFolder = true;


        PathDisplay.Text = _targetPath;
        Title = $"Shadow Explorer - {Path.GetFileName(_targetPath)}";
        ModeDisplay.Text = "[Folder History]";
        LoadingOverlay.Visibility = Visibility.Visible;
        await LoadFolderHistoryAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _service?.Dispose();
        base.OnClosed(e);
    }
}
