using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for opening an existing project directory or .bengine project manifest file.
/// </summary>
public class OpenProjectHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public OpenProjectHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "open_project";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? projectPath = null;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("projectPath", out var pp))
        {
            projectPath = pp.GetString();
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            var (chosenPath, content) = ProjectStorageService.LoadProjectDialog();
            projectPath = chosenPath;
        }

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var (projectRoot, manifest) = ProjectStorageService.OpenProject(projectPath);
            _assetDb.Initialize(projectRoot, window);
            var registry = _assetDb.GetRegistryData();

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "project_status_response",
                success = true,
                projectRoot,
                projectName = manifest.ProjectName,
                manifest
            }));

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "asset_registry_response",
                registry
            }));
        }
        else
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "project_status_response",
                success = false,
                message = "Open project cancelled"
            }));
        }

        return Task.CompletedTask;
    }
}
