using Microsoft.Extensions.DependencyInjection;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Geometry;
using BlockoutStudio.Services.Graphics;
using BlockoutStudio.Services.Ipc;
using BlockoutStudio.Services.Ipc.Handlers;
using BlockoutStudio.Services.NativeInterop;
using BlockoutStudio.Services.Toolchains;
using BlockoutStudio.Services.BuildPipeline;

namespace BlockoutStudio.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddEngineServices(this IServiceCollection services)
    {
        // Core Subsystem Services & Storage
        services.AddSingleton<IProjectStorageService, ProjectStorageService>();
        services.AddSingleton<ProjectStorageService>(sp => (ProjectStorageService)sp.GetRequiredService<IProjectStorageService>());
        services.AddSingleton<ICsgEngine, CsgEngine>();
        services.AddSingleton<CsgEngine>(sp => (CsgEngine)sp.GetRequiredService<ICsgEngine>());
        services.AddSingleton<IUsdStageService, UsdStageService>();
        services.AddSingleton<UsdStageService>(sp => (UsdStageService)sp.GetRequiredService<IUsdStageService>());
        services.AddSingleton<INativeInteropService, NativeInteropService>();
        services.AddSingleton<IToolchainDiscoveryService, ToolchainDiscoveryService>();
        services.AddSingleton<IAssetPipelineManager, AssetPipelineManager>();
        services.AddSingleton<IAssetDatabaseService, AssetDatabaseService>();
        services.AddSingleton<AssetDatabaseService>(sp => (AssetDatabaseService)sp.GetRequiredService<IAssetDatabaseService>());
        services.AddSingleton<IObjUsdPipelineService, ObjUsdPipelineService>();
        services.AddSingleton<ObjUsdPipelineService>(sp => (ObjUsdPipelineService)sp.GetRequiredService<IObjUsdPipelineService>());
        services.AddSingleton<IVulkanComputeService, VulkanComputeService>();
        services.AddSingleton<VulkanComputeService>(sp => (VulkanComputeService)sp.GetRequiredService<IVulkanComputeService>());
        services.AddSingleton<ILodGeneratorService, LodGeneratorService>();
        services.AddSingleton<LodGeneratorService>(sp => (LodGeneratorService)sp.GetRequiredService<ILodGeneratorService>());

        // IPC Handlers
        services.AddSingleton<IIpcActionHandler, CopyClipboardHandler>();
        services.AddSingleton<IIpcActionHandler, CsgCutHandler>();
        services.AddSingleton<IIpcActionHandler, BakeAoHandler>();
        services.AddSingleton<IIpcActionHandler, GetGridTextureHandler>();
        services.AddSingleton<IIpcActionHandler, SaveProjectHandler>();
        services.AddSingleton<IIpcActionHandler, LoadProjectHandler>();
        services.AddSingleton<IIpcActionHandler, ToolchainIpcHandler>();
        services.AddSingleton<IIpcActionHandler, ExportUsdHandler>();
        services.AddSingleton<IIpcActionHandler, ExportObjHandler>();
        services.AddSingleton<IIpcActionHandler, GetNativeStatusHandler>();

        // Asset & Project Management IPC Handlers
        services.AddSingleton<IIpcActionHandler, CreateProjectHandler>();
        services.AddSingleton<IIpcActionHandler, OpenProjectHandler>();
        services.AddSingleton<IIpcActionHandler, GetProjectStatusHandler>();
        services.AddSingleton<IIpcActionHandler, GetAssetRegistryHandler>();
        services.AddSingleton<IIpcActionHandler, ImportAssetHandler>();
        services.AddSingleton<IIpcActionHandler, ReimportAssetHandler>();
        services.AddSingleton<IIpcActionHandler, DeleteAssetHandler>();
        services.AddSingleton<IIpcActionHandler, ShowInExplorerHandler>();
        services.AddSingleton<IIpcActionHandler, GetAssetContentHandler>();

        // Native Geometry Pipeline IPC Handlers (Module 2)
        services.AddSingleton<IIpcActionHandler, LoadMeshBinaryHandler>();
        services.AddSingleton<IIpcActionHandler, SaveBlockoutAsObjHandler>();
        services.AddSingleton<IIpcActionHandler, SaveUsdStageHandler>();

        // Vulkan & Scalability IPC Handlers (Module 3)
        services.AddSingleton<IIpcActionHandler, CompileShaderHandler>();
        services.AddSingleton<IIpcActionHandler, GenerateLodsHandler>();
        services.AddSingleton<ScalabilityTierHandler>();
        services.AddSingleton<IIpcActionHandler>(sp => sp.GetRequiredService<ScalabilityTierHandler>());
        services.AddSingleton<IIpcActionHandler, GetScalabilityTiersHandler>();

        // Build Pipeline & Native SDK Services (Module 5)
        services.AddSingleton<ISdkDetectorService, SdkDetectorService>();
        services.AddSingleton<SdkDetectorService>(sp => (SdkDetectorService)sp.GetRequiredService<ISdkDetectorService>());
        services.AddSingleton<INativeCompilerService, NativeCompilerService>();
        services.AddSingleton<NativeCompilerService>(sp => (NativeCompilerService)sp.GetRequiredService<INativeCompilerService>());
        services.AddSingleton<IPlatformBuilderService, PlatformBuilderService>();
        services.AddSingleton<PlatformBuilderService>(sp => (PlatformBuilderService)sp.GetRequiredService<IPlatformBuilderService>());
        services.AddSingleton<IIpcActionHandler, GetSdkStatusHandler>();
        services.AddSingleton<IIpcActionHandler, SetSdkPathsHandler>();
        services.AddSingleton<IIpcActionHandler, CompileNativeModuleHandler>();
        services.AddSingleton<IIpcActionHandler, BuildProjectHandler>();
        services.AddSingleton<IIpcActionHandler, GetAdbDevicesHandler>();
        services.AddSingleton<IIpcActionHandler, DeployToDeviceHandler>();

        // IPC Router
        services.AddSingleton<IIpcRouter, IpcRouter>();

        return services;
    }

    public static IServiceProvider BuildEngineServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEngineServices();
        return services.BuildServiceProvider();
    }
}
