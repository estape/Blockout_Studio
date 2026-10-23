using System.Text.Json.Serialization;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Graphics;

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
