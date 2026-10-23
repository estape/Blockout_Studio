using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for reporting current workspace path, project name, and manifest configuration.
/// </summary>
public class GetProjectStatusHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public GetProjectStatusHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "get_project_status";

    /// <inheritdoc/>
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
