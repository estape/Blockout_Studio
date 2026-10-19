using Microsoft.Extensions.DependencyInjection;
using Photino.NET;
using BlockoutStudio.Services;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.BuildPipeline;
using BlockoutStudio.Services.Geometry;
using BlockoutStudio.Services.Graphics;
using BlockoutStudio.Services.Hosting;
using BlockoutStudio.Services.Ipc;

namespace BlockoutStudio;

internal class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            return AutomatedTests.Run();
        }

        // 1. Initialize Vulkan ANGLE backend for WebView2 before any window creation
        var angleStatus = VulkanAngleConfigurator.Configure();

        // 2. Build Dependency Injection Container & Resolve Subsystems
        var serviceProvider = ServiceRegistration.BuildEngineServiceProvider();
        var ipcRouter = serviceProvider.GetRequiredService<IIpcRouter>();
        var assetDb = serviceProvider.GetRequiredService<IAssetDatabaseService>();
        var objUsdPipeline = serviceProvider.GetRequiredService<IObjUsdPipelineService>();
        var usdStageService = serviceProvider.GetRequiredService<IUsdStageService>();
        var vulkanCompute = serviceProvider.GetRequiredService<IVulkanComputeService>();
        var sdkDetector = serviceProvider.GetRequiredService<ISdkDetectorService>();
        var nativeCompiler = serviceProvider.GetRequiredService<INativeCompilerService>();
        var platformBuilder = serviceProvider.GetRequiredService<IPlatformBuilderService>();
        var projectStorage = serviceProvider.GetRequiredService<IProjectStorageService>();
        var csgEngine = serviceProvider.GetRequiredService<ICsgEngine>();

        string backendTitleSuffix = angleStatus == VulkanBackendStatus.VulkanAngleActive
            ? " [Vulkan ANGLE WebGL 2.0]"
            : " [D3D11 ANGLE WebGL 2.0]";

        // 3. Configure Photino Window Host
        var window = new PhotinoWindow()
            .SetTitle($"Blockout Studio — 3D Level & Prototyping Engine (.NET 10){backendTitleSuffix}")
            .SetUseOsDefaultSize(false)
            .SetSize(1440, 900)
            .SetMinSize(1024, 640)
            .Center()
            .SetResizable(true);

        if (!string.IsNullOrEmpty(VulkanAngleConfigurator.ActiveBrowserArguments))
        {
            window.SetBrowserControlInitParameters(VulkanAngleConfigurator.ActiveBrowserArguments);
        }

        // 4. Initialize Default Project Workspace & Asset Database
        var defaultProjectRoot = Path.Combine(ProjectStorageService.DefaultProjectsDir, "DefaultProject");
        var (projectRoot, _) = projectStorage.OpenProject(defaultProjectRoot);
        assetDb.Initialize(projectRoot, window);

        // 5. Delegate all IPC messages to DI-resolved IPC Router
        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            _ = ipcRouter.DispatchAsync(message, window);
        });

        // 6. Load WebGL 2.0 Frontend Shell
        var indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(indexPath))
        {
            window.Load(indexPath);
        }
        else
        {
            window.Load("wwwroot/index.html");
        }

        window.WaitForClose();
        return 0;
    }
}
