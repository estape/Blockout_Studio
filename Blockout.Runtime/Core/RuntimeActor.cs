using System.Numerics;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Lightweight standalone Actor model for native shipping packages.
/// </summary>
public class Actor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Actor";
    public Vector3 Location { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
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
/// Lightweight standalone ActorComponent base class for native shipping packages.
/// </summary>
public abstract class ActorComponent
{
    public Actor Owner { get; set; } = new();
    public Action<string, float>? OnPlaySound { get; set; }
    public Action<string>? OnPrintString { get; set; }

    public virtual void OnBeginPlay() { }
    public virtual void OnTick(float deltaTime) { }
    public virtual void OnCollisionEnter(Actor other) { }
    public virtual void OnTriggerEnter(Actor other) { }
    public virtual void OnCustomEvent(string eventName) { }

    public Vector3 GetActorLocation() => Owner.Location;
    public void SetActorLocation(Vector3 newLocation) => Owner.Location = newLocation;
    public Vector3 GetActorRotation() => Owner.Rotation;
    public void SetActorRotation(Vector3 newRotation) => Owner.Rotation = newRotation;
    public Vector3 GetActorScale() => Owner.Scale;
    public void SetActorScale(Vector3 newScale) => Owner.Scale = newScale;
    public void AddActorWorldOffset(Vector3 deltaLocation) => Owner.Location += deltaLocation;
    public void AddActorWorldRotation(Vector3 deltaRotation) => Owner.Rotation += deltaRotation;
    public void PlaySound2D(string soundName, float volume = 1.0f) => OnPlaySound?.Invoke(soundName, volume);
    public void PrintString(string message)
    {
        OnPrintString?.Invoke(message);
        Console.WriteLine($"[Script] {message}");
    }
}

/// <summary>
/// Standalone compatibility alias for ActorBlueprint.
/// </summary>
public abstract class ActorBlueprint : ActorComponent
{
}

