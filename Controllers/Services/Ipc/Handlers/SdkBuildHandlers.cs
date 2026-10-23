using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.BuildPipeline;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for querying overall SDK and toolchain environment status.
/// </summary>
public class GetSdkStatusHandler : IIpcActionHandler
{
    /// <summary>SDK detector service.</summary>
    private readonly ISdkDetectorService _sdkDetector;

    /// <summary>Initializes handler with the SDK detector service.</summary>
    public GetSdkStatusHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    /// <inheritdoc/>
    public string Action => "get_sdk_status";

    /// <inheritdoc/>
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

/// <summary>
/// IPC handler for configuring custom toolchain and SDK directory paths.
/// </summary>
public class SetSdkPathsHandler : IIpcActionHandler
{
    /// <summary>SDK detector service.</summary>
    private readonly ISdkDetectorService _sdkDetector;

    /// <summary>Initializes handler with the SDK detector service.</summary>
    public SetSdkPathsHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    /// <inheritdoc/>
    public string Action => "set_sdk_paths";

    /// <inheritdoc/>
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

/// <summary>
/// IPC handler for triggering compilation of C++ native plugins with live log streaming.
/// </summary>
public class CompileNativeModuleHandler : IIpcActionHandler
{
    /// <summary>Native compiler service.</summary>
    private readonly INativeCompilerService _compiler;

    /// <summary>Initializes handler with the native compiler service.</summary>
    public CompileNativeModuleHandler(INativeCompilerService compiler)
    {
        _compiler = compiler;
    }

    /// <inheritdoc/>
    public string Action => "compile_native_module";

    /// <inheritdoc/>
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

/// <summary>
/// IPC handler for packaging projects into platform builds with asset cooking and live logs.
/// </summary>
public class BuildProjectHandler : IIpcActionHandler
{
    /// <summary>Platform builder service.</summary>
    private readonly IPlatformBuilderService _builder;

    /// <summary>Initializes handler with the platform builder service.</summary>
    public BuildProjectHandler(IPlatformBuilderService builder)
    {
        _builder = builder;
    }

    /// <inheritdoc/>
    public string Action => "build_project";

    /// <inheritdoc/>
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

/// <summary>
/// IPC handler for listing connected Android devices via ADB.
/// </summary>
public class GetAdbDevicesHandler : IIpcActionHandler
{
    /// <summary>SDK detector service.</summary>
    private readonly ISdkDetectorService _sdkDetector;

    /// <summary>Initializes handler with the SDK detector service.</summary>
    public GetAdbDevicesHandler(ISdkDetectorService sdkDetector)
    {
        _sdkDetector = sdkDetector;
    }

    /// <inheritdoc/>
    public string Action => "get_adb_devices";

    /// <inheritdoc/>
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

/// <summary>
/// IPC handler for deploying and launching built APK packages on an Android device via ADB.
/// </summary>
public class DeployToDeviceHandler : IIpcActionHandler
{
    /// <summary>Platform builder service.</summary>
    private readonly IPlatformBuilderService _builder;

    /// <summary>Initializes handler with the platform builder service.</summary>
    public DeployToDeviceHandler(IPlatformBuilderService builder)
    {
        _builder = builder;
    }

    /// <inheritdoc/>
    public string Action => "deploy_to_device";

    /// <inheritdoc/>
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
