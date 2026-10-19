using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class ExportObjHandler : IIpcActionHandler
{
    private readonly IAssetPipelineManager _assetManager;

    public ExportObjHandler(IAssetPipelineManager assetManager)
    {
        _assetManager = assetManager;
    }

    public string Action => "export_obj";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var model = _assetManager.ParseFromFrontendScene(root);
        var path = _assetManager.ExportObjDialog(model);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "export_obj_response",
            success = path != null,
            path
        }));
        return Task.CompletedTask;
    }
}
