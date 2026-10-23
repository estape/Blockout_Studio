using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Services.Blueprints.Models;

namespace BlockoutStudio.Services.Blueprints.Storage;

/// <summary>
/// Service managing disk persistence, asset indexing, and C# emission for Blueprint graphs.
/// </summary>
public interface IBlueprintStorageService
{
    /// <summary>
    /// Lists all .bprint and .bgraph assets located under Assets/Blueprints/.
    /// </summary>
    Task<List<BlueprintAssetInfo>> ListBlueprintsAsync(string projectRoot);

    /// <summary>
    /// Loads and deserializes a Blueprint graph from disk (.bprint format).
    /// </summary>
    Task<BlueprintGraph?> LoadBlueprintAsync(string projectRoot, string blueprintNameOrRelativePath);

    /// <summary>
    /// Saves a Blueprint graph to Assets/Blueprints/{Name}.bprint, updates .meta sidecar,
    /// and emits transpiled C# to Assets/Scripts/Generated/{Name}.cs.
    /// </summary>
    Task<SaveBlueprintResult> SaveBlueprintAsync(string projectRoot, BlueprintGraph graph);

    /// <summary>
    /// Loads and deserializes a GraphDocument from disk (.bgraph format).
    /// </summary>
    Task<GraphDocument?> LoadGraphAsync(string projectRoot, string graphNameOrRelativePath);

    /// <summary>
    /// Validates and saves a GraphDocument to Assets/Blueprints/{Name}.bgraph with zero reflection.
    /// </summary>
    Task<SaveBlueprintResult> SaveGraphAsync(string projectRoot, GraphDocument document);

    /// <summary>
    /// Deletes a Blueprint asset, its .meta sidecar, and its generated C# file.
    /// </summary>
    Task<bool> DeleteBlueprintAsync(string projectRoot, string blueprintName);
}
