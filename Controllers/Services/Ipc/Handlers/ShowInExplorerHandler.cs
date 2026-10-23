using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for launching the system file manager highlighting the asset file on disk.
/// </summary>
public class ShowInExplorerHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public ShowInExplorerHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "show_in_explorer";

    /// <inheritdoc/>
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
