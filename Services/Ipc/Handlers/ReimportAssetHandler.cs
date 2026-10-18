using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class ReimportAssetHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public ReimportAssetHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "reimport_asset";

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
