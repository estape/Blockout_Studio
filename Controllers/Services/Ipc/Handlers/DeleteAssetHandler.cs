using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for deleting an asset and its .meta sidecar from the project.
/// </summary>
public class DeleteAssetHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public DeleteAssetHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "delete_asset";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var guid = payload.TryGetProperty("guid", out var gp) ? gp.GetString() : null;
        var ok = !string.IsNullOrWhiteSpace(guid) && _assetDb.DeleteAsset(guid);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "delete_asset_response",
            success = ok,
            guid,
            registry = _assetDb.GetRegistryData()
        }));

        return Task.CompletedTask;
    }
}
