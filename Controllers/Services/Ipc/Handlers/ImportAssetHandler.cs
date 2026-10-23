using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for importing an external mesh or texture asset into the project's virtual file hierarchy.
/// </summary>
public class ImportAssetHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>Initializes handler with the asset database service.</summary>
    public ImportAssetHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public string Action => "import_asset";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? sourceFile = null;
        string targetFolder = "/Game/Meshes";

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("sourceFilePath", out var sp))
            {
                sourceFile = sp.GetString();
            }
            if (payload.TryGetProperty("targetFolder", out var tf) && !string.IsNullOrWhiteSpace(tf.GetString()))
            {
                targetFolder = tf.GetString()!;
            }
        }

        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            var (chosen, _) = ProjectStorageService.LoadProjectDialog();
            sourceFile = chosen;
        }

        if (!string.IsNullOrWhiteSpace(sourceFile))
        {
            var item = _assetDb.ImportAsset(sourceFile, targetFolder);
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "import_asset_response",
                success = item != null,
                item,
                registry = _assetDb.GetRegistryData()
            }));
        }
        else
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "import_asset_response",
                success = false,
                message = "No file chosen for import"
            }));
        }

        return Task.CompletedTask;
    }
}
