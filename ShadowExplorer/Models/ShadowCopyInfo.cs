namespace ShadowExplorer.Models;

public class ShadowCopyInfo
{
    public string Id { get; set; } = "";
    public string DeviceObject { get; set; } = "";
    public DateTime CreationTime { get; set; }
}

public class ShadowFileVersion
{
    public string OriginalPath { get; set; } = "";
    public string ShadowPath { get; set; } = "";
    public string ShadowCopyId { get; set; } = "";
    public DateTime SnapshotDate { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsCurrentVersion { get; set; }
    public bool IsDeleted { get; set; }

    public string SizeDisplay => FormatSize(Size);
    public string DateDisplay => SnapshotDate.ToString("yyyy-MM-dd HH:mm:ss");
    public string ModifiedDisplay => LastModified.ToString("yyyy-MM-dd HH:mm:ss");

    public string TimestampSuffix => SnapshotDate.ToString("yyyy-MM-dd_HHmmss");

    static string FormatSize(long bytes)
    {
        if (bytes < 0) return "?";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

public class FolderFileEntry
{
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool ExistsCurrently { get; set; }
    public bool IsFolder { get; set; }
    public int VersionCount { get; set; }
    public DateTime? LatestModified { get; set; }
    public long? CurrentSize { get; set; }
    public string SizeDisplay => IsFolder ? "" : (CurrentSize.HasValue ? FormatSize(CurrentSize.Value) : "Deleted");
    public string StatusDisplay => IsFolder ? "Folder" : (ExistsCurrently ? "Current" : "Deleted");

    static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
