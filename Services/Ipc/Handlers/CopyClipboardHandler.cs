using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services;

namespace BlockoutStudio.Services.Ipc.Handlers;

public class CopyClipboardHandler : IIpcActionHandler
{
    public string Action => "copy_clipboard";

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

