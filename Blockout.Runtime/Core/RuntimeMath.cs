using System.Numerics;

namespace BlockoutStudio.Services.Blueprints.Runtime;

/// <summary>
/// Lightweight pure math library functions for standalone game execution.
/// </summary>
public static class MathLibrary
{
    public static float Add(float a, float b) => a + b;
    public static float Subtract(float a, float b) => a - b;
    public static float Multiply(float a, float b) => a * b;
    public static float Divide(float a, float b) => b != 0 ? a / b : 0f;
    public static Vector3 VectorAdd(Vector3 a, Vector3 b) => a + b;
    public static Vector3 VectorSubtract(Vector3 a, Vector3 b) => a - b;
    public static Vector3 VectorMultiplyScalar(Vector3 vector, float scalar) => vector * scalar;
    public static float VectorDistance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
    public static float VectorLength(Vector3 v) => v.Length();
    public static Vector3 VectorNormalize(Vector3 v) => Vector3.Normalize(v);
}
