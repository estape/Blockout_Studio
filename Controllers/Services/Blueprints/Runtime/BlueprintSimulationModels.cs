using System.Numerics;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Initialization descriptor for an actor with an attached Blueprint participating in PIE.
/// </summary>
public record ActorSimulationInitInfo(
    string ActorId,
    string Name,
    string BlueprintName,
    Vector3 Location,
    Vector3 Rotation,
    Vector3 Scale);

/// <summary>
/// Transform delta report emitted from the C# simulation back to Three.js.
/// </summary>
public record ActorTransformUpdate(
    string ActorId,
    float PosX,
    float PosY,
    float PosZ,
    float RotX,
    float RotY,
    float RotZ,
    float ScaleX,
    float ScaleY,
    float ScaleZ);

/// <summary>
/// Event emitted by a blueprint during simulation (audio trigger, print string, etc.).
/// </summary>
public record BlueprintSimulationEvent(
    string ActorId,
    string EventType, // "sound", "print"
    string Parameter,
    float Value = 1.0f);

/// <summary>
/// Aggregate result returned by a single C# simulation tick.
/// </summary>
public class SimulationTickResult
{
    public float DeltaSeconds { get; set; }
    public List<ActorTransformUpdate> TransformUpdates { get; set; } = [];
    public List<BlueprintSimulationEvent> EmittedEvents { get; set; } = [];
}

/// <summary>
/// Service managing real-time execution of compiled Blueprint classes during Play-In-Editor (PIE).
/// </summary>
public interface IBlueprintSimulationService
{
    /// <summary>Whether a simulation session is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Initializes and starts the C# simulation runtime with a list of scene actors.
    /// </summary>
    Task<bool> StartSimulationAsync(string projectRoot, List<ActorSimulationInitInfo> actors);

    /// <summary>
    /// Advances the simulation by deltaSeconds, ticking all active blueprints and capturing state changes.
    /// </summary>
    SimulationTickResult StepSimulation(float deltaSeconds);

    /// <summary>
    /// Delivers a collision event between two actors to their blueprint scripts.
    /// </summary>
    void DispatchCollision(string actorAId, string actorBId);

    /// <summary>
    /// Stops the simulation session and releases active script instances.
    /// </summary>
    void StopSimulation();
}
