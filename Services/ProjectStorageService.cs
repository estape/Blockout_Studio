using System.Runtime.InteropServices;
using System.Text.Json;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services;

public class ProjectStorageService : IProjectStorageService
{
    public static readonly string DefaultProjectsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "BlockoutStudio"
    );

    string IProjectStorageService.DefaultProjectsDir => DefaultProjectsDir;
    string? IProjectStorageService.SaveProjectDialog(string jsonContent, string defaultFileName) => SaveProjectDialog(jsonContent, defaultFileName);
    (string? Path, string? Content) IProjectStorageService.LoadProjectDialog() => LoadProjectDialog();
    string? IProjectStorageService.CreateProjectDialog(string defaultProjectName) => CreateProjectDialog(defaultProjectName);
    (string ProjectRoot, ProjectManifest Manifest) IProjectStorageService.CreateNewProject(string rootDir, string projectName, string? starterSceneJson) => CreateNewProject(rootDir, projectName, starterSceneJson);
    (string ProjectRoot, ProjectManifest Manifest) IProjectStorageService.OpenProject(string projectPathOrDir) => OpenProject(projectPathOrDir);
    void IProjectStorageService.EnsureProjectStructure(string rootDir, ProjectManifest manifest, string? starterSceneJson) => EnsureProjectStructure(rootDir, manifest, starterSceneJson);

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static ProjectStorageService()
    {
        if (!Directory.Exists(DefaultProjectsDir))
        {
            Directory.CreateDirectory(DefaultProjectsDir);
        }
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string? filter = null;
        public string? customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string? file = null;
        public int maxFile = 0;
        public string? fileTitle = null;
        public int maxFileTitle = 0;
        public string? initialDir = null;
        public string? title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string? defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string? templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    public static string? SaveTextDialog(string content, string defaultFileName, string filter, string defExt, string title)
    {
        try
        {
            var ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = filter,
                file = defaultFileName + new string('\0', 260),
                maxFile = 260,
                initialDir = DefaultProjectsDir,
                title = title,
                defExt = defExt,
                flags = 0x00000002 | 0x00000004 // OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY
            };

            if (GetSaveFileName(ofn))
            {
                var chosenPath = ofn.file?.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(chosenPath))
                {
                    File.WriteAllText(chosenPath, content);
                    return chosenPath;
                }
            }
        }
        catch
        {
            var fallback = Path.Combine(DefaultProjectsDir, defaultFileName);
            File.WriteAllText(fallback, content);
            return fallback;
        }

        return null;
    }

    public static string? SaveProjectDialog(string jsonContent, string defaultFileName = "Level_Blockout.bproject")
    {
        return SaveTextDialog(
            jsonContent,
            defaultFileName,
            "Blockout Studio Project (*.bproject)\0*.bproject\0Blockout Scene (*.bscene)\0*.bscene\0All Files (*.*)\0*.*\0",
            "bproject",
            "Save Blockout Project"
        );
    }

    public static (string? Path, string? Content) LoadProjectDialog()
    {
        try
        {
            var ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = "Game Engine Projects (*.bengine;*.ueblock;*.bscene)\0*.bengine;*.ueblock;*.bscene\0All Files (*.*)\0*.*\0",
                file = new string('\0', 260),
                maxFile = 260,
                initialDir = DefaultProjectsDir,
                title = "Open Game Project or Scene",
                flags = 0x00001000 | 0x00000800 // OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST
            };

            if (GetOpenFileName(ofn))
            {
                var chosenPath = ofn.file?.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(chosenPath) && File.Exists(chosenPath))
                {
                    var text = File.ReadAllText(chosenPath);
                    return (chosenPath, text);
                }
            }
        }
        catch
        {
            // ignored
        }

        return (null, null);
    }

    public static string? CreateProjectDialog(string defaultProjectName = "NewGameProject")
    {
        try
        {
            var ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = "Blockout Engine Project (*.bengine)\0*.bengine\0All Files (*.*)\0*.*\0",
                file = "Project.bengine" + new string('\0', 260),
                maxFile = 260,
                initialDir = DefaultProjectsDir,
                title = "Select Folder & Name for New Project",
                defExt = "bengine",
                flags = 0x00000002 | 0x00000004
            };

            if (GetSaveFileName(ofn))
            {
                var chosenPath = ofn.file?.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(chosenPath))
                {
                    var rootDir = Path.GetDirectoryName(chosenPath) ?? DefaultProjectsDir;
                    var name = Path.GetFileName(rootDir);
                    if (string.IsNullOrWhiteSpace(name) || name == "BlockoutStudio")
                        name = defaultProjectName;
                    var (createdDir, _) = CreateNewProject(rootDir, name);
                    return createdDir;
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public static (string ProjectRoot, ProjectManifest Manifest) CreateNewProject(string rootDir, string projectName, string? starterSceneJson = null)
    {
        if (!Directory.Exists(rootDir))
        {
            Directory.CreateDirectory(rootDir);
        }

        var manifest = new ProjectManifest
        {
            EngineVersion = "1.0.0",
            ProjectName = projectName,
            DefaultScene = "/Game/Scenes/Main.bscene",
            ScalabilityTier = "DesktopHigh",
            TargetSDKs = new List<string> { "Vulkan", "DirectX12", "Android" }
        };

        EnsureProjectStructure(rootDir, manifest, starterSceneJson);
        return (rootDir, manifest);
    }

    public static (string ProjectRoot, ProjectManifest Manifest) OpenProject(string projectPathOrDir)
    {
        string rootDir;
        if (File.Exists(projectPathOrDir))
        {
            rootDir = Path.GetDirectoryName(projectPathOrDir) ?? DefaultProjectsDir;
        }
        else if (Directory.Exists(projectPathOrDir))
        {
            rootDir = projectPathOrDir;
        }
        else
        {
            rootDir = Path.Combine(DefaultProjectsDir, projectPathOrDir);
            if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);
        }

        var manifestPath = Path.Combine(rootDir, "Project.bengine");
        ProjectManifest manifest;
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(manifestPath)) ?? new ProjectManifest();
            }
            catch
            {
                manifest = new ProjectManifest { ProjectName = Path.GetFileName(rootDir) };
            }
        }
        else
        {
            manifest = new ProjectManifest { ProjectName = Path.GetFileName(rootDir) };
        }

        EnsureProjectStructure(rootDir, manifest);
        return (rootDir, manifest);
    }

    public static void EnsureProjectStructure(string rootDir, ProjectManifest manifest, string? starterSceneJson = null)
    {
        // 1. Core directories
        string[] folders = {
            Path.Combine(rootDir, "Assets", "Meshes"),
            Path.Combine(rootDir, "Assets", "Materials"),
            Path.Combine(rootDir, "Assets", "Shaders"),
            Path.Combine(rootDir, "Assets", "Textures"),
            Path.Combine(rootDir, "Assets", "Scenes"),
            Path.Combine(rootDir, "Assets", "Scripts"),
            Path.Combine(rootDir, "Assets", "Audio"),
            Path.Combine(rootDir, "Assets", "Prefabs"),
            Path.Combine(rootDir, "Assets", "NativePlugins"),
            Path.Combine(rootDir, "Binaries", "Win64"),
            Path.Combine(rootDir, "Binaries", "Linux64"),
            Path.Combine(rootDir, "Saved", "StagedBuilds", "Windows"),
            Path.Combine(rootDir, "Saved", "StagedBuilds", "Linux"),
            Path.Combine(rootDir, "Saved", "StagedBuilds", "Android"),
            Path.Combine(rootDir, "ProjectSettings"),
            Path.Combine(rootDir, "Library", "ArtifactCache"),
            Path.Combine(rootDir, "Intermediate"),
            Path.Combine(rootDir, "Saved", "Logs")
        };

        foreach (var dir in folders)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        // 2. Project.bengine manifest
        var manifestPath = Path.Combine(rootDir, "Project.bengine");
        if (!File.Exists(manifestPath))
        {
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
        }

        // 3. ProjectSettings/*.json
        var inputPath = Path.Combine(rootDir, "ProjectSettings", "Input.json");
        if (!File.Exists(inputPath))
        {
            File.WriteAllText(inputPath, JsonSerializer.Serialize(new InputSettings(), JsonOpts));
        }

        var physicsPath = Path.Combine(rootDir, "ProjectSettings", "Physics.json");
        if (!File.Exists(physicsPath))
        {
            File.WriteAllText(physicsPath, JsonSerializer.Serialize(new PhysicsSettings(), JsonOpts));
        }

        var qualityPath = Path.Combine(rootDir, "ProjectSettings", "QualityTiers.json");
        if (!File.Exists(qualityPath))
        {
            File.WriteAllText(qualityPath, JsonSerializer.Serialize(new QualitySettings(), JsonOpts));
        }

        var sdkPath = Path.Combine(rootDir, "ProjectSettings", "SDKPaths.json");
        if (!File.Exists(sdkPath))
        {
            File.WriteAllText(sdkPath, JsonSerializer.Serialize(new SdkPathsSettings(), JsonOpts));
        }

        // 4. Default Starter Scene
        var defaultScenePath = Path.Combine(rootDir, "Assets", "Scenes", "Main.bscene");
        if (!File.Exists(defaultScenePath))
        {
            var content = starterSceneJson ?? DefaultStarterSceneJson();
            File.WriteAllText(defaultScenePath, content);
        }

        // 5. Default Material & Shader presets
        var defaultMatPath = Path.Combine(rootDir, "Assets", "Materials", "M_PrototypeGrid.mat");
        if (!File.Exists(defaultMatPath))
        {
            File.WriteAllText(defaultMatPath, DefaultMaterialJson());
        }

        var defaultShaderPath = Path.Combine(rootDir, "Assets", "Shaders", "StandardPbr.glsl");
        if (!File.Exists(defaultShaderPath))
        {
            File.WriteAllText(defaultShaderPath, DefaultShaderContent());
        }

        // 6. Default Starter Scripts (Module 4 ECS)
        var scriptsDir = Path.Combine(rootDir, "Assets", "Scripts");
        var rotatorPath = Path.Combine(scriptsDir, "Rotator.js");
        if (!File.Exists(rotatorPath))
        {
            File.WriteAllText(rotatorPath, DefaultRotatorScriptContent());
        }

        var doorPath = Path.Combine(scriptsDir, "DoorTrigger.js");
        if (!File.Exists(doorPath))
        {
            File.WriteAllText(doorPath, DefaultDoorTriggerScriptContent());
        }

        var playerPath = Path.Combine(scriptsDir, "PlayerMovement.js");
        if (!File.Exists(playerPath))
        {
            File.WriteAllText(playerPath, DefaultPlayerMovementScriptContent());
        }

        // 7. Default Starter Native Plugin (Module 5 C++20)
        var pluginsDir = Path.Combine(rootDir, "Assets", "NativePlugins");
        var pluginPath = Path.Combine(pluginsDir, "GamePlugin.cpp");
        if (!File.Exists(pluginPath))
        {
            File.WriteAllText(pluginPath, BlockoutStudio.Services.BuildPipeline.NativeCompilerService.DefaultPluginSourceCode());
        }
    }

    private static string DefaultRotatorScriptContent()
    {
        return """
        // Rotator.js — Entity Continuous Rotation Script
        export default {
            speed: 45, // deg/sec
            onStart(entity, ctx) {
                this.speed = ctx.properties.speed || 45;
            },
            onUpdate(entity, dt, ctx) {
                entity.rotation.y += (this.speed * Math.PI / 180) * dt;
            },
            onCollisionEnter(entity, other, ctx) {
                ctx.playSound('impact');
            }
        };
        """;
    }

    private static string DefaultDoorTriggerScriptContent()
    {
        return """
        // DoorTrigger.js — Interactive Door Opening Script
        export default {
            isOpen: false,
            initialY: 0,
            onStart(entity, ctx) {
                this.isOpen = false;
                this.initialY = entity.position.y;
            },
            onUpdate(entity, dt, ctx) {
                const targetY = this.isOpen ? this.initialY + 250 : this.initialY;
                entity.position.y += (targetY - entity.position.y) * Math.min(1.0, 5.0 * dt);
            },
            onCollisionEnter(entity, other, ctx) {
                this.isOpen = !this.isOpen;
                ctx.playSound('chime');
            }
        };
        """;
    }

    private static string DefaultPlayerMovementScriptContent()
    {
        return """
        // PlayerMovement.js — Gameplay Event Hook Script
        export default {
            onStart(entity, ctx) {},
            onUpdate(entity, dt, ctx) {},
            onCollisionEnter(entity, other, ctx) {
                ctx.playSound('footstep');
            }
        };
        """;
    }

    private static string DefaultStarterSceneJson()
    {
        return """
        [
          {
            "name": "SM_Floor",
            "shapeType": "box",
            "mobility": "Static",
            "sizeX": 600,
            "sizeY": 600,
            "sizeZ": 20,
            "posX": 0,
            "posY": 0,
            "posZ": 0,
            "rotZ": 0
          },
          {
            "name": "SM_Wall_Back",
            "shapeType": "box",
            "mobility": "Static",
            "sizeX": 600,
            "sizeY": 20,
            "sizeZ": 300,
            "posX": 0,
            "posY": -290,
            "posZ": 20,
            "rotZ": 0
          },
          {
            "name": "SM_Ramp_Access",
            "shapeType": "ramp",
            "mobility": "Static",
            "sizeX": 150,
            "sizeY": 300,
            "sizeZ": 100,
            "posX": 150,
            "posY": 0,
            "posZ": 20,
            "rotZ": 0
          },
          {
            "name": "SM_CoverPillar",
            "shapeType": "cylinder",
            "mobility": "Static",
            "sizeX": 100,
            "sizeY": 100,
            "sizeZ": 200,
            "posX": -180,
            "posY": 0,
            "posZ": 20,
            "rotZ": 0
          },
          {
            "name": "PointLight_Key",
            "shapeType": "light_point",
            "mobility": "Stationary",
            "intensity": 5000,
            "radius": 1000,
            "color": "#ffd166",
            "sizeX": 30,
            "sizeY": 30,
            "sizeZ": 30,
            "posX": 0,
            "posY": 0,
            "posZ": 180,
            "rotZ": 0
          }
        ]
        """;
    }

    private static string DefaultMaterialJson()
    {
        return """
        {
          "Name": "M_PrototypeGrid",
          "BaseColor": "#4a5568",
          "Roughness": 0.6,
          "Metallic": 0.1,
          "GridSizeCm": 100.0,
          "Shader": "/Game/Shaders/StandardPbr"
        }
        """;
    }

    private static string DefaultShaderContent()
    {
        return """
        // Standard PBR Shader (WebGL 2.0 / Vulkan ANGLE Compatible)
        #version 300 es
        precision highp float;
        in vec3 vNormal;
        in vec2 vUv;
        out vec4 fragColor;
        uniform vec3 uColor;
        void main() {
            vec3 lightDir = normalize(vec3(0.5, 0.8, 1.0));
            float diff = max(dot(vNormal, lightDir), 0.2);
            fragColor = vec4(uColor * diff, 1.0);
        }
        """;
    }
}
