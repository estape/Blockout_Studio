using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.BuildPipeline;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler that triggers the automated standalone NativeAOT packaging pipeline.
/// </summary>
public class PackageStandaloneGameHandler : IIpcActionHandler
{
    private readonly IStandalonePackagingService _packagingService;
    private readonly IProjectStorageService _projectStorage;

    public PackageStandaloneGameHandler(
        IStandalonePackagingService packagingService,
        IProjectStorageService projectStorage)
    {
        _packagingService = packagingService;
        _projectStorage = projectStorage;
    }

    public string Action => "package_standalone_game";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string outputDir = Path.Combine(projectRoot, "Saved", "StagedBuilds", "Standalone");
        string config = "Release";
        string rid = "win-x64";
        bool skipAot = false;

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("projectRoot", out var pr) && !string.IsNullOrEmpty(pr.GetString()))
                projectRoot = pr.GetString()!;
            if (payload.TryGetProperty("outputDirectory", out var od) && !string.IsNullOrEmpty(od.GetString()))
                outputDir = od.GetString()!;
            if (payload.TryGetProperty("configuration", out var cfg) && !string.IsNullOrEmpty(cfg.GetString()))
                config = cfg.GetString()!;
            if (payload.TryGetProperty("runtimeIdentifier", out var r) && !string.IsNullOrEmpty(r.GetString()))
                rid = r.GetString()!;
            if (payload.TryGetProperty("skipNativeAotPublish", out var sa))
                skipAot = sa.GetBoolean();
        }

        var request = new StandalonePackageRequest(
            OutputDirectory: outputDir,
            Configuration: config,
            RuntimeIdentifier: rid,
            SkipNativeAotPublish: skipAot);

        void StreamLog(string line)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "package_standalone_game_log",
                message = line
            }));
        }

        var result = await _packagingService.PackageStandaloneGameAsync(projectRoot, request, StreamLog);

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "package_standalone_game_response",
            result
        }));
    }
}

/// <summary>
/// Class alias for PackageStandaloneGameHandler as requested by user architectural conventions.
/// </summary>
public class PSGH : PackageStandaloneGameHandler
{
    public PSGH(IStandalonePackagingService packagingService, IProjectStorageService projectStorage)
        : base(packagingService, projectStorage)
    {
    }
}
