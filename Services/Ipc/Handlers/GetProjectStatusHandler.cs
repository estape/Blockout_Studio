using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetProjectStatusHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public GetProjectStatusHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "get_project_status";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var rootDir = _assetDb.CurrentProjectRoot;
        var manifest = _assetDb.CurrentManifest;

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "project_status_response",
            success = rootDir != null,
            projectRoot = rootDir,
            projectName = manifest?.ProjectName ?? "Untitled",
            manifest
        }));

        return Task.CompletedTask;
    }
}
