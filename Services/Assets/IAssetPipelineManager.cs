using System.Text.Json;

namespace UE5BlockoutStudio.Services.Assets;

public interface IAssetPipelineManager
{
    string? ExportObjDialog(ModelData model, string defaultName = "Blockout_Level.obj");
    string? ExportUsdDialog(ModelData model, string defaultName = "Blockout_Stage.usda");
    ModelData ParseFromFrontendScene(JsonElement sceneElement);
}
