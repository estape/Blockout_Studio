using System.Numerics;
using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Geometry;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for serializing stage prims and layer hierarchy into a USDA scene file under Assets/Scenes/.
/// </summary>
public class SaveUsdStageHandler : IIpcActionHandler
{
    /// <summary>Geometry pipeline service.</summary>
    private readonly IObjUsdPipelineService _pipeline;

    /// <summary>Asset database service.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with geometry pipeline and asset database services.</summary>
    public SaveUsdStageHandler(IObjUsdPipelineService pipeline, IAssetDatabaseService assetDb)
    {
        _pipeline = pipeline;
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "save_usd_stage";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var projectRoot = _assetDb.CurrentProjectRoot;
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "save_usd_stage_response",
                success = false,
                message = "No active project open"
            }));
            return Task.CompletedTask;
        }

        var stageName = payload.TryGetProperty("stageName", out var snp) ? snp.GetString() ?? "Level_Stage" : "Level_Stage";
        var stage = new UsdStage { StageName = stageName };

        if (payload.TryGetProperty("sublayers", out var slp) && slp.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in slp.EnumerateArray())
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s)) stage.Sublayers.Add(s);
            }
        }

        if (payload.TryGetProperty("prims", out var pmp) && pmp.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in pmp.EnumerateArray())
            {
                var name = p.TryGetProperty("name", out var np) ? np.GetString() ?? "Prim" : "Prim";
                var spec = p.TryGetProperty("specifier", out var sp) ? sp.GetString() ?? "def" : "def";
                var refPath = p.TryGetProperty("referencePath", out var rp) ? rp.GetString() : null;
                var mat = p.TryGetProperty("materialBinding", out var mp) ? mp.GetString() : null;

                Vector3 trans = Vector3.Zero;
                if (p.TryGetProperty("position", out var pos) && pos.ValueKind == JsonValueKind.Array)
                {
                    var arr = pos.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    if (arr.Length >= 3) trans = new Vector3(arr[0], arr[1], arr[2]);
                }

                stage.RootPrims.Add(new UsdPrim
                {
                    Name = name,
                    Specifier = spec,
                    ReferencePath = refPath,
                    MaterialBinding = mat,
                    Translation = trans
                });
            }
        }

        var savedPath = _pipeline.SaveUsdStage(projectRoot, stageName, stage);
        _assetDb.ScanAllAssets();

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "save_usd_stage_response",
            success = true,
            filePath = savedPath,
            virtualPath = $"/Game/Scenes/{stageName}",
            registry = _assetDb.GetRegistryData()
        }));

        return Task.CompletedTask;
    }
}
