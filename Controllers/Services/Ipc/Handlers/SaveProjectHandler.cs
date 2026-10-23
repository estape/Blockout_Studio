using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for saving project scene data via a native save file dialog.
/// </summary>
public class SaveProjectHandler : IIpcActionHandler
{
    /// <summary>Default file type and name struct used by the save project handler.</summary>
    public static readonly ProjectFileType DefaultValue = ProjectFileType.Default;

    /// <summary>Project storage service instance.</summary>
    private readonly IProjectStorageService _storageService;

    /// <summary>The project file type and default name struct configured for this handler.</summary>
    private readonly ProjectFileType _defaultFileType;

    /// <summary>Gets the default file type struct configured for this handler.</summary>
    public ProjectFileType DefaultFileType => _defaultFileType;

    /// <summary>Initializes handler with the project storage service and a custom default file type struct.</summary>
    /// <param name="storageService">Project storage service instance.</param>
    /// <param name="defaultFileType">Default project file type struct.</param>
    public SaveProjectHandler(IProjectStorageService storageService, ProjectFileType defaultFileType)
    {
        _storageService = storageService;
        _defaultFileType = defaultFileType == default ? DefaultValue : defaultFileType;
    }

    /// <summary>Initializes handler with the project storage service using the default file type struct.</summary>
    /// <param name="storageService">Project storage service instance.</param>
    public SaveProjectHandler(IProjectStorageService storageService) : this(storageService, DefaultValue)
    {
    }

    /// <summary>Parameterless constructor using default storage service and default file type struct.</summary>
    public SaveProjectHandler() : this(new ProjectStorageService(), DefaultValue)
    {
    }

    /// <inheritdoc/>
    public string Action => "save_project";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string json = "{}";
        if (payload.TryGetProperty("projectData", out var pd) || root.TryGetProperty("projectData", out pd))
        {
            json = pd.ValueKind == JsonValueKind.String ? pd.GetString() ?? "{}" : pd.GetRawText();
        }

        var path = _storageService.SaveProjectDialog(json, _defaultFileType);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "save_project_response",
            success = path != null,
            path
        }));
        return Task.CompletedTask;
    }
}
