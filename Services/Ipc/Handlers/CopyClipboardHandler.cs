using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for copying selected scene actors into the system clipboard.
/// </summary>
public class CopyClipboardHandler : IIpcActionHandler
{
    /// <inheritdoc/>
    public string Action => "copy_clipboard";

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var data = root.TryGetProperty("data", out var d) ? d : payload;
        var (success, msg, count) = ClipboardExportService.CopyToClipboard(data);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "copy_clipboard_response",
            success,
            message = msg,
            actorCount = count
        }));
        return Task.CompletedTask;
    }
}

