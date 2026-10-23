using System.Text.Json;
using Photino.NET;

namespace BlockoutStudio.Services.Ipc;

/// <summary>
/// Interface for handlers that process incoming JSON messages from the Photino webview.
/// </summary>
public interface IIpcActionHandler
{
    /// <summary>Distinct action keyword matching the incoming message's "action" field.</summary>
    string Action { get; }

    /// <summary>
    /// Executes the backend logic for the specified IPC action.
    /// </summary>
    /// <param name="payload">The extracted "data" property payload or root document.</param>
    /// <param name="root">The entire incoming JSON root element.</param>
    /// <param name="window">Reference to the host Photino window for response replies.</param>
    /// <returns>A completed asynchronous task.</returns>
    Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window);
}
