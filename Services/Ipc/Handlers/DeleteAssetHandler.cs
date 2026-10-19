using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class DeleteAssetHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public DeleteAssetHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "delete_asset";

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
