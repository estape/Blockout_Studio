using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class ShowInExplorerHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public ShowInExplorerHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "show_in_explorer";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var guid = payload.TryGetProperty("guid", out var gp) ? gp.GetString() : null;
        if (!string.IsNullOrWhiteSpace(guid))
        {
            _assetDb.ShowInExplorer(guid);
        }
        return Task.CompletedTask;
    }
}
