using System.Numerics;

namespace BlockoutStudio.Services.Geometry;

/// <summary>
/// Service interface for composing, manipulating, and serializing Universal Scene Description (USD / USDA) stages and primitives.
/// </summary>
public interface IUsdStageService
{
    /// <summary>
    /// Initializes a new USD Stage definition.
    /// </summary>
    /// <param name="stageName">Root stage identifier name.</param>
    /// <param name="metersPerUnit">Linear unit scaling in meters (default 0.01 for centimeters).</param>
    /// <param name="upAxis">World vertical axis convention ("Z" or "Y").</param>
    /// <returns>A new <see cref="UsdStage"/> instance.</returns>
    UsdStage CreateStage(string stageName = "Level_Stage", float metersPerUnit = 0.01f, string upAxis = "Z");

    /// <summary>
    /// Adds a referenced external asset prim with spatial transform and optional material binding.
    /// </summary>
    /// <param name="stage">Target USD stage.</param>
    /// <param name="primName">Name of the prim.</param>
    /// <param name="assetVirtualOrDiskPath">Relative or absolute URI to the asset file.</param>
    /// <param name="translation">Position translation vector.</param>
    /// <param name="rotationDeg">Euler rotation in degrees.</param>
    /// <param name="scale">Scale vector.</param>
    /// <param name="materialBinding">Optional material reference path.</param>
    /// <returns>The created <see cref="UsdPrim"/>.</returns>
    UsdPrim AddReference(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale,
        string? materialBinding = null);

    /// <summary>
    /// Adds a deferred payload asset prim with spatial transform.
    /// </summary>
    /// <param name="stage">Target USD stage.</param>
    /// <param name="primName">Name of the prim.</param>
    /// <param name="assetVirtualOrDiskPath">URI to the payload file.</param>
    /// <param name="translation">Position translation vector.</param>
    /// <param name="rotationDeg">Euler rotation in degrees.</param>
    /// <param name="scale">Scale vector.</param>
    /// <returns>The created <see cref="UsdPrim"/>.</returns>
    UsdPrim AddPayload(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale);

    /// <summary>
    /// Attaches a variant set and active selection to a prim.
    /// </summary>
    /// <param name="prim">Target prim.</param>
    /// <param name="variantSetName">Name of the variant set.</param>
    /// <param name="variants">Dictionary mapping variant options to definitions.</param>
    /// <param name="defaultVariant">Default active variant key.</param>
    void AddVariantSet(
        UsdPrim prim,
        string variantSetName,
        Dictionary<string, string> variants,
        string defaultVariant);

    /// <summary>
    /// Applies non-destructive USD 'over' override opinion on an existing prim.
    /// </summary>
    /// <param name="stage">Target USD stage.</param>
    /// <param name="primName">Name of the prim to override.</param>
    /// <param name="translation">Optional overridden translation.</param>
    /// <param name="rotationDeg">Optional overridden Euler rotation.</param>
    /// <param name="scale">Optional overridden scale.</param>
    /// <param name="materialBinding">Optional overridden material binding path.</param>
    /// <returns>The overridden <see cref="UsdPrim"/>.</returns>
    UsdPrim ApplyOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialBinding = null);

    /// <summary>
    /// Adds or updates an 'over' opinion on a prim in the stage.
    /// </summary>
    /// <param name="stage">Target USD stage.</param>
    /// <param name="primName">Name of the prim.</param>
    /// <param name="translation">Optional translation override.</param>
    /// <param name="rotationDeg">Optional rotation override.</param>
    /// <param name="scale">Optional scale override.</param>
    /// <param name="materialOverride">Optional material override.</param>
    void AddOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialOverride = null);

    /// <summary>
    /// Serializes a USD stage hierarchy into ASCII USDA text.
    /// </summary>
    /// <param name="stage">Stage to serialize.</param>
    /// <returns>Formatted USDA string.</returns>
    string ExportToUsdaString(UsdStage stage);

    /// <summary>
    /// Alias for <see cref="ExportToUsdaString(UsdStage)"/>.
    /// </summary>
    /// <param name="stage">Stage to serialize.</param>
    /// <returns>Formatted USDA string.</returns>
    string ExportToUsda(UsdStage stage);

    /// <summary>
    /// Deserializes USDA ASCII text into an in-memory <see cref="UsdStage"/> representation.
    /// </summary>
    /// <param name="usdaText">USDA ASCII file content.</param>
    /// <returns>Parsed <see cref="UsdStage"/> object.</returns>
    UsdStage ParseUsda(string usdaText);
}
