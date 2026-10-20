using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for saving project scene data via a native save file dialog.
/// </summary>
public class SaveProjectHandler : IIpcActionHandler
{
    /// <summary>Project storage service instance.</summary>
    private readonly IProjectStorageService _storageService;

    /// <summary>Initializes handler with the project storage service.</summary>
    public SaveProjectHandler(IProjectStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>Parameterless constructor using default storage service.</summary>
    public SaveProjectHandler() : this(new ProjectStorageService())
    {
    }

    /// <inheritdoc/>
    public string Action => "save_project";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var json = root.TryGetProperty("projectData", out var pd) ? pd.GetString() ?? "{}" : "{}";
        var path = _storageService.SaveProjectDialog(json);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "save_project_response",
            success = path != null,
            path
        }));
        return Task.CompletedTask;
    }
}
