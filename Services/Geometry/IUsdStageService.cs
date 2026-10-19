using System.Numerics;

namespace BlockoutStudio.Services.Geometry;

public interface IUsdStageService
{
    UsdStage CreateStage(string stageName = "Level_Stage", float metersPerUnit = 0.01f, string upAxis = "Z");

    UsdPrim AddReference(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale,
        string? materialBinding = null);

    UsdPrim AddPayload(
        UsdStage stage,
        string primName,
        string assetVirtualOrDiskPath,
        Vector3 translation,
        Vector3 rotationDeg,
        Vector3 scale);

    void AddVariantSet(
        UsdPrim prim,
        string variantSetName,
        Dictionary<string, string> variants,
        string defaultVariant);

    UsdPrim ApplyOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialBinding = null);

    void AddOverOpinion(
        UsdStage stage,
        string primName,
        Vector3? translation = null,
        Vector3? rotationDeg = null,
        Vector3? scale = null,
        string? materialOverride = null);

    string ExportToUsdaString(UsdStage stage);
    string ExportToUsda(UsdStage stage);

    UsdStage ParseUsda(string usdaText);
}
