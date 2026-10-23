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

/// <summary>
/// Application entry point configuring ANGLE Vulkan runtime, DI container, and Photino native desktop window.
/// </summary>
internal class Program
{
    /// <summary>
    /// Main executable entry point.
    /// </summary>
    /// <param name="args">Command-line arguments. Accepts '--test' to run automated unit verification tests.</param>
    /// <returns>Process exit code (0 for success).</returns>
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            return AutomatedTests.Run();
        }

        if (args.Length > 0 && args[0] == "--export-sample-usd")
        {
            string outDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "SampleUSD");
            return ExportSampleUsdFiles(outDir);
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
        var nativeVulkanService = serviceProvider.GetRequiredService<INativeVulkanViewportService>();

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

        // Attach native Vulkan interop bridge to hook window creation, resize, and close
        nativeVulkanService.AttachToPhotino(window);

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

    /// <summary>
    /// Exports representative sample OpenUSD (.usda) stages for validation against official OpenUSD specifications.
    /// </summary>
    /// <param name="outDir">Destination directory for generated USDA files.</param>
    /// <returns>Process exit code (0 for success).</returns>
    private static int ExportSampleUsdFiles(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Console.WriteLine($"Exporting sample OpenUSD (.usda) files to: {outDir}");

        // 1. Export sample_blockout_mesh.usda using UsdPipeline
        var usdPipe = new UsdPipeline();
        var meshModel = new ModelData
        {
            SceneName = "Modular_Room_Mesh",
            Parts = [
                new ModelMeshPart
                {
                    Name = "SM_Floor_01",
                    Positions = [
                        -200, 0, -200,   200, 0, -200,   200, 0, 200,
                        -200, 0, -200,   200, 0, 200,   -200, 0, 200
                    ],
                    Normals = [
                        0, 1, 0,  0, 1, 0,  0, 1, 0,
                        0, 1, 0,  0, 1, 0,  0, 1, 0
                    ],
                    UVs = [
                        0, 0,  2, 0,  2, 2,
                        0, 0,  2, 2,  0, 2
                    ],
                    Indices = [0, 1, 2, 3, 4, 5]
                },
                new ModelMeshPart
                {
                    Name = "SM_Wall_North",
                    Positions = [
                        -200, 0, 200,   200, 0, 200,   200, 300, 200,
                        -200, 0, 200,   200, 300, 200,  -200, 300, 200
                    ],
                    Normals = [
                        0, 0, -1,  0, 0, -1,  0, 0, -1,
                        0, 0, -1,  0, 0, -1,  0, 0, -1
                    ],
                    UVs = [
                        0, 0,  4, 0,  4, 3,
                        0, 0,  4, 3,  0, 3
                    ],
                    Indices = [0, 1, 2, 3, 4, 5]
                }
            ]
        };
        var meshUsda = usdPipe.ExportToString(meshModel);
        var meshPath = Path.Combine(outDir, "sample_blockout_mesh.usda");
        File.WriteAllText(meshPath, meshUsda);
        Console.WriteLine($" -> Wrote {meshPath}");

        // 2. Export sample_lighting_rig.usda (Modern UsdLux schemas)
        var lightSb = new System.Text.StringBuilder();
        lightSb.AppendLine("#usda 1.0");
        lightSb.AppendLine("(");
        lightSb.AppendLine("    doc = \"Lighting Rig sublayer for Level Arena\"");
        lightSb.AppendLine("    defaultPrim = \"Lights\"");
        lightSb.AppendLine("    metersPerUnit = 0.01");
        lightSb.AppendLine("    upAxis = \"Z\"");
        lightSb.AppendLine(")");
        lightSb.AppendLine();
        lightSb.AppendLine("def Xform \"Lights\"");
        lightSb.AppendLine("{");
        lightSb.AppendLine("    def SphereLight \"KeyPointLight\"");
        lightSb.AppendLine("    {");
        lightSb.AppendLine("        float inputs:intensity = 5000.0");
        lightSb.AppendLine("        color3f inputs:color = (1.0, 0.95, 0.8)");
        lightSb.AppendLine("        float inputs:radius = 25.0");
        lightSb.AppendLine("        bool treatAsPoint = true");
        lightSb.AppendLine("        double3 xformOp:translate = (0.0, 0.0, 250.0)");
        lightSb.AppendLine("        uniform token[] xformOpOrder = [\"xformOp:translate\"]");
        lightSb.AppendLine("    }");
        lightSb.AppendLine();
        lightSb.AppendLine("    def DistantLight \"SunLight\"");
        lightSb.AppendLine("    {");
        lightSb.AppendLine("        float inputs:intensity = 1000.0");
        lightSb.AppendLine("        color3f inputs:color = (1.0, 0.98, 0.92)");
        lightSb.AppendLine("        float inputs:angle = 0.53");
        lightSb.AppendLine("        float3 xformOp:rotateXYZ = (-45.0, 30.0, 0.0)");
        lightSb.AppendLine("        uniform token[] xformOpOrder = [\"xformOp:rotateXYZ\"]");
        lightSb.AppendLine("    }");
        lightSb.AppendLine("}");
        var lightPath = Path.Combine(outDir, "sample_lighting_rig.usda");
        File.WriteAllText(lightPath, lightSb.ToString());
        Console.WriteLine($" -> Wrote {lightPath}");

        // 3. Export sample_composition_stage.usda using UsdStageService
        var usdService = new UsdStageService();
        var stage = usdService.CreateStage("Level_Arena", 0.01f, "Z");
        stage.Sublayers.Add("./sample_lighting_rig.usda");

        // Reference modular mesh
        var pillarPrim = usdService.AddReference(
            stage,
            "Cover_Pillar_01",
            "./sample_blockout_mesh.usda",
            new System.Numerics.Vector3(50, 50, 0),
            new System.Numerics.Vector3(0, 0, 45),
            new System.Numerics.Vector3(1, 1, 1),
            "/World/Materials/M_Concrete"
        );

        usdService.AddVariantSet(
            pillarPrim,
            "DamageState",
            new Dictionary<string, string> { { "Clean", "" }, { "Damaged", "" } },
            "Clean"
        );

        // Apply non-destructive over opinion overriding SunLight from the lighting sublayer
        usdService.ApplyOverOpinion(
            stage,
            "SunLight",
            new System.Numerics.Vector3(0, 0, 500),
            new System.Numerics.Vector3(-60.0f, 45.0f, 0.0f),
            null
        );

        var compUsda = usdService.ExportToUsdaString(stage);
        var compPath = Path.Combine(outDir, "sample_composition_stage.usda");
        File.WriteAllText(compPath, compUsda);
        Console.WriteLine($" -> Wrote {compPath}");

        Console.WriteLine("All sample USD stages exported successfully.");
        return 0;
    }
}
