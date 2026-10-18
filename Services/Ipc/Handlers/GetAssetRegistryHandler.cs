using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetAssetRegistryHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public GetAssetRegistryHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "get_asset_registry";

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
