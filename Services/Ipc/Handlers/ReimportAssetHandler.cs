using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for triggering reimport and metadata hash recalculation for a specified asset.
/// </summary>
public class ReimportAssetHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public ReimportAssetHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "reimport_asset";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var guid = payload.TryGetProperty("guid", out var gp) ? gp.GetString() : null;
        var ok = !string.IsNullOrWhiteSpace(guid) && _assetDb.ReimportAsset(guid);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "reimport_asset_response",
            success = ok,
            guid,
            registry = _assetDb.GetRegistryData()
        }));

        return Task.CompletedTask;
    }
}
