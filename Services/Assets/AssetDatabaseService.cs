using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Photino.NET;

namespace BlockoutStudio.Services.Assets;

public class AssetDatabaseService : IAssetDatabaseService, IDisposable
{
    private readonly ConcurrentDictionary<string, AssetRegistryItem> _itemsByGuid = new();
    private readonly ConcurrentDictionary<string, AssetRegistryItem> _itemsByVirtualPath = new();

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _lock = new();
    private PhotinoWindow? _window;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public string? CurrentProjectRoot { get; private set; }
    public ProjectManifest? CurrentManifest { get; private set; }

    public void Initialize(string projectRoot, PhotinoWindow? window = null)
    {
        lock (_lock)
        {
            CurrentProjectRoot = projectRoot;
            _window = window;

            var manifestPath = Path.Combine(projectRoot, "Project.bengine");
            if (File.Exists(manifestPath))
            {
                try
                {
                    CurrentManifest = JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(manifestPath));
                }
                catch
                {
                    CurrentManifest = new ProjectManifest { ProjectName = Path.GetFileName(projectRoot) };
                }
            }
            else
            {
                CurrentManifest = new ProjectManifest { ProjectName = Path.GetFileName(projectRoot) };
            }

            SetupWatcher(projectRoot);
            ScanAllAssets();
        }
    }

    private void SetupWatcher(string projectRoot)
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();

        var assetsDir = Path.Combine(projectRoot, "Assets");
        if (!Directory.Exists(assetsDir))
        {
            Directory.CreateDirectory(assetsDir);
        }

        _watcher = new FileSystemWatcher(assetsDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemRenamed;

        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath)) return;
        TriggerDebouncedScan();
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath) && ShouldIgnoreFile(e.OldFullPath)) return;
        TriggerDebouncedScan();
    }

    private static bool ShouldIgnoreFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains(".git", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("Library", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("Intermediate", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("Saved", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void TriggerDebouncedScan()
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                ScanAllAssets();
                NotifyFrontendOfUpdate();
            }, null, 150, Timeout.Infinite);
        }
    }

    private void NotifyFrontendOfUpdate()
    {
        if (_window == null) return;
        try
        {
            var registry = GetRegistryData();
            _window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "asset_database_updated",
                registry
            }));
        }
        catch
        {
            // ignored
        }
    }

    public void ScanAllAssets()
    {
        if (string.IsNullOrWhiteSpace(CurrentProjectRoot)) return;
        var assetsDir = Path.Combine(CurrentProjectRoot, "Assets");
        if (!Directory.Exists(assetsDir)) return;

        lock (_lock)
        {
            _itemsByGuid.Clear();
            _itemsByVirtualPath.Clear();

            var allFiles = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories);
            var scannedItems = new List<AssetRegistryItem>();

            foreach (var file in allFiles)
            {
                if (ShouldIgnoreFile(file)) continue;

                var meta = AssetMeta.LoadOrCreate(file);
                var fi = new FileInfo(file);

                var relativeDisk = Path.GetRelativePath(CurrentProjectRoot, file).Replace('\\', '/');
                var relativeFromAssets = Path.GetRelativePath(assetsDir, file).Replace('\\', '/');

                var ext = Path.GetExtension(relativeFromAssets);
                var virtualWithoutExt = string.IsNullOrEmpty(ext)
                    ? relativeFromAssets
                    : relativeFromAssets[..^ext.Length];

                var virtualPath = "/Game/" + virtualWithoutExt.TrimStart('/');

                var item = new AssetRegistryItem
                {
                    Guid = meta.Guid,
                    VirtualPath = virtualPath,
                    RelativeDiskPath = relativeDisk,
                    FileName = Path.GetFileName(file),
                    AssetType = meta.AssetType,
                    FileSize = fi.Exists ? fi.Length : 0,
                    SourceHash = meta.SourceHash,
                    LastModifiedUtc = meta.LastModifiedUtc,
                    Dependencies = new List<string>(),
                    Referencers = new List<string>()
                };

                _itemsByGuid[item.Guid] = item;
                _itemsByVirtualPath[item.VirtualPath] = item;
                scannedItems.Add(item);
            }

            // Dependency & Referencer Graph Analysis
            ResolveDependencies(scannedItems);

            // Persist Library/AssetRegistry.json
            var registryData = new AssetRegistryData
            {
                Version = 1,
                LastIndexedUtc = DateTime.UtcNow.ToString("o"),
                ProjectName = CurrentManifest?.ProjectName ?? Path.GetFileName(CurrentProjectRoot),
                Assets = scannedItems
            };

            var libDir = Path.Combine(CurrentProjectRoot, "Library");
            if (!Directory.Exists(libDir)) Directory.CreateDirectory(libDir);

            var regPath = Path.Combine(libDir, "AssetRegistry.json");
            try
            {
                File.WriteAllText(regPath, JsonSerializer.Serialize(registryData, JsonOpts));
            }
            catch
            {
                // ignored
            }
        }
    }

    private void ResolveDependencies(List<AssetRegistryItem> items)
    {
        if (CurrentProjectRoot == null) return;

        foreach (var item in items)
        {
            var fullPath = Path.Combine(CurrentProjectRoot, item.RelativeDiskPath);
            if (!File.Exists(fullPath)) continue;

            if (item.AssetType == "Scene")
            {
                try
                {
                    var text = File.ReadAllText(fullPath);
                    foreach (var other in items)
                    {
                        if (other.Guid == item.Guid) continue;
                        if (other.AssetType is "StaticMesh" or "UsdStage" or "Material")
                        {
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(other.FileName);
                            if (text.Contains(other.FileName, StringComparison.OrdinalIgnoreCase) ||
                                text.Contains(other.VirtualPath, StringComparison.OrdinalIgnoreCase) ||
                                text.Contains(nameWithoutExt, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!item.Dependencies.Contains(other.VirtualPath))
                                    item.Dependencies.Add(other.VirtualPath);
                                if (!other.Referencers.Contains(item.VirtualPath))
                                    other.Referencers.Add(item.VirtualPath);
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
            else if (item.AssetType == "Material")
            {
                try
                {
                    var text = File.ReadAllText(fullPath);
                    foreach (var other in items)
                    {
                        if (other.Guid == item.Guid) continue;
                        if (other.AssetType is "Texture" or "Shader")
                        {
                            if (text.Contains(other.FileName, StringComparison.OrdinalIgnoreCase) ||
                                text.Contains(other.VirtualPath, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!item.Dependencies.Contains(other.VirtualPath))
                                    item.Dependencies.Add(other.VirtualPath);
                                if (!other.Referencers.Contains(item.VirtualPath))
                                    other.Referencers.Add(item.VirtualPath);
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    public AssetRegistryData GetRegistryData()
    {
        lock (_lock)
        {
            return new AssetRegistryData
            {
                Version = 1,
                LastIndexedUtc = DateTime.UtcNow.ToString("o"),
                ProjectName = CurrentManifest?.ProjectName ?? (CurrentProjectRoot != null ? Path.GetFileName(CurrentProjectRoot) : "Untitled"),
                Assets = _itemsByGuid.Values.OrderBy(x => x.VirtualPath).ToList()
            };
        }
    }

    public AssetRegistryItem? FindByGuid(string guid)
    {
        _itemsByGuid.TryGetValue(guid, out var item);
        return item;
    }

    public AssetRegistryItem? FindByVirtualPath(string virtualPath)
    {
        _itemsByVirtualPath.TryGetValue(virtualPath, out var item);
        return item;
    }

    public AssetRegistryItem? ImportAsset(string sourceFilePath, string targetVirtualFolder = "/Game/Meshes")
    {
        if (CurrentProjectRoot == null || !File.Exists(sourceFilePath)) return null;

        var folderRelative = targetVirtualFolder.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            ? targetVirtualFolder[6..]
            : targetVirtualFolder.TrimStart('/');

        var destFolder = Path.Combine(CurrentProjectRoot, "Assets", folderRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);

        var fileName = Path.GetFileName(sourceFilePath);
        var destFile = Path.Combine(destFolder, fileName);
        File.Copy(sourceFilePath, destFile, true);

        var meta = AssetMeta.LoadOrCreate(destFile);
        ScanAllAssets();
        return FindByGuid(meta.Guid);
    }

    public bool ReimportAsset(string guid)
    {
        var item = FindByGuid(guid);
        if (item == null || CurrentProjectRoot == null) return false;

        var fullPath = Path.Combine(CurrentProjectRoot, item.RelativeDiskPath);
        if (!File.Exists(fullPath)) return false;

        var meta = AssetMeta.LoadOrCreate(fullPath);
        meta.SourceHash = AssetMeta.ComputeFileSha256(fullPath);
        meta.LastModifiedUtc = DateTime.UtcNow.ToString("o");

        var metaPath = fullPath + ".meta";
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, JsonOpts));

        ScanAllAssets();
        NotifyFrontendOfUpdate();
        return true;
    }

    public bool DeleteAsset(string guid)
    {
        var item = FindByGuid(guid);
        if (item == null || CurrentProjectRoot == null) return false;

        var fullPath = Path.Combine(CurrentProjectRoot, item.RelativeDiskPath);
        var metaPath = fullPath + ".meta";

        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
            if (File.Exists(metaPath)) File.Delete(metaPath);
            ScanAllAssets();
            NotifyFrontendOfUpdate();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ShowInExplorer(string guid)
    {
        var item = FindByGuid(guid);
        if (item == null || CurrentProjectRoot == null) return false;

        var fullPath = Path.Combine(CurrentProjectRoot, item.RelativeDiskPath);
        if (!File.Exists(fullPath)) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public (string? Content, bool IsBinary) GetAssetContent(string guid)
    {
        var item = FindByGuid(guid);
        if (item == null || CurrentProjectRoot == null) return (null, false);

        var fullPath = Path.Combine(CurrentProjectRoot, item.RelativeDiskPath);
        if (!File.Exists(fullPath)) return (null, false);

        try
        {
            if (item.AssetType is "StaticMesh" or "UsdStage" or "Material" or "Shader" or "Scene" or "Script")
            {
                return (File.ReadAllText(fullPath), false);
            }
            else
            {
                var bytes = File.ReadAllBytes(fullPath);
                return (Convert.ToBase64String(bytes), true);
            }
        }
        catch
        {
            return (null, false);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
    }
}
