using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Blueprints;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler providing the dynamically discovered Blueprint node catalog to the Photino web frontend.
/// </summary>
public class GetNodeCatalogHandler : IIpcActionHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly INodeRegistryScanner _scanner;

    /// <inheritdoc/>
    public string Action => "get_node_registry";

    public GetNodeCatalogHandler(INodeRegistryScanner scanner)
    {
        _scanner = scanner;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        bool forceRescan = false;
        if (root.TryGetProperty("forceRescan", out var fr))
        {
            forceRescan = fr.GetBoolean();
        }

        var catalog = _scanner.GetCatalog(forceRescan);

        var response = JsonSerializer.Serialize(new
        {
            type = "get_node_registry_response",
            success = true,
            catalog
        }, JsonOpts);

        window.SendWebMessage(response);
        return Task.CompletedTask;
    }
}

