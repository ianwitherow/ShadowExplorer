using System.IO;
using System.Management;
using ShadowExplorer.Models;

namespace ShadowExplorer.Services;

public class ShadowCopyService : IDisposable
{
    private readonly List<(string linkPath, string id)> _activeLinks = new();
    private readonly string _tempBase;
    private static int _linkCounter;

    public ShadowCopyService()
    {
        _tempBase = Path.Combine(Path.GetTempPath(), "ShadowExplorer");
        Directory.CreateDirectory(_tempBase);
    }

    public List<ShadowCopyInfo> GetShadowCopies()
    {
        var copies = new List<ShadowCopyInfo>();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ShadowCopy");
        foreach (ManagementObject obj in searcher.Get())
        {
            var installDate = ManagementDateTimeConverter.ToDateTime((string)obj["InstallDate"]);
            copies.Add(new ShadowCopyInfo
            {
                Id = (string)obj["ID"],
                DeviceObject = (string)obj["DeviceObject"],
                CreationTime = installDate
            });
        }
        return copies.OrderBy(c => c.CreationTime).ToList();
    }

    public string MountShadowCopy(ShadowCopyInfo shadow)
    {
        var existing = _activeLinks.FirstOrDefault(l => l.id == shadow.Id);
        if (existing.linkPath != null && Directory.Exists(existing.linkPath))
            return existing.linkPath;

        var linkPath = Path.Combine(_tempBase, $"vss_{Interlocked.Increment(ref _linkCounter)}");
        if (Directory.Exists(linkPath))
        {
            try { Directory.Delete(linkPath); } catch { }
        }

        var devicePath = shadow.DeviceObject.TrimEnd('\\') + "\\";
        var result = RunCmd($"mklink /d \"{linkPath}\" \"{devicePath}\"");
        if (!Directory.Exists(linkPath))
            throw new InvalidOperationException($"Failed to create symlink: {result}");

        _activeLinks.Add((linkPath, shadow.Id));
        return linkPath;
    }

    public List<ShadowFileVersion> GetFileVersions(string filePath)
    {
        var versions = new List<ShadowFileVersion>();
        var shadows = GetShadowCopies();

        // Determine drive-relative path (e.g. "Users\Dave\file.txt" from "C:\Users\Dave\file.txt")
        var root = Path.GetPathRoot(filePath) ?? "C:\\";
        var relativePath = filePath.Substring(root.Length);

        // Add current version if it exists
        if (File.Exists(filePath))
        {
            var fi = new FileInfo(filePath);
            versions.Add(new ShadowFileVersion
            {
                OriginalPath = filePath,
                ShadowPath = filePath,
                ShadowCopyId = "current",
                SnapshotDate = DateTime.Now,
                Size = fi.Length,
                LastModified = fi.LastWriteTime,
                IsCurrentVersion = true
            });
        }

        foreach (var shadow in shadows)
        {
            try
            {
                var mountPoint = MountShadowCopy(shadow);
                var shadowFilePath = Path.Combine(mountPoint, relativePath);
                if (File.Exists(shadowFilePath))
                {
                    var fi = new FileInfo(shadowFilePath);
                    versions.Add(new ShadowFileVersion
                    {
                        OriginalPath = filePath,
                        ShadowPath = shadowFilePath,
                        ShadowCopyId = shadow.Id,
                        SnapshotDate = shadow.CreationTime,
                        Size = fi.Length,
                        LastModified = fi.LastWriteTime,
                        IsCurrentVersion = false
                    });
                }
            }
            catch { /* Skip inaccessible shadow copies */ }
        }

        return versions.OrderByDescending(v => v.SnapshotDate).ToList();
    }

