using Photino.NET;

namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Service interface for indexing, querying, watching, and mutating assets in the project workspace.
/// </summary>
public interface IAssetDatabaseService
{
    /// <summary>Root folder path of the currently opened project.</summary>
    string? CurrentProjectRoot { get; }

    /// <summary>Parsed project manifest of the current project.</summary>
    ProjectManifest? CurrentManifest { get; }

    /// <summary>
    /// Initializes asset scanning and hot-reload file watching for a project directory.
    /// </summary>
    /// <param name="projectRoot">Absolute path to project directory.</param>
    /// <param name="window">Optional Photino window for IPC hot-reload events.</param>
    void Initialize(string projectRoot, PhotinoWindow? window = null);

    /// <summary>
    /// Retrieves a snapshot of the current asset registry.
    /// </summary>
    /// <returns>Populated <see cref="AssetRegistryData"/> index.</returns>
    AssetRegistryData GetRegistryData();

    /// <summary>
    /// Finds a registered asset by its unique 128-bit GUID.
    /// </summary>
    /// <param name="guid">Asset GUID string.</param>
    /// <returns>Matching item or null if not found.</returns>
    AssetRegistryItem? FindByGuid(string guid);

    /// <summary>
    /// Finds an asset item by its virtual engine path (e.g. "/Game/Meshes/SM_Cube").
    /// </summary>
    /// <param name="virtualPath">Virtual path string.</param>
    /// <returns>Matching item or null.</returns>
    AssetRegistryItem? FindByVirtualPath(string virtualPath);

    /// <summary>
    /// Copies an external file into the project's Assets folder and registers its metadata sidecar.
    /// </summary>
    /// <param name="sourceFilePath">Disk path to source file.</param>
    /// <param name="targetVirtualFolder">Destination folder under /Game/.</param>
    /// <returns>Created <see cref="AssetRegistryItem"/>.</returns>
    AssetRegistryItem? ImportAsset(string sourceFilePath, string targetVirtualFolder = "/Game/Meshes");

    /// <summary>
    /// Re-evaluates hash and metadata for an asset identified by GUID.
    /// </summary>
    /// <param name="guid">Target asset GUID.</param>
    /// <returns>True if reimport succeeded.</returns>
    bool ReimportAsset(string guid);

    /// <summary>
    /// Permanently deletes an asset and its accompanying .meta file from disk.
    /// </summary>
    /// <param name="guid">Target asset GUID.</param>
    /// <returns>True if deleted.</returns>
    bool DeleteAsset(string guid);

    /// <summary>
    /// Opens the operating system's file manager highlighting the asset file on disk.
    /// </summary>
    /// <param name="guid">Target asset GUID.</param>
    /// <returns>True if file manager launched.</returns>
    bool ShowInExplorer(string guid);

    /// <summary>
    /// Reads asset content as either raw UTF-8 text or Base64-encoded binary string.
    /// </summary>
    /// <param name="guid">Target asset GUID.</param>
    /// <returns>Tuple containing string content and binary flag.</returns>
    (string? Content, bool IsBinary) GetAssetContent(string guid);

    /// <summary>
    /// Performs a full recursive filesystem crawl of Assets/ and updates Library/AssetRegistry.json.
    /// </summary>
    void ScanAllAssets();
}
