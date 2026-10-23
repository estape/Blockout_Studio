using BlockoutStudio.Services.Blueprints.Models;

namespace BlockoutStudio.Services.Blueprints.Storage;

/// <summary>
/// Service managing disk persistence, asset indexing, and C# emission for Blueprint graphs.
/// </summary>
public interface IBlueprintStorageService
{
    /// <summary>
    /// Lists all .bprint assets located under Assets/Blueprints/.
    /// </summary>
    Task<List<BlueprintAssetInfo>> ListBlueprintsAsync(string projectRoot);

    /// <summary>
    /// Loads and deserializes a Blueprint graph from disk.
    /// </summary>
    Task<BlueprintGraph?> LoadBlueprintAsync(string projectRoot, string blueprintNameOrRelativePath);

    /// <summary>
    /// Saves a Blueprint graph to Assets/Blueprints/{Name}.bprint, updates .meta sidecar,
    /// and emits transpiled C# to Assets/Scripts/Generated/{Name}.cs.
    /// </summary>
    Task<SaveBlueprintResult> SaveBlueprintAsync(string projectRoot, BlueprintGraph graph);

    /// <summary>
    /// Deletes a Blueprint asset, its .meta sidecar, and its generated C# file.
    /// </summary>
    Task<bool> DeleteBlueprintAsync(string projectRoot, string blueprintName);
}
