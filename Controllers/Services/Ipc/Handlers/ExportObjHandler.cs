using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for opening an export dialog to save scene geometry as a Wavefront OBJ.
/// </summary>
public class ExportObjHandler : IIpcActionHandler
{
    /// <summary>Asset pipeline manager service.</summary>
    private readonly IAssetPipelineManager _assetManager;

    /// <summary>Initializes handler with the asset pipeline manager.</summary>
    public ExportObjHandler(IAssetPipelineManager assetManager)
    {
        _assetManager = assetManager;
    }

    /// <inheritdoc/>
    public string Action => "export_obj";

    /// <inheritdoc/>
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
