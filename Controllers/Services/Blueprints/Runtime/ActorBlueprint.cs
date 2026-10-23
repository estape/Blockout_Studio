using System.Numerics;
using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Represents a scene entity/actor driven by the C# Blueprint simulation runtime.
/// Coordinates with Three.js engine metrics (1 Unit = 1 cm, Z-Up).
/// </summary>
public class Actor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Actor";
    public Vector3 Location { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero; // Pitch (X), Yaw (Y), Roll (Z) in degrees
    public Vector3 Scale { get; set; } = Vector3.One;
    public Dictionary<string, object?> Properties { get; } = [];

    public Actor() { }

    public Actor(string id, string name, Vector3 location, Vector3 rotation, Vector3 scale)
    {
        Id = id;
        Name = name;
        Location = location;
        Rotation = rotation;
        Scale = scale;
    }
}

/// <summary>
/// Base class for all transpiled and compiled Actor Blueprints.
/// Provides UE5-style gameplay lifecycle methods and actor transformation APIs.
/// </summary>
public abstract class ActorBlueprint
{
    /// <summary>The actor instance in the scene owned and driven by this blueprint.</summary>
    public Actor Owner { get; set; } = new();

    /// <summary>Optional callback dispatched when PlaySound2D is invoked.</summary>
    public Action<string, float>? OnPlaySound { get; set; }

    /// <summary>Optional callback dispatched when PrintString is invoked.</summary>
    public Action<string>? OnPrintString { get; set; }

    /// <summary>
    /// Invoked once when the simulation starts or actor enters Play mode.
    /// </summary>
    public virtual void OnBeginPlay() { }

    /// <summary>
    /// Invoked every frame during simulation update.
    /// </summary>
    /// <param name="deltaSeconds">Elapsed delta time since last frame in seconds.</param>
    public virtual void OnTick(float deltaSeconds) { }

    /// <summary>
    /// Invoked when this actor's collider collides with another actor.
    /// </summary>
    public virtual void OnCollisionEnter(Actor other) { }

    /// <summary>
    /// Invoked when another actor enters this actor's trigger zone.
    /// </summary>
    public virtual void OnTriggerEnter(Actor other) { }

    /// <summary>
    /// Invoked when a named custom event is triggered on this blueprint.
    /// </summary>
    public virtual void OnCustomEvent(string eventName) { }

    // =========================================================================
    // Core Actor APIs (UE5 Parity)
    // =========================================================================

    [BlueprintNode("Actor", "Get Actor Location", isPure: true)]
    public Vector3 GetActorLocation() => Owner.Location;

    [BlueprintNode("Actor", "Set Actor Location", isPure: false)]
    public void SetActorLocation(Vector3 newLocation) => Owner.Location = newLocation;

    [BlueprintNode("Actor", "Get Actor Rotation", isPure: true)]
    public Vector3 GetActorRotation() => Owner.Rotation;

    [BlueprintNode("Actor", "Set Actor Rotation", isPure: false)]
    public void SetActorRotation(Vector3 newRotation) => Owner.Rotation = newRotation;

    [BlueprintNode("Actor", "Get Actor Scale", isPure: true)]
    public Vector3 GetActorScale() => Owner.Scale;

    [BlueprintNode("Actor", "Set Actor Scale", isPure: false)]
    public void SetActorScale(Vector3 newScale) => Owner.Scale = newScale;

    [BlueprintNode("Actor", "Add Actor World Offset", isPure: false)]
    public void AddActorWorldOffset(Vector3 deltaLocation)
    {
        Owner.Location += deltaLocation;
    }

    [BlueprintNode("Actor", "Add Actor World Rotation", isPure: false)]
    public void AddActorWorldRotation(Vector3 deltaRotation)
    {
        Owner.Rotation += deltaRotation;
    }

    [BlueprintNode("Audio", "Play Sound 2D", isPure: false)]
    public void PlaySound2D(string soundName, float volume = 1.0f)
    {
        OnPlaySound?.Invoke(soundName, volume);
    }

    [BlueprintNode("Debug", "Print String", isPure: false)]
    public void PrintString(string message)
    {
        OnPrintString?.Invoke(message);
    }
}

/// <summary>
/// Base class for modular gameplay components attached to actors.
/// </summary>
public abstract class ActorComponent
{
    public Actor Owner { get; set; } = new();
    public Action<string, float>? OnPlaySound { get; set; }
    public Action<string>? OnPrintString { get; set; }

    public virtual void OnBeginPlay() { }
    public virtual void OnTick(float deltaSeconds) { }
    public virtual void OnCollisionEnter(Actor other) { }
    public virtual void OnTriggerEnter(Actor other) { }
    public virtual void OnCustomEvent(string eventName) { }

    [BlueprintNode("Actor", "Get Actor Location", isPure: true)]
    public Vector3 GetActorLocation() => Owner.Location;

    [BlueprintNode("Actor", "Set Actor Location", isPure: false)]
    public void SetActorLocation(Vector3 newLocation) => Owner.Location = newLocation;

    [BlueprintNode("Actor", "Get Actor Rotation", isPure: true)]
    public Vector3 GetActorRotation() => Owner.Rotation;

    [BlueprintNode("Actor", "Set Actor Rotation", isPure: false)]
    public void SetActorRotation(Vector3 newRotation) => Owner.Rotation = newRotation;

    [BlueprintNode("Actor", "Get Actor Scale", isPure: true)]
    public Vector3 GetActorScale() => Owner.Scale;

    [BlueprintNode("Actor", "Set Actor Scale", isPure: false)]
    public void SetActorScale(Vector3 newScale) => Owner.Scale = newScale;

    [BlueprintNode("Actor", "Add Actor World Offset", isPure: false)]
    public void AddActorWorldOffset(Vector3 deltaLocation)
    {
        Owner.Location += deltaLocation;
    }

    [BlueprintNode("Actor", "Add Actor World Rotation", isPure: false)]
    public void AddActorWorldRotation(Vector3 deltaRotation)
    {
        Owner.Rotation += deltaRotation;
    }

    [BlueprintNode("Audio", "Play Sound 2D", isPure: false)]
    public void PlaySound2D(string soundName, float volume = 1.0f)
    {
        OnPlaySound?.Invoke(soundName, volume);
    }

    [BlueprintNode("Debug", "Print String", isPure: false)]
    public void PrintString(string message)
    {
        OnPrintString?.Invoke(message);
    }
}

/// <summary>
/// Base class for Level-wide orchestrator Blueprints.
/// </summary>
public abstract class LevelBlueprint
{
    public Action<string>? OnPrintString { get; set; }

    public virtual void OnBeginPlay() { }

    public virtual void OnTick(float deltaSeconds) { }

    public void PrintString(string message)
    {
        OnPrintString?.Invoke(message);
    }
}
