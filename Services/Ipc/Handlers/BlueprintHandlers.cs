using System.Numerics;
using System.Text.Json;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Blueprints.Compiler;
using BlockoutStudio.Services.Blueprints.Models;
using BlockoutStudio.Services.Blueprints.Runtime;
using BlockoutStudio.Services.Blueprints.Storage;
using BlockoutStudio.Services.Blueprints.Transpiler;
using Photino.NET;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler to enumerate all .bprint assets in the project workspace.
/// </summary>
public class ListBlueprintsHandler : IIpcActionHandler
{
    private readonly IBlueprintStorageService _storage;
    private readonly IAssetDatabaseService _assetDb;

    public ListBlueprintsHandler(IBlueprintStorageService storage, IAssetDatabaseService assetDb)
    {
        _storage = storage;
        _assetDb = assetDb;
    }

    public string Action => "list_blueprints";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var rootDir = _assetDb.CurrentProjectRoot ?? Directory.GetCurrentDirectory();
        var list = await _storage.ListBlueprintsAsync(rootDir);

        window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "list_blueprints_response",
                action = "list_blueprints",
                success = true,
                blueprints = list
            }));
    }
}

/// <summary>
/// IPC handler to load and parse a Blueprint graph JSON document.
/// </summary>
public class LoadBlueprintHandler : IIpcActionHandler
{
    private readonly IBlueprintStorageService _storage;
    private readonly IAssetDatabaseService _assetDb;

    public LoadBlueprintHandler(IBlueprintStorageService storage, IAssetDatabaseService assetDb)
    {
        _storage = storage;
        _assetDb = assetDb;
    }

    public string Action => "load_blueprint";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var name = payload.TryGetProperty("name", out var np) ? np.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "load_blueprint_response",
                action = "load_blueprint",
                success = false,
                message = "Blueprint name is required."
            }));
            return;
        }

        var projectRoot = _assetDb.CurrentProjectRoot ?? Directory.GetCurrentDirectory();
        var graph = await _storage.LoadBlueprintAsync(projectRoot, name);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "load_blueprint_response",
            action = "load_blueprint",
            success = graph != null,
            blueprint = graph,
            message = graph == null ? $"Blueprint '{name}' not found." : null
        }));
    }
}

/// <summary>
/// IPC handler to save a Blueprint graph and emit its transpiled C# file.
/// </summary>
public class SaveBlueprintHandler : IIpcActionHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IBlueprintStorageService _storage;
    private readonly IAssetDatabaseService _assetDb;

    public SaveBlueprintHandler(IBlueprintStorageService storage, IAssetDatabaseService assetDb)
    {
        _storage = storage;
        _assetDb = assetDb;
    }

    public string Action => "save_blueprint";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        if (!payload.TryGetProperty("graph", out var graphEl))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "save_blueprint_response",
                action = "save_blueprint",
                success = false,
                message = "Missing graph payload."
            }));
            return;
        }

        try
        {
            var graph = JsonSerializer.Deserialize<BlueprintGraph>(graphEl.GetRawText(), JsonOpts);
            if (graph == null)
            {
                window.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "save_blueprint_response",
                    action = "save_blueprint",
                    success = false,
                    message = "Invalid graph serialization."
                }));
                return;
            }

            var projectRoot = _assetDb.CurrentProjectRoot ?? Directory.GetCurrentDirectory();
            var res = await _storage.SaveBlueprintAsync(projectRoot, graph);
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "save_blueprint_response",
                action = "save_blueprint",
                success = res.Success,
                blueprintPath = res.BlueprintPath,
                csharpPath = res.GeneratedCSharpPath,
                message = res.ErrorMessage
            }));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "save_blueprint_response",
                action = "save_blueprint",
                success = false,
                message = ex.Message
            }));
        }
    }
}

/// <summary>
/// IPC handler to generate C# source preview without requiring a disk write.
/// </summary>
public class TranspileBlueprintHandler : IIpcActionHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IBlueprintTranspiler _transpiler;

    public TranspileBlueprintHandler(IBlueprintTranspiler transpiler)
    {
        _transpiler = transpiler;
    }

    public string Action => "transpile_blueprint";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        if (!payload.TryGetProperty("graph", out var graphEl))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "transpile_blueprint_response",
                action = "transpile_blueprint",
                success = false,
                message = "Missing graph payload."
            }));
            return Task.CompletedTask;
        }

        try
        {
            var graph = JsonSerializer.Deserialize<BlueprintGraph>(graphEl.GetRawText(), JsonOpts);
            if (graph == null)
            {
                window.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "transpile_blueprint_response",
                    action = "transpile_blueprint",
                    success = false,
                    message = "Invalid graph JSON."
                }));
                return Task.CompletedTask;
            }

            var csharp = _transpiler.TranspileToCSharp(graph);
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "transpile_blueprint_response",
                action = "transpile_blueprint",
                success = true,
                csharpCode = csharp
            }));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "transpile_blueprint_response",
                action = "transpile_blueprint",
                success = false,
                message = ex.Message
            }));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// IPC handler to compile C# source code via Roslyn and report diagnostic errors/warnings.
/// </summary>
public class CompileBlueprintHandler : IIpcActionHandler
{
    private readonly IBlueprintCompilerService _compiler;

