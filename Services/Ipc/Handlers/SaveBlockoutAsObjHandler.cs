using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;
using UE5BlockoutStudio.Services.Geometry;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class SaveBlockoutAsObjHandler : IIpcActionHandler
{
    private readonly IObjUsdPipelineService _pipeline;
    private readonly IAssetDatabaseService _assetDb;

    public SaveBlockoutAsObjHandler(IObjUsdPipelineService pipeline, IAssetDatabaseService assetDb)
    {
        _pipeline = pipeline;
        _assetDb = assetDb;
    }

    public string Action => "save_blockout_as_obj";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var projectRoot = _assetDb.CurrentProjectRoot;
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "save_blockout_as_obj_response",
                success = false,
                message = "No active project open"
            }));
            return Task.CompletedTask;
        }

        var blockName = payload.TryGetProperty("blockName", out var bnp) ? bnp.GetString() ?? "SM_Blockout" : "SM_Blockout";
        var bakeAo = payload.TryGetProperty("bakeAo", out var bap) && bap.GetBoolean();
        var pivotStr = payload.TryGetProperty("pivotMode", out var pmp) ? pmp.GetString() : "bottom";
        var pivot = pivotStr == "world" ? ObjPivotMode.WorldOrigin : ObjPivotMode.BottomCenter;

        float[] positions = [];
        float[]? normals = null;
        float[]? uvs = null;
        int[]? indices = null;

        if (payload.TryGetProperty("positions", out var pp) && pp.ValueKind == JsonValueKind.Array)
            positions = pp.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (payload.TryGetProperty("normals", out var pno) && pno.ValueKind == JsonValueKind.Array)
            normals = pno.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (payload.TryGetProperty("uvs", out var pu) && pu.ValueKind == JsonValueKind.Array)
            uvs = pu.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (payload.TryGetProperty("indices", out var pi) && pi.ValueKind == JsonValueKind.Array)
            indices = pi.EnumerateArray().Select(x => x.GetInt32()).ToArray();

        var savedPath = _pipeline.SaveBlockoutAsObj(projectRoot, blockName, positions, normals, uvs, indices, bakeAo, pivot);
        _assetDb.ScanAllAssets();

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "save_blockout_as_obj_response",
            success = true,
            filePath = savedPath,
            virtualPath = $"/Game/Meshes/{blockName}",
            registry = _assetDb.GetRegistryData()
        }));

        return Task.CompletedTask;
    }
}
