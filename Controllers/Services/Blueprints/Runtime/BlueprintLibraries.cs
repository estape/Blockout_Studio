using System.Numerics;
using BlockoutStudio.Models.Blueprints;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Static math library functions exposed as pure Blueprint nodes in Blockout Studio.
/// </summary>
public static class MathLibrary
{
    [BlueprintNode("Math", "Vector Distance", isPure: true)]
    public static float VectorDistance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }

    [BlueprintNode("Math", "Vector Lerp", isPure: true)]
    public static Vector3 VectorLerp(Vector3 a, Vector3 b, float alpha)
    {
        return Vector3.Lerp(a, b, Math.Clamp(alpha, 0.0f, 1.0f));
    }

    [BlueprintNode("Math", "Vector Dot Product", isPure: true)]
    public static float VectorDotProduct(Vector3 a, Vector3 b)
    {
        return Vector3.Dot(a, b);
    }

    [BlueprintNode("Math", "Float Clamp", isPure: true)]
    public static float FloatClamp(float value, float min, float max)
    {
        return Math.Clamp(value, min, max);
    }

    [BlueprintNode("Math", "Float Lerp", isPure: true)]
    public static float FloatLerp(float a, float b, float alpha)
    {
        return a + (b - a) * Math.Clamp(alpha, 0.0f, 1.0f);
    }

    [BlueprintNode("Math", "Random Float in Range", isPure: true)]
    public static float RandomFloatInRange(float min, float max)
    {
        return min + (float)Random.Shared.NextDouble() * (max - min);
    }
}

/// <summary>
/// Gameplay statics and actor lifecycle methods exposed to visual scripting.
/// </summary>
public static class GameplayStatics
{
    [BlueprintNode("Gameplay", "Spawn Actor", isPure: false)]
    public static Actor SpawnActor(string name, Vector3 location, Vector3 rotation)
    {
        return new Actor(Guid.NewGuid().ToString("N"), name, location, rotation, Vector3.One);
    }

    [BlueprintNode("Gameplay", "Destroy Actor", isPure: false)]
    public static void DestroyActor(Actor actor)
    {
        // Actor destruction handled by simulation world
    }

    [BlueprintNode("Gameplay", "Get Actor by Tag", isPure: true)]
    public static Actor? GetActorByTag(string tag)
    {
        return null;
    }
}

