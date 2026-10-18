using System.Text.Json.Serialization;

namespace UE5BlockoutStudio.Services.Assets;

public class CompilerSettings
{
    [JsonPropertyName("CppStandard")]
    public string CppStandard { get; set; } = "c++20";

    [JsonPropertyName("OptimizationLevel")]
    public string OptimizationLevel { get; set; } = "O2";

    [JsonPropertyName("EnableSimd")]
    public bool EnableSimd { get; set; } = true;
}

public class ProjectManifest
{
    [JsonPropertyName("EngineVersion")]
    public string EngineVersion { get; set; } = "1.0.0";

    [JsonPropertyName("ProjectName")]
    public string ProjectName { get; set; } = "BlockoutProject";

    [JsonPropertyName("DefaultScene")]
    public string DefaultScene { get; set; } = "/Game/Scenes/Main.bscene";

    [JsonPropertyName("ScalabilityTier")]
    public string ScalabilityTier { get; set; } = "DesktopHigh";

    [JsonPropertyName("TargetSDKs")]
    public List<string> TargetSDKs { get; set; } = new() { "Vulkan", "DirectX12", "Android" };

    [JsonPropertyName("CompilerSettings")]
    public CompilerSettings CompilerSettings { get; set; } = new();
}

public class ActionMapping
{
    public string ActionName { get; set; } = "";
    public List<string> Keys { get; set; } = new();
}

public class InputSettings
{
    public List<ActionMapping> Actions { get; set; } = new()
    {
        new() { ActionName = "MoveForward", Keys = new() { "W", "Up" } },
        new() { ActionName = "MoveBackward", Keys = new() { "S", "Down" } },
        new() { ActionName = "MoveLeft", Keys = new() { "A", "Left" } },
        new() { ActionName = "MoveRight", Keys = new() { "D", "Right" } },
        new() { ActionName = "Jump", Keys = new() { "Space" } },
        new() { ActionName = "ToggleContentDrawer", Keys = new() { "Ctrl+Space" } },
        new() { ActionName = "Undo", Keys = new() { "Ctrl+Z" } },
        new() { ActionName = "Redo", Keys = new() { "Ctrl+Y", "Ctrl+Shift+Z" } }
    };
}

public class PhysicsSettings
{
    public float GravityZ { get; set; } = -980.0f; // UE5 centimeters / s^2
    public float BounceThreshold { get; set; } = 200.0f;
    public string DefaultPhysicalMaterial { get; set; } = "PM_Default";
}

public class QualityTierPreset
{
    public int TierIndex { get; set; }
    public string TierName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float ResolutionScale { get; set; } = 1.0f;
    public int ShadowResolution { get; set; } = 2048;
    public int MaxDynamicShadowLights { get; set; } = 1;
    public int AoSamples { get; set; } = 32;
    public string ShaderModel { get; set; } = "FullPBR";
    public string Precision { get; set; } = "highp";
    public string AntiAliasing { get; set; } = "FXAA";
    public string PostProcessing { get; set; } = "None";
    public int TargetFps { get; set; } = 60;
    public bool EnableVulkanAngle { get; set; } = true;
    public bool HighPrecisionVertexColors { get; set; } = true;
    public bool EnableGpuInstancing { get; set; } = true;
    public float LodBias { get; set; } = 1.0f;
}

public class QualitySettings
{
    public string ActiveTier { get; set; } = "DesktopPC";
    public int ActiveTierIndex { get; set; } = 2;
    public List<QualityTierPreset> Tiers { get; set; } = new()
    {
        new()
        {
            TierIndex = 0,
            TierName = "MobileLow",
            DisplayName = "Tier 0 — Mobile Low (Tile-GPU Optimized)",
            ResolutionScale = 0.75f,
            ShadowResolution = 1024,
            MaxDynamicShadowLights = 1,
            AoSamples = 4,
            ShaderModel = "MatCap/Lambert + Baked Vertex AO",
            Precision = "mediump",
            AntiAliasing = "None",
            PostProcessing = "None",
            TargetFps = 30,
            EnableVulkanAngle = false,
            HighPrecisionVertexColors = false,
            EnableGpuInstancing = true,
            LodBias = 1.5f
        },
        new()
        {
            TierIndex = 1,
            TierName = "MobileHigh",
            DisplayName = "Tier 1 — Mobile High / Handheld",
            ResolutionScale = 1.0f,
            ShadowResolution = 1024,
            MaxDynamicShadowLights = 1,
            AoSamples = 16,
            ShaderModel = "PBR-Lite (mediump) + Baked Lightmaps",
            Precision = "mediump",
            AntiAliasing = "FXAA",
            PostProcessing = "ColorGrading",
            TargetFps = 60,
            EnableVulkanAngle = true,
            HighPrecisionVertexColors = true,
            EnableGpuInstancing = true,
            LodBias = 1.0f
        },
        new()
        {
            TierIndex = 2,
            TierName = "DesktopPC",
            DisplayName = "Tier 2 — Desktop PC",
            ResolutionScale = 1.0f,
            ShadowResolution = 2048,
            MaxDynamicShadowLights = 4,
            AoSamples = 32,
            ShaderModel = "Cook-Torrance PBR (highp)",
            Precision = "highp",
            AntiAliasing = "FXAA + ACES Tone Mapping",
            PostProcessing = "Bloom, SSAO, ACES",
            TargetFps = 60,
            EnableVulkanAngle = true,
            HighPrecisionVertexColors = true,
            EnableGpuInstancing = true,
            LodBias = 1.0f
        },
        new()
        {
            TierIndex = 3,
            TierName = "Cinematic",
            DisplayName = "Tier 3 — High-End PC & Console",
            ResolutionScale = 1.0f,
            ShadowResolution = 4096,
            MaxDynamicShadowLights = 8,
            AoSamples = 64,
            ShaderModel = "Full USD Streaming PBR",
            Precision = "highp",
            AntiAliasing = "TAA/SMAA",
            PostProcessing = "SSR, Volumetric Fog, ACES Filmic, Bloom",
            TargetFps = 120,
            EnableVulkanAngle = true,
            HighPrecisionVertexColors = true,
            EnableGpuInstancing = true,
            LodBias = 0.5f
        }
    };
}

public class SdkPathsSettings
{
    public string? VulkanSdkPath { get; set; }
    public string? MsvcPath { get; set; }
    public string? WindowsSdkPath { get; set; }
    public string? AndroidSdkPath { get; set; }
    public string? AndroidNdkPath { get; set; }
    public string? GccPath { get; set; }
}
