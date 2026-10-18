using System.Numerics;
using System.Text.Json;

namespace UE5BlockoutStudio.Services.Assets;

public class AssetPipelineManager : IAssetPipelineManager
{
    private readonly ObjPipeline _objPipeline = new();
    private readonly UsdPipeline _usdPipeline = new();

    public string? ExportObjDialog(ModelData model, string defaultName = "Blockout_Level.obj")
    {
        var text = _objPipeline.ExportToString(model);
        return ProjectStorageService.SaveTextDialog(
            text,
            defaultName,
            "Wavefront OBJ (*.obj)\0*.obj\0All Files (*.*)\0*.*\0",
            "obj",
            "Export .OBJ for Unreal Engine 5"
        );
    }

    public string? ExportUsdDialog(ModelData model, string defaultName = "Blockout_Stage.usda")
    {
        var text = _usdPipeline.ExportToString(model);
        return ProjectStorageService.SaveTextDialog(
            text,
            defaultName,
            "USD ASCII (*.usda)\0*.usda\0Universal Scene Description (*.usd)\0*.usd\0All Files (*.*)\0*.*\0",
            "usda",
            "Export USD Stage for Unreal Engine 5"
        );
    }

    public ModelData ParseFromFrontendScene(JsonElement sceneElement)
    {
        var model = new ModelData { SceneName = "UE5_Blockout_Stage" };

        if (!sceneElement.TryGetProperty("blocks", out var blocksProp) || blocksProp.ValueKind != JsonValueKind.Array)
        {
            return model;
        }

        foreach (var b in blocksProp.EnumerateArray())
        {
            var name = b.TryGetProperty("name", out var pn) ? pn.GetString() ?? "Mesh" : "Mesh";
            var part = new ModelMeshPart { Name = name };

            if (b.TryGetProperty("positions", out var pp) && pp.ValueKind == JsonValueKind.Array)
            {
                part.Positions = pp.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            }

            if (b.TryGetProperty("normals", out var pno) && pno.ValueKind == JsonValueKind.Array)
            {
                part.Normals = pno.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            }

            if (b.TryGetProperty("uvs", out var pu) && pu.ValueKind == JsonValueKind.Array)
            {
                part.UVs = pu.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            }

            if (b.TryGetProperty("indices", out var pi) && pi.ValueKind == JsonValueKind.Array)
            {
                part.Indices = pi.EnumerateArray().Select(x => x.GetInt32()).ToArray();
            }

            model.Parts.Add(part);
        }

        return model;
    }
}
