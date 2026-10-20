using System.Text.Json;

namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Service coordinating asset export dialogs and scene tree deserialization from frontend messages.
/// </summary>
public interface IAssetPipelineManager
{
    /// <summary>
    /// Displays a native file save dialog and exports the model as a Wavefront OBJ.
    /// </summary>
    /// <param name="model">Model data to export.</param>
    /// <param name="defaultName">Default proposed file name.</param>
    /// <returns>Selected file path or null if canceled.</returns>
    string? ExportObjDialog(ModelData model, string defaultName = "Blockout_Level.obj");

    /// <summary>
    /// Displays a native file save dialog and exports the model as a USD stage (USDA).
    /// </summary>
    /// <param name="model">Model data to export.</param>
    /// <param name="defaultName">Default proposed file name.</param>
    /// <returns>Selected file path or null if canceled.</returns>
    string? ExportUsdDialog(ModelData model, string defaultName = "Blockout_Stage.usda");

    /// <summary>
    /// Parses frontend JSON scene graph payloads containing blocks and mesh buffers into <see cref="ModelData"/>.
    /// </summary>
    /// <param name="sceneElement">JSON element representing scene blocks.</param>
    /// <returns>Structured <see cref="ModelData"/>.</returns>
    ModelData ParseFromFrontendScene(JsonElement sceneElement);
}
