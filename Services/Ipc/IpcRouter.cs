using System.Text.Json;
using Photino.NET;

namespace BlockoutStudio.Services.Ipc;

/// <summary>
/// Dispatcher routing JSON action messages received over the Photino webview bridge to registered action handlers.
/// </summary>
public class IpcRouter : IIpcRouter
{
    /// <summary>Dictionary mapping action strings to their registered handlers.</summary>
    private readonly Dictionary<string, IIpcActionHandler> _handlers;

    /// <summary>
    /// Initializes an IPC router with an enumerable of registered action handlers.
    /// </summary>
    /// <param name="handlers">Collection of action handlers.</param>
    public IpcRouter(IEnumerable<IIpcActionHandler> handlers)
    {
        _handlers = new Dictionary<string, IIpcActionHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
        {
            _handlers[h.Action] = h;
        }
    }

    /// <inheritdoc/>
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
