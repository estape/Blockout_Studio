using System.Text.Json.Serialization;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Graphics;

/// <summary>
/// Specifies the programmable shader execution stage for glslc compilation and SPIR-V transpilation.
/// </summary>
public enum ShaderStage
{
    /// <summary>Vertex processing stage (.vert).</summary>
    Vertex,
    /// <summary>Fragment/pixel color evaluation stage (.frag).</summary>
    Fragment,
    /// <summary>General-purpose compute dispatch stage (.comp).</summary>
    Compute
}

/// <summary>
/// Represents the output of compiling or cross-compiling GLSL shaders through LunarG Vulkan SDK tools.
/// </summary>
public class ShaderCompilationResult
{
    /// <summary>Gets or sets whether compilation succeeded without errors.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the Base64-encoded SPIR-V binary bytecode.</summary>
    public string? SpirvBase64 { get; set; }

    /// <summary>Gets or sets the length of the SPIR-V binary buffer in bytes.</summary>
    public int SpirvByteLength { get; set; }

    /// <summary>Gets or sets the WebGL 2.0 compatible GLSL ES 3.00 source code transpiled via spirv-cross.</summary>
    public string? GlslEsSource { get; set; }

    /// <summary>Gets or sets error diagnostic messages if compilation failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the string name of the compiled shader stage.</summary>
    public string Stage { get; set; } = "Vertex";

    /// <summary>Gets or sets whether bytecode was optimized with spirv-opt.</summary>
    public bool Optimized { get; set; }

    /// <summary>Gets or sets the total elapsed execution duration in milliseconds.</summary>
    public double ElapsedMs { get; set; }
}

/// <summary>
/// Encapsulates the results of a raymarched per-vertex Ambient Occlusion baking run.
/// </summary>
public class VulkanAoBakeResult
{
    /// <summary>Gets or sets whether the baking pass completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets flat RGB vertex color array [r0, g0, b0, r1, g1, b1, ...].</summary>
    public float[] Colors { get; set; } = Array.Empty<float>();

    /// <summary>Gets or sets the hardware pipeline used (e.g. Vulkan Compute GPU, Native C++20 AVX2, or Managed C# SIMD).</summary>
    public string AccelerationMode { get; set; } = "C# SIMD Multi-Core";

    /// <summary>Gets or sets total bake computation time in milliseconds.</summary>
    public double ElapsedMs { get; set; }

    /// <summary>Gets or sets total count of vertices processed.</summary>
    public int VerticesProcessed { get; set; }

    /// <summary>Gets or sets total number of scene occlusion triangles evaluated.</summary>
    public int TrianglesTested { get; set; }

    /// <summary>Gets or sets error details if baking failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Manages Vulkan SDK probing, GLSL compilation, SPIR-V optimization, and compute-based AO baking.
/// </summary>
public interface IVulkanComputeService
{
    /// <summary>Gets whether a valid LunarG Vulkan SDK installation was detected.</summary>
    bool IsVulkanSdkAvailable { get; }

    /// <summary>Gets the detected Vulkan SDK version string (e.g., "1.4.363.0").</summary>
    string? VulkanVersion { get; }

    /// <summary>Gets the absolute root path to the installed Vulkan SDK.</summary>
    string? SdkPath { get; }

    /// <summary>Gets the absolute executable path to glslc.</summary>
    string? GlslcPath { get; }

    /// <summary>Gets the absolute executable path to spirv-opt.</summary>
    string? SpirvOptPath { get; }

    /// <summary>Gets the absolute executable path to spirv-cross.</summary>
    string? SpirvCrossPath { get; }

    /// <summary>
    /// Compiles raw GLSL source into SPIR-V binary bytecode.
    /// </summary>
    /// <param name="glslSource">GLSL shader text.</param>
    /// <param name="stage">Target shader stage.</param>
    /// <param name="optimize">Whether to invoke spirv-opt -O.</param>
    /// <returns>A <see cref="ShaderCompilationResult"/> containing bytecode or diagnostics.</returns>
    ShaderCompilationResult CompileGlslToSpirv(string glslSource, ShaderStage stage, bool optimize = true);

    /// <summary>
    /// Transpiles SPIR-V bytecode into WebGL 2.0 GLSL ES 3.00 using spirv-cross.
    /// </summary>
    /// <param name="spirvBytes">SPIR-V binary bytecode array.</param>
    /// <param name="stage">Shader stage.</param>
    /// <returns>A <see cref="ShaderCompilationResult"/> containing GLSL ES source.</returns>
    ShaderCompilationResult TranspileSpirvToGlslEs(byte[] spirvBytes, ShaderStage stage);

    /// <summary>
    /// Executes end-to-end compilation from GLSL to SPIR-V to optimized WebGL 2.0 GLSL ES 3.00.
    /// </summary>
    /// <param name="glslSource">Input GLSL code.</param>
    /// <param name="stage">Target stage.</param>
    /// <param name="optimize">Whether to apply spirv-opt optimizations.</param>
    /// <returns>Completed compilation and transpilation result.</returns>
    ShaderCompilationResult CompileAndTranspileShader(string glslSource, ShaderStage stage, bool optimize = true);

    /// <summary>
    /// Bakes Ambient Occlusion onto vertex colors, choosing the optimal available acceleration path.
    /// </summary>
    /// <param name="positions">Array of mesh vertex positions.</param>
    /// <param name="normals">Array of mesh vertex normals.</param>
    /// <param name="sceneTriangles">Collection of triangles forming the scene occlusion geometry.</param>
    /// <param name="sampleCount">Number of Fibonacci hemisphere rays cast per vertex.</param>
    /// <param name="maxDistance">Maximum ray distance considered for occlusion.</param>
    /// <returns>A <see cref="VulkanAoBakeResult"/> with computed vertex colors and metrics.</returns>
    VulkanAoBakeResult BakeVertexAo(
        float[] positions,
        float[] normals,
        List<SceneTriangle> sceneTriangles,
        int sampleCount = 32,
        float maxDistance = 120.0f);
}
