using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for opening and loading project scene files via native file open dialogs.
/// </summary>
public class LoadProjectHandler : IIpcActionHandler
{
    /// <summary>Project storage service instance.</summary>
    private readonly IProjectStorageService _storageService;

    /// <summary>Initializes handler with injected project storage service.</summary>
    public LoadProjectHandler(IProjectStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>Parameterless constructor using default storage service.</summary>
    public LoadProjectHandler() : this(new ProjectStorageService())
    {
    }

    /// <inheritdoc/>
    public string Action => "load_project";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var (path, content) = _storageService.LoadProjectDialog();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "load_project_response",
            success = content != null,
            path,
            content
        }));
        return Task.CompletedTask;
    }
}
