using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for retrieving the raw text or base64 binary content of a project asset by GUID.
/// </summary>
public class GetAssetContentHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public GetAssetContentHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "get_asset_content";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var guid = payload.TryGetProperty("guid", out var gp) ? gp.GetString() : null;
        if (!string.IsNullOrWhiteSpace(guid))
        {
            var item = _assetDb.FindByGuid(guid);
            var (content, isBinary) = _assetDb.GetAssetContent(guid);

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "asset_content_response",
                guid,
                success = content != null,
                content,
                isBinary,
                assetType = item?.AssetType,
                fileName = item?.FileName,
                virtualPath = item?.VirtualPath
            }));
        }
        else
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "asset_content_response",
                success = false,
                message = "Missing guid"
            }));
        }

        return Task.CompletedTask;
    }
}
