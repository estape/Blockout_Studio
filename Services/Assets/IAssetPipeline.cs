namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Pipeline interface for converting internal model data to/from specific 3D interchange file formats.
/// </summary>
public interface IAssetPipeline
{
    /// <summary>Primary file extension handled by this pipeline (e.g. ".obj", ".usda").</summary>
    string FormatExtension { get; }

    /// <summary>Human-readable format description.</summary>
    string FormatDescription { get; }

    /// <summary>
    /// Serializes model geometry into file format ASCII content.
    /// </summary>
    /// <param name="model">Model data to serialize.</param>
    /// <returns>Formatted text string.</returns>
    string ExportToString(ModelData model);

    /// <summary>
    /// Deserializes format ASCII content into internal model geometry.
    /// </summary>
    /// <param name="content">Raw file text content.</param>
    /// <returns>Populated <see cref="ModelData"/> object.</returns>
    ModelData ImportFromString(string content);
}