    public List<FolderFileEntry> GetFolderHistory(string folderPath)
    {
        var allFiles = new Dictionary<string, FolderFileEntry>(StringComparer.OrdinalIgnoreCase);
        var shadows = GetShadowCopies();
        var root = Path.GetPathRoot(folderPath) ?? "C:\\";
        var relativePath = folderPath.Substring(root.Length);

        // Scan current directory
        if (Directory.Exists(folderPath))
        {
            foreach (var dir in Directory.GetDirectories(folderPath))
            {
                var di = new DirectoryInfo(dir);
                allFiles[di.Name + "\\"] = new FolderFileEntry
                {
                    FileName = di.Name,
                    RelativePath = di.Name,
                    FullPath = dir,
                    ExistsCurrently = true,
                    IsFolder = true,
                    VersionCount = 0,
                    LatestModified = di.LastWriteTime
                };
            }

            foreach (var file in Directory.GetFiles(folderPath))
            {
                var fi = new FileInfo(file);
                var name = fi.Name;
                allFiles[name] = new FolderFileEntry
                {
                    FileName = name,
                    RelativePath = name,
                    FullPath = file,
                    ExistsCurrently = true,
                    VersionCount = 1,
                    LatestModified = fi.LastWriteTime,
                    CurrentSize = fi.Length
                };
            }
        }

        // Scan shadow copies
        foreach (var shadow in shadows)
        {
            try
            {
                var mountPoint = MountShadowCopy(shadow);
                var shadowDir = Path.Combine(mountPoint, relativePath);
                if (!Directory.Exists(shadowDir)) continue;

                foreach (var dir in Directory.GetDirectories(shadowDir))
                {
                    var name = Path.GetFileName(dir);
                    var key = name + "\\";
                    if (!allFiles.ContainsKey(key))
                    {
                        allFiles[key] = new FolderFileEntry
                        {
                            FileName = name,
                            RelativePath = name,
                            FullPath = Path.Combine(folderPath, name),
                            ExistsCurrently = false,
                            IsFolder = true,
                            VersionCount = 0,
                            LatestModified = Directory.GetLastWriteTime(dir)
                        };
                    }
                }

                foreach (var file in Directory.GetFiles(shadowDir))
                {
                    var name = Path.GetFileName(file);
                    if (allFiles.TryGetValue(name, out var entry))
                    {
                        entry.VersionCount++;
                        var mod = File.GetLastWriteTime(file);
                        if (!entry.LatestModified.HasValue || mod > entry.LatestModified)
                            entry.LatestModified = mod;
                    }
                    else
                    {
                        var fi = new FileInfo(file);
                        allFiles[name] = new FolderFileEntry
                        {
                            FileName = name,
                            RelativePath = name,
                            FullPath = Path.Combine(folderPath, name),
                            ExistsCurrently = false,
                            VersionCount = 1,
                            LatestModified = fi.LastWriteTime,
                            CurrentSize = null
                        };
                    }
                }
            }
            catch { }
        }

        return allFiles.Values
            .OrderByDescending(f => f.IsFolder)
            .ThenBy(f => f.ExistsCurrently ? 0 : 1)
            .ThenBy(f => f.FileName)
            .ToList();
    }

    /// <summary>
    /// Copy a file, falling back to cmd.exe for shadow copy symlink paths
    /// that .NET can't traverse directly.
    /// </summary>
    private static void RobustCopy(string source, string dest)
    {
        try
        {
            File.Copy(source, dest, true);
        }
        catch
        {
            // .NET File.Copy can fail on \\?\GLOBALROOT symlink paths - use cmd copy
            var result = RunCmd($"copy /Y \"{source}\" \"{dest}\"");
            if (!File.Exists(dest))
                throw new IOException($"Failed to copy file: {result}");
        }
    }

    /// <summary>
    /// Read a text file, falling back to cmd.exe for shadow copy symlink paths.
    /// </summary>
    public static string RobustReadText(string path, int maxBytes = 100 * 1024)
    {
        try
        {
            return ReadTextDirect(path, maxBytes);
        }
        catch
        {
            // Fallback: copy to temp, read, delete
            var tmp = Path.Combine(Path.GetTempPath(), $"se_preview_{Guid.NewGuid():N}.tmp");
            try
            {
                RunCmd($"copy /Y \"{path}\" \"{tmp}\"");
                if (!File.Exists(tmp))
                    return "[Could not read file through shadow copy]";
                return ReadTextDirect(tmp, maxBytes);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }
    }

    private static string ReadTextDirect(string path, int maxBytes)
    {
        var fi = new FileInfo(path);
        if (fi.Length > maxBytes)
        {
            using var reader = new StreamReader(path);
            var buffer = new char[maxBytes];
            var read = reader.Read(buffer, 0, buffer.Length);
            return new string(buffer, 0, read) + "\n\n--- [Truncated at 100KB] ---";
        }
        return File.ReadAllText(path);
    }

    public string CreateTempCopy(ShadowFileVersion version)
    {
        var ext = Path.GetExtension(version.OriginalPath);
        var nameNoExt = Path.GetFileNameWithoutExtension(version.OriginalPath);
        var stampedName = $"{nameNoExt}_{version.TimestampSuffix}{ext}";
        var tempDir = Path.Combine(_tempBase, "exports");
        Directory.CreateDirectory(tempDir);
        var destPath = Path.Combine(tempDir, stampedName);

        RobustCopy(version.ShadowPath, destPath);
        return destPath;
    }

    public void RestoreFile(ShadowFileVersion version)
    {
        if (version.IsCurrentVersion) return;

        // Create a backup of current first
        if (File.Exists(version.OriginalPath))
        {
            var backupPath = version.OriginalPath + ".bak";
            File.Copy(version.OriginalPath, backupPath, true);
        }

        RobustCopy(version.ShadowPath, version.OriginalPath);
    }

    private static string RunCmd(string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return output + error;
    }

    public void Dispose()
    {
        foreach (var (linkPath, _) in _activeLinks)
        {
            try
            {
                if (Directory.Exists(linkPath))
                    RunCmd($"rmdir \"{linkPath}\"");
            }
            catch { }
        }
        _activeLinks.Clear();
    }
}
