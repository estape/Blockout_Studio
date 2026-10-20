using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for opening an export dialog to save scene geometry as a USDA stage.
/// </summary>
public class ExportUsdHandler : IIpcActionHandler
{
    /// <summary>Asset pipeline manager service.</summary>
    private readonly IAssetPipelineManager _assetManager;

    /// <summary>Initializes handler with the asset pipeline manager.</summary>
    public ExportUsdHandler(IAssetPipelineManager assetManager)
    {
        _assetManager = assetManager;
    }

    /// <inheritdoc/>
    public string Action => "export_usd";

    /// <inheritdoc/>
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
