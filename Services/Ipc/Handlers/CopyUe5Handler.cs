using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class CopyUe5Handler : IIpcActionHandler
{
    public string Action => "copy_ue5";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var data = root.TryGetProperty("data", out var d) ? d : payload;
        var (success, msg, count) = Ue5ClipboardService.CopyToUe5(data);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "copy_ue5_response",
            success,
            message = msg,
            actorCount = count
        }));
        return Task.CompletedTask;
    }
}