    public CompileBlueprintHandler(IBlueprintCompilerService compiler)
    {
        _compiler = compiler;
    }

    public string Action => "compile_blueprint";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var code = payload.TryGetProperty("csharpCode", out var cp) ? cp.GetString() : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            window.SendWebMessage(JsonSerializer.Serialize(new
            {
                type = "compile_blueprint_response",
                action = "compile_blueprint",
                success = false,
                message = "No C# code provided."
            }));
            return Task.CompletedTask;
        }

        var res = _compiler.CompileSource(code);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "compile_blueprint_response",
            action = "compile_blueprint",
            success = res.Success,
            diagnostics = res.Diagnostics,
            compilationTimeMs = res.CompilationTimeMs,
            message = res.ErrorMessage
        }));

        return Task.CompletedTask;
    }
}

/// <summary>
/// IPC handler to start C# PIE simulation for scene actors.
/// </summary>
public class PieBlueprintStartHandler : IIpcActionHandler
{
    private readonly IBlueprintSimulationService _simulation;
    private readonly IAssetDatabaseService _assetDb;

    public PieBlueprintStartHandler(IBlueprintSimulationService simulation, IAssetDatabaseService assetDb)
    {
        _simulation = simulation;
        _assetDb = assetDb;
    }

    public string Action => "pie_blueprint_start";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var actorsList = new List<ActorSimulationInitInfo>();

        if (payload.TryGetProperty("actors", out var actorsArray) && actorsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in actorsArray.EnumerateArray())
            {
                var id = a.TryGetProperty("id", out var ip) ? ip.GetString() ?? "" : "";
                var name = a.TryGetProperty("name", out var np) ? np.GetString() ?? "Actor" : "Actor";
                var bpName = a.TryGetProperty("blueprintName", out var bp) ? bp.GetString() ?? "" : "";

                var px = a.TryGetProperty("posX", out var pxp) ? pxp.GetSingle() : 0f;
                var py = a.TryGetProperty("posY", out var pyp) ? pyp.GetSingle() : 0f;
                var pz = a.TryGetProperty("posZ", out var pzp) ? pzp.GetSingle() : 0f;

                var rx = a.TryGetProperty("rotX", out var rxp) ? rxp.GetSingle() : 0f;
                var ry = a.TryGetProperty("rotY", out var ryp) ? ryp.GetSingle() : 0f;
                var rz = a.TryGetProperty("rotZ", out var rzp) ? rzp.GetSingle() : 0f;

                var sx = a.TryGetProperty("scaleX", out var sxp) ? sxp.GetSingle() : 1f;
                var sy = a.TryGetProperty("scaleY", out var syp) ? syp.GetSingle() : 1f;
                var sz = a.TryGetProperty("scaleZ", out var szp) ? szp.GetSingle() : 1f;

                actorsList.Add(new ActorSimulationInitInfo(
                    ActorId: id,
                    Name: name,
                    BlueprintName: bpName,
                    Location: new Vector3(px, py, pz),
                    Rotation: new Vector3(rx, ry, rz),
                    Scale: new Vector3(sx, sy, sz)));
            }
        }

        var projectRoot = _assetDb.CurrentProjectRoot ?? Directory.GetCurrentDirectory();
        var ok = await _simulation.StartSimulationAsync(projectRoot, actorsList);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "pie_blueprint_start_response",
            action = "pie_blueprint_start",
            success = ok,
            activeCount = actorsList.Count
        }));
    }
}

/// <summary>
/// IPC handler to tick the C# simulation runtime during PIE.
/// </summary>
public class PieBlueprintTickHandler : IIpcActionHandler
{
    private readonly IBlueprintSimulationService _simulation;

    public PieBlueprintTickHandler(IBlueprintSimulationService simulation)
    {
        _simulation = simulation;
    }

    public string Action => "pie_blueprint_tick";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var dt = payload.TryGetProperty("deltaSeconds", out var dp) ? dp.GetSingle() : (1f / 60f);

        // Optional collision notifications
        if (payload.TryGetProperty("collisionA", out var ca) && payload.TryGetProperty("collisionB", out var cb))
        {
            var idA = ca.GetString();
            var idB = cb.GetString();
            if (!string.IsNullOrEmpty(idA) && !string.IsNullOrEmpty(idB))
            {
                _simulation.DispatchCollision(idA, idB);
            }
        }

        var result = _simulation.StepSimulation(dt);
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "pie_blueprint_tick_response",
            action = "pie_blueprint_tick",
            success = true,
            deltaSeconds = result.DeltaSeconds,
            transforms = result.TransformUpdates,
            events = result.EmittedEvents
        }));

        return Task.CompletedTask;
    }
}

/// <summary>
/// IPC handler to stop the C# simulation runtime.
/// </summary>
public class PieBlueprintStopHandler : IIpcActionHandler
{
    private readonly IBlueprintSimulationService _simulation;

    public PieBlueprintStopHandler(IBlueprintSimulationService simulation)
    {
        _simulation = simulation;
    }

    public string Action => "pie_blueprint_stop";

    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        _simulation.StopSimulation();
        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "pie_blueprint_stop_response",
            action = "pie_blueprint_stop",
            success = true
        }));
        return Task.CompletedTask;
    }
}
