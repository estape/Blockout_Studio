using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class ExportUsdHandler : IIpcActionHandler
{
    private readonly IAssetPipelineManager _assetManager;

    public ExportUsdHandler(IAssetPipelineManager assetManager)
    {
        _assetManager = assetManager;
    }

    public string Action => "export_usd";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var model = _assetManager.ParseFromFrontendScene(root);
        var path = _assetManager.ExportUsdDialog(model);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "export_usd_response",
            success = path != null,
            path
        }));
        return Task.CompletedTask;
    }
}
