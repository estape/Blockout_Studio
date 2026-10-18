using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class SaveProjectHandler : IIpcActionHandler
{
    private readonly IProjectStorageService _storageService;

    public SaveProjectHandler(IProjectStorageService storageService)
    {
        _storageService = storageService;
    }

    public SaveProjectHandler() : this(new ProjectStorageService())
    {
    }

    public string Action => "save_project";

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
