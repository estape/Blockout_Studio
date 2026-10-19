namespace BlockoutStudio.Services.Assets;

public interface IAssetPipeline
{
    string FormatExtension { get; }
    string FormatDescription { get; }
    string ExportToString(ModelData model);
    ModelData ImportFromString(string content);
}
