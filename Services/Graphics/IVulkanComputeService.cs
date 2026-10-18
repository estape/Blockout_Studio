using System.Text.Json.Serialization;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio.Services.Graphics;

public enum ShaderStage
{
    Vertex,
    Fragment,
    Compute
}

public class ShaderCompilationResult
{
    public bool Success { get; set; }
    public string? SpirvBase64 { get; set; }
    public int SpirvByteLength { get; set; }
    public string? GlslEsSource { get; set; }
    public string? Error { get; set; }
    public string Stage { get; set; } = "Vertex";
    public bool Optimized { get; set; }
    public double ElapsedMs { get; set; }
}

public class VulkanAoBakeResult
{
    public bool Success { get; set; }
    public float[] Colors { get; set; } = Array.Empty<float>();
    public string AccelerationMode { get; set; } = "C# SIMD Multi-Core";
    public double ElapsedMs { get; set; }
    public int VerticesProcessed { get; set; }
    public int TrianglesTested { get; set; }
    public string? Error { get; set; }
}

public interface IVulkanComputeService
{
    bool IsVulkanSdkAvailable { get; }
    string? VulkanVersion { get; }
    string? SdkPath { get; }
    string? GlslcPath { get; }
    string? SpirvOptPath { get; }
    string? SpirvCrossPath { get; }

    ShaderCompilationResult CompileGlslToSpirv(string glslSource, ShaderStage stage, bool optimize = true);
    ShaderCompilationResult TranspileSpirvToGlslEs(byte[] spirvBytes, ShaderStage stage);
    ShaderCompilationResult CompileAndTranspileShader(string glslSource, ShaderStage stage, bool optimize = true);

    VulkanAoBakeResult BakeVertexAo(
        float[] positions,
        float[] normals,
        List<SceneTriangle> sceneTriangles,
        int sampleCount = 32,
        float maxDistance = 120.0f);
}
