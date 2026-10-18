using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class OpenProjectHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public OpenProjectHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "open_project";

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
