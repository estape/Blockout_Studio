using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Graphics;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for compiling GLSL shader code to SPIR-V and transpiling to WebGL ES.
/// </summary>
public class CompileShaderHandler : IIpcActionHandler
{
    /// <summary>Vulkan compute service instance.</summary>
    private readonly IVulkanComputeService _vulkanCompute;

    /// <inheritdoc/>
    public string Action => "compile_shader";

    /// <summary>Initializes handler with the Vulkan compute service.</summary>
    public CompileShaderHandler(IVulkanComputeService vulkanCompute)
    {
        _vulkanCompute = vulkanCompute;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string source = payload.TryGetProperty("source", out var sp) ? sp.GetString() ?? "" : "";
        string stageStr = payload.TryGetProperty("stage", out var stp) ? stp.GetString() ?? "Vertex" : "Vertex";
        bool optimize = !payload.TryGetProperty("optimize", out var op) || op.GetBoolean();

        var stage = stageStr.ToLowerInvariant() switch
        {
            "fragment" or "frag" => ShaderStage.Fragment,
            "compute" or "comp" => ShaderStage.Compute,
            _ => ShaderStage.Vertex
        };

        var result = _vulkanCompute.CompileAndTranspileShader(source, stage, optimize);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "compile_shader_response",
            success = result.Success,
            stage = result.Stage,
            spirvBase64 = result.SpirvBase64,
            spirvByteLength = result.SpirvByteLength,
            glslEsSource = result.GlslEsSource,
            error = result.Error,
            optimized = result.Optimized,
            elapsedMs = result.ElapsedMs
        }));

        return Task.CompletedTask;
    }
}
