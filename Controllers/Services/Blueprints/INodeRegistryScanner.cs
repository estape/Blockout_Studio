using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Services.Blueprints;

/// <summary>
/// Service interface for scanning engine assemblies and discovering methods decorated with [BlueprintNode].
/// </summary>
public interface INodeRegistryScanner
{
    /// <summary>
    /// Scans the BlockoutStudio assembly (and any loaded game assemblies) to compile the complete node catalog.
    /// </summary>
    /// <param name="forceRescan">True to bypass cached catalog and re-reflect all types.</param>
    /// <returns>Serialized NodeCatalog containing all discovered nodes and category index.</returns>
    NodeCatalog GetCatalog(bool forceRescan = false);
}
