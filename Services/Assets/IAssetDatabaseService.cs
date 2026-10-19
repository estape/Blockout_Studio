using Photino.NET;

namespace BlockoutStudio.Services.Assets;

public interface IAssetDatabaseService
{
    string? CurrentProjectRoot { get; }
    ProjectManifest? CurrentManifest { get; }

    void Initialize(string projectRoot, PhotinoWindow? window = null);
    AssetRegistryData GetRegistryData();
    AssetRegistryItem? FindByGuid(string guid);
    AssetRegistryItem? FindByVirtualPath(string virtualPath);
    AssetRegistryItem? ImportAsset(string sourceFilePath, string targetVirtualFolder = "/Game/Meshes");
    bool ReimportAsset(string guid);
    bool DeleteAsset(string guid);
    bool ShowInExplorer(string guid);
    (string? Content, bool IsBinary) GetAssetContent(string guid);
    void ScanAllAssets();
}
