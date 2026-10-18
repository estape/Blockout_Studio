using System.Text.Json;
using Photino.NET;

namespace UE5BlockoutStudio.Services.Ipc;

public class IpcRouter : IIpcRouter
{
    private readonly Dictionary<string, IIpcActionHandler> _handlers;

    public IpcRouter(IEnumerable<IIpcActionHandler> handlers)
    {
        _handlers = new Dictionary<string, IIpcActionHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
        {
            _handlers[h.Action] = h;
        }
    }

    public async Task DispatchAsync(string message, PhotinoWindow window)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            if (!root.TryGetProperty("action", out var actionProp)) return;
            var action = actionProp.GetString();
            if (string.IsNullOrEmpty(action)) return;

            if (_handlers.TryGetValue(action, out var handler))
            {
                var payload = root.TryGetProperty("data", out var d) ? d : root;
                await handler.HandleAsync(payload, root, window);
            }
            else
            {
                window.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = $"Unknown engine action: '{action}'"
                }));
            }
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "error",
                message = ex.Message
            }));
        }
    }
}
