using System.Text.Json;
using Photino.NET;
using UE5BlockoutStudio.Services.Assets;
using UE5BlockoutStudio.Services.BuildPipeline;

namespace UE5BlockoutStudio.Services.Ipc.Handlers;

public class GetSdkStatusHandler : IIpcActionHandler
{
    private readonly ISdkDetectorService _sdkDetector;

    public GetSdkStatusHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    public string Action => "get_sdk_status";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? projectRoot = null;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("projectRoot", out var prElem))
        {
            projectRoot = prElem.GetString();
        }

        var report = _sdkDetector.DetectAllSdks(projectRoot);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "sdk_status_response",
            report
        }));
        return Task.CompletedTask;
    }
}

public class SetSdkPathsHandler : IIpcActionHandler
{
    private readonly ISdkDetectorService _sdkDetector;

    public SetSdkPathsHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    public string Action => "set_sdk_paths";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? projectRoot = null;
        SdkPathsSettings paths = new();

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectRoot", out var prElem))
            {
                projectRoot = prElem.GetString();
            }
            if (payload.TryGetProperty("paths", out var pElem))
            {
                paths = JsonSerializer.Deserialize<SdkPathsSettings>(pElem.GetRawText()) ?? new();
            }
        }

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            _sdkDetector.SaveCustomSdkPaths(projectRoot, paths);
        }

        var report = _sdkDetector.DetectAllSdks(projectRoot);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "sdk_paths_saved",
            success = true,
            report
        }));
        return Task.CompletedTask;
    }
}

public class CompileNativeModuleHandler : IIpcActionHandler
{
    private readonly INativeCompilerService _compiler;

    public CompileNativeModuleHandler(INativeCompilerService compiler)
    {
        _compiler = compiler;
    }

    public string Action => "compile_native_module";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string projectRoot = "";
        CompilationOptions options = new();

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectRoot", out var prElem))
            {
                projectRoot = prElem.GetString() ?? "";
            }
            if (payload.TryGetProperty("options", out var optElem))
            {
                options = JsonSerializer.Deserialize<CompilationOptions>(optElem.GetRawText()) ?? new();
            }
        }

        void StreamLog(string line)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "compiler_log",
                line
            }));
        }

        var result = await _compiler.CompileModuleAsync(projectRoot, options, StreamLog);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "compile_native_module_response",
            result
        }));
    }
}

public class BuildProjectHandler : IIpcActionHandler
{
    private readonly IPlatformBuilderService _builder;

    public BuildProjectHandler(IPlatformBuilderService builder)
    {
        _builder = builder;
    }

    public string Action => "build_project";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string projectRoot = "";
        BuildRequest request = new();

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectRoot", out var prElem))
            {
                projectRoot = prElem.GetString() ?? "";
            }
            if (payload.TryGetProperty("request", out var reqElem))
            {
                request = JsonSerializer.Deserialize<BuildRequest>(reqElem.GetRawText()) ?? new();
            }
        }

        void StreamLog(string line)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "build_log",
                line
            }));
        }

        var result = await _builder.BuildProjectAsync(projectRoot, request, StreamLog);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "build_project_response",
            result
        }));
    }
}

public class GetAdbDevicesHandler : IIpcActionHandler
{
    private readonly ISdkDetectorService _sdkDetector;

    public GetAdbDevicesHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    public string Action => "get_adb_devices";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string? customAdb = null;
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("customAdbPath", out var pElem))
        {
            customAdb = pElem.GetString();
        }

        var devices = _sdkDetector.GetConnectedAdbDevices(customAdb);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "adb_devices_response",
            devices
        }));
        return Task.CompletedTask;
    }
}

public class DeployToDeviceHandler : IIpcActionHandler
{
    private readonly IPlatformBuilderService _builder;

    public DeployToDeviceHandler(IPlatformBuilderService builder)
    {
        _builder = builder;
    }

    public string Action => "deploy_to_device";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string projectRoot = "";
        string? deviceId = null;

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectRoot", out var prElem))
            {
                projectRoot = prElem.GetString() ?? "";
            }
            if (payload.TryGetProperty("deviceId", out var dElem))
            {
                deviceId = dElem.GetString();
            }
        }

        void StreamLog(string line)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "deploy_log",
                line
            }));
        }

        var success = await _builder.DeployToDeviceAsync(projectRoot, deviceId, StreamLog);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "deploy_to_device_response",
            success
        }));
    }
}
