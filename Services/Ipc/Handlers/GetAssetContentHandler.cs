using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetAssetContentHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public GetAssetContentHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "get_asset_content";

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
