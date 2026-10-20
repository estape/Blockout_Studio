using System.Text.Json.Serialization;

namespace BlockoutStudio.Services.Assets;

/// <summary>
/// Native C++ compiler and SIMD optimization settings.
/// </summary>
public class CompilerSettings
{
    /// <summary>C++ language dialect standard (e.g. "c++20", "c++17").</summary>
    [JsonPropertyName("CppStandard")]
    public string CppStandard { get; set; } = "c++20";

    /// <summary>Compiler optimization level flag (e.g. "O2", "O3", "Od").</summary>
    [JsonPropertyName("OptimizationLevel")]
    public string OptimizationLevel { get; set; } = "O2";

    /// <summary>Whether hardware SIMD vector extensions (AVX2/NEON) are enabled.</summary>
    [JsonPropertyName("EnableSimd")]
    public bool EnableSimd { get; set; } = true;
}

/// <summary>
/// Root manifest schema stored in ProjectSettings/ProjectManifest.json defining project identity and target platforms.
/// </summary>
public class ProjectManifest
{
    /// <summary>Target engine specification version.</summary>
    [JsonPropertyName("EngineVersion")]
    public string EngineVersion { get; set; } = "1.0.0";

    /// <summary>Human-readable project title.</summary>
    [JsonPropertyName("ProjectName")]
    public string ProjectName { get; set; } = "BlockoutProject";

    /// <summary>Virtual path of the initial default scene loaded on boot.</summary>
    [JsonPropertyName("DefaultScene")]
    public string DefaultScene { get; set; } = "/Game/Scenes/Main.bscene";

    /// <summary>Default scalability tier preset name.</summary>
    [JsonPropertyName("ScalabilityTier")]
    public string ScalabilityTier { get; set; } = "DesktopHigh";

    /// <summary>List of supported graphics and platform SDKs targeted by this project.</summary>
    [JsonPropertyName("TargetSDKs")]
    public List<string> TargetSDKs { get; set; } = new() { "Vulkan", "DirectX12", "Android" };

    /// <summary>C++ native build and compilation configuration.</summary>
    [JsonPropertyName("CompilerSettings")]
    public CompilerSettings CompilerSettings { get; set; } = new();
}

/// <summary>
/// Mapping between a logical action event and hardware keyboard keys.
/// </summary>
public class ActionMapping
{
    /// <summary>Identifier for the action event (e.g. "Jump", "MoveForward").</summary>
    public string ActionName { get; set; } = "";

    /// <summary>Key bindings triggering this action.</summary>
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// Project input bindings schema stored in ProjectSettings/Input.json.
/// </summary>
public class InputSettings
{
    /// <summary>Registered action mappings for movement, camera, and tools.</summary>
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

/// <summary>
/// Global physics simulation properties stored in ProjectSettings/Physics.json.
/// </summary>
public class PhysicsSettings
{
    /// <summary>Vertical gravity acceleration in cm/s^2 (default -980.0).</summary>
    public float GravityZ { get; set; } = -980.0f;

    /// <summary>Velocity threshold below which elastic bouncing ceases.</summary>
    public float BounceThreshold { get; set; } = 200.0f;

    /// <summary>Default physical material name assigned to untagged colliders.</summary>
    public string DefaultPhysicalMaterial { get; set; } = "PM_Default";
}

/// <summary>
/// Scalability tier preset defining graphics fidelity, shader complexity, and target performance.
/// </summary>
public class QualityTierPreset
{
    /// <summary>Tier index (0 = MobileLow, 1 = MobileHigh, 2 = DesktopPC, 3 = Cinematic).</summary>
    public int TierIndex { get; set; }

    /// <summary>Internal tier programmatic identifier.</summary>
    public string TierName { get; set; } = "";

    /// <summary>User-facing display name.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Internal render resolution scale multiplier.</summary>
    public float ResolutionScale { get; set; } = 1.0f;

    /// <summary>Shadow map atlas texture resolution in pixels.</summary>
    public int ShadowResolution { get; set; } = 2048;

    /// <summary>Maximum number of shadow-casting dynamic point/spot lights.</summary>
    public int MaxDynamicShadowLights { get; set; } = 1;

    /// <summary>Number of hemisphere ray samples for ambient occlusion baking.</summary>
    public int AoSamples { get; set; } = 32;

    /// <summary>Shader illumination model.</summary>
    public string ShaderModel { get; set; } = "FullPBR";

    /// <summary>GLSL float precision qualifier (e.g. "mediump", "highp").</summary>
    public string Precision { get; set; } = "highp";

    /// <summary>Anti-aliasing post-process filter technique.</summary>
    public string AntiAliasing { get; set; } = "FXAA";

    /// <summary>Active post-processing effects pipeline.</summary>
    public string PostProcessing { get; set; } = "None";

    /// <summary>Target refresh / frame rate in frames per second.</summary>
    public int TargetFps { get; set; } = 60;

    /// <summary>Whether ANGLE Vulkan translation layer is active.</summary>
    public bool EnableVulkanAngle { get; set; } = true;

    /// <summary>Whether vertex color stream stores high-precision 32-bit floats.</summary>
    public bool HighPrecisionVertexColors { get; set; } = true;

    /// <summary>Whether hardware instanced rendering is enabled for duplicate meshes.</summary>
    public bool EnableGpuInstancing { get; set; } = true;

    /// <summary>LOD transition distance bias multiplier.</summary>
    public float LodBias { get; set; } = 1.0f;
}

/// <summary>
/// Scalability configuration stored in ProjectSettings/Scalability.json.
/// </summary>
public class QualitySettings
{
    /// <summary>Currently active scalability tier name.</summary>
    public string ActiveTier { get; set; } = "DesktopPC";

    /// <summary>Currently active scalability tier numeric index.</summary>
    public int ActiveTierIndex { get; set; } = 2;

    /// <summary>List of predefined scalability presets.</summary>
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

/// <summary>
/// File system paths for discovered or manually configured external SDKs and toolchains.
/// </summary>
public class SdkPathsSettings
{
    /// <summary>Root folder path to the Vulkan SDK installation.</summary>
    public string? VulkanSdkPath { get; set; }

    /// <summary>Folder path to the MSVC toolset binary directory.</summary>
    public string? MsvcPath { get; set; }

    /// <summary>Path to the Windows SDK include/lib directory.</summary>
    public string? WindowsSdkPath { get; set; }

    /// <summary>Path to the Android SDK installation.</summary>
    public string? AndroidSdkPath { get; set; }

    /// <summary>Path to the Android NDK root folder.</summary>
    public string? AndroidNdkPath { get; set; }

    /// <summary>Path to GCC or Clang toolchain executable directory.</summary>
    public string? GccPath { get; set; }
}
