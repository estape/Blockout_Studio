using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for requesting a fresh snapshot of the project asset registry.
/// </summary>
public class GetAssetRegistryHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public GetAssetRegistryHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "get_asset_registry";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var registry = _assetDb.GetRegistryData();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "asset_registry_response",
            registry
        }));
        return Task.CompletedTask;
    }
}
