using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class LoadProjectHandler : IIpcActionHandler
{
    private readonly IProjectStorageService _storageService;

    public LoadProjectHandler(IProjectStorageService storageService)
    {
        _storageService = storageService;
    }

    public LoadProjectHandler() : this(new ProjectStorageService())
    {
    }

    public string Action => "load_project";

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
