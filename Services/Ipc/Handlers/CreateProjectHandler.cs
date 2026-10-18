using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class CreateProjectHandler : IIpcActionHandler
{
    private readonly IAssetDatabaseService _assetDb;

    public CreateProjectHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    public string Action => "create_project";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? projectRoot = null;
        string projectName = "MyGameProject";

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectName", out var pn) && !string.IsNullOrWhiteSpace(pn.GetString()))
            {
                projectName = pn.GetString()!;
            }
            if (payload.TryGetProperty("projectPath", out var pp) && !string.IsNullOrWhiteSpace(pp.GetString()))
            {
                projectRoot = pp.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            projectRoot = ProjectStorageService.CreateProjectDialog(projectName);
        }
        else
        {
            var (createdRoot, _) = ProjectStorageService.CreateNewProject(projectRoot, projectName);
            projectRoot = createdRoot;
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            _assetDb.Initialize(projectRoot, window);
            var manifest = _assetDb.CurrentManifest;
            var registry = _assetDb.GetRegistryData();

            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "project_status_response",
                success = true,
                projectRoot,
                projectName = manifest?.ProjectName ?? projectName,
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
                message = "Project creation cancelled"
            }));
        }

        return Task.CompletedTask;
    }
}
