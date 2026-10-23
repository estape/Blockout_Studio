using System.Numerics;
using System.Reflection;
using BlockoutStudio.Services.Blueprints.Compiler;
using BlockoutStudio.Services.Blueprints.Storage;
using BlockoutStudio.Services.Blueprints.Transpiler;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Execution engine running compiled C# Actor Blueprint instances during Play-In-Editor (PIE),
/// collecting transform mutations and streaming updates back to Three.js.
/// </summary>
public class BlueprintSimulationService : IBlueprintSimulationService
{
    private readonly IBlueprintStorageService _storageService;
    private readonly IBlueprintTranspiler _transpiler;
    private readonly IBlueprintCompilerService _compilerService;

    private readonly Dictionary<string, ActorBlueprint> _activeInstances = [];
    private readonly Dictionary<string, Actor> _actorsById = [];
    private readonly List<BlueprintSimulationEvent> _queuedEvents = [];
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }

    public BlueprintSimulationService(
        IBlueprintStorageService storageService,
        IBlueprintTranspiler transpiler,
        IBlueprintCompilerService compilerService)
    {
        _storageService = storageService;
        _transpiler = transpiler;
        _compilerService = compilerService;
    }

    public async Task<bool> StartSimulationAsync(string projectRoot, List<ActorSimulationInitInfo> actors)
    {
        StopSimulation();

        lock (_lock)
        {
            _activeInstances.Clear();
            _actorsById.Clear();
            _queuedEvents.Clear();
        }

        var assemblyCache = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in actors)
        {
            if (string.IsNullOrWhiteSpace(info.BlueprintName)) continue;

            try
            {
                if (!assemblyCache.TryGetValue(info.BlueprintName, out var assembly))
                {
                    var graph = await _storageService.LoadBlueprintAsync(projectRoot, info.BlueprintName);
                    if (graph == null) continue;

                    var csSource = _transpiler.TranspileToCSharp(graph);
                    var compRes = _compilerService.CompileSource(csSource, $"Dyn_{graph.Name}_{Guid.NewGuid():N}");
                    if (!compRes.Success || compRes.CompiledAssembly == null)
                    {
                        Console.Error.WriteLine($"[BlueprintSimulation] Compile error in {info.BlueprintName}: {compRes.ErrorMessage}");
                        continue;
                    }

                    assembly = compRes.CompiledAssembly;
                    assemblyCache[info.BlueprintName] = assembly;
                }

                var actorType = assembly.GetTypes().FirstOrDefault(t => typeof(ActorBlueprint).IsAssignableFrom(t));
                if (actorType == null) continue;

                if (Activator.CreateInstance(actorType) is ActorBlueprint instance)
                {
                    var actorObj = new Actor(info.ActorId, info.Name, info.Location, info.Rotation, info.Scale);
                    instance.Owner = actorObj;

                    instance.OnPlaySound = (sound, volume) =>
                    {
                        lock (_lock)
                        {
                            _queuedEvents.Add(new BlueprintSimulationEvent(info.ActorId, "sound", sound, volume));
                        }
                    };

                    instance.OnPrintString = (message) =>
                    {
                        lock (_lock)
                        {
                            _queuedEvents.Add(new BlueprintSimulationEvent(info.ActorId, "print", message));
                        }
                    };

                    lock (_lock)
                    {
                        _actorsById[info.ActorId] = actorObj;
                        _activeInstances[info.ActorId] = instance;
                    }

                    try
                    {
                        instance.OnBeginPlay();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[BlueprintSimulation] Exception in OnBeginPlay of {info.ActorId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BlueprintSimulation] Failed to instantiate blueprint {info.BlueprintName}: {ex.Message}");
            }
        }

        IsRunning = true;
        return true;
    }

    public SimulationTickResult StepSimulation(float deltaSeconds)
    {
        var result = new SimulationTickResult { DeltaSeconds = deltaSeconds };
        if (!IsRunning) return result;

        List<ActorBlueprint> instancesToTick;
        lock (_lock)
        {
            instancesToTick = [.. _activeInstances.Values];
        }

        foreach (var instance in instancesToTick)
        {
            try
            {
                instance.OnTick(deltaSeconds);

                var owner = instance.Owner;
                result.TransformUpdates.Add(new ActorTransformUpdate(
                    ActorId: owner.Id,
                    PosX: owner.Location.X,
                    PosY: owner.Location.Y,
                    PosZ: owner.Location.Z,
                    RotX: owner.Rotation.X,
                    RotY: owner.Rotation.Y,
                    RotZ: owner.Rotation.Z,
                    ScaleX: owner.Scale.X,
                    ScaleY: owner.Scale.Y,
                    ScaleZ: owner.Scale.Z));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BlueprintSimulation] Error ticking {instance.Owner.Id}: {ex.Message}");
            }
        }

        lock (_lock)
        {
            if (_queuedEvents.Count > 0)
            {
                result.EmittedEvents.AddRange(_queuedEvents);
                _queuedEvents.Clear();
            }
        }

        return result;
    }

    public void DispatchCollision(string actorAId, string actorBId)
    {
        if (!IsRunning) return;

        ActorBlueprint? instA = null;
        ActorBlueprint? instB = null;
        Actor? actA = null;
        Actor? actB = null;

        lock (_lock)
        {
            _activeInstances.TryGetValue(actorAId, out instA);
            _activeInstances.TryGetValue(actorBId, out instB);
            _actorsById.TryGetValue(actorAId, out actA);
            _actorsById.TryGetValue(actorBId, out actB);
        }

        if (instA != null && actB != null)
        {
            try { instA.OnCollisionEnter(actB); } catch { }
        }

        if (instB != null && actA != null)
        {
            try { instB.OnCollisionEnter(actA); } catch { }
        }
    }

    public void StopSimulation()
    {
        IsRunning = false;
        lock (_lock)
        {
            _activeInstances.Clear();
            _actorsById.Clear();
            _queuedEvents.Clear();
        }
    }
}
