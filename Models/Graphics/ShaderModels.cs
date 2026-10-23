using System.Text.Json.Serialization;

namespace BlockoutStudio.Models.Graphics;

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

    /// <summary>Gets or sets error description if baking failed.</summary>
    public string? Error { get; set; }
}

